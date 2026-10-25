#define WIN32_LEAN_AND_MEAN
#include <windows.h>

#include <atomic>
#include <new>

#include "event_ring.hpp"
#include "tufreplay_win_native.h"

namespace {
using tufreplay::native::EventRing;

constexpr size_t kQueueCapacity = 8192;
constexpr DWORD kStartTimeoutMs = 2000;
constexpr DWORD kStopTimeoutMs = 2000;

struct InputContext {
  EventRing<tufreplay_win_key_event, kQueueCapacity> ring;
  HANDLE thread = nullptr;
  HANDLE started = nullptr;
  HANDLE events_available = nullptr;
  std::atomic<DWORD> thread_id{0};
  std::atomic<bool> running{false}, wake_pending{false};
  std::atomic<int32_t> last_error{TUFREPLAY_WIN_INPUT_ERROR_NONE}, last_system_error{0};
  std::atomic<uint64_t> callbacks{0}, queued{0};
};

// WH_KEYBOARD_LL procedures receive no user data, but the system calls them on the
// thread that installed the hook, so each capture thread finds its own context.
thread_local InputContext *t_context = nullptr;

void signal_if_queued(InputContext *context) {
  // Coalesce wakeups: the bridge drains every queued event per wakeup.
  if (!context->wake_pending.exchange(true, std::memory_order_acq_rel))
    SetEvent(context->events_available);
}

LRESULT CALLBACK keyboard_hook(int code, WPARAM message, LPARAM data) {
  InputContext *context = t_context;
  if (code == HC_ACTION && context != nullptr) {
    LARGE_INTEGER now;
    QueryPerformanceCounter(&now);
    const bool down = message == WM_KEYDOWN || message == WM_SYSKEYDOWN;
    const bool up = message == WM_KEYUP || message == WM_SYSKEYUP;
    if (down || up) {
      const auto *hook = reinterpret_cast<const KBDLLHOOKSTRUCT *>(data);
      tufreplay_win_key_event event{};
      event.qpc_ticks = now.QuadPart;
      event.time_ms = hook->time;
      event.virtual_key = hook->vkCode;
      event.scan_code = hook->scanCode;
      event.flags = hook->flags;
      event.down = down ? 1 : 0;
      context->callbacks.fetch_add(1, std::memory_order_relaxed);
      if (context->ring.push(event)) {
        context->queued.fetch_add(1, std::memory_order_relaxed);
        signal_if_queued(context);
      }
    }
  }
  // Never call managed code, take a lock, or wait here: every low-level hook in
  // the system runs in one chain, and a slow procedure delays all keyboard input.
  return CallNextHookEx(nullptr, code, message, data);
}

HMODULE this_module() {
  HMODULE module = nullptr;
  GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
    reinterpret_cast<LPCWSTR>(&keyboard_hook), &module);
  return module;
}

DWORD WINAPI hook_thread(void *opaque) {
  auto *context = static_cast<InputContext *>(opaque);
  // The system waits for this thread on every keystroke; answer promptly even when
  // capture, encoding or the game saturate the CPU.
  SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_HIGHEST);
  MSG message;
  PeekMessageW(&message, nullptr, 0, 0, PM_NOREMOVE); // Create the message queue.
  context->thread_id.store(GetCurrentThreadId(), std::memory_order_release);

  t_context = context;
  HHOOK hook = SetWindowsHookExW(WH_KEYBOARD_LL, keyboard_hook, this_module(), 0);
  if (hook == nullptr) {
    context->last_system_error.store(static_cast<int32_t>(GetLastError()), std::memory_order_release);
    context->last_error.store(TUFREPLAY_WIN_INPUT_ERROR_HOOK_INSTALL, std::memory_order_release);
    t_context = nullptr;
    SetEvent(context->started);
    return 0;
  }
  context->running.store(true, std::memory_order_release);
  SetEvent(context->started);

  while (GetMessageW(&message, nullptr, 0, 0) > 0) {
    TranslateMessage(&message);
    DispatchMessageW(&message);
  }

  UnhookWindowsHookEx(hook);
  t_context = nullptr;
  context->running.store(false, std::memory_order_release);
  SetEvent(context->events_available);
  return 0;
}

InputContext *as_context(void *opaque) { return static_cast<InputContext *>(opaque); }
} // namespace

