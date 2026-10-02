#ifndef TUFREPLAY_INPUT_CORE_HPP
#define TUFREPLAY_INPUT_CORE_HPP

#include "tufreplay_input.h"
#include <array>
#include <atomic>
#include <cstddef>
#include <cstdint>

namespace tufreplay::input {
constexpr size_t kQueueCapacity = 8192;
constexpr size_t kQueueMask = kQueueCapacity - 1;
constexpr uint64_t kMaxEventDelayNs = 1'000'000'000;
inline bool is_supported_key(uint32_t key) { return key < TUFREPLAY_INPUT_KEY_CAPACITY; }

// One producer (the dedicated event-tap RunLoop), one bridge consumer.
// Enqueue and overflow handling never wait for the consumer or allocate.
class EventRing {
 public:
  bool push(const tufreplay_input_event &event) {
    const uint64_t write = write_sequence_.load(std::memory_order_relaxed);
    if (write - read_sequence_.load(std::memory_order_acquire) >= kQueueCapacity) {
      dropped_.fetch_add(1, std::memory_order_relaxed);
      return false;
    }
    events_[write & kQueueMask] = event;
    write_sequence_.store(write + 1, std::memory_order_release);
    return true;
  }
  int32_t drain(tufreplay_input_event *destination, int32_t capacity) {
    if (destination == nullptr || capacity <= 0) return 0;
    const uint64_t read = read_sequence_.load(std::memory_order_relaxed);
    const uint64_t available = write_sequence_.load(std::memory_order_acquire) - read;
    const uint64_t count = available < static_cast<uint64_t>(capacity) ? available : static_cast<uint64_t>(capacity);
    for (uint64_t i = 0; i < count; ++i) destination[i] = events_[(read + i) & kQueueMask];
    read_sequence_.store(read + count, std::memory_order_release);
    return static_cast<int32_t>(count);
  }
  uint32_t depth() const {
    const uint64_t read = read_sequence_.load(std::memory_order_acquire);
    const uint64_t write = write_sequence_.load(std::memory_order_acquire);
    const uint64_t depth = write >= read ? write - read : 0;
    return static_cast<uint32_t>(depth > kQueueCapacity ? kQueueCapacity : depth);
  }
  uint64_t take_dropped() { return dropped_.exchange(0, std::memory_order_acq_rel); }
  uint64_t dropped() const { return dropped_.load(std::memory_order_acquire); }
 private:
  std::array<tufreplay_input_event, kQueueCapacity> events_{};
  std::atomic<uint64_t> read_sequence_{0}, write_sequence_{0}, dropped_{0};
};

class InputCore {
 public:
  bool apply(uint32_t key, bool down, uint64_t timestamp_ns, uint64_t flags) {
    callbacks_.fetch_add(1, std::memory_order_relaxed);
    if (!is_supported_key(key)) {
      unmapped_.fetch_add(1, std::memory_order_relaxed);
      return false;
    }
    const uint8_t next = down ? 1 : 0;
    if (state_[key].load(std::memory_order_relaxed) == next) {
      repeats_.fetch_add(1, std::memory_order_relaxed);
      return false;
    }
    state_[key].store(next, std::memory_order_release);
    tufreplay_input_event event{};
    event.timestamp_ns = timestamp_ns;
    event.modifier_flags = flags;
    event.key_code = key;
    event.down = next;
    if (!ring_.push(event)) return false;
    queued_.fetch_add(1, std::memory_order_relaxed);
    return true;
  }
  void synchronize(uint32_t key, bool down) {
    if (is_supported_key(key)) state_[key].store(down ? 1 : 0, std::memory_order_release);
  }
  bool down(uint32_t key) const {
    return is_supported_key(key) && state_[key].load(std::memory_order_acquire) != 0;
  }
  // Side-specific NX_DEVICE*KEYMASK bits carried by CGEventFlags. No query
  // back to WindowServer is required on the callback path.
  bool apply_modifier(uint32_t key, uint64_t flags, uint64_t timestamp_ns) {
    uint64_t mask = 0;
    switch (key) {
      case 0x3B: mask = 0x0001; break;
      case 0x38: mask = 0x0002; break;
      case 0x3C: mask = 0x0004; break;
      case 0x37: mask = 0x0008; break;
      case 0x36: mask = 0x0010; break;
      case 0x3A: mask = 0x0020; break;
      case 0x3D: mask = 0x0040; break;
      case 0x3E: mask = 0x2000; break;
      case 0x3F: mask = 1ull << 23; break; // Fn
      case 0x39: {
        // Caps Lock has a toggle event rather than a physical key-up stream.
        const bool pressed = apply(key, true, timestamp_ns, flags);
        return apply(key, false, timestamp_ns, flags) || pressed;
      }
      default: return false;
    }
    return apply(key, (flags & mask) != 0, timestamp_ns, flags);
  }
  void mark_fault(uint32_t fault) { faults_.fetch_or(fault, std::memory_order_relaxed); }
  void observe_delay(uint64_t timestamp_ns, uint64_t now_ns) {
    if (now_ns > timestamp_ns && now_ns - timestamp_ns > kMaxEventDelayNs)
      mark_fault(TUFREPLAY_INPUT_FAULT_EVENT_DELAYED);
  }
  uint32_t take_faults() { return faults_.exchange(0, std::memory_order_acq_rel); }
  int32_t drain(tufreplay_input_event *events, int32_t capacity) { return ring_.drain(events, capacity); }
  uint64_t take_dropped() { return ring_.take_dropped(); }
  void get_stats(tufreplay_input_stats *stats) const {
    if (stats == nullptr) return;
    stats->callback_count = callbacks_.load(std::memory_order_acquire);
    stats->queued_count = queued_.load(std::memory_order_acquire);
    stats->dropped_count = ring_.dropped();
    stats->repeat_count = repeats_.load(std::memory_order_acquire);
    stats->unmapped_count = unmapped_.load(std::memory_order_acquire);
    stats->device_count = 0; // Quartz merges devices.
    stats->queue_depth = ring_.depth();
  }
 private:
  std::array<std::atomic<uint8_t>, TUFREPLAY_INPUT_KEY_CAPACITY> state_{};
  EventRing ring_{};
  std::atomic<uint64_t> callbacks_{0}, queued_{0}, repeats_{0}, unmapped_{0};
  std::atomic<uint32_t> faults_{0};
};
} // namespace tufreplay::input
#endif
