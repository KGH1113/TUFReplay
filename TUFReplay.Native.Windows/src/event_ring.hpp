#ifndef TUFREPLAY_WIN_EVENT_RING_HPP
#define TUFREPLAY_WIN_EVENT_RING_HPP

#include <atomic>
#include <cstddef>
#include <cstdint>

namespace tufreplay::native {

// Single producer (the hook thread), single consumer (the managed bridge). Push
// never waits or allocates; a full ring drops the event and counts it.
template <typename T, size_t Capacity>
class EventRing {
  static_assert((Capacity & (Capacity - 1)) == 0, "Capacity must be a power of two.");

 public:
  bool push(const T &event) {
    const uint64_t write = write_.load(std::memory_order_relaxed);
    if (write - read_.load(std::memory_order_acquire) >= Capacity) {
      dropped_.fetch_add(1, std::memory_order_relaxed);
      return false;
    }
    items_[write & (Capacity - 1)] = event;
    write_.store(write + 1, std::memory_order_release);
    return true;
  }

  int32_t drain(T *destination, int32_t capacity) {
    if (destination == nullptr || capacity <= 0) return 0;
    const uint64_t read = read_.load(std::memory_order_relaxed);
    const uint64_t available = write_.load(std::memory_order_acquire) - read;
    const uint64_t count = available < static_cast<uint64_t>(capacity) ? available : static_cast<uint64_t>(capacity);
    for (uint64_t i = 0; i < count; ++i) destination[i] = items_[(read + i) & (Capacity - 1)];
    read_.store(read + count, std::memory_order_release);
    return static_cast<int32_t>(count);
  }

  uint32_t depth() const {
    const uint64_t read = read_.load(std::memory_order_acquire);
    const uint64_t write = write_.load(std::memory_order_acquire);
    const uint64_t depth = write >= read ? write - read : 0;
    return static_cast<uint32_t>(depth > Capacity ? Capacity : depth);
  }

  uint64_t take_dropped() { return dropped_.exchange(0, std::memory_order_acq_rel); }
  uint64_t dropped() const { return dropped_.load(std::memory_order_acquire); }

 private:
  T items_[Capacity]{};
  std::atomic<uint64_t> read_{0}, write_{0}, dropped_{0};
};

} // namespace tufreplay::native

#endif
