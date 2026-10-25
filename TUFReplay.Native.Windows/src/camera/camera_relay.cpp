// Native camera relay. The FFmpeg capture process writes raw I420 frames to its
// stdout and showinfo timestamps to its stderr; this relay pairs them, keeps the
// latest frame, converts throttled previews into the shared mapping, and streams
// timestamped frames to the run's encoder. Managed code never touches a frame.
#include <algorithm>
#include <atomic>
#include <chrono>
#include <climits>
#include <condition_variable>
#include <cstring>
#include <deque>
#include <memory>
#include <mutex>
#include <new>
#include <string>

#include "capture_log.hpp"
#include "frames.hpp"
#include "matroska_writer.hpp"
#include "platform.hpp"
#include "preview.hpp"
#include "tufreplay_camera.h"

namespace tufreplay::camera {
namespace {

constexpr size_t kTimestampCapacity = 128;
constexpr size_t kDiagnosticLines = 16;
constexpr size_t kDiagnosticLineLength = 500;
constexpr size_t kMaxLogLine = 64 * 1024;
constexpr int64_t kPreviewIntervalNs = 1'000'000'000LL / 30;
constexpr int32_t kPreviewStopTimeoutMs = 2000;

struct FrameInfo {
  int64_t ticks;
  int32_t width;
  int32_t height;
};

class CameraCore;

// One run's encoder stream. It is prepared before the run, so its writer can open
// the encoder with a warm-up frame, and begins receiving frames at the run boundary.
class Recording : public ByteSink, public std::enable_shared_from_this<Recording> {
 public:
  Recording(std::shared_ptr<CameraCore> core, std::shared_ptr<FramePool> pool, int32_t width, int32_t height,
    int32_t frame_rate, int64_t frequency, size_t queue_capacity)
      : core_(std::move(core)), pool_(std::move(pool)), width_(width), height_(height), frame_rate_(frame_rate),
        frequency_(frequency), frames_(queue_capacity) {}

  ~Recording() override {
    frames_.dispose();
    close_handle(output_);
  }

  // Called serially under the camera's sink gate. Only retains the frame.
  void submit(Frame *frame) {
    if (frame->ticks < boundary_ || frame->ticks <= last_submitted_ || capture_failed_.load() || cancelled_.load())
      return;
    if (frames_.submit(frame)) last_submitted_ = frame->ticks;
  }

  void fail_capture() {
    capture_failed_.store(true);
    frames_.complete();
  }

  // Called under the camera's sink gate before this recording becomes the sink.
  bool start(int64_t boundary) {
    if (started_ || cancelled_.load() || sealed_.load()) return false;
    boundary_ = boundary;
    started_ = true;
    return true;
  }

  int32_t set_output(OsHandle handle);
  int32_t begin(int64_t boundary, int64_t now);
  void seal();
  int32_t finish(int32_t timeout_ms, tufreplay_camera_recording_stats *stats);
  void cancel();
  bool wait_writer(int32_t timeout_ms) { return writer_.wait(timeout_ms); }
  void get_stats(tufreplay_camera_recording_stats *stats) const;
  FrameQueue &queue_for_tests() { return frames_; }

  bool write(const void *data, size_t length) override { return write_all(output_, data, length); }

 private:
  static void writer_entry(void *argument);
  void run_writer();
  void detach();

  std::shared_ptr<CameraCore> core_;
  std::shared_ptr<FramePool> pool_;
  int64_t boundary_ = INT64_MAX; // Guarded by the camera's sink gate.
  bool started_ = false;         // Guarded by the camera's sink gate.
  const int32_t width_, height_, frame_rate_;
  const int64_t frequency_;
  int64_t last_submitted_ = INT64_MIN;
  FrameQueue frames_;
  std::mutex gate_;
  OsHandle output_ = kInvalidHandle;
  WorkerThread writer_;
  std::atomic<bool> capture_failed_{false}, cancelled_{false}, finished_{false}, sealed_{false};
  std::atomic<bool> writer_finished_{false}, write_failed_{false}, output_closed_early_{false};
  std::atomic<int64_t> written_{0}, first_ticks_{0}, duration_us_{0};
};

class CameraCore : public std::enable_shared_from_this<CameraCore> {
 public:
  explicit CameraCore(const tufreplay_camera_config &config)
      : max_width_(config.max_width), max_height_(config.max_height), clock_unix_us_(config.clock_unix_us),
        clock_ticks_(config.clock_ticks), frequency_(config.ticks_frequency),
        pool_capacity_(config.pool_capacity > 0 ? config.pool_capacity : 8),
        timestamp_timeout_ms_(config.timestamp_timeout_ms > 0 ? config.timestamp_timeout_ms : 10000),
        preview_frames_(1) {
    if (config.preview_path != nullptr) preview_path_ = config.preview_path;
    preview_max_width_ = config.preview_max_width;
    preview_max_height_ = config.preview_max_height;
  }

