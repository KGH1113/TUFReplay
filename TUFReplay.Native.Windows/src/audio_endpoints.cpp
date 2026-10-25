#define WIN32_LEAN_AND_MEAN
#define INITGUID
#include <windows.h>

#include <devicetopology.h>
#include <functiondiscoverykeys_devpkey.h>
#include <mmdeviceapi.h>
#include <propvarutil.h>

#include <cwchar>
#include <cwctype>

#include "tufreplay_win_native.h"

// The Windows SDK names this IPart method GetTopologyObject; mingw-w64 declares the
// same vtable slot as GetTopologyObjects.
#if defined(__MINGW32__)
#define TUFREPLAY_PART_TOPOLOGY GetTopologyObjects
#else
#define TUFREPLAY_PART_TOPOLOGY GetTopologyObject
#endif

namespace {

template <typename T>
void release(T *&value) {
  if (value != nullptr) {
    value->Release();
    value = nullptr;
  }
}

class ComScope {
 public:
  ComScope() {
    const HRESULT result = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    // RPC_E_CHANGED_MODE: the caller already uses an STA, which MMDevice supports.
    initialized_ = SUCCEEDED(result);
    usable_ = initialized_ || result == RPC_E_CHANGED_MODE;
  }
  ~ComScope() {
    if (initialized_) CoUninitialize();
  }
  bool usable() const { return usable_; }

 private:
  bool initialized_ = false;
  bool usable_ = false;
};

bool contains_ignore_case(const wchar_t *text, const wchar_t *token) {
  if (text == nullptr || token == nullptr) return false;
  const size_t token_length = wcslen(token);
  for (const wchar_t *start = text; *start != L'\0'; ++start) {
    size_t i = 0;
    while (i < token_length && start[i] != L'\0' && towupper(start[i]) == towupper(token[i])) ++i;
    if (i == token_length) return true;
  }
  return false;
}

// Unity may shorten long endpoint names, so a listed name can be a prefix.
bool matches_endpoint_name(const wchar_t *friendly, const wchar_t *requested) {
  if (friendly == nullptr || requested == nullptr || *requested == L'\0') return false;
  if (wcscmp(friendly, requested) == 0) return true;
  const size_t length = wcslen(requested);
  return length >= 16 && wcsncmp(friendly, requested, length) == 0;
}

// An endpoint's topology connects to the adapter that owns it. Bluetooth audio
// adapters enumerate under BTHENUM (A2DP), BTHHFENUM (hands-free) or BTHLE (LE audio).
int32_t endpoint_transport(IMMDevice *device) {
  IDeviceTopology *endpoint_topology = nullptr;
  IConnector *endpoint_connector = nullptr;
  IConnector *adapter_connector = nullptr;
  IPart *adapter_part = nullptr;
  IDeviceTopology *adapter_topology = nullptr;
  LPWSTR adapter_id = nullptr;
  int32_t transport = TUFREPLAY_WIN_AUDIO_TRANSPORT_UNKNOWN;

  if (SUCCEEDED(device->Activate(__uuidof(IDeviceTopology), CLSCTX_ALL, nullptr,
        reinterpret_cast<void **>(&endpoint_topology)))
    && SUCCEEDED(endpoint_topology->GetConnector(0, &endpoint_connector))
    && SUCCEEDED(endpoint_connector->GetConnectedTo(&adapter_connector))
    && SUCCEEDED(adapter_connector->QueryInterface(__uuidof(IPart), reinterpret_cast<void **>(&adapter_part)))
    && SUCCEEDED(adapter_part->TUFREPLAY_PART_TOPOLOGY(&adapter_topology))
    && SUCCEEDED(adapter_topology->GetDeviceId(&adapter_id))) {
    const bool bluetooth = contains_ignore_case(adapter_id, L"BTHENUM")
      || contains_ignore_case(adapter_id, L"BTHHFENUM") || contains_ignore_case(adapter_id, L"BTHLE");
    transport = bluetooth ? TUFREPLAY_WIN_AUDIO_TRANSPORT_BLUETOOTH : TUFREPLAY_WIN_AUDIO_TRANSPORT_OTHER;
  }

  if (adapter_id != nullptr) CoTaskMemFree(adapter_id);
  release(adapter_topology);
  release(adapter_part);
  release(adapter_connector);
  release(endpoint_connector);
  release(endpoint_topology);
  return transport;
}

bool endpoint_named(IMMDevice *device, const wchar_t *requested) {
  IPropertyStore *properties = nullptr;
  if (FAILED(device->OpenPropertyStore(STGM_READ, &properties))) return false;
  PROPVARIANT value;
  PropVariantInit(&value);
  bool matches = SUCCEEDED(properties->GetValue(PKEY_Device_FriendlyName, &value)) && value.vt == VT_LPWSTR
    && matches_endpoint_name(value.pwszVal, requested);
  PropVariantClear(&value);
  release(properties);
  return matches;
}

} // namespace

extern "C" int32_t tufreplay_win_audio_capture_transport(const uint16_t *name) {
  ComScope com;
  if (!com.usable()) return TUFREPLAY_WIN_AUDIO_TRANSPORT_UNKNOWN;

  IMMDeviceEnumerator *enumerator = nullptr;
  if (FAILED(CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL, __uuidof(IMMDeviceEnumerator),
        reinterpret_cast<void **>(&enumerator))))
    return TUFREPLAY_WIN_AUDIO_TRANSPORT_UNKNOWN;

  int32_t transport = TUFREPLAY_WIN_AUDIO_TRANSPORT_UNKNOWN;
  const auto *requested = reinterpret_cast<const wchar_t *>(name);
  if (requested == nullptr) {
    IMMDevice *device = nullptr;
    if (SUCCEEDED(enumerator->GetDefaultAudioEndpoint(eCapture, eConsole, &device))) {
      transport = endpoint_transport(device);
      release(device);
    }
  } else {
    IMMDeviceCollection *devices = nullptr;
    UINT count = 0;
    if (SUCCEEDED(enumerator->EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, &devices))
      && SUCCEEDED(devices->GetCount(&count))) {
      for (UINT i = 0; i < count && transport == TUFREPLAY_WIN_AUDIO_TRANSPORT_UNKNOWN; ++i) {
        IMMDevice *device = nullptr;
        if (SUCCEEDED(devices->Item(i, &device)) && endpoint_named(device, requested))
          transport = endpoint_transport(device);
        release(device);
      }
    }
    release(devices);
  }

  release(enumerator);
  return transport;
}
