#ifndef TUFREPLAY_CAMERA_FRAMES_HPP
#define TUFREPLAY_CAMERA_FRAMES_HPP

#include <atomic>
#include <condition_variable>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <mutex>
#include <new>
#include <vector>

namespace tufreplay::camera {

class FramePool;

// One raw I420 frame shared by capture, preview and encoding. It returns to the
// pool only after every consumer released it.
struct Frame {
  FramePool *pool = nullptr;
  uint8_t *pixels = nullptr;
  int64_t ticks = 0;
  std::atomic<int32_t> references{0};

  Frame *retain() {
    references.fetch_add(1, std::memory_order_relaxed);
    return this;
  }
  void release();
  // At most a quarter second old and not from the future.
  bool fresh(int64_t now, int64_t frequency) const {
    return ticks > 0 && frequency > 0 && now >= ticks && now - ticks <= frequency / 4;
  }
};

// Allocated once when the capture size is known; a slow consumer cannot grow it.
class FramePool {
 public:
  static std::shared_ptr<FramePool> create(size_t byte_count, int32_t capacity) {
    if (byte_count == 0 || capacity <= 0) return nullptr;
    std::shared_ptr<FramePool> pool(new (std::nothrow) FramePool(byte_count));
    if (pool == nullptr) return nullptr;
    pool->frames_.reserve(static_cast<size_t>(capacity));
    pool->available_.reserve(static_cast<size_t>(capacity));
    for (int32_t index = 0; index < capacity; ++index) {
      auto *frame = new (std::nothrow) Frame();
      uint8_t *pixels = frame == nullptr ? nullptr : new (std::nothrow) uint8_t[byte_count];
      if (pixels == nullptr) {
        delete frame;
        return nullptr;
      }
      frame->pool = pool.get();
      frame->pixels = pixels;
      pool->frames_.push_back(frame);
      pool->available_.push_back(frame);
    }
    return pool;
  }

  ~FramePool() {
    for (Frame *frame : frames_) {
      delete[] frame->pixels;
      delete frame;
    }
  }

  Frame *try_rent() {
    std::lock_guard<std::mutex> lock(gate_);
    if (available_.empty()) return nullptr;
    Frame *frame = available_.back();
    available_.pop_back();
    frame->references.store(1, std::memory_order_relaxed);
    frame->ticks = 0;
    return frame;
  }

  void give_back(Frame *frame) {
    std::lock_guard<std::mutex> lock(gate_);
    available_.push_back(frame);
  }

  size_t byte_count() const { return byte_count_; }

 private:
  explicit FramePool(size_t byte_count) : byte_count_(byte_count) {}
  const size_t byte_count_;
  std::mutex gate_;
  std::vector<Frame *> frames_;
  std::vector<Frame *> available_;
};

inline void Frame::release() {
  if (references.fetch_sub(1, std::memory_order_acq_rel) == 1) pool->give_back(this);
}

// Never makes the capture thread wait. A full queue replaces its oldest pending
// frame; the timestamped stream keeps the resulting gap instead of compressing time.
class FrameQueue {
 public:
  explicit FrameQueue(size_t capacity) : capacity_(capacity == 0 ? 1 : capacity) { frames_.reserve(capacity_); }
  FrameQueue(const FrameQueue &) = delete;
  FrameQueue &operator=(const FrameQueue &) = delete;
  ~FrameQueue() { dispose(); }

  bool submit(Frame *frame) {
    std::lock_guard<std::mutex> lock(gate_);
    if (completed_) return false;
    if (frames_.size() == capacity_) {
      frames_.front()->release();
      frames_.erase(frames_.begin());
      dropped_.fetch_add(1, std::memory_order_relaxed);
    }
    frames_.push_back(frame->retain());
    changed_.notify_one();
    return true;
  }

  // Blocks until a frame is available; nullptr once completed and drained.
  Frame *take() {
    std::unique_lock<std::mutex> lock(gate_);
    changed_.wait(lock, [this] { return !frames_.empty() || completed_; });
    if (frames_.empty()) return nullptr;
    Frame *frame = frames_.front();
    frames_.erase(frames_.begin());
    return frame;
  }

  void complete() {
    std::lock_guard<std::mutex> lock(gate_);
    completed_ = true;
    changed_.notify_all();
  }

  void dispose() {
    std::lock_guard<std::mutex> lock(gate_);
    completed_ = true;
    for (Frame *frame : frames_) frame->release();
    frames_.clear();
    changed_.notify_all();
  }

  int64_t dropped() const { return dropped_.load(std::memory_order_acquire); }

  // Lets tests hold the consumer between frames to reproduce a scheduling stall.
  std::mutex &gate_for_tests() { return gate_; }

 private:
  const size_t capacity_;
  std::mutex gate_;
  std::condition_variable changed_;
  std::vector<Frame *> frames_;
  bool completed_ = false;
  std::atomic<int64_t> dropped_{0};
};

} // namespace tufreplay::camera

#endif