  ~CameraCore() {
    preview_frames_.dispose();
    close_handle(frame_handle_);
    close_handle(log_handle_);
  }

  // Returns 0 or the stage that failed (see tufreplay_camera_last_create_error).
  int32_t open(const tufreplay_camera_config &config) {
    if (max_width_ < 2 || max_height_ < 2 || frequency_ <= 0) return 1;
    if (!duplicate_handle(config.frame_handle, &frame_handle_)) return 2;
    if (!duplicate_handle(config.log_handle, &log_handle_)) return 2;
    if (!preview_path_.empty() && !preview_.open(preview_path_.c_str(), preview_max_width_, preview_max_height_))
      return 3;
    return start_thread(log_thread_, &CameraCore::run_log) && start_thread(frame_thread_, &CameraCore::run_frames) ? 0
                                                                                                               : 4;
  }

  int32_t wait_ready(int32_t timeout_ms) {
    std::unique_lock<std::mutex> lock(state_gate_);
    auto settled = [this] { return ready_ || error_ != TUFREPLAY_CAMERA_ERROR_NONE; };
    if (timeout_ms < 0)
      state_changed_.wait(lock, settled);
    else
      state_changed_.wait_for(lock, std::chrono::milliseconds(timeout_ms), settled);
    if (error_ != TUFREPLAY_CAMERA_ERROR_NONE) return -1;
    return ready_ ? 1 : 0;
  }

  void get_state(tufreplay_camera_state *state) {
    std::lock_guard<std::mutex> lock(state_gate_);
    state->ready = ready_ ? 1 : 0;
    state->error = error_;
    state->width = width_;
    state->height = height_;
    state->input_rate_numerator = input_rate_numerator_;
    state->input_rate_denominator = input_rate_denominator_;
    state->frames_captured = frames_captured_.load();
    state->previews_published = previews_published_.load();
  }

  int32_t diagnostics(char *destination, int32_t capacity) {
    std::lock_guard<std::mutex> lock(state_gate_);
    std::string joined;
    for (const std::string &line : diagnostics_) {
      if (!joined.empty()) joined += " | ";
      joined += line;
    }
    if (destination == nullptr || capacity <= 0) return static_cast<int32_t>(joined.size());
    size_t count = std::min(joined.size(), static_cast<size_t>(capacity));
    std::memcpy(destination, joined.data(), count);
    return static_cast<int32_t>(count);
  }

  void stop() {
    std::shared_ptr<Recording> sink;
    Frame *latest = nullptr;
    {
      std::lock_guard<std::mutex> lock(sink_gate_);
      if (disposed_) return;
      disposed_ = true;
      sink = std::move(sink_);
      latest = latest_;
      latest_ = nullptr;
    }
    if (sink != nullptr) sink->fail_capture();
    if (latest != nullptr) latest->release();
    fail(TUFREPLAY_CAMERA_ERROR_STOPPED);
    {
      std::lock_guard<std::mutex> lock(timestamp_gate_);
      timestamps_completed_ = true;
      timestamp_changed_.notify_all();
    }
    preview_frames_.dispose();
    // Never unmap while a conversion could still write into the mapping.
    if (preview_thread_.wait(kPreviewStopTimeoutMs)) preview_.close();
  }

  bool wait_readers(int32_t timeout_ms) {
    int64_t deadline = monotonic_ns() + static_cast<int64_t>(timeout_ms) * 1'000'000;
    if (!log_thread_.wait(timeout_ms)) return false;
    int64_t remaining = std::max<int64_t>(0, (deadline - monotonic_ns()) / 1'000'000);
    return frame_thread_.wait(static_cast<int32_t>(remaining));
  }

