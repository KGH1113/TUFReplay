#ifndef TUFREPLAY_CAMERA_MATROSKA_WRITER_HPP
#define TUFREPLAY_CAMERA_MATROSKA_WRITER_HPP

#include <cstddef>
#include <cstdint>
#include <vector>

namespace tufreplay::camera {

class ByteSink {
 public:
  virtual ~ByteSink() = default;
  virtual bool write(const void *data, size_t length) = 0;
};

// A streaming Matroska writer for one raw I420 video track. Unlike a rawvideo
// pipe, each frame carries its capture time, so dropping queued frames cannot
// shorten the recording or shift it relative to the input replay.
// See https://www.matroska.org/technical/codec_specs.html#v-ms-vfw-fourcc.
class MatroskaWriter {
 public:
  enum class Result { Ok, Regressed, WriteFailed, Invalid };

  MatroskaWriter(ByteSink &sink, int32_t width, int32_t height, int32_t frame_rate, int64_t ticks_frequency);

  Result write_header();
  // A black frame at time zero, before any captured frame. It makes the encoder
  // open its session ahead of the run; the encoder drops its packet and rebases
  // the run's timestamps (see FfmpegCameraEncoder.GetWarmUpArguments).
  Result write_warm_up_frame();
  // pixels must hold width * height * 3 / 2 bytes.
  Result write_frame(const uint8_t *pixels, int64_t timestamp_ticks);

  bool valid() const { return valid_; }
  int64_t first_timestamp_ticks() const { return first_ticks_; }
  int64_t last_timestamp_us() const { return last_us_; }
  int64_t duration_us() const { return has_frame_ ? last_us_ + frame_duration_us_ : 0; }

 private:
  Result write_block(const uint8_t *pixels, int64_t timecode_us);

  ByteSink &sink_;
  int32_t width_, height_, frame_rate_;
  int64_t frequency_;
  size_t frame_bytes_ = 0;
  int64_t frame_duration_us_ = 1;
  bool valid_ = false;
  bool has_frame_ = false;
  int64_t first_ticks_ = 0;
  int64_t last_ticks_ = 0;
  int64_t last_us_ = 0;
  // Captured frames follow the warm-up frame by one frame duration.
  int64_t offset_us_ = 0;
  uint8_t packet_header_[64] = {};
};

} // namespace tufreplay::camera

#endif
