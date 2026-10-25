// Real-desktop smoke test for TUFReplayNative.dll. Run it in an interactive
// session: it installs the low-level keyboard hook, injects key transitions with
// SendInput, and reports how long the hook took to see them.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <algorithm>
#include <cstdio>
#include <vector>

#include "tufreplay_win_native.h"

namespace {
using AbiVersion = uint32_t (*)();
using Create = void *(*)();
using Start = int32_t (*)(void *);
using Action = void (*)(void *);
using WaitDequeue = int32_t (*)(void *, tufreplay_win_key_event *, int32_t, int32_t);
using Transport = int32_t (*)(const uint16_t *);

template <typename T>
T bind(HMODULE module, const char *name) {
  return reinterpret_cast<T>(reinterpret_cast<void *>(GetProcAddress(module, name)));
}

void send_key(WORD key, bool down) {
  INPUT input{};
  input.type = INPUT_KEYBOARD;
  input.ki.wVk = key;
  input.ki.dwFlags = down ? 0 : KEYEVENTF_KEYUP;
  SendInput(1, &input, sizeof(input));
}
} // namespace

int wmain(int argc, wchar_t **argv) {
  const wchar_t *path = argc > 1 ? argv[1] : L"TUFReplayNative.dll";
  HMODULE module = LoadLibraryW(path);
  if (module == nullptr) {
    std::printf("FAIL load error=%lu\n", GetLastError());
    return 1;
  }
  auto abi = bind<AbiVersion>(module, "tufreplay_win_native_abi_version");
  auto create = bind<Create>(module, "tufreplay_win_input_create");
  auto start = bind<Start>(module, "tufreplay_win_input_start");
  auto destroy = bind<Action>(module, "tufreplay_win_input_destroy");
  auto wait = bind<WaitDequeue>(module, "tufreplay_win_input_wait_dequeue");
  auto transport = bind<Transport>(module, "tufreplay_win_audio_capture_transport");
  if (!abi || !create || !start || !destroy || !wait || !transport || abi() != TUFREPLAY_WIN_NATIVE_ABI_VERSION) {
    std::printf("FAIL exports or ABI\n");
    return 1;
  }

  std::printf("audio default capture transport=%d\n", transport(nullptr));

  void *context = create();
  int32_t error = start(context);
  if (error != TUFREPLAY_WIN_INPUT_ERROR_NONE) {
    std::printf("FAIL start error=%d\n", error);
    return 1;
  }

  LARGE_INTEGER frequency;
  QueryPerformanceFrequency(&frequency);
  const WORD key = VK_F24; // Unused by the desktop and games.
  int received = 0;
  std::vector<double> latencies;
  for (int i = 0; i < 200; ++i) {
    LARGE_INTEGER sent;
    QueryPerformanceCounter(&sent);
    send_key(key, i % 2 == 0);
    tufreplay_win_key_event events[16];
    int32_t count = wait(context, events, 16, 1000);
    for (int32_t j = 0; j < count; ++j) {
      if (events[j].virtual_key != key) continue;
      ++received;
      latencies.push_back((events[j].qpc_ticks - sent.QuadPart) * 1e6 / static_cast<double>(frequency.QuadPart));
    }
  }
  send_key(key, false);
  destroy(context);
  std::sort(latencies.begin(), latencies.end());
  auto at = [&](double q) { return latencies.empty() ? 0.0 : latencies[static_cast<size_t>(q * (latencies.size() - 1))]; };
  std::printf("%s keyboard events received=%d/200 send_to_hook_us p50=%.1f p90=%.1f p99=%.1f max=%.1f\n",
    received == 200 ? "PASS" : "FAIL", received, at(0.5), at(0.9), at(0.99), at(1.0));
  return received == 200 ? 0 : 1;
}
