#include "matroska_writer.hpp"

#include <algorithm>
#include <cstring>
#include <limits>

namespace tufreplay::camera {

namespace {
using Bytes = std::vector<uint8_t>;

int unsigned_size(uint64_t value) {
  int count = 1;
  while (count < 8 && (value >> (count * 8)) != 0) ++count;
  return count;
}

// All-one payloads mean unknown length, so reserve them at each size.
int vint_size(uint64_t value) {
  int count = 1;
  while (count < 8 && value >= (1ull << (count * 7)) - 1) ++count;
  return count;
}

void write_unsigned(uint8_t *target, int &offset, uint64_t value, int count) {
  for (int index = count - 1; index >= 0; --index) target[offset++] = static_cast<uint8_t>(value >> (index * 8));
}

void write_vint(uint8_t *target, int &offset, uint64_t value) {
  int count = vint_size(value);
  write_unsigned(target, offset, value | (1ull << (count * 7)), count);
}

void write_id(uint8_t *target, int &offset, uint32_t id) { write_unsigned(target, offset, id, unsigned_size(id)); }

void append_element(Bytes &out, uint32_t id, const Bytes &content) {
  uint8_t header[12];
  int offset = 0;
  write_id(header, offset, id);
  write_vint(header, offset, content.size());
  out.insert(out.end(), header, header + offset);
  out.insert(out.end(), content.begin(), content.end());
}

Bytes element(uint32_t id, const Bytes &content) {
  Bytes out;
  append_element(out, id, content);
  return out;
}

Bytes uint_element(uint32_t id, uint64_t value) {
  Bytes content(static_cast<size_t>(unsigned_size(value)));
  int offset = 0;
  write_unsigned(content.data(), offset, value, static_cast<int>(content.size()));
  return element(id, content);
}

Bytes string_element(uint32_t id, const char *value) {
  return element(id, Bytes(value, value + std::strlen(value)));
}

Bytes concat(std::initializer_list<Bytes> parts) {
  Bytes out;
  for (const Bytes &part : parts) out.insert(out.end(), part.begin(), part.end());
  return out;
}

void write_le32(uint8_t *target, int offset, int32_t value) {
  for (int index = 0; index < 4; ++index) target[offset + index] = static_cast<uint8_t>(value >> (index * 8));
}
} // namespace

MatroskaWriter::MatroskaWriter(
  ByteSink &sink, int32_t width, int32_t height, int32_t frame_rate, int64_t ticks_frequency)
    : sink_(sink), width_(width), height_(height), frame_rate_(frame_rate), frequency_(ticks_frequency) {
  valid_ = width >= 2 && height >= 2 && (width & 1) == 0 && (height & 1) == 0 && frame_rate >= 1 && frame_rate <= 1000
    && ticks_frequency >= 1;
  if (!valid_) return;
  frame_bytes_ = static_cast<size_t>(width) * static_cast<size_t>(height) * 3 / 2;
  frame_duration_us_ = 1'000'000 / frame_rate > 1 ? 1'000'000 / frame_rate : 1;
}

MatroskaWriter::Result MatroskaWriter::write_header() {
  if (!valid_) return Result::Invalid;
  Bytes out;
  append_element(out, 0x1A45DFA3,
    concat({uint_element(0x4286, 1), uint_element(0x42F7, 1), uint_element(0x42F2, 4), uint_element(0x42F3, 8),
      string_element(0x4282, "matroska"), uint_element(0x4287, 4), uint_element(0x4285, 2)}));
  // A live pipe cannot seek back to fill the segment length.
  const uint8_t segment[] = {0x18, 0x53, 0x80, 0x67, 0x01, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF};
  out.insert(out.end(), segment, segment + sizeof(segment));
  append_element(out, 0x1549A966,
    concat({uint_element(0x2AD7B1, 1000), string_element(0x4D80, "TUFReplay"), string_element(0x5741, "TUFReplay")}));
  Bytes bitmap(40, 0);
  write_le32(bitmap.data(), 0, 40);
  write_le32(bitmap.data(), 4, width_);
  write_le32(bitmap.data(), 8, height_);
  bitmap[12] = 1;  // biPlanes.
  bitmap[14] = 12; // I420 bits per pixel.
  bitmap[16] = 'I';
  bitmap[17] = '4';
  bitmap[18] = '2';
  bitmap[19] = '0';
  write_le32(bitmap.data(), 20, static_cast<int32_t>(frame_bytes_));
  append_element(out, 0x1654AE6B,
    element(0xAE,
      concat({uint_element(0xD7, 1), uint_element(0x73C5, 1), uint_element(0x83, 1), uint_element(0x9C, 0),
        string_element(0x86, "V_MS/VFW/FOURCC"), element(0x63A2, bitmap),
        uint_element(0x23E383, static_cast<uint64_t>(1'000'000'000LL / frame_rate_)),
        element(0xE0,
          concat({uint_element(0xB0, static_cast<uint64_t>(width_)),
            uint_element(0xBA, static_cast<uint64_t>(height_))}))})));
  return sink_.write(out.data(), out.size()) ? Result::Ok : Result::WriteFailed;
}

MatroskaWriter::Result MatroskaWriter::write_frame(const uint8_t *pixels, int64_t timestamp_ticks) {
  if (!valid_ || pixels == nullptr) return Result::Invalid;
  if (has_frame_ && timestamp_ticks < last_ticks_) return Result::Regressed;
  const int64_t first = has_frame_ ? first_ticks_ : timestamp_ticks;
  const int64_t elapsed = timestamp_ticks - first;
  // Splitting whole seconds avoids overflowing when recording for many hours.
  const int64_t timestamp_us = elapsed / frequency_ * 1'000'000
    + static_cast<int64_t>(static_cast<double>(elapsed % frequency_) * (1'000'000.0 / static_cast<double>(frequency_)));
  Result result = write_block(pixels, timestamp_us + offset_us_);
  if (result != Result::Ok) return result;
  first_ticks_ = first;
  last_ticks_ = timestamp_ticks;
  last_us_ = timestamp_us;
  has_frame_ = true;
  return Result::Ok;
}

MatroskaWriter::Result MatroskaWriter::write_warm_up_frame() {
  if (!valid_ || has_frame_ || offset_us_ != 0) return Result::Invalid;
  std::vector<uint8_t> black(frame_bytes_, 128);
  std::fill(black.begin(), black.begin() + static_cast<size_t>(width_) * static_cast<size_t>(height_), 16);
  Result result = write_block(black.data(), 0);
  if (result == Result::Ok) offset_us_ = frame_duration_us_;
  return result;
}

MatroskaWriter::Result MatroskaWriter::write_block(const uint8_t *pixels, int64_t timecode_us) {
  // One known-size cluster per frame keeps the block's signed relative timestamp
  // zero, including after a long scheduling or camera stall.
  int offset = 0;
  write_id(packet_header_, offset, 0x1F43B675);
  const int time_bytes = unsigned_size(static_cast<uint64_t>(timecode_us));
  const uint64_t block_bytes = frame_bytes_ + 4;
  const uint64_t cluster_bytes =
    1 + 1 + static_cast<uint64_t>(time_bytes) + 1 + static_cast<uint64_t>(vint_size(block_bytes)) + block_bytes;
  write_vint(packet_header_, offset, cluster_bytes);
  write_id(packet_header_, offset, 0xE7);
  write_vint(packet_header_, offset, static_cast<uint64_t>(time_bytes));
  write_unsigned(packet_header_, offset, static_cast<uint64_t>(timecode_us), time_bytes);
  write_id(packet_header_, offset, 0xA3);
  write_vint(packet_header_, offset, block_bytes);
  packet_header_[offset++] = 0x81; // Track 1.
  packet_header_[offset++] = 0;
  packet_header_[offset++] = 0;
  packet_header_[offset++] = 0x80; // Keyframe, no lacing.
  if (!sink_.write(packet_header_, static_cast<size_t>(offset)) || !sink_.write(pixels, frame_bytes_))
    return Result::WriteFailed;
  return Result::Ok;
}

} // namespace tufreplay::camera