  std::shared_ptr<Recording> prepare(int32_t frame_rate, int32_t queue_capacity) {
    std::lock_guard<std::mutex> lock(sink_gate_);
    if (disposed_ || pool_ == nullptr || error_snapshot() != TUFREPLAY_CAMERA_ERROR_NONE) return nullptr;
    return std::make_shared<Recording>(shared_from_this(), pool_, width_snapshot_, height_snapshot_, frame_rate,
      frequency_, static_cast<size_t>(queue_capacity > 0 ? queue_capacity : 3));
  }

  bool begin(const std::shared_ptr<Recording> &recording, int64_t boundary, int64_t now) {
    std::lock_guard<std::mutex> lock(sink_gate_);
    if (disposed_ || error_snapshot() != TUFREPLAY_CAMERA_ERROR_NONE || !recording->start(boundary)) return false;
    sink_ = recording;
    // Keep the last complete frame's real capture time when it is recent enough.
    if (latest_ != nullptr && latest_->fresh(now, frequency_)) recording->submit(latest_);
    return true;
  }

  void detach(const Recording *recording) {
    std::lock_guard<std::mutex> lock(sink_gate_);
    if (sink_.get() == recording) sink_.reset();
  }

 private:
  // Each thread owns a reference, so the relay outlives a reader still blocked on
  // a pipe after the managed side has released its handle.
  struct ThreadArgument {
    std::shared_ptr<CameraCore> core;
    void (CameraCore::*run)();
  };

  static void thread_entry(void *opaque) {
    std::unique_ptr<ThreadArgument> argument(static_cast<ThreadArgument *>(opaque));
    ((*argument->core).*(argument->run))();
  }

  bool start_thread(WorkerThread &thread, void (CameraCore::*run)()) {
    auto *argument = new (std::nothrow) ThreadArgument{shared_from_this(), run};
    if (argument == nullptr) return false;
    if (thread.start(&CameraCore::thread_entry, argument)) return true;
    delete argument;
    return false;
  }

  int32_t error_snapshot() {
    std::lock_guard<std::mutex> lock(state_gate_);
    return error_;
  }

  void fail(int32_t error) {
    {
      std::lock_guard<std::mutex> lock(state_gate_);
      if (error_ == TUFREPLAY_CAMERA_ERROR_NONE) error_ = error;
      state_changed_.notify_all();
    }
    std::shared_ptr<Recording> sink;
    {
      std::lock_guard<std::mutex> lock(sink_gate_);
      sink = sink_;
    }
    if (sink != nullptr) sink->fail_capture();
  }

  void record_diagnostic(const char *line, size_t length) {
    std::lock_guard<std::mutex> lock(state_gate_);
    diagnostics_.emplace_back(line, std::min(length, kDiagnosticLineLength));
    while (diagnostics_.size() > kDiagnosticLines) diagnostics_.pop_front();
  }

  void handle_log_line(const char *line, size_t length) {
    CaptureLogLine parsed = parse_capture_log_line(line, length);
    switch (parsed.kind) {
      case CaptureLogLine::Kind::TimeBase: {
        std::lock_guard<std::mutex> lock(state_gate_);
        if (parsed.has_time_base) {
          time_base_numerator_ = parsed.time_base_numerator;
          time_base_denominator_ = parsed.time_base_denominator;
        }
        if (parsed.has_input_rate) {
          input_rate_numerator_ = parsed.input_rate_numerator;
          input_rate_denominator_ = parsed.input_rate_denominator;
        }
        return;
      }
      case CaptureLogLine::Kind::Other:
        record_diagnostic(line, length);
        return;
      case CaptureLogLine::Kind::ShowinfoOther:
        return;
      case CaptureLogLine::Kind::Frame:
        break;
    }
    int64_t numerator = 0, denominator = 0;
    {
      std::lock_guard<std::mutex> lock(state_gate_);
      numerator = time_base_numerator_;
      denominator = time_base_denominator_;
    }
    if (denominator <= 0) return;
    if (parsed.frame_size_overflow) {
      fail(TUFREPLAY_CAMERA_ERROR_RESOLUTION_UNAVAILABLE);
      return;
    }
    int64_t ticks = 0;
    if (!wall_clock_to_ticks(parsed.pts, numerator, denominator, clock_unix_us_, clock_ticks_, frequency_, &ticks)) {
      fail(TUFREPLAY_CAMERA_ERROR_TIMESTAMP);
      return;
    }
    std::lock_guard<std::mutex> lock(timestamp_gate_);
    if (timestamps_.size() >= kTimestampCapacity) {
      fail(TUFREPLAY_CAMERA_ERROR_FELL_BEHIND);
      return;
    }
    timestamps_.push_back(FrameInfo{ticks, static_cast<int32_t>(parsed.width), static_cast<int32_t>(parsed.height)});
    timestamp_changed_.notify_one();
  }

