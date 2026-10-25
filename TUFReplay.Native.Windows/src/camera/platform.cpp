#include "platform.hpp"

#include <chrono>
#include <new>

#if defined(_WIN32)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <cerrno>
#include <fcntl.h>
#include <pthread.h>
#include <sys/mman.h>
#include <unistd.h>
#endif

namespace tufreplay::camera {

namespace {
void run_worker(std::shared_ptr<WorkerThread::State> state) {
  state->entry(state->argument);
  std::lock_guard<std::mutex> lock(state->gate);
  state->done = true;
  state->changed.notify_all();
}

#if defined(_WIN32)
DWORD WINAPI worker_trampoline(void *opaque) {
  auto *state = static_cast<std::shared_ptr<WorkerThread::State> *>(opaque);
  std::shared_ptr<WorkerThread::State> owned = std::move(*state);
  delete state;
  run_worker(std::move(owned));
  return 0;
}

HANDLE as_handle(OsHandle handle) { return reinterpret_cast<HANDLE>(static_cast<intptr_t>(handle)); }
#else
void *worker_trampoline(void *opaque) {
  auto *state = static_cast<std::shared_ptr<WorkerThread::State> *>(opaque);
  std::shared_ptr<WorkerThread::State> owned = std::move(*state);
  delete state;
  run_worker(std::move(owned));
  return nullptr;
}
#endif
} // namespace

bool WorkerThread::start(void (*entry)(void *), void *argument) {
  if (state_ != nullptr || entry == nullptr) return false;
  auto state = std::make_shared<State>();
  state->entry = entry;
  state->argument = argument;
  auto *transfer = new (std::nothrow) std::shared_ptr<State>(state);
  if (transfer == nullptr) return false;
#if defined(_WIN32)
  HANDLE thread = CreateThread(nullptr, 0, worker_trampoline, transfer, 0, nullptr);
  if (thread == nullptr) {
    delete transfer;
    return false;
  }
  CloseHandle(thread);
#else
  pthread_t thread;
  if (pthread_create(&thread, nullptr, worker_trampoline, transfer) != 0) {
    delete transfer;
    return false;
  }
  pthread_detach(thread);
#endif
  state_ = std::move(state);
  return true;
}

bool WorkerThread::wait(int32_t timeout_ms) {
  if (state_ == nullptr) return true;
  std::unique_lock<std::mutex> lock(state_->gate);
  if (timeout_ms < 0) {
    state_->changed.wait(lock, [this] { return state_->done; });
    return true;
  }
  return state_->changed.wait_for(lock, std::chrono::milliseconds(timeout_ms), [this] { return state_->done; });
}

int64_t monotonic_ns() {
  return std::chrono::duration_cast<std::chrono::nanoseconds>(std::chrono::steady_clock::now().time_since_epoch())
    .count();
}

#if defined(_WIN32)
bool duplicate_handle(OsHandle source, OsHandle *duplicate) {
  HANDLE copy = nullptr;
  if (!DuplicateHandle(GetCurrentProcess(), as_handle(source), GetCurrentProcess(), &copy, 0, FALSE,
        DUPLICATE_SAME_ACCESS))
    return false;
  *duplicate = static_cast<OsHandle>(reinterpret_cast<intptr_t>(copy));
  return true;
}

void close_handle(OsHandle handle) {
  if (handle != kInvalidHandle && handle != 0) CloseHandle(as_handle(handle));
}

int64_t read_some(OsHandle handle, void *buffer, size_t length) {
  DWORD read = 0;
  DWORD request = length > 0x40000000u ? 0x40000000u : static_cast<DWORD>(length);
  if (ReadFile(as_handle(handle), buffer, request, &read, nullptr)) return read;
  return GetLastError() == ERROR_BROKEN_PIPE ? 0 : -1;
}

bool write_all(OsHandle handle, const void *buffer, size_t length) {
  const auto *bytes = static_cast<const uint8_t *>(buffer);
  while (length > 0) {
    DWORD written = 0;
    DWORD request = length > 0x40000000u ? 0x40000000u : static_cast<DWORD>(length);
    if (!WriteFile(as_handle(handle), bytes, request, &written, nullptr) || written == 0) return false;
    bytes += written;
    length -= written;
  }
  return true;
}

bool map_file(const char *utf8_path, size_t size, MappedFile *mapped) {
  if (utf8_path == nullptr || mapped == nullptr || size == 0) return false;
  int length = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, utf8_path, -1, nullptr, 0);
  if (length <= 0) return false;
  auto *path = new (std::nothrow) wchar_t[length];
  if (path == nullptr) return false;
  MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, utf8_path, -1, path, length);
  HANDLE file = CreateFileW(path, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
    nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
  delete[] path;
  if (file == INVALID_HANDLE_VALUE) return false;
  ULARGE_INTEGER bytes;
  bytes.QuadPart = size;
  HANDLE mapping = CreateFileMappingW(file, nullptr, PAGE_READWRITE, bytes.HighPart, bytes.LowPart, nullptr);
  if (mapping == nullptr) {
    CloseHandle(file);
    return false;
  }
  void *view = MapViewOfFile(mapping, FILE_MAP_READ | FILE_MAP_WRITE, 0, 0, size);
  if (view == nullptr) {
    CloseHandle(mapping);
    CloseHandle(file);
    return false;
  }
  mapped->data = view;
  mapped->size = size;
  mapped->file = reinterpret_cast<intptr_t>(file);
  mapped->mapping = reinterpret_cast<intptr_t>(mapping);
  return true;
}

