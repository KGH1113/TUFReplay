#ifndef TUFREPLAY_WIN_NATIVE_H
#define TUFREPLAY_WIN_NATIVE_H

#include <stdint.h>

#if defined(_WIN32)
#define TUFREPLAY_WIN_EXPORT __declspec(dllexport)
#else
#define TUFREPLAY_WIN_EXPORT
#endif

#if defined(__cplusplus)
extern "C" {
#endif

#define TUFREPLAY_WIN_NATIVE_ABI_VERSION 1u

typedef enum tufreplay_win_input_error {
  TUFREPLAY_WIN_INPUT_ERROR_NONE = 0,
  TUFREPLAY_WIN_INPUT_ERROR_HOOK_INSTALL = 1,
  TUFREPLAY_WIN_INPUT_ERROR_THREAD_START = 2,
  TUFREPLAY_WIN_INPUT_ERROR_START_TIMEOUT = 3,
} tufreplay_win_input_error;

// One WH_KEYBOARD_LL transition, exactly as the hook received it. Normalization
// stays in managed code so recordings keep their existing key semantics.
typedef struct tufreplay_win_key_event {
  int64_t qpc_ticks;    // Raw QueryPerformanceCounter at hook entry. Not Stopwatch ticks under Unity's Mono.
  uint32_t time_ms;     // KBDLLHOOKSTRUCT.time
  uint32_t virtual_key; // KBDLLHOOKSTRUCT.vkCode
  uint32_t scan_code;   // KBDLLHOOKSTRUCT.scanCode
  uint32_t flags;       // KBDLLHOOKSTRUCT.flags
  uint8_t down;
  uint8_t reserved[7];
} tufreplay_win_key_event;

typedef struct tufreplay_win_input_stats {
  uint64_t callback_count;
  uint64_t queued_count;
  uint64_t dropped_count;
  uint32_t queue_depth;
  uint32_t reserved;
} tufreplay_win_input_stats;

typedef enum tufreplay_win_audio_transport {
  TUFREPLAY_WIN_AUDIO_TRANSPORT_UNKNOWN = 0,
  TUFREPLAY_WIN_AUDIO_TRANSPORT_OTHER = 1,
  TUFREPLAY_WIN_AUDIO_TRANSPORT_BLUETOOTH = 2,
} tufreplay_win_audio_transport;

TUFREPLAY_WIN_EXPORT uint32_t tufreplay_win_native_abi_version(void);

// Keyboard capture. The hook procedure runs on a dedicated native thread, never
// calls managed code, never takes a lock and never waits for the consumer.
TUFREPLAY_WIN_EXPORT void *tufreplay_win_input_create(void);
TUFREPLAY_WIN_EXPORT int32_t tufreplay_win_input_start(void *context);
TUFREPLAY_WIN_EXPORT void tufreplay_win_input_stop(void *context);
TUFREPLAY_WIN_EXPORT void tufreplay_win_input_destroy(void *context);
TUFREPLAY_WIN_EXPORT int32_t tufreplay_win_input_is_running(void *context);
TUFREPLAY_WIN_EXPORT int32_t tufreplay_win_input_last_error(void *context);
TUFREPLAY_WIN_EXPORT int32_t tufreplay_win_input_last_system_error(void *context);
TUFREPLAY_WIN_EXPORT int32_t tufreplay_win_input_wait_dequeue(
  void *context, tufreplay_win_key_event *events, int32_t capacity, int32_t timeout_ms);
TUFREPLAY_WIN_EXPORT uint64_t tufreplay_win_input_take_dropped(void *context);
TUFREPLAY_WIN_EXPORT void tufreplay_win_input_get_stats(void *context, tufreplay_win_input_stats *stats);

// Audio capture endpoints. name is a UTF-16 endpoint friendly name as Unity lists
// it; a null name queries the default recording endpoint.
TUFREPLAY_WIN_EXPORT int32_t tufreplay_win_audio_capture_transport(const uint16_t *name);

#if defined(__cplusplus)
}
#endif

#endif
