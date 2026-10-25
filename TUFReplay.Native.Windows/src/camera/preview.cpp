#include "preview.hpp"

#include <algorithm>
#include <cmath>
#include <cstring>

namespace tufreplay::camera {

namespace {
uint8_t clamp_byte(int32_t value) { return static_cast<uint8_t>(value < 0 ? 0 : value > 255 ? 255 : value); }

// Math.Round in managed code rounds midpoints to even, like the default FPU mode.
int32_t round_even(double value) { return static_cast<int32_t>(std::nearbyint(value)); }

template <typename T>
T load(const void *base, size_t offset) {
  return __atomic_load_n(reinterpret_cast<const T *>(static_cast<const uint8_t *>(base) + offset), __ATOMIC_ACQUIRE);
}

template <typename T>
void store(void *base, size_t offset, T value) {
  __atomic_store_n(reinterpret_cast<T *>(static_cast<uint8_t *>(base) + offset), value, __ATOMIC_RELEASE);
}
} // namespace

FrameSize fit_size(int32_t width, int32_t height, int32_t max_width, int32_t max_height) {
  if (width < 2 || height < 2 || max_width < 2 || max_height < 2) return {};
  double scale = std::min(1.0,
    std::min(static_cast<double>(max_width) / width, static_cast<double>(max_height) / height));
  return {std::max(2, static_cast<int32_t>(width * scale) / 2 * 2),
    std::max(2, static_cast<int32_t>(height * scale) / 2 * 2)};
}

bool PreviewConverter::configure(int32_t width, int32_t height, int32_t target_width, int32_t target_height) {
  if ((width & 1) != 0 || (height & 1) != 0 || width < 2 || height < 2 || target_width < 1 || target_height < 1
    || target_width > width || target_height > height)
    return false;
  width_ = width;
  height_ = height;
  target_width_ = target_width;
  target_height_ = target_height;
  build_axis(width, target_width, luma_x_);
  build_axis(height, target_height, luma_y_);
  build_axis(width / 2, target_width, chroma_x_);
  build_axis(height / 2, target_height, chroma_y_);
  return true;
}

void PreviewConverter::build_axis(int32_t source, int32_t target, Axis &axis) {
  axis.first.assign(static_cast<size_t>(target), 0);
  axis.offset.assign(static_cast<size_t>(target), 0);
  axis.count.assign(static_cast<size_t>(target), 0);
  axis.weights.clear();
  const double scale = static_cast<double>(source) / target;
  for (int32_t index = 0; index < target; ++index) {
    axis.offset[index] = static_cast<int32_t>(axis.weights.size());
    if (scale < 1) {
      double position = std::max(0.0, std::min(static_cast<double>(source - 1), (index + 0.5) * scale - 0.5));
      int32_t first = static_cast<int32_t>(position);
      int32_t next_weight = round_even((position - first) * 256);
      axis.first[index] = first;
      if (first == source - 1) {
        axis.weights.push_back(256);
      } else {
        axis.weights.push_back(256 - next_weight);
        axis.weights.push_back(next_weight);
      }
    } else {
      double start = index * scale;
      double end = (index + 1) * scale;
      int32_t first = static_cast<int32_t>(start);
      int32_t last = std::min(source - 1, static_cast<int32_t>(std::ceil(end)) - 1);
      int32_t taps = last - first + 1;
      int32_t total = 0;
      for (int32_t tap = 0; tap < taps - 1; ++tap) {
        double overlap = std::min(end, static_cast<double>(first + tap + 1)) - std::max(start, static_cast<double>(first + tap));
        int32_t weight = round_even(overlap / scale * 256);
        axis.weights.push_back(weight);
        total += weight;
      }
      axis.weights.push_back(256 - total);
      axis.first[index] = first;
    }
    axis.count[index] = static_cast<int32_t>(axis.weights.size()) - axis.offset[index];
  }
}

int32_t PreviewConverter::sample(
  const uint8_t *plane, int32_t stride, const Axis &x, int32_t xi, const Axis &y, int32_t yi) {
  const int32_t x_first = x.first[xi], x_count = x.count[xi];
  const int32_t y_first = y.first[yi], y_count = y.count[yi];
  if (x_count == 1 && y_count == 1) return plane[y_first * stride + x_first];
  const int32_t *x_weights = &x.weights[x.offset[xi]];
  const int32_t *y_weights = &y.weights[y.offset[yi]];
  int32_t value = 0;
  for (int32_t row = 0; row < y_count; ++row) {
    const uint8_t *start = plane + (y_first + row) * stride + x_first;
    int32_t row_value = 0;
    for (int32_t column = 0; column < x_count; ++column) row_value += start[column] * x_weights[column];
    value += row_value * y_weights[row];
  }
  return (value + 32768) >> 16;
}

void PreviewConverter::convert(const uint8_t *i420, uint8_t *rgba) const {
  const int32_t luma_size = width_ * height_;
  const uint8_t *luma = i420;
  const uint8_t *u = i420 + luma_size;
  const uint8_t *v = i420 + luma_size + luma_size / 4;
  const int32_t chroma_stride = width_ / 2;
  for (int32_t y = 0; y < target_height_; ++y) {
    uint8_t *row = rgba + static_cast<size_t>(target_height_ - 1 - y) * target_width_ * 4;
    for (int32_t x = 0; x < target_width_; ++x) {
      const int32_t c = sample(luma, width_, luma_x_, x, luma_y_, y) - 16;
      const int32_t d = sample(u, chroma_stride, chroma_x_, x, chroma_y_, y) - 128;
      const int32_t e = sample(v, chroma_stride, chroma_x_, x, chroma_y_, y) - 128;
      uint8_t *pixel = row + x * 4;
      pixel[0] = clamp_byte((298 * c + 409 * e + 128) >> 8);
      pixel[1] = clamp_byte((298 * c - 100 * d - 208 * e + 128) >> 8);
      pixel[2] = clamp_byte((298 * c + 516 * d + 128) >> 8);
      pixel[3] = 255;
    }
  }
}

bool PreviewTarget::open(const char *path, int32_t max_width, int32_t max_height) {
  close();
  if (path == nullptr || max_width < 1 || max_height < 1) return false;
  size_t size = kHeaderSize + static_cast<size_t>(max_width) * max_height * 4;
  if (!map_file(path, size, &mapped_)) return false;
  max_width_ = max_width;
  max_height_ = max_height;
  return true;
}

void PreviewTarget::close() { unmap_file(&mapped_); }

bool PreviewTarget::should_publish() const {
  if (mapped_.data == nullptr) return false;
  return load<int64_t>(mapped_.data, 0) == 0 || load<int32_t>(mapped_.data, 16) != 0;
}

bool PreviewTarget::publish(const uint8_t *rgba, int32_t width, int32_t height) {
  if (mapped_.data == nullptr || width < 1 || width > max_width_ || height < 1 || height > max_height_) return false;
  const int64_t sequence = load<int64_t>(mapped_.data, 0);
  store<int64_t>(mapped_.data, 0, sequence + 1);
  __atomic_thread_fence(__ATOMIC_SEQ_CST);
  store<int32_t>(mapped_.data, 8, width);
  store<int32_t>(mapped_.data, 12, height);
  std::memcpy(static_cast<uint8_t *>(mapped_.data) + kHeaderSize, rgba, static_cast<size_t>(width) * height * 4);
  __atomic_thread_fence(__ATOMIC_SEQ_CST);
  store<int64_t>(mapped_.data, 0, sequence + 2);
  return true;
}

} // namespace tufreplay::camera
