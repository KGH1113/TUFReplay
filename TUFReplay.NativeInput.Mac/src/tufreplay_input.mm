#include "input_core.hpp"

#include <CoreFoundation/CoreFoundation.h>
#include <CoreGraphics/CoreGraphics.h>
#include <IOKit/hid/IOHIDManager.h>
#include <IOKit/hidsystem/IOHIDLib.h>
#include <dispatch/dispatch.h>
#include <mach/mach_time.h>
#include <pthread.h>

#include <atomic>
#include <cstdint>
#include <vector>

using tufreplay::input::InputCore;
using tufreplay::input::is_supported_usage;

namespace {

constexpr uint32_t kKeyboardUsagePage = 0x07;
constexpr uint32_t kKeyboardDeviceUsage = 0x06;
constexpr uint64_t kCapsLockFlag = 1ull << 16;

struct InputContext {
  InputCore core;
  IOHIDManagerRef manager = nullptr;
  std::atomic<CFRunLoopRef> run_loop{nullptr};
  pthread_t thread{};
  bool thread_created = false;
  std::atomic<bool> running{false};
  std::atomic<bool> stopping{false};
  std::atomic<int32_t> last_error{TUFREPLAY_INPUT_ERROR_NONE};
  dispatch_semaphore_t started = dispatch_semaphore_create(0);
  dispatch_semaphore_t events_available = dispatch_semaphore_create(0);
};

CFMutableDictionaryRef create_number_matching(CFStringRef page_key, uint32_t page, CFStringRef usage_key, uint32_t usage) {
  CFMutableDictionaryRef dictionary = CFDictionaryCreateMutable(
    kCFAllocatorDefault,
    0,
    &kCFTypeDictionaryKeyCallBacks,
    &kCFTypeDictionaryValueCallBacks
  );
  if (dictionary == nullptr)
    return nullptr;
  CFNumberRef page_number = CFNumberCreate(kCFAllocatorDefault, kCFNumberSInt32Type, &page);
  CFNumberRef usage_number = CFNumberCreate(kCFAllocatorDefault, kCFNumberSInt32Type, &usage);
  if (page_number != nullptr)
    CFDictionarySetValue(dictionary, page_key, page_number);
  if (usage_number != nullptr)
    CFDictionarySetValue(dictionary, usage_key, usage_number);
  if (page_number != nullptr)
    CFRelease(page_number);
  if (usage_number != nullptr)
    CFRelease(usage_number);
  return dictionary;
}

void signal_if_queued(InputContext *context, bool queued) {
  if (queued)
    dispatch_semaphore_signal(context->events_available);
}

void on_device_added(void *opaque, IOReturn result, void *, IOHIDDeviceRef device) {
  InputContext *context = static_cast<InputContext *>(opaque);
  if (context == nullptr || device == nullptr || result != kIOReturnSuccess || context->stopping.load(std::memory_order_acquire))
    return;
  const uintptr_t token = reinterpret_cast<uintptr_t>(device);
  if (!context->core.add_device(token))
    return;

  CFArrayRef elements = IOHIDDeviceCopyMatchingElements(device, nullptr, kIOHIDOptionsTypeNone);
  if (elements == nullptr)
    return;
  const CFIndex count = CFArrayGetCount(elements);
  for (CFIndex i = 0; i < count; ++i) {
    IOHIDElementRef element = static_cast<IOHIDElementRef>(const_cast<void *>(CFArrayGetValueAtIndex(elements, i)));
    if (element == nullptr || IOHIDElementGetUsagePage(element) != kKeyboardUsagePage)
      continue;
    const uint32_t usage = IOHIDElementGetUsage(element);
    if (!is_supported_usage(usage))
      continue;
    IOHIDValueRef value = nullptr;
    if (IOHIDDeviceGetValueWithOptions(device, element, &value, kIOHIDDeviceGetValueWithoutUpdate) != kIOReturnSuccess || value == nullptr)
      continue;
    if (IOHIDValueGetIntegerValue(value) != 0)
      signal_if_queued(context, context->core.apply(token, usage, true, IOHIDValueGetTimeStamp(value)));
  }
  CFRelease(elements);
}

void on_device_removed(void *opaque, IOReturn, void *, IOHIDDeviceRef device) {
  InputContext *context = static_cast<InputContext *>(opaque);
  if (context == nullptr || device == nullptr)
    return;
  signal_if_queued(context, context->core.remove_device(reinterpret_cast<uintptr_t>(device), mach_absolute_time()));
}

void on_input_value(void *opaque, IOReturn result, void *, IOHIDValueRef value) {
  InputContext *context = static_cast<InputContext *>(opaque);
  if (context == nullptr || value == nullptr || result != kIOReturnSuccess || context->stopping.load(std::memory_order_relaxed))
    return;
  IOHIDElementRef element = IOHIDValueGetElement(value);
  if (element == nullptr || IOHIDElementGetUsagePage(element) != kKeyboardUsagePage)
    return;
  IOHIDDeviceRef device = IOHIDElementGetDevice(element);
  if (device == nullptr)
    return;
  signal_if_queued(
    context,
    context->core.apply(
      reinterpret_cast<uintptr_t>(device),
      IOHIDElementGetUsage(element),
      IOHIDValueGetIntegerValue(value) != 0,
      IOHIDValueGetTimeStamp(value)
    )
  );
}

void *input_thread_main(void *opaque) {
  InputContext *context = static_cast<InputContext *>(opaque);
  context->manager = IOHIDManagerCreate(kCFAllocatorDefault, kIOHIDOptionsTypeNone);
  if (context->manager == nullptr) {
    context->last_error.store(TUFREPLAY_INPUT_ERROR_MANAGER_CREATE, std::memory_order_release);
    dispatch_semaphore_signal(context->started);
    return nullptr;
  }

  CFMutableDictionaryRef device_matching = create_number_matching(
    CFSTR(kIOHIDDeviceUsagePageKey),
    1,
    CFSTR(kIOHIDDeviceUsageKey),
    kKeyboardDeviceUsage
  );
  CFMutableDictionaryRef value_matching = create_number_matching(
    CFSTR(kIOHIDElementUsagePageKey),
    kKeyboardUsagePage,
    CFSTR(kIOHIDElementUsageKey),
    0
  );
  if (value_matching != nullptr)
    CFDictionaryRemoveValue(value_matching, CFSTR(kIOHIDElementUsageKey));

  IOHIDManagerSetDeviceMatching(context->manager, device_matching);
  IOHIDManagerSetInputValueMatching(context->manager, value_matching);
  IOHIDManagerRegisterDeviceMatchingCallback(context->manager, on_device_added, context);
  IOHIDManagerRegisterDeviceRemovalCallback(context->manager, on_device_removed, context);
  IOHIDManagerRegisterInputValueCallback(context->manager, on_input_value, context);

  if (device_matching != nullptr)
    CFRelease(device_matching);
  if (value_matching != nullptr)
    CFRelease(value_matching);

  CFRunLoopRef loop = CFRunLoopGetCurrent();
  CFRetain(loop);
  context->run_loop.store(loop, std::memory_order_release);
  IOHIDManagerScheduleWithRunLoop(context->manager, loop, kCFRunLoopDefaultMode);
  const IOReturn opened = IOHIDManagerOpen(context->manager, kIOHIDOptionsTypeNone);
  if (opened != kIOReturnSuccess) {
    context->last_error.store(TUFREPLAY_INPUT_ERROR_MANAGER_OPEN, std::memory_order_release);
    IOHIDManagerUnscheduleFromRunLoop(context->manager, loop, kCFRunLoopDefaultMode);
    context->run_loop.store(nullptr, std::memory_order_release);
    CFRelease(loop);
    CFRelease(context->manager);
    context->manager = nullptr;
    dispatch_semaphore_signal(context->started);
    return nullptr;
  }

  const CGEventFlags current_flags = CGEventSourceFlagsState(kCGEventSourceStateHIDSystemState);
  context->core.set_caps_lock((static_cast<uint64_t>(current_flags) & kCapsLockFlag) != 0);
  CFSetRef devices = IOHIDManagerCopyDevices(context->manager);
  if (devices != nullptr) {
    const CFIndex device_count = CFSetGetCount(devices);
    std::vector<const void *> values(static_cast<size_t>(device_count));
    CFSetGetValues(devices, values.data());
    for (const void *value : values)
      on_device_added(context, kIOReturnSuccess, nullptr, static_cast<IOHIDDeviceRef>(const_cast<void *>(value)));
    CFRelease(devices);
  }
  context->running.store(true, std::memory_order_release);
  dispatch_semaphore_signal(context->started);
  CFRunLoopRun();

  context->running.store(false, std::memory_order_release);
  IOHIDManagerRegisterInputValueCallback(context->manager, nullptr, nullptr);
  IOHIDManagerRegisterDeviceMatchingCallback(context->manager, nullptr, nullptr);
  IOHIDManagerRegisterDeviceRemovalCallback(context->manager, nullptr, nullptr);
  IOHIDManagerUnscheduleFromRunLoop(context->manager, loop, kCFRunLoopDefaultMode);
  IOHIDManagerClose(context->manager, kIOHIDOptionsTypeNone);
  CFRelease(context->manager);
  context->manager = nullptr;
  context->run_loop.store(nullptr, std::memory_order_release);
  CFRelease(loop);
  dispatch_semaphore_signal(context->events_available);
  return nullptr;
}

InputContext *as_context(void *opaque) { return static_cast<InputContext *>(opaque); }

}  // namespace

