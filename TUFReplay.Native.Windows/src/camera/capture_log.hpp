#ifndef TUFREPLAY_CAMERA_CAPTURE_LOG_HPP
#define TUFREPLAY_CAMERA_CAPTURE_LOG_HPP

#include <cstddef>
#include <cstdint>

namespace tufreplay::camera {

// One FFmpeg stderr line from the capture process. The parser reads each line
// in place, without allocating, so steady capture creates no garbage at all.
struct CaptureLogLine {
  enum class Kind { Other, TimeBase, Frame, ShowinfoOther };
  Kind kind = Kind::Other;
  bool has_time_base = false;
  int64_t time_base_numerator = 0;
  int64_t time_base_denominator = 0;
  bool has_input_rate = false;
  int64_t input_rate_numerator = 0;
  int64_t input_rate_denominator = 0;
  int64_t pts = 0;
  int64_t width = 0;
  int64_t height = 0;
  bool frame_size_overflow = false;
};

// Equivalent to the managed parser's patterns:
//   config in time_base: (\d+)/(\d+)
//   \bframe_rate:\s*(\d+)/(\d+)(?:\s|$)   (positive, input rate only)
//   \bn:\s*\d+\s+pts:\s*(-?\d+) and \bs:(\d+)x(\d+)
CaptureLogLine parse_capture_log_line(const char *line, size_t length);

// Converts a wall-clock PTS to Stopwatch ticks like WebcamWallClock.ToTimestamp,
// including midpoint-to-even rounding. Returns false on overflow or a bad time base.
bool wall_clock_to_ticks(int64_t pts, int64_t numerator, int64_t denominator, int64_t anchor_unix_us,
  int64_t anchor_ticks, int64_t frequency, int64_t *ticks);

} // namespace tufreplay::camera

#endif
