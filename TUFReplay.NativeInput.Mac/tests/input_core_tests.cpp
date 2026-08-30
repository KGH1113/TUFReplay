#include "input_core.hpp"

#include <array>
#include <cstdlib>
#include <iostream>

using tufreplay::input::InputCore;
using tufreplay::input::kFlagAlphaShift;
using tufreplay::input::kFlagShift;
using tufreplay::input::kQueueCapacity;

namespace {

void require(bool condition, const char *message) {
  if (!condition) {
    std::cerr << "FAIL: " << message << '\n';
    std::exit(1);
  }
}

void test_fifo_and_repeat() {
  InputCore core;
  core.add_device(1);
  require(core.apply(1, 4, true, 100), "first down was not queued");
  require(!core.apply(1, 4, true, 101), "repeat was queued");
  require(core.apply(1, 4, false, 102), "up was not queued");
  std::array<tufreplay_input_event, 4> events{};
  require(core.drain(events.data(), events.size()) == 2, "FIFO count changed");
  require(events[0].mach_timestamp == 100 && events[0].down == 1, "down timestamp/order changed");
  require(events[1].mach_timestamp == 102 && events[1].down == 0, "up timestamp/order changed");
  tufreplay_input_stats stats{};
  core.get_stats(&stats);
  require(stats.repeat_count == 1, "repeat counter changed");
}

void test_multi_keyboard_aggregation() {
  InputCore core;
  core.add_device(10);
  core.add_device(20);
  require(core.apply(10, 5, true, 1), "first device down missing");
  require(!core.apply(20, 5, true, 2), "second device emitted duplicate aggregate down");
  require(!core.apply(10, 5, false, 3), "first release emitted while second device held");
  require(core.apply(20, 5, false, 4), "final aggregate release missing");
  std::array<tufreplay_input_event, 4> events{};
  require(core.drain(events.data(), events.size()) == 2, "aggregate transition count changed");
  require(events[0].down == 1 && events[1].down == 0, "aggregate ordering changed");
}

void test_device_removal_releases_keys() {
  InputCore core;
  core.add_device(30);
  core.apply(30, 6, true, 10);
  core.apply(30, 7, true, 11);
  require(core.remove_device(30, 12), "device removal did not queue releases");
  std::array<tufreplay_input_event, 8> events{};
  require(core.drain(events.data(), events.size()) == 4, "device removal transition count changed");
  require(events[2].down == 0 && events[3].down == 0, "device removal left keys held");
  std::array<uint8_t, TUFREPLAY_INPUT_USAGE_CAPACITY> state{};
  require(core.copy_state(state.data(), state.size()) == TUFREPLAY_INPUT_USAGE_CAPACITY, "snapshot failed");
  require(state[6] == 0 && state[7] == 0, "snapshot retained removed device state");
}

void test_modifier_and_caps_flags() {
  InputCore core;
  core.add_device(40);
  core.apply(40, 225, true, 1);
  core.apply(40, 4, true, 2);
  core.apply(40, 57, true, 3);
  std::array<tufreplay_input_event, 8> events{};
  const int count = core.drain(events.data(), events.size());
  require(count == 3, "modifier transition count changed");
  require((events[0].modifier_flags & kFlagShift) != 0, "shift flag missing");
  require((events[1].modifier_flags & kFlagShift) != 0, "held shift flag missing");
  require((events[2].modifier_flags & kFlagAlphaShift) != 0, "caps lock flag missing");
}

void test_overflow_and_stress() {
  InputCore core;
  core.add_device(50);
  for (size_t i = 0; i < kQueueCapacity + 256; ++i)
    core.apply(50, 8, (i & 1) == 0, i + 1);
  require(core.take_dropped() == 256, "overflow drop count changed");
  std::array<tufreplay_input_event, 256> events{};
  size_t drained = 0;
  int count;
  while ((count = core.drain(events.data(), events.size())) != 0)
    drained += static_cast<size_t>(count);
  require(drained == kQueueCapacity, "overflow queue capacity changed");

  for (int i = 0; i < 1000; ++i) {
    InputCore restart;
    restart.add_device(100 + i);
    restart.apply(100 + i, 4 + (i % 26), true, i);
    restart.remove_device(100 + i, i + 1);
  }
}

}  // namespace

int main() {
  test_fifo_and_repeat();
  test_multi_keyboard_aggregation();
  test_device_removal_releases_keys();
  test_modifier_and_caps_flags();
  test_overflow_and_stress();
  std::cout << "TUFReplay native input core tests passed.\n";
  return 0;
}
