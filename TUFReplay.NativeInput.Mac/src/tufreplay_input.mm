#include "input_core.hpp"
#include <CoreFoundation/CoreFoundation.h>
#include <CoreGraphics/CoreGraphics.h>
#include <dispatch/dispatch.h>
#include <mach/mach_time.h>
#include <pthread.h>
#include <new>

using tufreplay::input::InputCore;

namespace {
struct InputContext {
  InputCore core;
  CFMachPortRef tap = nullptr; // Owned exclusively by the native RunLoop thread.
  pthread_t thread{};
  bool thread_created = false;
  std::atomic<bool> running{false}, stopping{false}, wake_pending{false};
  std::atomic<int32_t> last_error{TUFREPLAY_INPUT_ERROR_NONE}, last_system_error{0};
  dispatch_semaphore_t started = dispatch_semaphore_create(0);
  dispatch_semaphore_t events_available = dispatch_semaphore_create(0);
  mach_timebase_info_data_t timebase{};
};

uint64_t clock_now_ns(const mach_timebase_info_data_t &timebase) {
  return static_cast<uint64_t>((static_cast<__uint128_t>(mach_absolute_time()) * timebase.numer) / timebase.denom);
}
bool query_state(uint32_t key) {
  if (key < TUFREPLAY_INPUT_MOUSE_BASE) {
    if (key == 0x39) return false; // Caps Lock is represented as a momentary transition.
    return CGEventSourceKeyState(kCGEventSourceStateCombinedSessionState, static_cast<CGKeyCode>(key));
  }
  return CGEventSourceButtonState(kCGEventSourceStateCombinedSessionState,
    static_cast<CGMouseButton>(key - TUFREPLAY_INPUT_MOUSE_BASE));
}
void synchronize(InputContext *context) {
  for (uint32_t key = 0; key < TUFREPLAY_INPUT_KEY_CAPACITY; ++key)
    context->core.synchronize(key, query_state(key));
}
void signal_if_queued(InputContext *context, bool queued) {
  // Coalesce wakeups: a notification per event accumulates stale semaphore
  // credits when the bridge drains a whole batch in one operation.
  if (queued && !context->wake_pending.exchange(true, std::memory_order_acq_rel))
    dispatch_semaphore_signal(context->events_available);
}
constexpr CGEventMask event_mask() {
  return CGEventMaskBit(kCGEventKeyDown) | CGEventMaskBit(kCGEventKeyUp)
    | CGEventMaskBit(kCGEventFlagsChanged)
    | CGEventMaskBit(kCGEventLeftMouseDown) | CGEventMaskBit(kCGEventLeftMouseUp)
    | CGEventMaskBit(kCGEventRightMouseDown) | CGEventMaskBit(kCGEventRightMouseUp)
    | CGEventMaskBit(kCGEventOtherMouseDown) | CGEventMaskBit(kCGEventOtherMouseUp);
}
CGEventRef on_event(CGEventTapProxy, CGEventType type, CGEventRef event, void *opaque) {
  auto *context = static_cast<InputContext *>(opaque);
  if (context->stopping.load(std::memory_order_relaxed)) return event;
  if (type == kCGEventTapDisabledByTimeout || type == kCGEventTapDisabledByUserInput) {
    context->core.mark_fault(type == kCGEventTapDisabledByTimeout
      ? TUFREPLAY_INPUT_FAULT_TAP_TIMEOUT : TUFREPLAY_INPUT_FAULT_TAP_DISABLED);
    return event; // Recover outside the callback on this same native thread.
  }
  if (event == nullptr) return event;
  const uint64_t timestamp = CGEventGetTimestamp(event); // Nanoseconds, not Mach ticks.
  const uint64_t flags = static_cast<uint64_t>(CGEventGetFlags(event));
  context->core.observe_delay(timestamp, clock_now_ns(context->timebase));
  bool queued = false;
  if (type == kCGEventKeyDown || type == kCGEventKeyUp || type == kCGEventFlagsChanged) {
    const int64_t key = CGEventGetIntegerValueField(event, kCGKeyboardEventKeycode);
    if (key < 0 || key >= TUFREPLAY_INPUT_MOUSE_BASE) return event;
    queued = type == kCGEventFlagsChanged
      ? context->core.apply_modifier(static_cast<uint32_t>(key), flags, timestamp)
      : context->core.apply(static_cast<uint32_t>(key), type == kCGEventKeyDown, timestamp, flags);
  } else if (type < 64 && (event_mask() & CGEventMaskBit(type)) != 0) {
    const int64_t button = CGEventGetIntegerValueField(event, kCGMouseEventButtonNumber);
    if (button < 0 || button >= 32) return event;
    const bool down = type == kCGEventLeftMouseDown || type == kCGEventRightMouseDown || type == kCGEventOtherMouseDown;
    queued = context->core.apply(TUFREPLAY_INPUT_MOUSE_BASE + static_cast<uint32_t>(button), down, timestamp, flags);
  }
  signal_if_queued(context, queued);
  // Never call managed code, take a lock, post an event, or wait for Unity.
  return event;
}
void *input_thread_main(void *opaque) {
  auto *context = static_cast<InputContext *>(opaque);
  pthread_setname_np("TUFReplay CGEvent capture");
  context->tap = CGEventTapCreate(kCGSessionEventTap, kCGTailAppendEventTap,
    kCGEventTapOptionListenOnly, event_mask(), on_event, context);
  if (context->tap == nullptr) {
    context->last_error.store(TUFREPLAY_INPUT_ERROR_TAP_CREATE, std::memory_order_release);
    dispatch_semaphore_signal(context->started);
    return nullptr;
  }
  CFRunLoopSourceRef source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, context->tap, 0);
  if (source == nullptr) {
    CFRelease(context->tap);
    context->tap = nullptr;
    context->last_error.store(TUFREPLAY_INPUT_ERROR_RUN_LOOP_SOURCE, std::memory_order_release);
    dispatch_semaphore_signal(context->started);
    return nullptr;
  }
  CFRunLoopRef loop = CFRunLoopGetCurrent();
  CFRunLoopAddSource(loop, source, kCFRunLoopDefaultMode);
  synchronize(context);
  CGEventTapEnable(context->tap, true);
  context->running.store(true, std::memory_order_release);
  dispatch_semaphore_signal(context->started);
  while (!context->stopping.load(std::memory_order_acquire)) {
    // Bounded slices let Stop finish without sharing a CFRunLoopRef whose
    // lifetime could race shutdown, or depending on Unity's message loop.
    CFRunLoopRunInMode(kCFRunLoopDefaultMode, 0.05, false);
    if (!CGEventTapIsEnabled(context->tap)) {
      context->core.mark_fault(TUFREPLAY_INPUT_FAULT_TAP_DISABLED);
      synchronize(context);
      CGEventTapEnable(context->tap, true);
    }
  }
  CGEventTapEnable(context->tap, false);
  CFMachPortInvalidate(context->tap);
  CFRunLoopRemoveSource(loop, source, kCFRunLoopDefaultMode);
  CFRelease(source);
  CFRelease(context->tap);
  context->tap = nullptr;
  context->running.store(false, std::memory_order_release);
  dispatch_semaphore_signal(context->events_available);
  return nullptr;
}
InputContext *as_context(void *opaque) { return static_cast<InputContext *>(opaque); }
} // namespace

