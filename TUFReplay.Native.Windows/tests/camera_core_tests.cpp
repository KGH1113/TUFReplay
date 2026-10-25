// Host tests for the camera relay core (macOS/Linux). The same sources build into
// TUFReplayNative.dll; only the platform layer differs.
#include <atomic>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fcntl.h>
#include <mutex>
#include <string>
#include <thread>
#include <unistd.h>
#include <vector>

#include "camera/capture_log.hpp"
#include "camera/frames.hpp"
#include "camera/matroska_writer.hpp"
#include "camera/preview.hpp"
#include "tufreplay_camera.h"

using namespace tufreplay::camera;

namespace {
void check(bool condition, const char *message) {
  if (!condition) {
    std::fprintf(stderr, "FAIL: %s\n", message);
    std::exit(1);
  }
}

uint32_t fnv(const uint8_t *data, size_t length) {
  uint32_t hash = 2166136261u;
  for (size_t i = 0; i < length; ++i) {
    hash ^= data[i];
    hash *= 16777619u;
  }
  return hash;
}

class MemorySink : public ByteSink {
 public:
  bool write(const void *data, size_t length) override {
    if (fail_after >= 0 && static_cast<int64_t>(bytes.size() + length) > fail_after) return false;
    const auto *begin = static_cast<const uint8_t *>(data);
    bytes.insert(bytes.end(), begin, begin + length);
    return true;
  }
  std::vector<uint8_t> bytes;
  int64_t fail_after = -1;
};

struct ParsedFrame {
  int64_t timestamp;
  std::vector<uint8_t> pixels;
};

uint64_t read_vint(const std::vector<uint8_t> &bytes, size_t &offset, bool keep_marker) {
  uint8_t first = bytes[offset++];
  int mask = 0x80, count = 1;
  while ((first & mask) == 0) {
    mask >>= 1;
    ++count;
  }
  uint64_t value = keep_marker ? first : static_cast<uint64_t>(first & (mask - 1));
  for (int i = 1; i < count; ++i) value = (value << 8) | bytes[offset++];
  return value;
}

std::vector<ParsedFrame> parse_frames(const std::vector<uint8_t> &bytes) {
  std::vector<ParsedFrame> frames;
  size_t offset = 0;
  while (offset < bytes.size()) {
    uint64_t id = read_vint(bytes, offset, true);
    uint64_t size = read_vint(bytes, offset, false);
    if (id == 0x18538067) continue; // Unknown-length Segment.
    size_t end = offset + static_cast<size_t>(size);
    if (id != 0x1F43B675) {
      offset = end;
      continue;
    }
    int64_t timestamp = 0;
    while (offset < end) {
      uint64_t child = read_vint(bytes, offset, true);
      size_t child_size = static_cast<size_t>(read_vint(bytes, offset, false));
      if (child == 0xE7) {
        for (size_t i = 0; i < child_size; ++i) timestamp = (timestamp << 8) | bytes[offset + i];
      } else if (child == 0xA3) {
        check(bytes[offset] == 0x81 && bytes[offset + 3] == 0x80, "Invalid I420 block flags.");
        frames.push_back({timestamp, std::vector<uint8_t>(bytes.begin() + offset + 4, bytes.begin() + offset + child_size)});
      }
      offset += child_size;
    }
    check(offset == end, "Cluster payload size was incorrect.");
  }
  return frames;
}

void frames_return_only_after_every_consumer_releases() {
  auto pool = FramePool::create(4, 2);
  Frame *shared = pool->try_rent();
  shared->pixels[0] = 77;
  shared->ticks = 456;
  shared->retain();
  shared->retain();
  Frame *other = pool->try_rent();
  check(pool->try_rent() == nullptr, "The pool exceeded its capacity.");
  shared->release();
  shared->release();
  check(pool->try_rent() == nullptr, "Capture reused a frame still held by a consumer.");
  check(shared->pixels[0] == 77 && shared->ticks == 456, "An active frame was reset.");
  shared->release();
  Frame *reused = pool->try_rent();
  check(reused == shared && reused->ticks == 0, "The pool did not reuse a released frame cleanly.");
  check(shared->fresh(0, 1000) == false, "A frame without a timestamp was fresh.");
  reused->ticks = 1000;
  check(reused->fresh(1060, 1000) && !reused->fresh(999, 1000) && !reused->fresh(1251, 1000), "Freshness changed.");
  reused->release();
  other->release();
}

void overload_replaces_stale_frames_without_waiting() {
  auto pool = FramePool::create(4, 5);
  FrameQueue queue(3);
  Frame *held = pool->try_rent();
  held->pixels[0] = 0xDA;
  check(queue.submit(held), "The queue rejected its first frame.");
  Frame *active = queue.take();
  check(active == held, "The consumer did not take the first frame.");
  for (int index = 0; index < 1000; ++index) {
    Frame *frame = pool->try_rent();
    check(frame != nullptr, "Replacing stale frames exhausted the bounded pool.");
    frame->ticks = index + 1;
    check(queue.submit(frame), "Overload aborted the recording queue.");
    frame->release();
  }
  check(active->pixels[0] == 0xDA, "Overload overwrote the encoder's active frame.");
  check(queue.dropped() == 997, "Dropped-frame diagnostics lost replaced frames.");
  Frame *next = queue.take();
  check(next != nullptr && next->ticks == 998, "The queue did not keep the most recent frames.");
  next->release();
  active->release();
  held->release();
  queue.dispose();
  std::vector<Frame *> all;
  for (int i = 0; i < 5; ++i) all.push_back(pool->try_rent());
  for (Frame *frame : all) check(frame != nullptr, "A consumer leaked a camera frame.");
  check(pool->try_rent() == nullptr, "The camera frame pool exceeded its memory bound.");
  for (Frame *frame : all) frame->release();
}

void completing_queue_wakes_consumer_and_rejects_frames() {
  auto pool = FramePool::create(4, 2);
  FrameQueue queue(1);
  std::atomic<bool> woke{false};
  std::thread consumer([&] {
    Frame *frame = queue.take();
    check(frame == nullptr, "A completed empty queue returned a frame.");
    woke = true;
  });
  std::this_thread::sleep_for(std::chrono::milliseconds(20));
  queue.complete();
  consumer.join();
  Frame *late = pool->try_rent();
  check(woke && !queue.submit(late), "A completed queue accepted another frame.");
  late->release();
}

void matroska_matches_the_previous_stream_byte_for_byte() {
  struct Golden {
    int32_t width, height, fps;
    int64_t frequency;
    size_t length;
    uint32_t hash;
    int64_t duration;
    std::vector<int64_t> times;
  } cases[] = {
    {12, 8, 60, 10'000'000, 667, 0x89D3DF78u, 21600029011,
      {8'000'000'000'000'000LL, 8'000'000'000'166'670LL, 8'000'216'000'123'450LL}},
    {1920, 1080, 30, 1'000'000'000, 12441870, 0x81DE91D6u, 4033333,
      {5'000'000'000LL, 5'033'333'333LL, 5'066'666'667LL, 9'000'000'000LL}},
    {2, 2, 1000, 1000, 223, 0x851E736Eu, 2000, {1000, 1001}},
  };
  for (const Golden &golden : cases) {
    std::vector<uint8_t> pixels(static_cast<size_t>(golden.width) * golden.height * 3 / 2 + 7);
    for (size_t i = 0; i < pixels.size(); ++i) pixels[i] = static_cast<uint8_t>(i * 31 + 7);
    MemorySink sink;
    MatroskaWriter writer(sink, golden.width, golden.height, golden.fps, golden.frequency);
    check(writer.write_header() == MatroskaWriter::Result::Ok, "Matroska header failed.");
    for (int64_t time : golden.times)
      check(writer.write_frame(pixels.data(), time) == MatroskaWriter::Result::Ok, "Matroska frame failed.");
    check(sink.bytes.size() == golden.length && fnv(sink.bytes.data(), sink.bytes.size()) == golden.hash,
      "The native Matroska stream differs from the managed stream.");
    check(writer.duration_us() == golden.duration, "Duration ignored elapsed capture time.");
  }

  MemorySink sink;
  MatroskaWriter writer(sink, 12, 8, 60, 10'000'000);
  writer.write_header();
  std::vector<uint8_t> pixels(256, 0xCD);
  std::memset(pixels.data(), 128, 144);
  int64_t first = 8'000'000'000'000'000LL;
  writer.write_frame(pixels.data(), first);
  writer.write_frame(pixels.data(), first + 166'670);
  writer.write_frame(pixels.data(), first + 216'000'123'450LL);
  auto frames = parse_frames(sink.bytes);
  check(frames.size() == 3 && frames[0].timestamp == 0 && frames[1].timestamp == 16'667
      && frames[2].timestamp == 21'600'012'345,
    "Capture times were not kept.");
  for (const ParsedFrame &frame : frames)
    check(frame.pixels.size() == 144 && frame.pixels[0] == 128 && frame.pixels[143] == 128, "Pool capacity leaked.");
  check(writer.first_timestamp_ticks() == first, "Capture origin changed.");

  size_t length = sink.bytes.size();
  check(writer.write_frame(pixels.data(), first + 166'669) == MatroskaWriter::Result::Regressed
      && sink.bytes.size() == length,
    "Regressing capture time wrote a partial packet.");
}

void preview_matches_the_previous_conversion() {
  struct Golden {
    int32_t width, height, target_width, target_height;
    uint32_t hash;
  } cases[] = {
    {64, 48, 32, 24, 0x2BF90072u},
    {64, 48, 64, 48, 0x78DAA7CCu},
    {1920, 1080, 960, 540, 0xEE6BD22Eu},
    {1280, 720, 960, 540, 0xFFDC58EFu},
    {640, 480, 640, 480, 0x0EE27F21u},
    {100, 60, 37, 23, 0xEF9A3338u},
    {6, 6, 2, 2, 0x8B334B7Cu},
    {2, 2, 2, 2, 0x19018418u},
    {48, 64, 30, 40, 0x7119D825u},
  };
  for (const Golden &golden : cases) {
    std::vector<uint8_t> yuv(static_cast<size_t>(golden.width) * golden.height * 3 / 2);
    uint32_t state = 12345;
    for (uint8_t &value : yuv) {
      state = state * 1103515245u + 12345u;
      value = static_cast<uint8_t>(state >> 16);
    }
    PreviewConverter converter;
    check(converter.configure(golden.width, golden.height, golden.target_width, golden.target_height),
      "Preview converter rejected valid sizes.");
    std::vector<uint8_t> rgba(converter.target_bytes());
    converter.convert(yuv.data(), rgba.data());
    check(fnv(rgba.data(), rgba.size()) == golden.hash, "Native preview pixels differ from the managed conversion.");
  }
  // Top row black, bottom row white, neutral chroma. Unity textures are bottom-up.
  uint8_t yuv[] = {16, 16, 235, 235, 128, 128};
  uint8_t rgba[16];
  PreviewConverter converter;
  converter.configure(2, 2, 2, 2);
  converter.convert(yuv, rgba);
  check(rgba[0] == 255 && rgba[1] == 255 && rgba[2] == 255 && rgba[8] == 0 && rgba[11] == 255,
    "Camera preview color or orientation changed.");
  FrameSize fitted = fit_size(1920, 1080, 960, 720);
  check(fitted.width == 960 && fitted.height == 540, "Preview fitting changed the camera aspect.");
  fitted = fit_size(640, 480, 960, 720);
  check(fitted.width == 640 && fitted.height == 480, "Preview fitting upscaled a small camera.");
}

void log_lines_parse_like_the_managed_patterns() {
  auto parse = [](const char *line) { return parse_capture_log_line(line, std::strlen(line)); };
  CaptureLogLine config = parse("[Parsed_showinfo_4 @ 0] config in time_base: 1/10000000, frame_rate: 30000/1001");
  check(config.kind == CaptureLogLine::Kind::TimeBase && config.has_time_base && config.time_base_numerator == 1
      && config.time_base_denominator == 10'000'000 && config.has_input_rate && config.input_rate_numerator == 30000
      && config.input_rate_denominator == 1001,
    "The negotiated NTSC frame rate was not parsed.");
  check(parse("[Parsed_showinfo_4 @ 0] config in time_base: 1/10000000, frame_rate: 10/1").input_rate_numerator == 10,
    "A device-default 10 fps stream was not recognized.");
  for (const char *invalid : {"0/0", "0/1", "30/0", "-30/1", "30/-1", "NaN/1", "30/1x",
         "9999999999999999999999999999999999999999/1"}) {
    std::string line = std::string("[Parsed_showinfo_4 @ 0] config in time_base: 1/10000000, frame_rate: ") + invalid;
    check(!parse(line.c_str()).has_input_rate, "An invalid frame rate became a camera-speed warning.");
  }
  check(!parse("config out time_base: 1/1000000, frame_rate: 30/1").has_input_rate,
    "An output rate was mistaken for the camera's negotiated input rate.");
  CaptureLogLine frame = parse(
    "[Parsed_showinfo_4 @ 000001] n:  12 pts:1790000000370000 pts_time:1.79e+09 duration:333333 fmt:yuv420p "
    "cl:left sar:1/1 s:640x360 i:P iskey:1 type:I");
  check(frame.kind == CaptureLogLine::Kind::Frame && frame.pts == 1'790'000'000'370'000LL && frame.width == 640
      && frame.height == 360,
    "A showinfo frame line was not parsed.");
  check(parse("[Parsed_showinfo_4 @ 0] n:1 pts:-5 s:2x2").pts == -5, "A negative PTS was not parsed.");
  check(parse("[dshow @ 0] Could not run graph").kind == CaptureLogLine::Kind::Other, "A diagnostic was hidden.");
  check(parse("[Parsed_showinfo_4 @ 0] side data").kind == CaptureLogLine::Kind::ShowinfoOther,
    "A showinfo detail became a diagnostic.");
  check(parse("[x] n: pts:1 s:2x2").kind != CaptureLogLine::Kind::Frame, "A malformed frame line was accepted.");
}

void wall_clock_keeps_large_timestamp_precision() {
  const int64_t epoch_us = 1'790'000'000'000'000LL;
  for (int64_t frequency : {10'000'000LL, 1'000'000'000LL}) {
    int64_t ticks = 0;
    check(wall_clock_to_ticks(epoch_us * 10 + 370, 1, 10'000'000, epoch_us, 10 * frequency, frequency, &ticks),
      "A valid wall-clock timestamp was rejected.");
    int64_t expected = 10 * frequency + 37 * frequency / 1'000'000;
    check(std::llabs(ticks - expected) <= 1, "Large FFmpeg timestamps lost sub-frame precision.");
  }
  int64_t ticks = 0;
  check(!wall_clock_to_ticks(1, 0, 1, 0, 0, 1000, &ticks), "A zero time base was accepted.");
}

void warm_up_frame_precedes_the_run_by_one_frame() {
  MemorySink sink;
  MatroskaWriter writer(sink, 4, 2, 30, 1000);
  check(writer.write_header() == MatroskaWriter::Result::Ok, "The header was not written.");
  check(writer.write_warm_up_frame() == MatroskaWriter::Result::Ok, "The warm-up frame was not written.");
  check(writer.duration_us() == 0, "The warm-up frame counted as captured video.");
  std::vector<uint8_t> pixels(12, 200);
  check(writer.write_frame(pixels.data(), 5000) == MatroskaWriter::Result::Ok
      && writer.write_frame(pixels.data(), 5100) == MatroskaWriter::Result::Ok,
    "Captured frames after the warm-up were rejected.");
  check(writer.write_warm_up_frame() == MatroskaWriter::Result::Invalid, "A warm-up frame followed captured video.");
  check(writer.first_timestamp_ticks() == 5000 && writer.duration_us() == 100'000 + 33'333,
    "The warm-up frame changed the run's timing.");
  auto frames = parse_frames(sink.bytes);
  check(frames.size() == 3 && frames[0].timestamp == 0 && frames[1].timestamp == 33'333
      && frames[2].timestamp == 133'333,
    "Captured frames did not follow the warm-up frame by one frame.");
  check(frames[0].pixels[0] == 16 && frames[0].pixels[8] == 128 && frames[1].pixels[0] == 200,
    "The warm-up frame was not black.");
}

std::string temp_path(const char *name) {
  const char *root = std::getenv("TMPDIR");
  std::string path = std::string(root ? root : "/tmp") + "/" + name + std::to_string(getpid());
  return path;
}

void preview_target_uses_the_shared_layout() {
  std::string path = temp_path("tufreplay-preview-test-");
  const int32_t max_width = 4, max_height = 2;
  size_t size = PreviewTarget::kHeaderSize + max_width * max_height * 4;
  int file = open(path.c_str(), O_RDWR | O_CREAT | O_TRUNC, 0600);
  check(file >= 0 && ftruncate(file, static_cast<off_t>(size)) == 0, "Preview fixture could not be created.");
  close(file);
  PreviewTarget target;
  check(target.open(path.c_str(), max_width, max_height), "The preview mapping did not open.");
  check(target.should_publish(), "The first preview frame was not published for readiness.");
  std::vector<uint8_t> rgba(16, 0x5A);
  check(target.publish(rgba.data(), 2, 2), "A preview frame was rejected.");
  check(!target.should_publish(), "Previews kept publishing without demand.");
  check(!target.publish(rgba.data(), 5, 1), "A preview larger than the mapping was accepted.");
  target.close();
  std::vector<uint8_t> raw(size);
  file = open(path.c_str(), O_RDONLY);
  check(read(file, raw.data(), size) == static_cast<ssize_t>(size), "Preview fixture could not be read.");
  close(file);
  unlink(path.c_str());
  int64_t sequence;
  int32_t width, height;
  std::memcpy(&sequence, raw.data(), 8);
  std::memcpy(&width, raw.data() + 8, 4);
  std::memcpy(&height, raw.data() + 12, 4);
  check(sequence == 2 && width == 2 && height == 2 && raw[64] == 0x5A && raw[64 + 15] == 0x5A,
    "The preview mapping layout changed.");
}

// Drives the full relay through real pipes, like the FFmpeg capture process.
void relay_pairs_timestamps_with_frames_and_records_from_the_boundary() {
  int frames_pipe[2], log_pipe[2], output_pipe[2];
  check(pipe(frames_pipe) == 0 && pipe(log_pipe) == 0 && pipe(output_pipe) == 0, "Pipes were unavailable.");
  std::string preview_path = temp_path("tufreplay-relay-preview-");
  int file = open(preview_path.c_str(), O_RDWR | O_CREAT | O_TRUNC, 0600);
  check(file >= 0 && ftruncate(file, 64 + 960 * 720 * 4) == 0, "Preview mapping fixture failed.");
  close(file);

  const int64_t frequency = 1'000'000'000;
  const int64_t anchor_unix_us = 1'790'000'000'000'000LL;
  const int64_t anchor_ticks = 100 * frequency;
  tufreplay_camera_config config{};
  config.frame_handle = frames_pipe[0];
  config.log_handle = log_pipe[0];
  config.max_width = 64;
  config.max_height = 48;
  config.clock_unix_us = anchor_unix_us;
  config.clock_ticks = anchor_ticks;
  config.ticks_frequency = frequency;
  config.pool_capacity = 8;
  config.timestamp_timeout_ms = 5000;
  config.preview_path = preview_path.c_str();
  config.preview_max_width = 960;
  config.preview_max_height = 720;
  void *camera = tufreplay_camera_create(&config);
  check(camera != nullptr, "The relay did not start.");
  close(frames_pipe[0]);
  close(log_pipe[0]);

  const int32_t width = 16, height = 8;
  std::vector<uint8_t> pixels(width * height * 3 / 2);
  auto emit = [&](int64_t index, int64_t offset_us) {
    std::string line = "[Parsed_showinfo_4 @ 0] n:" + std::to_string(index)
      + " pts:" + std::to_string(anchor_unix_us + offset_us) + " s:16x8 i:P\n";
    check(write(log_pipe[1], line.data(), line.size()) == static_cast<ssize_t>(line.size()), "Log write failed.");
    std::fill(pixels.begin(), pixels.end(), static_cast<uint8_t>(index));
    check(write(frames_pipe[1], pixels.data(), pixels.size()) == static_cast<ssize_t>(pixels.size()),
      "Frame write failed.");
  };
  const char *config_line = "[Parsed_showinfo_4 @ 0] config in time_base: 1/1000000, frame_rate: 30/1\r\n";
  check(write(log_pipe[1], config_line, std::strlen(config_line)) > 0, "Config write failed.");
  const char *diagnostic = "[dshow @ 0] camera warming up\n";
  check(write(log_pipe[1], diagnostic, std::strlen(diagnostic)) > 0, "Diagnostic write failed.");
  emit(1, 0);
  check(tufreplay_camera_wait_ready(camera, 5000) == 1, "The relay never became ready.");
  tufreplay_camera_state state{};
  tufreplay_camera_get_state(camera, &state);
  check(state.width == 16 && state.height == 8 && state.input_rate_numerator == 30 && state.input_rate_denominator == 1,
    "The relay state lost the capture size or rate.");
  char diagnostics[256];
  int32_t length = tufreplay_camera_get_diagnostics(camera, diagnostics, sizeof(diagnostics));
  check(std::string(diagnostics, static_cast<size_t>(length)) == "[dshow @ 0] camera warming up",
    "FFmpeg diagnostics were lost.");

  auto attach = [&](int64_t boundary, int64_t now) {
    void *prepared = tufreplay_camera_recording_prepare(camera, 30, 3);
    check(prepared != nullptr, "The recording was not prepared.");
    check(tufreplay_camera_recording_begin(prepared, boundary, now) == 0, "The recording did not begin.");
    return prepared;
  };

  // A prepared recording opens its encoder with a warm-up frame before the run.
  void *recording = tufreplay_camera_recording_prepare(camera, 30, 3);
  check(recording != nullptr, "The recording was not prepared.");
  std::mutex written_gate;
  std::vector<uint8_t> written;
  std::thread reader([&] {
    uint8_t buffer[4096];
    for (;;) {
      ssize_t count = read(output_pipe[0], buffer, sizeof(buffer));
      if (count <= 0) break;
      std::lock_guard<std::mutex> lock(written_gate);
      written.insert(written.end(), buffer, buffer + count);
    }
  });
  check(tufreplay_camera_recording_set_output(recording, output_pipe[1]) == 0, "The encoder output was rejected.");
  close(output_pipe[1]);
  for (int attempt = 0; attempt < 1000; ++attempt) {
    {
      std::lock_guard<std::mutex> lock(written_gate);
      if (parse_frames(written).size() == 1) break;
    }
    std::this_thread::sleep_for(std::chrono::milliseconds(5));
  }
  {
    std::lock_guard<std::mutex> lock(written_gate);
    check(parse_frames(written).size() == 1, "The warm-up frame was not written before the run.");
  }
  // A run starting 50 ms after the latest frame reuses it with its real capture time.
  const int64_t boundary = anchor_ticks;
  check(tufreplay_camera_recording_begin(recording, boundary, anchor_ticks + frequency / 20) == 0,
    "The prepared recording did not begin.");
  check(tufreplay_camera_recording_begin(recording, boundary, anchor_ticks) != 0, "A recording began twice.");
  // Pace the fixture by the writer, so the bounded queue never replaces a frame.
  auto wait_written = [&](int64_t count) {
    for (int attempt = 0; attempt < 1000; ++attempt) {
      tufreplay_camera_recording_stats progress{};
      tufreplay_camera_recording_get_stats(recording, &progress);
      if (progress.written_frames >= count) return;
      std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    check(false, "The recording stopped consuming frames.");
  };
  wait_written(1);
  emit(2, 33'333);
  wait_written(2);
  emit(3, 66'667);
  wait_written(3);
  emit(4, 2'000'000);
  wait_written(4);
  tufreplay_camera_recording_stats stats{};
  check(tufreplay_camera_recording_finish(recording, 5000, &stats) == 1, "The writer did not finish.");
  reader.join();
  check(stats.written_frames == 4 && stats.first_timestamp_ticks == anchor_ticks && stats.write_failed == 0
      && stats.capture_failed == 0,
    "The recording lost frames or its first capture time.");
  check(stats.duration_us == 2'000'000 + 1'000'000 / 30, "The recording duration ignored the capture gap.");
  auto frames = parse_frames(written);
  check(frames.size() == 5 && frames[0].timestamp == 0 && frames[1].timestamp == 33'333
      && frames[2].timestamp == 66'666 && frames[3].timestamp == 100'000 && frames[4].timestamp == 2'033'333,
    "The encoder stream lost real capture times.");
  check(frames[0].pixels[0] == 16 && frames[1].pixels[0] == 1 && frames[4].pixels[0] == 4,
    "Frames were paired with the wrong timestamps.");
  tufreplay_camera_recording_destroy(recording, 1000);

  // A run that ends before its encoder starts still encodes the frames it queued.
  void *early = attach(anchor_ticks, anchor_ticks + frequency / 20);
  emit(6, 2'100'000);
  for (int attempt = 0; attempt < 1000; ++attempt) {
    tufreplay_camera_recording_stats queued{};
    tufreplay_camera_recording_get_stats(early, &queued);
    tufreplay_camera_state current{};
    tufreplay_camera_get_state(camera, &current);
    if (current.frames_captured >= 5) break;
    std::this_thread::sleep_for(std::chrono::milliseconds(5));
  }
  tufreplay_camera_recording_seal(early);
  int early_pipe[2];
  check(pipe(early_pipe) == 0, "Pipe was unavailable.");
  std::vector<uint8_t> early_written;
  std::thread early_reader([&] {
    uint8_t buffer[4096];
    for (;;) {
      ssize_t count = read(early_pipe[0], buffer, sizeof(buffer));
      if (count <= 0) break;
      early_written.insert(early_written.end(), buffer, buffer + count);
    }
  });
  check(tufreplay_camera_recording_set_output(early, early_pipe[1]) == 0, "A sealed run rejected its encoder.");
  close(early_pipe[1]);
  tufreplay_camera_recording_stats early_stats{};
  check(tufreplay_camera_recording_finish(early, 5000, &early_stats) == 1, "The sealed writer did not finish.");
  early_reader.join();
  close(early_pipe[0]);
  check(early_stats.written_frames == 1 && parse_frames(early_written).size() == 2,
    "Frames queued before the encoder started were lost.");
  tufreplay_camera_recording_destroy(early, 1000);

  // A later run excludes frames from before its boundary.
  void *late = attach(anchor_ticks + 3 * frequency, anchor_ticks + 3 * frequency);
  emit(5, 2'500'000);
  for (int attempt = 0; attempt < 1000; ++attempt) {
    tufreplay_camera_state current{};
    tufreplay_camera_get_state(camera, &current);
    if (current.frames_captured >= 6) break;
    std::this_thread::sleep_for(std::chrono::milliseconds(5));
  }
  tufreplay_camera_recording_stats late_stats{};
  check(tufreplay_camera_recording_finish(late, 1000, &late_stats) == 1 && late_stats.written_frames == 0,
    "A frame from before the run boundary was recorded.");
  tufreplay_camera_recording_destroy(late, 1000);

  // A prepared recording released before its run never receives frames.
  void *unused = tufreplay_camera_recording_prepare(camera, 30, 3);
  tufreplay_camera_recording_seal(unused);
  check(tufreplay_camera_recording_begin(unused, anchor_ticks, anchor_ticks) != 0, "A sealed recording began.");
  tufreplay_camera_recording_destroy(unused, 1000);

  tufreplay_camera_get_state(camera, &state);
  check(state.previews_published >= 1, "The relay never published a preview frame.");
  close(frames_pipe[1]);
  close(log_pipe[1]);
  check(tufreplay_camera_destroy(camera, 5000) == 1, "The relay readers did not end with the process.");
  close(output_pipe[0]);
  unlink(preview_path.c_str());
}

void failed_output_reports_an_encoder_failure() {
  MemorySink sink;
  sink.fail_after = 0;
  MatroskaWriter writer(sink, 2, 2, 30, 1000);
  check(writer.write_header() == MatroskaWriter::Result::WriteFailed, "A closed encoder accepted the header.");
}
} // namespace

int main() {
  frames_return_only_after_every_consumer_releases();
  overload_replaces_stale_frames_without_waiting();
  completing_queue_wakes_consumer_and_rejects_frames();
  matroska_matches_the_previous_stream_byte_for_byte();
  preview_matches_the_previous_conversion();
  log_lines_parse_like_the_managed_patterns();
  wall_clock_keeps_large_timestamp_precision();
  warm_up_frame_precedes_the_run_by_one_frame();
  preview_target_uses_the_shared_layout();
  relay_pairs_timestamps_with_frames_and_records_from_the_boundary();
  failed_output_reports_an_encoder_failure();
  std::puts("PASS Windows native camera relay core");
  return 0;
}