extern "C" {
uint32_t tufreplay_win_native_abi_version(void) { return TUFREPLAY_WIN_NATIVE_ABI_VERSION; }

void *tufreplay_win_input_create(void) {
  auto *context = new (std::nothrow) InputContext();
  if (context == nullptr) return nullptr;
  context->started = CreateEventW(nullptr, TRUE, FALSE, nullptr);
  context->events_available = CreateEventW(nullptr, FALSE, FALSE, nullptr);
  if (context->started == nullptr || context->events_available == nullptr) {
    if (context->started != nullptr) CloseHandle(context->started);
    if (context->events_available != nullptr) CloseHandle(context->events_available);
    delete context;
    return nullptr;
  }
  return context;
}

int32_t tufreplay_win_input_start(void *opaque) {
  InputContext *context = as_context(opaque);
  if (context == nullptr) return TUFREPLAY_WIN_INPUT_ERROR_THREAD_START;
  if (context->running.load(std::memory_order_acquire)) return TUFREPLAY_WIN_INPUT_ERROR_NONE;
  if (context->thread != nullptr) return TUFREPLAY_WIN_INPUT_ERROR_THREAD_START;
  context->last_error.store(TUFREPLAY_WIN_INPUT_ERROR_NONE, std::memory_order_release);
  ResetEvent(context->started);
  context->thread = CreateThread(nullptr, 0, hook_thread, context, 0, nullptr);
  if (context->thread == nullptr) {
    context->last_system_error.store(static_cast<int32_t>(GetLastError()), std::memory_order_release);
    context->last_error.store(TUFREPLAY_WIN_INPUT_ERROR_THREAD_START, std::memory_order_release);
    return TUFREPLAY_WIN_INPUT_ERROR_THREAD_START;
  }
  if (WaitForSingleObject(context->started, kStartTimeoutMs) != WAIT_OBJECT_0) {
    context->last_error.store(TUFREPLAY_WIN_INPUT_ERROR_START_TIMEOUT, std::memory_order_release);
    tufreplay_win_input_stop(context);
    return TUFREPLAY_WIN_INPUT_ERROR_START_TIMEOUT;
  }
  if (!context->running.load(std::memory_order_acquire)) {
    WaitForSingleObject(context->thread, kStopTimeoutMs);
    CloseHandle(context->thread);
    context->thread = nullptr;
    return context->last_error.load(std::memory_order_acquire);
  }
  return TUFREPLAY_WIN_INPUT_ERROR_NONE;
}

void tufreplay_win_input_stop(void *opaque) {
  InputContext *context = as_context(opaque);
  if (context == nullptr || context->thread == nullptr) return;
  DWORD thread_id = context->thread_id.load(std::memory_order_acquire);
  if (thread_id != 0) PostThreadMessageW(thread_id, WM_QUIT, 0, 0);
  if (GetCurrentThreadId() != thread_id) WaitForSingleObject(context->thread, kStopTimeoutMs);
  CloseHandle(context->thread);
  context->thread = nullptr;
  context->thread_id.store(0, std::memory_order_release);
  context->running.store(false, std::memory_order_release);
  SetEvent(context->events_available);
}

void tufreplay_win_input_destroy(void *opaque) {
  InputContext *context = as_context(opaque);
  if (context == nullptr) return;
  tufreplay_win_input_stop(context);
  CloseHandle(context->started);
  CloseHandle(context->events_available);
  delete context;
}

int32_t tufreplay_win_input_is_running(void *opaque) {
  InputContext *context = as_context(opaque);
  return context != nullptr && context->running.load(std::memory_order_acquire) ? 1 : 0;
}

int32_t tufreplay_win_input_last_error(void *opaque) {
  InputContext *context = as_context(opaque);
  return context == nullptr ? TUFREPLAY_WIN_INPUT_ERROR_THREAD_START
                            : context->last_error.load(std::memory_order_acquire);
}

int32_t tufreplay_win_input_last_system_error(void *opaque) {
  InputContext *context = as_context(opaque);
  return context == nullptr ? 0 : context->last_system_error.load(std::memory_order_acquire);
}

int32_t tufreplay_win_input_wait_dequeue(void *opaque, tufreplay_win_key_event *events, int32_t capacity,
  int32_t timeout_ms) {
  InputContext *context = as_context(opaque);
  if (context == nullptr || events == nullptr || capacity <= 0) return 0;
  context->wake_pending.store(false, std::memory_order_release);
  WaitForSingleObject(context->events_available, 0);
  int32_t count = context->ring.drain(events, capacity);
  if (count != 0 || timeout_ms <= 0 || !context->running.load(std::memory_order_acquire)) return count;
  WaitForSingleObject(context->events_available, static_cast<DWORD>(timeout_ms));
  context->wake_pending.store(false, std::memory_order_release);
  return context->ring.drain(events, capacity);
}

uint64_t tufreplay_win_input_take_dropped(void *opaque) {
  InputContext *context = as_context(opaque);
  return context == nullptr ? 0 : context->ring.take_dropped();
}

void tufreplay_win_input_get_stats(void *opaque, tufreplay_win_input_stats *stats) {
  InputContext *context = as_context(opaque);
  if (context == nullptr || stats == nullptr) return;
  stats->callback_count = context->callbacks.load(std::memory_order_acquire);
  stats->queued_count = context->queued.load(std::memory_order_acquire);
  stats->dropped_count = context->ring.dropped();
  stats->queue_depth = context->ring.depth();
  stats->reserved = 0;
}
} // extern "C"
