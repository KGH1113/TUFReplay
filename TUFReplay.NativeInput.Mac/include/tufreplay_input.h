#ifndef TUFREPLAY_INPUT_H
#define TUFREPLAY_INPUT_H

#include <stdbool.h>
#include <stdint.h>

#if defined(__cplusplus)
extern "C" {
#endif

#define TUFREPLAY_INPUT_ABI_VERSION 1u
#define TUFREPLAY_INPUT_USAGE_CAPACITY 256u

typedef enum tufreplay_input_access {
  TUFREPLAY_INPUT_ACCESS_GRANTED = 0,
  TUFREPLAY_INPUT_ACCESS_DENIED = 1,
  TUFREPLAY_INPUT_ACCESS_UNKNOWN = 2,
  TUFREPLAY_INPUT_ACCESS_UNAVAILABLE = 3,
} tufreplay_input_access;

typedef enum tufreplay_input_error {
  TUFREPLAY_INPUT_ERROR_NONE = 0,
  TUFREPLAY_INPUT_ERROR_PERMISSION = 1,
  TUFREPLAY_INPUT_ERROR_MANAGER_CREATE = 2,
  TUFREPLAY_INPUT_ERROR_MANAGER_OPEN = 3,
  TUFREPLAY_INPUT_ERROR_THREAD_START = 4,
  TUFREPLAY_INPUT_ERROR_START_TIMEOUT = 5,
} tufreplay_input_error;

typedef struct tufreplay_input_event {
  uint64_t mach_timestamp;
  uint64_t modifier_flags;
  uint32_t usage;
  uint8_t down;
  uint8_t reserved[3];
} tufreplay_input_event;

typedef struct tufreplay_input_stats {
  uint64_t callback_count;
  uint64_t queued_count;
  uint64_t dropped_count;
  uint64_t repeat_count;
  uint64_t unmapped_count;
  uint32_t device_count;
  uint32_t queue_depth;
} tufreplay_input_stats;

uint32_t tufreplay_input_abi_version(void);
int32_t tufreplay_input_check_access(void);
int32_t tufreplay_input_request_access(void);
void tufreplay_input_timebase(uint32_t *numerator, uint32_t *denominator);
uint64_t tufreplay_input_mach_now(void);
void *tufreplay_input_create(void);
int32_t tufreplay_input_start(void *opaque_context);
void tufreplay_input_stop(void *opaque_context);
void tufreplay_input_destroy(void *opaque_context);
bool tufreplay_input_is_running(void *opaque_context);
int32_t tufreplay_input_last_error(void *opaque_context);
int32_t tufreplay_input_wait_dequeue(void *opaque_context, tufreplay_input_event *events, int32_t capacity, int32_t timeout_ms);
int32_t tufreplay_input_copy_state(void *opaque_context, uint8_t *usage_down, int32_t capacity);
uint64_t tufreplay_input_take_dropped(void *opaque_context);
void tufreplay_input_get_stats(void *opaque_context, tufreplay_input_stats *stats);

#if defined(__cplusplus)
}
#endif

#endif