  // Always drains stderr until the process exits, so FFmpeg never blocks on it.
  void run_log() {
    lower_current_thread_priority();
    std::string line;
    char buffer[4096];
    bool pending_cr = false;
    for (;;) {
      int64_t count = read_some(log_handle_, buffer, sizeof(buffer));
      if (count <= 0) break;
      for (int64_t index = 0; index < count; ++index) {
        char c = buffer[index];
        if (pending_cr) {
          pending_cr = false;
          if (c == '\n') continue;
        }
        if (c == '\r' || c == '\n') {
          handle_log_line(line.data(), line.size());
          line.clear();
          pending_cr = c == '\r';
        } else if (line.size() < kMaxLogLine) {
          line.push_back(c);
        }
      }
    }
    if (!line.empty()) handle_log_line(line.data(), line.size());
    std::lock_guard<std::mutex> lock(timestamp_gate_);
    timestamps_completed_ = true;
    timestamp_changed_.notify_all();
  }

  bool take_timestamp(FrameInfo *info) {
    std::unique_lock<std::mutex> lock(timestamp_gate_);
    timestamp_changed_.wait_for(lock, std::chrono::milliseconds(timestamp_timeout_ms_),
      [this] { return !timestamps_.empty() || timestamps_completed_; });
    if (timestamps_.empty()) return false;
    *info = timestamps_.front();
    timestamps_.pop_front();
    return true;
  }

  bool read_exact(uint8_t *destination, size_t length) {
    size_t read = 0;
    while (read < length) {
      int64_t count = read_some(frame_handle_, destination + read, length - read);
      if (count <= 0) return false;
      read += static_cast<size_t>(count);
    }
    return true;
  }

  bool disposed() {
    std::lock_guard<std::mutex> lock(sink_gate_);
    return disposed_;
  }

  void run_frames() {
    lower_current_thread_priority();
    Frame *frame = nullptr;
    std::shared_ptr<FramePool> pool;
    while (!disposed()) {
      FrameInfo info{};
      if (!take_timestamp(&info)) {
        if (!disposed()) fail(TUFREPLAY_CAMERA_ERROR_STOPPED_SENDING);
        break;
      }
      if (info.width < 2 || info.height < 2 || info.width > max_width_ || info.height > max_height_
        || (info.width & 1) != 0 || (info.height & 1) != 0) {
        fail(TUFREPLAY_CAMERA_ERROR_RESOLUTION_UNAVAILABLE);
        break;
      }
      if (frame == nullptr) {
        pool = FramePool::create(static_cast<size_t>(info.width) * info.height * 3 / 2, pool_capacity_);
        if (pool == nullptr) {
          fail(TUFREPLAY_CAMERA_ERROR_OUT_OF_MEMORY);
          break;
        }
        frame = pool->try_rent();
        {
          std::lock_guard<std::mutex> lock(state_gate_);
          width_ = info.width;
          height_ = info.height;
        }
        std::lock_guard<std::mutex> lock(sink_gate_);
        if (disposed_) break;
        pool_ = pool;
        width_snapshot_ = info.width;
        height_snapshot_ = info.height;
        start_preview(info.width, info.height);
      } else if (info.width != width_snapshot_ || info.height != height_snapshot_) {
        fail(TUFREPLAY_CAMERA_ERROR_RESOLUTION_CHANGED);
        break;
      }
      if (!read_exact(frame->pixels, pool->byte_count())) {
        if (!disposed()) fail(TUFREPLAY_CAMERA_ERROR_STOPPED_SENDING);
        break;
      }
      if (error_snapshot() != TUFREPLAY_CAMERA_ERROR_NONE) break;
      frame->ticks = info.ticks;
      frames_captured_.fetch_add(1);
      Frame *next = pool->try_rent();
      // Keep draining the device even if every consumer is temporarily busy.
      if (next == nullptr) continue;
      std::shared_ptr<Recording> sink;
      {
        std::lock_guard<std::mutex> lock(sink_gate_);
        if (disposed_) {
          next->release();
          break;
        }
        if (sink_ != nullptr) sink_->submit(frame);
        submit_preview(frame);
        if (latest_ != nullptr) latest_->release();
        latest_ = frame;
        frame = next;
      }
      std::lock_guard<std::mutex> lock(state_gate_);
      if (!ready_) {
        ready_ = true;
        state_changed_.notify_all();
      }
    }
    if (frame != nullptr) frame->release();
  }

