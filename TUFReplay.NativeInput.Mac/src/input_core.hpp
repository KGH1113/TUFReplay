#ifndef TUFREPLAY_INPUT_CORE_HPP
#define TUFREPLAY_INPUT_CORE_HPP

#include "tufreplay_input.h"

#include <array>
#include <atomic>
#include <cstdint>

namespace tufreplay::input {

constexpr size_t kQueueCapacity = 8192;
constexpr size_t kQueueMask = kQueueCapacity - 1;
constexpr size_t kMaxDevices = 64;
constexpr uint64_t kFlagAlphaShift = 1ull << 16;
constexpr uint64_t kFlagShift = 1ull << 17;
constexpr uint64_t kFlagControl = 1ull << 18;
constexpr uint64_t kFlagAlternate = 1ull << 19;
constexpr uint64_t kFlagCommand = 1ull << 20;

inline bool is_supported_usage(uint32_t usage) {
  return (usage >= 4 && usage <= 100)
    || (usage >= 104 && usage <= 111)
    || (usage >= 224 && usage <= 231);
}

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
    if (destination == nullptr || capacity <= 0)
      return 0;
    const uint64_t read = read_sequence_.load(std::memory_order_relaxed);
    const uint64_t write = write_sequence_.load(std::memory_order_acquire);
    const uint64_t available = write - read;
    const uint64_t count = available < static_cast<uint64_t>(capacity) ? available : static_cast<uint64_t>(capacity);
    for (uint64_t i = 0; i < count; ++i)
      destination[i] = events_[(read + i) & kQueueMask];
    read_sequence_.store(read + count, std::memory_order_release);
    return static_cast<int32_t>(count);
  }

  uint32_t depth() const {
    const uint64_t value = write_sequence_.load(std::memory_order_acquire) - read_sequence_.load(std::memory_order_acquire);
    return static_cast<uint32_t>(value > kQueueCapacity ? kQueueCapacity : value);
  }

  uint64_t take_dropped() { return dropped_.exchange(0, std::memory_order_acq_rel); }
  uint64_t dropped() const { return dropped_.load(std::memory_order_acquire); }

 private:
  std::array<tufreplay_input_event, kQueueCapacity> events_{};
  std::atomic<uint64_t> read_sequence_{0};
  std::atomic<uint64_t> write_sequence_{0};
  std::atomic<uint64_t> dropped_{0};
};

class InputCore {
 public:
  bool add_device(uintptr_t token) {
    if (find_device(token) != nullptr)
      return true;
    for (auto &device : devices_) {
      if (!device.active) {
        device.active = true;
        device.token = token;
        device.down.fill(0);
        device_count_.fetch_add(1, std::memory_order_relaxed);
        return true;
      }
    }
    return false;
  }

  bool apply(uintptr_t token, uint32_t usage, bool down, uint64_t timestamp) {
    callback_count_.fetch_add(1, std::memory_order_relaxed);
    if (!is_supported_usage(usage)) {
      unmapped_count_.fetch_add(1, std::memory_order_relaxed);
      return false;
    }
    DeviceState *device = find_device(token);
    if (device == nullptr) {
      if (!add_device(token)) {
        unmapped_count_.fetch_add(1, std::memory_order_relaxed);
        return false;
      }
      device = find_device(token);
    }
    const uint8_t next = down ? 1 : 0;
    if (device->down[usage] == next) {
      repeat_count_.fetch_add(1, std::memory_order_relaxed);
      return false;
    }
    device->down[usage] = next;
    uint16_t &count = aggregate_count_[usage];
    const bool was_down = count != 0;
    if (down)
      ++count;
    else if (count != 0)
      --count;
    const bool is_down = count != 0;
    aggregate_down_[usage].store(is_down ? 1 : 0, std::memory_order_release);
    if (was_down == is_down)
      return false;
    if (usage == 57 && is_down)
      caps_lock_on_ = !caps_lock_on_;

    tufreplay_input_event event{};
    event.mach_timestamp = timestamp;
    event.modifier_flags = modifier_flags();
    event.usage = usage;
    event.down = is_down ? 1 : 0;
    if (!ring_.push(event))
      return false;
    queued_count_.fetch_add(1, std::memory_order_relaxed);
    return true;
  }

