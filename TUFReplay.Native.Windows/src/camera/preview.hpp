#ifndef TUFREPLAY_CAMERA_PREVIEW_HPP
#define TUFREPLAY_CAMERA_PREVIEW_HPP

#include <cstddef>
#include <cstdint>
#include <vector>

#include "platform.hpp"

namespace tufreplay::camera {

struct FrameSize {
  int32_t width = 0;
  int32_t height = 0;
};

// H.264/YUV420 dimensions must be even. Scale uniformly and never upscale.
// Matches CameraFrameSize.Fit in managed code.
FrameSize fit_size(int32_t width, int32_t height, int32_t max_width, int32_t max_height);

// Bottom-up RGBA preview from I420. Area averaging prevents fine detail from
// aliasing when downscaled; chroma upsampling is bilinear. Coefficients are built
// once, so conversion never allocates.
class PreviewConverter {
 public:
  bool configure(int32_t width, int32_t height, int32_t target_width, int32_t target_height);
  void convert(const uint8_t *i420, uint8_t *rgba) const;
  int32_t target_width() const { return target_width_; }
  int32_t target_height() const { return target_height_; }
  size_t target_bytes() const { return static_cast<size_t>(target_width_) * target_height_ * 4; }

 private:
  struct Axis {
    std::vector<int32_t> first;
    std::vector<int32_t> offset; // Into weights.
    std::vector<int32_t> count;
    std::vector<int32_t> weights;
  };
  static void build_axis(int32_t source, int32_t target, Axis &axis);
  static int32_t sample(const uint8_t *plane, int32_t stride, const Axis &x, int32_t xi, const Axis &y, int32_t yi);

  int32_t width_ = 0, height_ = 0, target_width_ = 0, target_height_ = 0;
  Axis luma_x_, luma_y_, chroma_x_, chroma_y_;
};

// The shared preview mapping read by Unity and the companion (CameraPreviewBuffer).
// Layout: int64 sequence (odd while writing), int32 width, int32 height, int32
// preview-requested flag at 16, pixels from byte 64.
class PreviewTarget {
 public:
  static constexpr size_t kHeaderSize = 64;
  ~PreviewTarget() { close(); }
  bool open(const char *path, int32_t max_width, int32_t max_height);
  void close();
  bool is_open() const { return mapped_.data != nullptr; }
  // A first frame is always published for readiness; later ones only on demand.
  bool should_publish() const;
  bool publish(const uint8_t *rgba, int32_t width, int32_t height);
  int32_t max_width() const { return max_width_; }
  int32_t max_height() const { return max_height_; }

 private:
  MappedFile mapped_;
  int32_t max_width_ = 0, max_height_ = 0;
};

} // namespace tufreplay::camera

#endif
