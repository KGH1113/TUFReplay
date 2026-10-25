#include <cstdio>
#include <cstdlib>

#include "event_ring.hpp"

namespace {
struct Item {
  int value;
};

void check(bool condition, const char *message) {
  if (!condition) {
    std::fprintf(stderr, "FAIL: %s\n", message);
    std::exit(1);
  }
}
} // namespace

int main() {
  tufreplay::native::EventRing<Item, 4> ring;
  Item drained[8] = {};
  check(ring.drain(drained, 8) == 0, "An empty ring returned events.");
  for (int i = 0; i < 4; ++i) check(ring.push(Item{i}), "A ring with free slots rejected an event.");
  check(!ring.push(Item{99}), "A full ring accepted an event.");
  check(ring.dropped() == 1 && ring.depth() == 4, "A full ring did not count its drop.");
  check(ring.drain(drained, 2) == 2 && drained[0].value == 0 && drained[1].value == 1, "Drain lost FIFO order.");
  check(ring.push(Item{4}) && ring.push(Item{5}), "Drained slots were not reusable.");
  check(ring.drain(drained, 8) == 4 && drained[0].value == 2 && drained[3].value == 5, "Wrapped drain lost order.");
  check(ring.take_dropped() == 1 && ring.dropped() == 0, "Dropped events were not consumed.");
  std::puts("PASS Windows native event ring");
  return 0;
}