  // Called under the sink gate once the capture size is known.
  void start_preview(int32_t width, int32_t height) {
    if (!preview_.is_open()) return;
    FrameSize target = fit_size(width, height, preview_.max_width(), preview_.max_height());
    if (target.width < 1 || !converter_.configure(width, height, target.width, target.height)) return;
    rgba_.assign(converter_.target_bytes(), 0);
    if (!start_thread(preview_thread_, &CameraCore::run_preview)) rgba_.clear();
  }

  // Never waits on the conversion worker; at most one pending frame, 30 per second.
  void submit_preview(Frame *frame) {
    if (rgba_.empty() || !preview_.should_publish()) return;
    int64_t now = monotonic_ns();
    if (last_preview_ns_ != 0 && now - last_preview_ns_ < kPreviewIntervalNs) return;
    if (preview_frames_.submit(frame)) last_preview_ns_ = now;
  }

  void run_preview() {
    lower_current_thread_priority();
    Frame *frame = nullptr;
    while ((frame = preview_frames_.take()) != nullptr) {
      if (preview_.should_publish()) {
        converter_.convert(frame->pixels, rgba_.data());
        if (preview_.publish(rgba_.data(), converter_.target_width(), converter_.target_height()))
          previews_published_.fetch_add(1);
      }
      frame->release();
    }
  }

  const int32_t max_width_, max_height_;
  const int64_t clock_unix_us_, clock_ticks_, frequency_;
  const int32_t pool_capacity_, timestamp_timeout_ms_;
  std::string preview_path_;
  int32_t preview_max_width_ = 0, preview_max_height_ = 0;

  OsHandle frame_handle_ = kInvalidHandle, log_handle_ = kInvalidHandle;
  WorkerThread log_thread_, frame_thread_, preview_thread_;

  std::mutex state_gate_;
  std::condition_variable state_changed_;
  bool ready_ = false;
  int32_t error_ = TUFREPLAY_CAMERA_ERROR_NONE;
  int32_t width_ = 0, height_ = 0;
  int64_t time_base_numerator_ = 0, time_base_denominator_ = 0;
  int64_t input_rate_numerator_ = 0, input_rate_denominator_ = 0;
  std::deque<std::string> diagnostics_;
  std::atomic<int64_t> frames_captured_{0}, previews_published_{0};

  std::mutex timestamp_gate_;
  std::condition_variable timestamp_changed_;
  std::deque<FrameInfo> timestamps_;
  bool timestamps_completed_ = false;

  std::mutex sink_gate_;
  bool disposed_ = false;
  std::shared_ptr<FramePool> pool_;
  int32_t width_snapshot_ = 0, height_snapshot_ = 0;
  std::shared_ptr<Recording> sink_;
  Frame *latest_ = nullptr;

