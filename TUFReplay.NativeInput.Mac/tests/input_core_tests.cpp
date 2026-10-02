#include "input_core.hpp"
#include <array>
#include <cstdlib>
#include <iostream>
#include <thread>

using tufreplay::input::InputCore;
using tufreplay::input::kQueueCapacity;

void require(bool condition, const char *message) {
  if (!condition) { std::cerr << "FAIL: " << message << '\n'; std::exit(1); }
}

void test_fifo_repeat_and_mouse() {
  InputCore core;
  require(core.apply(0, true, 100, 0x20000), "A down missing");
  require(!core.apply(0, true, 101, 0x20000), "repeat was recorded");
  require(core.apply(0, false, 102, 0), "A up missing");
  require(core.apply(128, true, 103, 0), "left mouse down missing");
  require(core.apply(159, true, 104, 0), "mouse button 31 missing");
  require(!core.apply(160, true, 105, 0), "invalid code accepted");
  std::array<tufreplay_input_event, 8> events{};
  require(core.drain(events.data(), events.size()) == 4, "FIFO count changed");
  require(events[0].timestamp_ns == 100 && events[0].modifier_flags == 0x20000, "native metadata changed");
  require(events[1].down == 0 && events[2].key_code == 128 && events[3].key_code == 159, "mouse range/order changed");
  tufreplay_input_stats stats{};
  core.get_stats(&stats);
  require(stats.repeat_count == 1 && stats.unmapped_count == 1, "diagnostic counters changed");
}

void test_modifiers_and_caps() {
  InputCore core;
  core.apply_modifier(0x38, 0x20002, 1); // Left Shift.
  core.apply_modifier(0x3C, 0x20006, 2); // Both Shift keys.
  core.apply_modifier(0x38, 0x20004, 3); // Right still held.
  require(!core.down(0x38) && core.down(0x3C), "opposite modifier release changed held side");
  core.apply_modifier(0x3C, 0, 4);
  core.apply_modifier(0x39, 0x10000, 5);
  require(!core.down(0x39), "Caps Lock was left physically held");
  std::array<tufreplay_input_event, 8> events{};
  require(core.drain(events.data(), events.size()) == 6, "modifier count changed");
  require(events[4].down == 1 && events[5].down == 0 && events[5].modifier_flags == 0x10000, "Caps Lock toggle metadata lost");
}

void test_faults_and_overflow() {
  InputCore core;
  for (size_t i = 0; i < kQueueCapacity + 256; ++i) core.apply(4, (i & 1) == 0, i + 1, 0);
  require(core.take_dropped() == 256 && core.take_dropped() == 0, "overflow counter not consumable");
  core.mark_fault(TUFREPLAY_INPUT_FAULT_TAP_TIMEOUT);
  core.mark_fault(TUFREPLAY_INPUT_FAULT_TAP_DISABLED);
  core.observe_delay(100, 1'000'000'101);
  require(core.take_faults() == 7 && core.take_faults() == 0, "tap faults were not retained/consumed");
  std::array<tufreplay_input_event, 256> batch{};
  size_t drained = 0;
  int count;
  while ((count = core.drain(batch.data(), batch.size())) != 0) drained += count;
  require(drained == kQueueCapacity, "overflow changed bounded capacity");
  require(core.apply(4, true, 10000, 0), "queue did not recover after overflow");
}

void test_concurrent_long_stream() {
  InputCore core;
  constexpr uint64_t total = 1'000'000;
  std::thread producer([&] {
    // Deliberately fill the queue before releasing the consumer, then sustain
    // many wraparounds. Count losses instead of ever blocking the callback.
    for (uint64_t i = 0; i < total; ++i) core.apply(1, (i & 1) == 0, i + 1, 0);
  });
  std::array<tufreplay_input_event, 256> batch{};
  uint64_t last = 0, received = 0;
  tufreplay_input_stats stats{};
  while (true) {
    int count = core.drain(batch.data(), batch.size());
    for (int i = 0; i < count; ++i) {
      require(batch[i].timestamp_ns > last, "concurrent FIFO order changed");
      last = batch[i].timestamp_ns;
      ++received;
    }
    core.get_stats(&stats);
    if (stats.callback_count == total && stats.queue_depth == 0) break;
    std::this_thread::yield();
  }
  producer.join();
  // Producer increments callback_count before enqueue; drain a final handoff.
  int count = core.drain(batch.data(), batch.size());
  for (int i = 0; i < count; ++i) {
    require(batch[i].timestamp_ns > last, "final FIFO order changed");
    last = batch[i].timestamp_ns;
    ++received;
  }
  require(received + core.take_dropped() == total, "long stream lost unaccounted events");
}

int main() {
  test_fifo_repeat_and_mouse();
  test_modifiers_and_caps();
  test_faults_and_overflow();
  test_concurrent_long_stream();
  std::cout << "TUFReplay CGEvent input core tests passed.\n";
}
