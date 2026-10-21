// Test callback routing without installing a tap or posting input to macOS.
#include "../src/tufreplay_input.mm"
#include <cstdlib>
#include <iostream>

void require(bool condition, const char *message) {
  if (!condition) { std::cerr << "FAIL: " << message << '\n'; std::exit(1); }
}
int main() {
  require((event_mask() & CGEventMaskBit(kCGEventMouseMoved)) == 0, "mouse motion subscribed");
  require((event_mask() & CGEventMaskBit(kCGEventLeftMouseDragged)) == 0, "drag subscribed");
  require((event_mask() & CGEventMaskBit(kCGEventScrollWheel)) == 0, "scroll subscribed");
  InputContext context;
  mach_timebase_info(&context.timebase);
  CGEventRef keyboard = CGEventCreateKeyboardEvent(nullptr, 0, true);
  require(keyboard != nullptr, "offline key event creation failed");
  const uint64_t timestamp = clock_now_ns(context.timebase);
  CGEventSetTimestamp(keyboard, timestamp);
  require(on_event(nullptr, kCGEventKeyDown, keyboard, &context) == keyboard, "callback changed event");
  on_event(nullptr, kCGEventKeyDown, keyboard, &context);
  on_event(nullptr, kCGEventKeyUp, keyboard, &context);
  CFRelease(keyboard);
  CGEventRef mouse = CGEventCreateMouseEvent(nullptr, kCGEventOtherMouseDown, CGPointZero, static_cast<CGMouseButton>(4));
  require(mouse != nullptr, "offline mouse event creation failed");
  CGEventSetTimestamp(mouse, timestamp + 1);
  on_event(nullptr, kCGEventOtherMouseDown, mouse, &context);
  on_event(nullptr, kCGEventOtherMouseUp, mouse, &context);
  CFRelease(mouse);
  tufreplay_input_event events[8]{};
  require(context.core.drain(events, 8) == 4, "callback duplicated or lost input");
  require(events[0].timestamp_ns == timestamp, "Quartz timestamp was scaled twice");
  require(events[2].key_code == 132 && events[3].down == 0, "mouse button mapping changed");
  on_event(nullptr, kCGEventTapDisabledByTimeout, nullptr, &context);
  require(context.core.take_faults() == TUFREPLAY_INPUT_FAULT_TAP_TIMEOUT, "timeout reason lost");
  context.stopping.store(true);
  on_event(nullptr, kCGEventTapDisabledByUserInput, nullptr, &context);
  require(context.core.take_faults() == 0, "shutdown callback changed state");
  std::cout << "TUFReplay CGEvent offline callback tests passed.\n";
}