void unmap_file(MappedFile *mapped) {
  if (mapped == nullptr || mapped->data == nullptr) return;
  UnmapViewOfFile(mapped->data);
  CloseHandle(reinterpret_cast<HANDLE>(mapped->mapping));
  CloseHandle(reinterpret_cast<HANDLE>(mapped->file));
  *mapped = MappedFile{};
}

void lower_current_thread_priority() { SetThreadPriority(GetCurrentThread(), THREAD_PRIORITY_BELOW_NORMAL); }

int32_t last_system_error() { return static_cast<int32_t>(GetLastError()); }
#else
bool duplicate_handle(OsHandle source, OsHandle *duplicate) {
  int copy = fcntl(static_cast<int>(source), F_DUPFD_CLOEXEC, 0);
  if (copy < 0) return false;
  *duplicate = copy;
  return true;
}

void close_handle(OsHandle handle) {
  if (handle >= 0) close(static_cast<int>(handle));
}

int64_t read_some(OsHandle handle, void *buffer, size_t length) {
  for (;;) {
    ssize_t count = read(static_cast<int>(handle), buffer, length);
    if (count >= 0) return count;
    if (errno != EINTR) return -1;
  }
}

bool write_all(OsHandle handle, const void *buffer, size_t length) {
  const auto *bytes = static_cast<const uint8_t *>(buffer);
  while (length > 0) {
    ssize_t count = write(static_cast<int>(handle), bytes, length);
    if (count < 0 && errno == EINTR) continue;
    if (count <= 0) return false;
    bytes += count;
    length -= static_cast<size_t>(count);
  }
  return true;
}

bool map_file(const char *utf8_path, size_t size, MappedFile *mapped) {
  if (utf8_path == nullptr || mapped == nullptr || size == 0) return false;
  int file = open(utf8_path, O_RDWR | O_CLOEXEC);
  if (file < 0) return false;
  void *view = mmap(nullptr, size, PROT_READ | PROT_WRITE, MAP_SHARED, file, 0);
  if (view == MAP_FAILED) {
    close(file);
    return false;
  }
  mapped->data = view;
  mapped->size = size;
  mapped->file = file;
  return true;
}

void unmap_file(MappedFile *mapped) {
  if (mapped == nullptr || mapped->data == nullptr) return;
  munmap(mapped->data, mapped->size);
  close(static_cast<int>(mapped->file));
  *mapped = MappedFile{};
}

void lower_current_thread_priority() {}

int32_t last_system_error() { return errno; }
#endif

} // namespace tufreplay::camera