  PreviewTarget preview_;
  PreviewConverter converter_;
  std::vector<uint8_t> rgba_;
  FrameQueue preview_frames_;
  int64_t last_preview_ns_ = 0;
};

int32_t Recording::set_output(OsHandle handle) {
  std::lock_guard<std::mutex> lock(gate_);
  if (output_ != kInvalidHandle || cancelled_.load() || finished_.load()) return TUFREPLAY_CAMERA_ERROR_INVALID_ARGUMENT;
  if (!duplicate_handle(handle, &output_)) return TUFREPLAY_CAMERA_ERROR_INVALID_ARGUMENT;
  auto *argument = new (std::nothrow) std::shared_ptr<Recording>(shared_from_this());
  if (argument == nullptr || !writer_.start(&Recording::writer_entry, argument)) {
    delete argument;
    close_handle(output_);
    output_ = kInvalidHandle;
    return TUFREPLAY_CAMERA_ERROR_OUT_OF_MEMORY;
  }
  return TUFREPLAY_CAMERA_ERROR_NONE;
}

int32_t Recording::begin(int64_t boundary, int64_t now) {
  return core_ != nullptr && core_->begin(shared_from_this(), boundary, now) ? TUFREPLAY_CAMERA_ERROR_NONE
                                                                            : TUFREPLAY_CAMERA_ERROR_STOPPED;
}

void Recording::writer_entry(void *opaque) {
  std::unique_ptr<std::shared_ptr<Recording>> recording(static_cast<std::shared_ptr<Recording> *>(opaque));
  (*recording)->run_writer();
}

void Recording::run_writer() {
  lower_current_thread_priority();
  MatroskaWriter stream(*this, width_, height_, frame_rate_, frequency_);
  // The warm-up frame opens the encoder session now instead of at the first
  // captured frame, which a hardware encoder can take seconds to do.
  bool failed = stream.write_header() != MatroskaWriter::Result::Ok
    || stream.write_warm_up_frame() != MatroskaWriter::Result::Ok;
  if (failed) write_failed_.store(true);
  while (!failed) {
    Frame *frame = frames_.take();
    if (frame == nullptr) break;
    MatroskaWriter::Result result = stream.write_frame(frame->pixels, frame->ticks);
    frame->release();
    if (result != MatroskaWriter::Result::Ok) {
      // FFmpeg's -fs closes its input after the limit; earlier failures are real.
      if (written_.load() > 0 && result == MatroskaWriter::Result::WriteFailed)
        output_closed_early_.store(true);
      else
        write_failed_.store(true);
      break;
    }
    if (written_.load() == 0) first_ticks_.store(stream.first_timestamp_ticks());
    duration_us_.store(stream.duration_us());
    written_.fetch_add(1);
  }
  frames_.dispose();
  {
    std::lock_guard<std::mutex> lock(gate_);
    close_handle(output_); // End of stream lets the encoder finalize the MP4.
    output_ = kInvalidHandle;
  }
  writer_finished_.store(true);
}

void Recording::detach() {
  if (core_ != nullptr) core_->detach(this);
}

void Recording::seal() {
  sealed_.store(true);
  detach();
  frames_.complete();
}

int32_t Recording::finish(int32_t timeout_ms, tufreplay_camera_recording_stats *stats) {
  seal();
  bool started;
  {
    std::lock_guard<std::mutex> lock(gate_);
    finished_.store(true);
    started = writer_.started();
  }
  bool done = true;
  if (started)
    done = writer_.wait(timeout_ms);
  else
    frames_.dispose();
  get_stats(stats);
  if (stats != nullptr) stats->writer_finished = done ? 1 : 0;
  return done ? 1 : 0;
}

void Recording::cancel() {
  cancelled_.store(true);
  detach();
  frames_.dispose();
}

void Recording::get_stats(tufreplay_camera_recording_stats *stats) const {
  if (stats == nullptr) return;
  stats->written_frames = written_.load();
  stats->first_timestamp_ticks = first_ticks_.load();
  stats->duration_us = duration_us_.load();
  stats->dropped_frames = frames_.dropped();
  stats->writer_finished = writer_finished_.load() ? 1 : 0;
  stats->write_failed = write_failed_.load() ? 1 : 0;
  stats->output_closed_early = output_closed_early_.load() ? 1 : 0;
  stats->capture_failed = capture_failed_.load() ? 1 : 0;
}

struct CameraHandle {
  std::shared_ptr<CameraCore> core;
};

struct RecordingHandle {
  std::shared_ptr<Recording> recording;
};

} // namespace
} // namespace tufreplay::camera

using tufreplay::camera::CameraCore;
using tufreplay::camera::CameraHandle;
using tufreplay::camera::RecordingHandle;