extern "C" {
uint32_t tufreplay_input_abi_version(void) { return TUFREPLAY_INPUT_ABI_VERSION; }
int32_t tufreplay_input_check_access(void) {
  return CGPreflightListenEventAccess() ? TUFREPLAY_INPUT_ACCESS_GRANTED : TUFREPLAY_INPUT_ACCESS_UNKNOWN;
}
int32_t tufreplay_input_request_access(void) {
  return CGRequestListenEventAccess() ? TUFREPLAY_INPUT_ACCESS_GRANTED : TUFREPLAY_INPUT_ACCESS_DENIED;
}
uint64_t tufreplay_input_clock_now_ns(void) {
  mach_timebase_info_data_t timebase{};
  mach_timebase_info(&timebase);
  return clock_now_ns(timebase);
}
void *tufreplay_input_create(void) {
  auto *context = new (std::nothrow) InputContext();
  if (context != nullptr) mach_timebase_info(&context->timebase);
  return context;
}
int32_t tufreplay_input_start(void *opaque) {
  auto *context = as_context(opaque);
  if (context == nullptr) return TUFREPLAY_INPUT_ERROR_THREAD_START;
  if (context->running.load(std::memory_order_acquire)) return TUFREPLAY_INPUT_ERROR_NONE;
  if (context->thread_created) return TUFREPLAY_INPUT_ERROR_THREAD_START;
  if (tufreplay_input_check_access() != TUFREPLAY_INPUT_ACCESS_GRANTED) {
    context->last_error.store(TUFREPLAY_INPUT_ERROR_PERMISSION, std::memory_order_release);
    return TUFREPLAY_INPUT_ERROR_PERMISSION;
  }
  const int result = pthread_create(&context->thread, nullptr, input_thread_main, context);
  if (result != 0) {
    context->last_system_error.store(result, std::memory_order_release);
    context->last_error.store(TUFREPLAY_INPUT_ERROR_THREAD_START, std::memory_order_release);
    return TUFREPLAY_INPUT_ERROR_THREAD_START;
  }
  context->thread_created = true;
  if (dispatch_semaphore_wait(context->started, dispatch_time(DISPATCH_TIME_NOW, 2ll * NSEC_PER_SEC)) != 0) {
    context->last_error.store(TUFREPLAY_INPUT_ERROR_START_TIMEOUT, std::memory_order_release);
    tufreplay_input_stop(context);
    return TUFREPLAY_INPUT_ERROR_START_TIMEOUT;
  }
  return context->running.load(std::memory_order_acquire) ? TUFREPLAY_INPUT_ERROR_NONE
    : context->last_error.load(std::memory_order_acquire);
}
void tufreplay_input_stop(void *opaque) {
  auto *context = as_context(opaque);
  if (context == nullptr) return;
  context->stopping.store(true, std::memory_order_release);
  dispatch_semaphore_signal(context->events_available);
  if (context->thread_created && !pthread_equal(pthread_self(), context->thread)) {
    pthread_join(context->thread, nullptr);
    context->thread_created = false;
  }
  context->running.store(false, std::memory_order_release);
}
void tufreplay_input_destroy(void *opaque) {
  auto *context = as_context(opaque);
  if (context == nullptr) return;
  tufreplay_input_stop(context);
  delete context;
}
bool tufreplay_input_is_running(void *opaque) {
  auto *context = as_context(opaque);
  return context != nullptr && context->running.load(std::memory_order_acquire);
}
int32_t tufreplay_input_last_error(void *opaque) {
  auto *context = as_context(opaque);
  return context == nullptr ? TUFREPLAY_INPUT_ERROR_THREAD_START : context->last_error.load(std::memory_order_acquire);
}
int32_t tufreplay_input_last_system_error(void *opaque) {
  auto *context = as_context(opaque);
  return context == nullptr ? 0 : context->last_system_error.load(std::memory_order_acquire);
}
int32_t tufreplay_input_wait_dequeue(void *opaque, tufreplay_input_event *events, int32_t capacity, int32_t timeout_ms) {
  auto *context = as_context(opaque);
  if (context == nullptr || events == nullptr || capacity <= 0) return 0;
  context->wake_pending.store(false, std::memory_order_release);
  dispatch_semaphore_wait(context->events_available, DISPATCH_TIME_NOW);
  int32_t count = context->core.drain(events, capacity);
  if (count != 0 || timeout_ms <= 0 || context->stopping.load(std::memory_order_acquire)) return count;
  dispatch_semaphore_wait(context->events_available,
    dispatch_time(DISPATCH_TIME_NOW, static_cast<int64_t>(timeout_ms) * NSEC_PER_MSEC));
  context->wake_pending.store(false, std::memory_order_release);
  return context->core.drain(events, capacity);
}
int32_t tufreplay_input_copy_state(void *opaque, uint8_t *key_down, int32_t capacity) {
  if (as_context(opaque) == nullptr || key_down == nullptr || capacity < static_cast<int32_t>(TUFREPLAY_INPUT_KEY_CAPACITY)) return 0;
  for (uint32_t key = 0; key < TUFREPLAY_INPUT_KEY_CAPACITY; ++key) key_down[key] = query_state(key) ? 1 : 0;
  return TUFREPLAY_INPUT_KEY_CAPACITY;
}
uint64_t tufreplay_input_take_dropped(void *opaque) {
  auto *context = as_context(opaque);
  return context == nullptr ? 0 : context->core.take_dropped();
}
uint32_t tufreplay_input_take_faults(void *opaque) {
  auto *context = as_context(opaque);
  return context == nullptr ? 0 : context->core.take_faults();
}
void tufreplay_input_get_stats(void *opaque, tufreplay_input_stats *stats) {
  auto *context = as_context(opaque);
  if (context != nullptr) context->core.get_stats(stats);
}
} // extern "C"