  bool remove_device(uintptr_t token, uint64_t timestamp) {
    DeviceState *device = find_device(token);
    if (device == nullptr)
      return false;
    bool queued = false;
    for (uint32_t usage = 0; usage < TUFREPLAY_INPUT_USAGE_CAPACITY; ++usage) {
      if (device->down[usage] != 0)
        queued = apply(token, usage, false, timestamp) || queued;
    }
    device->active = false;
    device->token = 0;
    device_count_.fetch_sub(1, std::memory_order_relaxed);
    return queued;
  }

  int32_t drain(tufreplay_input_event *events, int32_t capacity) { return ring_.drain(events, capacity); }

  int32_t copy_state(uint8_t *destination, int32_t capacity) const {
    if (destination == nullptr || capacity < static_cast<int32_t>(TUFREPLAY_INPUT_USAGE_CAPACITY))
      return 0;
    for (size_t i = 0; i < TUFREPLAY_INPUT_USAGE_CAPACITY; ++i)
      destination[i] = aggregate_down_[i].load(std::memory_order_acquire);
    return static_cast<int32_t>(TUFREPLAY_INPUT_USAGE_CAPACITY);
  }

  uint64_t take_dropped() { return ring_.take_dropped(); }
  void set_caps_lock(bool enabled) { caps_lock_on_ = enabled; }

  void get_stats(tufreplay_input_stats *stats) const {
    if (stats == nullptr)
      return;
    stats->callback_count = callback_count_.load(std::memory_order_acquire);
    stats->queued_count = queued_count_.load(std::memory_order_acquire);
    stats->dropped_count = ring_.dropped();
    stats->repeat_count = repeat_count_.load(std::memory_order_acquire);
    stats->unmapped_count = unmapped_count_.load(std::memory_order_acquire);
    stats->device_count = device_count_.load(std::memory_order_acquire);
    stats->queue_depth = ring_.depth();
  }

 private:
  struct DeviceState {
    uintptr_t token = 0;
    bool active = false;
    std::array<uint8_t, TUFREPLAY_INPUT_USAGE_CAPACITY> down{};
  };

  DeviceState *find_device(uintptr_t token) {
    for (auto &device : devices_) {
      if (device.active && device.token == token)
        return &device;
    }
    return nullptr;
  }

  bool aggregate(uint32_t usage) const { return aggregate_down_[usage].load(std::memory_order_relaxed) != 0; }

  uint64_t modifier_flags() const {
    uint64_t flags = caps_lock_on_ ? kFlagAlphaShift : 0;
    if (aggregate(225) || aggregate(229)) flags |= kFlagShift;
    if (aggregate(224) || aggregate(228)) flags |= kFlagControl;
    if (aggregate(226) || aggregate(230)) flags |= kFlagAlternate;
    if (aggregate(227) || aggregate(231)) flags |= kFlagCommand;
    return flags;
  }

  std::array<DeviceState, kMaxDevices> devices_{};
  std::array<uint16_t, TUFREPLAY_INPUT_USAGE_CAPACITY> aggregate_count_{};
  std::array<std::atomic<uint8_t>, TUFREPLAY_INPUT_USAGE_CAPACITY> aggregate_down_{};
  EventRing ring_{};
  bool caps_lock_on_ = false;
  std::atomic<uint64_t> callback_count_{0};
  std::atomic<uint64_t> queued_count_{0};
  std::atomic<uint64_t> repeat_count_{0};
  std::atomic<uint64_t> unmapped_count_{0};
  std::atomic<uint32_t> device_count_{0};
};

}  // namespace tufreplay::input

#endif