extern "C" {
uint32_t tufreplay_camera_abi_version(void) { return TUFREPLAY_CAMERA_ABI_VERSION; }

namespace {
thread_local int32_t t_create_error = 0;
thread_local int32_t t_create_system_error = 0;
} // namespace

void *tufreplay_camera_create(const tufreplay_camera_config *config) {
  t_create_error = 1;
  t_create_system_error = 0;
  if (config == nullptr) return nullptr;
  auto core = std::make_shared<CameraCore>(*config);
  t_create_error = core->open(*config);
  if (t_create_error != 0) {
    t_create_system_error = tufreplay::camera::last_system_error();
    core->stop();
    return nullptr;
  }
  return new (std::nothrow) CameraHandle{std::move(core)};
}

int32_t tufreplay_camera_last_create_error(int32_t *system_error) {
  if (system_error != nullptr) *system_error = t_create_system_error;
  return t_create_error;
}

int32_t tufreplay_camera_wait_ready(void *camera, int32_t timeout_ms) {
  auto *handle = static_cast<CameraHandle *>(camera);
  return handle == nullptr ? -1 : handle->core->wait_ready(timeout_ms);
}

void tufreplay_camera_get_state(void *camera, tufreplay_camera_state *state) {
  auto *handle = static_cast<CameraHandle *>(camera);
  if (handle != nullptr && state != nullptr) handle->core->get_state(state);
}

int32_t tufreplay_camera_get_diagnostics(void *camera, char *destination, int32_t capacity) {
  auto *handle = static_cast<CameraHandle *>(camera);
  return handle == nullptr ? 0 : handle->core->diagnostics(destination, capacity);
}

void tufreplay_camera_stop(void *camera) {
  auto *handle = static_cast<CameraHandle *>(camera);
  if (handle != nullptr) handle->core->stop();
}

int32_t tufreplay_camera_destroy(void *camera, int32_t timeout_ms) {
  auto *handle = static_cast<CameraHandle *>(camera);
  if (handle == nullptr) return 1;
  handle->core->stop();
  // Reader threads own a reference and end when the process exits; a reader that
  // is still blocked keeps the relay alive instead of reading freed memory.
  bool ended = handle->core->wait_readers(timeout_ms);
  delete handle;
  return ended ? 1 : 0;
}

void *tufreplay_camera_recording_prepare(void *camera, int32_t frame_rate, int32_t queue_capacity) {
  auto *handle = static_cast<CameraHandle *>(camera);
  if (handle == nullptr || frame_rate < 1) return nullptr;
  auto recording = handle->core->prepare(frame_rate, queue_capacity);
  if (recording == nullptr) return nullptr;
  return new (std::nothrow) RecordingHandle{std::move(recording)};
}

int32_t tufreplay_camera_recording_begin(void *recording, int64_t boundary_ticks, int64_t now_ticks) {
  auto *handle = static_cast<RecordingHandle *>(recording);
  return handle == nullptr ? TUFREPLAY_CAMERA_ERROR_INVALID_ARGUMENT
                           : handle->recording->begin(boundary_ticks, now_ticks);
}

int32_t tufreplay_camera_recording_set_output(void *recording, int64_t output_handle) {
  auto *handle = static_cast<RecordingHandle *>(recording);
  return handle == nullptr ? TUFREPLAY_CAMERA_ERROR_INVALID_ARGUMENT : handle->recording->set_output(output_handle);
}

void tufreplay_camera_recording_get_stats(void *recording, tufreplay_camera_recording_stats *stats) {
  auto *handle = static_cast<RecordingHandle *>(recording);
  if (handle != nullptr) handle->recording->get_stats(stats);
}

void tufreplay_camera_recording_seal(void *recording) {
  auto *handle = static_cast<RecordingHandle *>(recording);
  if (handle != nullptr) handle->recording->seal();
}

int32_t tufreplay_camera_recording_finish(void *recording, int32_t timeout_ms, tufreplay_camera_recording_stats *stats) {
  auto *handle = static_cast<RecordingHandle *>(recording);
  return handle == nullptr ? 1 : handle->recording->finish(timeout_ms, stats);
}

void tufreplay_camera_recording_cancel(void *recording) {
  auto *handle = static_cast<RecordingHandle *>(recording);
  if (handle != nullptr) handle->recording->cancel();
}

int32_t tufreplay_camera_recording_destroy(void *recording, int32_t timeout_ms) {
  auto *handle = static_cast<RecordingHandle *>(recording);
  if (handle == nullptr) return 1;
  handle->recording->cancel();
  bool ended = handle->recording->wait_writer(timeout_ms);
  delete handle;
  return ended ? 1 : 0;
}
} // extern "C"