extern "C" {

uint32_t tufreplay_input_abi_version(void) { return TUFREPLAY_INPUT_ABI_VERSION; }

int32_t tufreplay_input_check_access(void) {
  if (__builtin_available(macOS 10.15, *))
    return static_cast<int32_t>(IOHIDCheckAccess(kIOHIDRequestTypeListenEvent));
  return TUFREPLAY_INPUT_ACCESS_GRANTED;
}

int32_t tufreplay_input_request_access(void) {
  if (__builtin_available(macOS 10.15, *)) {
    if (IOHIDRequestAccess(kIOHIDRequestTypeListenEvent))
      return TUFREPLAY_INPUT_ACCESS_GRANTED;
    return static_cast<int32_t>(IOHIDCheckAccess(kIOHIDRequestTypeListenEvent));
  }
  return TUFREPLAY_INPUT_ACCESS_GRANTED;
}

void tufreplay_input_timebase(uint32_t *numerator, uint32_t *denominator) {
  mach_timebase_info_data_t info{};
  mach_timebase_info(&info);
  if (numerator != nullptr)
    *numerator = info.numer;
  if (denominator != nullptr)
    *denominator = info.denom;
}

uint64_t tufreplay_input_mach_now(void) { return mach_absolute_time(); }

void *tufreplay_input_create(void) { return new InputContext(); }

int32_t tufreplay_input_start(void *opaque_context) {
  InputContext *context = as_context(opaque_context);
  if (context == nullptr)
    return TUFREPLAY_INPUT_ERROR_THREAD_START;
  if (context->running.load(std::memory_order_acquire))
    return TUFREPLAY_INPUT_ERROR_NONE;
  if (tufreplay_input_check_access() != TUFREPLAY_INPUT_ACCESS_GRANTED) {
    context->last_error.store(TUFREPLAY_INPUT_ERROR_PERMISSION, std::memory_order_release);
    return TUFREPLAY_INPUT_ERROR_PERMISSION;
  }
  context->stopping.store(false, std::memory_order_release);
  context->last_error.store(TUFREPLAY_INPUT_ERROR_NONE, std::memory_order_release);
  if (pthread_create(&context->thread, nullptr, input_thread_main, context) != 0) {
    context->last_error.store(TUFREPLAY_INPUT_ERROR_THREAD_START, std::memory_order_release);
    return TUFREPLAY_INPUT_ERROR_THREAD_START;
  }
  context->thread_created = true;
  if (dispatch_semaphore_wait(context->started, dispatch_time(DISPATCH_TIME_NOW, 2ll * NSEC_PER_SEC)) != 0) {
    context->last_error.store(TUFREPLAY_INPUT_ERROR_START_TIMEOUT, std::memory_order_release);
    tufreplay_input_stop(context);
    return TUFREPLAY_INPUT_ERROR_START_TIMEOUT;
  }
  return context->running.load(std::memory_order_acquire)
    ? TUFREPLAY_INPUT_ERROR_NONE
    : context->last_error.load(std::memory_order_acquire);
}

void tufreplay_input_stop(void *opaque_context) {
  InputContext *context = as_context(opaque_context);
  if (context == nullptr)
    return;
  context->stopping.store(true, std::memory_order_release);
  CFRunLoopRef loop = context->run_loop.load(std::memory_order_acquire);
  if (loop != nullptr)
    CFRunLoopStop(loop);
  dispatch_semaphore_signal(context->events_available);
  if (context->thread_created && !pthread_equal(pthread_self(), context->thread))
    pthread_join(context->thread, nullptr);
  context->thread_created = false;
  context->running.store(false, std::memory_order_release);
}

void tufreplay_input_destroy(void *opaque_context) {
  InputContext *context = as_context(opaque_context);
  if (context == nullptr)
    return;
  tufreplay_input_stop(context);
  delete context;
}

bool tufreplay_input_is_running(void *opaque_context) {
  InputContext *context = as_context(opaque_context);
  return context != nullptr && context->running.load(std::memory_order_acquire);
}

int32_t tufreplay_input_last_error(void *opaque_context) {
  InputContext *context = as_context(opaque_context);
  return context == nullptr ? TUFREPLAY_INPUT_ERROR_THREAD_START : context->last_error.load(std::memory_order_acquire);
}

int32_t tufreplay_input_wait_dequeue(void *opaque_context, tufreplay_input_event *events, int32_t capacity, int32_t timeout_ms) {
  InputContext *context = as_context(opaque_context);
  if (context == nullptr || events == nullptr || capacity <= 0)
    return 0;
  int32_t count = context->core.drain(events, capacity);
  if (count != 0)
    return count;
  const int64_t wait_ns = timeout_ms <= 0 ? 0 : static_cast<int64_t>(timeout_ms) * NSEC_PER_MSEC;
  dispatch_semaphore_wait(context->events_available, dispatch_time(DISPATCH_TIME_NOW, wait_ns));
  return context->core.drain(events, capacity);
}

int32_t tufreplay_input_copy_state(void *opaque_context, uint8_t *usage_down, int32_t capacity) {
  InputContext *context = as_context(opaque_context);
  return context == nullptr ? 0 : context->core.copy_state(usage_down, capacity);
}

uint64_t tufreplay_input_take_dropped(void *opaque_context) {
  InputContext *context = as_context(opaque_context);
  return context == nullptr ? 0 : context->core.take_dropped();
}

void tufreplay_input_get_stats(void *opaque_context, tufreplay_input_stats *stats) {
  InputContext *context = as_context(opaque_context);
  if (context != nullptr)
    context->core.get_stats(stats);
}

}  // extern "C"
