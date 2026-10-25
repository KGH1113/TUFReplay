#ifndef TUFREPLAY_CAMERA_H
#define TUFREPLAY_CAMERA_H

#include <stdint.h>

#if defined(_WIN32)
#define TUFREPLAY_CAMERA_EXPORT __declspec(dllexport)
#else
#define TUFREPLAY_CAMERA_EXPORT __attribute__((visibility("default")))
#endif

#if defined(__cplusplus)
extern "C" {
#endif

// Camera relay between the FFmpeg capture and encoder processes. Raw frames,
// their timestamps, preview conversion and the timestamped encoder stream stay in
// native memory and native threads; managed code only controls the lifecycle.
#define TUFREPLAY_CAMERA_ABI_VERSION 1u

typedef enum tufreplay_camera_error {
  TUFREPLAY_CAMERA_ERROR_NONE = 0,
  TUFREPLAY_CAMERA_ERROR_STOPPED_SENDING = 1,
  TUFREPLAY_CAMERA_ERROR_RESOLUTION_UNAVAILABLE = 2,
  TUFREPLAY_CAMERA_ERROR_RESOLUTION_CHANGED = 3,
  TUFREPLAY_CAMERA_ERROR_FELL_BEHIND = 4,
  TUFREPLAY_CAMERA_ERROR_TIMESTAMP = 5,
  TUFREPLAY_CAMERA_ERROR_OUT_OF_MEMORY = 6,
  TUFREPLAY_CAMERA_ERROR_STOPPED = 7,
  TUFREPLAY_CAMERA_ERROR_INVALID_ARGUMENT = 8,
} tufreplay_camera_error;

typedef struct tufreplay_camera_config {
  int64_t frame_handle;     // Capture stdout: a Windows HANDLE or POSIX fd. Duplicated.
  int64_t log_handle;       // Capture stderr with FFmpeg showinfo lines. Duplicated.
  int32_t max_width;        // Capture bounds; frames outside them fail capture.
  int32_t max_height;
  int64_t clock_unix_us;    // Wall-clock anchor for -use_wallclock_as_timestamps.
  int64_t clock_ticks;      // The same instant on the managed Stopwatch clock.
  int64_t ticks_frequency;  // Stopwatch.Frequency.
  int32_t pool_capacity;    // Raw I420 frames allocated once the size is known.
  int32_t timestamp_timeout_ms;
  const char *preview_path; // UTF-8 path of the shared preview mapping, or null.
  int32_t preview_max_width;
  int32_t preview_max_height;
} tufreplay_camera_config;

typedef struct tufreplay_camera_state {
  int32_t ready;
  int32_t error;
  int32_t width;
  int32_t height;
  int64_t input_rate_numerator;
  int64_t input_rate_denominator;
  int64_t frames_captured;
  int64_t previews_published;
} tufreplay_camera_state;

typedef struct tufreplay_camera_recording_stats {
  int64_t written_frames;
  int64_t first_timestamp_ticks;
  int64_t duration_us;
  int64_t dropped_frames;
  int32_t writer_finished;
  int32_t write_failed;         // Output failed before any frame was written.
  int32_t output_closed_early;  // Output closed after frames, e.g. FFmpeg's -fs limit.
  int32_t capture_failed;
} tufreplay_camera_recording_stats;

TUFREPLAY_CAMERA_EXPORT uint32_t tufreplay_camera_abi_version(void);

TUFREPLAY_CAMERA_EXPORT void *tufreplay_camera_create(const tufreplay_camera_config *config);
// Why the last create on this thread failed: 1 invalid config, 2 capture handles,
// 3 preview mapping, 4 threads; with the system error code in *system_error.
TUFREPLAY_CAMERA_EXPORT int32_t tufreplay_camera_last_create_error(int32_t *system_error);
// 1 when the first frame arrived, -1 on failure, 0 on timeout.
TUFREPLAY_CAMERA_EXPORT int32_t tufreplay_camera_wait_ready(void *camera, int32_t timeout_ms);
TUFREPLAY_CAMERA_EXPORT void tufreplay_camera_get_state(void *camera, tufreplay_camera_state *state);
// Recent non-frame FFmpeg log lines joined by " | " as UTF-8; returns the byte count.
TUFREPLAY_CAMERA_EXPORT int32_t tufreplay_camera_get_diagnostics(void *camera, char *destination, int32_t capacity);
// Stops consumers and preview writes; reader threads end when the process exits.
TUFREPLAY_CAMERA_EXPORT void tufreplay_camera_stop(void *camera);
// Frees the relay once its readers ended; returns 0 and keeps it alive otherwise.
TUFREPLAY_CAMERA_EXPORT int32_t tufreplay_camera_destroy(void *camera, int32_t timeout_ms);

// Attaches a run. Frames captured before boundary_ticks are excluded; the latest
// frame is reused when it is at most 250 ms older than now_ticks.
TUFREPLAY_CAMERA_EXPORT void *tufreplay_camera_recording_attach(
  void *camera, int64_t boundary_ticks, int64_t now_ticks, int32_t frame_rate, int32_t queue_capacity);
// Starts writing the timestamped stream to the encoder's stdin (duplicated).
TUFREPLAY_CAMERA_EXPORT int32_t tufreplay_camera_recording_set_output(void *recording, int64_t output_handle);
TUFREPLAY_CAMERA_EXPORT void tufreplay_camera_recording_get_stats(void *recording, tufreplay_camera_recording_stats *stats);
// Ends the run boundary: no new frames are accepted. Queued frames stay queued and
// are still written once the output is set; call finish afterwards.
TUFREPLAY_CAMERA_EXPORT void tufreplay_camera_recording_seal(void *recording);
// Seals, writes queued frames, closes the output and waits for the writer.
// Returns 1 when the writer finished within timeout_ms.
TUFREPLAY_CAMERA_EXPORT int32_t tufreplay_camera_recording_finish(
  void *recording, int32_t timeout_ms, tufreplay_camera_recording_stats *stats);
// Detaches and drops queued frames. The caller kills the encoder to unblock writes.
TUFREPLAY_CAMERA_EXPORT void tufreplay_camera_recording_cancel(void *recording);
TUFREPLAY_CAMERA_EXPORT int32_t tufreplay_camera_recording_destroy(void *recording, int32_t timeout_ms);

#if defined(__cplusplus)
}
#endif

#endif
