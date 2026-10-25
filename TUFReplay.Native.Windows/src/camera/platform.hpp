#ifndef TUFREPLAY_CAMERA_PLATFORM_HPP
#define TUFREPLAY_CAMERA_PLATFORM_HPP

#include <condition_variable>
#include <cstddef>
#include <cstdint>
#include <memory>
#include <mutex>

namespace tufreplay::camera {

// A Windows HANDLE or a POSIX file descriptor, as passed through the C ABI.
using OsHandle = int64_t;
constexpr OsHandle kInvalidHandle = -1;

bool duplicate_handle(OsHandle source, OsHandle *duplicate);
void close_handle(OsHandle handle);
// Blocking read. Returns the byte count, 0 at end of stream, or -1 on failure.
int64_t read_some(OsHandle handle, void *buffer, size_t length);
bool write_all(OsHandle handle, const void *buffer, size_t length);

struct MappedFile {
  void *data = nullptr;
  size_t size = 0;
  intptr_t file = -1;
  intptr_t mapping = 0;
};
bool map_file(const char *utf8_path, size_t size, MappedFile *mapped);
void unmap_file(MappedFile *mapped);

void lower_current_thread_priority();
// GetLastError() or errno from the last failed platform call on this thread.
int32_t last_system_error();
int64_t monotonic_ns();

// A detached native thread whose completion can be awaited with a timeout. A
// thread that misses its deadline keeps running safely; its state is shared.
class WorkerThread {
 public:
  WorkerThread() = default;
  WorkerThread(const WorkerThread &) = delete;
  WorkerThread &operator=(const WorkerThread &) = delete;

  bool start(void (*entry)(void *), void *argument);
  // True once the entry function returned (immediately when never started).
  bool wait(int32_t timeout_ms);
  bool started() const { return state_ != nullptr; }

  struct State {
    std::mutex gate;
    std::condition_variable changed;
    bool done = false;
    void (*entry)(void *) = nullptr;
    void *argument = nullptr;
  };

 private:
  std::shared_ptr<State> state_;
};

} // namespace tufreplay::camera

#endif
