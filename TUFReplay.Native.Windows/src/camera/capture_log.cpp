#include "capture_log.hpp"

#include <cmath>
#include <cstring>
#include <limits>

namespace tufreplay::camera {

namespace {
struct Text {
  const char *data;
  size_t length;
};

bool is_word(char c) {
  return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_';
}
bool is_space(char c) { return c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\f' || c == '\v'; }
bool is_digit(char c) { return c >= '0' && c <= '9'; }

bool contains(Text text, const char *token) {
  size_t length = std::strlen(token);
  if (length > text.length) return false;
  for (size_t i = 0; i + length <= text.length; ++i)
    if (std::memcmp(text.data + i, token, length) == 0) return true;
  return false;
}

// Matches \d+ at position. A structural match with an out-of-range value still
// matches (overflow = true), like a regex followed by a failing TryParse.
bool digit_run(Text text, size_t &position, int64_t *value, bool *overflow) {
  size_t start = position;
  uint64_t result = 0;
  bool too_large = false;
  while (position < text.length && is_digit(text.data[position])) {
    uint64_t digit = static_cast<uint64_t>(text.data[position] - '0');
    if (!too_large && result > (static_cast<uint64_t>(std::numeric_limits<int64_t>::max()) - digit) / 10)
      too_large = true;
    if (!too_large) result = result * 10 + digit;
    ++position;
  }
  if (position == start) return false;
  *value = too_large ? 0 : static_cast<int64_t>(result);
  *overflow = too_large;
  return true;
}

void skip_spaces(Text text, size_t &position) {
  while (position < text.length && is_space(text.data[position])) ++position;
}

bool literal(Text text, size_t &position, const char *token) {
  size_t length = std::strlen(token);
  if (position + length > text.length || std::memcmp(text.data + position, token, length) != 0) return false;
  position += length;
  return true;
}

// Finds the first position matching \b<token>... where try_match succeeds.
template <typename Match>
bool find_word(Text text, const char *token, Match try_match) {
  size_t length = std::strlen(token);
  for (size_t i = 0; i + length <= text.length; ++i) {
    if (std::memcmp(text.data + i, token, length) != 0) continue;
    if (i > 0 && is_word(text.data[i - 1])) continue;
    size_t position = i + length;
    if (try_match(position)) return true;
  }
  return false;
}

bool parse_time_base(Text text, CaptureLogLine &out) {
  const char *token = "config in time_base: ";
  size_t length = std::strlen(token);
  for (size_t i = 0; i + length <= text.length; ++i) {
    if (std::memcmp(text.data + i, token, length) != 0) continue;
    size_t position = i + length;
    int64_t numerator = 0, denominator = 0;
    bool numerator_overflow = false, denominator_overflow = false;
    if (!digit_run(text, position, &numerator, &numerator_overflow) || !literal(text, position, "/")
      || !digit_run(text, position, &denominator, &denominator_overflow))
      continue;
    // int.TryParse in managed code turns an out-of-range part into zero.
    const int64_t limit = std::numeric_limits<int32_t>::max();
    out.time_base_numerator = numerator_overflow || numerator > limit ? 0 : numerator;
    out.time_base_denominator = denominator_overflow || denominator > limit ? 0 : denominator;
    out.has_time_base = true;
    return true;
  }
  return false;
}

void parse_input_rate(Text text, CaptureLogLine &out) {
  // The first structural match decides, like Regex.Match; invalid values give no rate.
  find_word(text, "frame_rate:", [&](size_t position) {
    skip_spaces(text, position);
    int64_t numerator = 0, denominator = 0;
    bool numerator_overflow = false, denominator_overflow = false;
    if (!digit_run(text, position, &numerator, &numerator_overflow) || !literal(text, position, "/")
      || !digit_run(text, position, &denominator, &denominator_overflow))
      return false;
    if (position < text.length && !is_space(text.data[position])) return false;
    if (numerator_overflow || denominator_overflow || numerator <= 0 || denominator <= 0) return true;
    out.has_input_rate = true;
    out.input_rate_numerator = numerator;
    out.input_rate_denominator = denominator;
    return true;
  });
}

bool parse_frame(Text text, CaptureLogLine &out) {
  bool pts_valid = false;
  bool has_pts = find_word(text, "n:", [&](size_t position) {
    skip_spaces(text, position);
    int64_t index = 0;
    bool index_overflow = false;
    if (!digit_run(text, position, &index, &index_overflow)) return false;
    size_t spaces = position;
    skip_spaces(text, position);
    if (position == spaces || !literal(text, position, "pts:")) return false;
    skip_spaces(text, position);
    bool negative = position < text.length && text.data[position] == '-';
    if (negative) ++position;
    int64_t pts = 0;
    bool pts_overflow = false;
    if (!digit_run(text, position, &pts, &pts_overflow)) return false;
    pts_valid = !pts_overflow;
    out.pts = negative ? -pts : pts;
    return true;
  });
  bool has_size = find_word(text, "s:", [&](size_t position) {
    int64_t width = 0, height = 0;
    bool width_overflow = false, height_overflow = false;
    if (!digit_run(text, position, &width, &width_overflow) || !literal(text, position, "x")
      || !digit_run(text, position, &height, &height_overflow))
      return false;
    const int64_t limit = std::numeric_limits<int32_t>::max();
    out.frame_size_overflow = width_overflow || height_overflow || width > limit || height > limit;
    out.width = width;
    out.height = height;
    return true;
  });
  // A PTS outside long.TryParse's range drops the line, as in managed code.
  return has_pts && has_size && pts_valid;
}

// Rounds n / d to the nearest integer, midpoints to even, like decimal Math.Round.
bool divide_round_even(__int128 n, __int128 d, __int128 *result) {
  if (d == 0) return false;
  if (d < 0) {
    n = -n;
    d = -d;
  }
  __int128 q = n / d;
  __int128 r = n % d;
  if (r < 0) {
    r += d;
    q -= 1;
  }
  __int128 twice = r * 2;
  if (twice > d || (twice == d && (q & 1) != 0)) q += 1;
  *result = q;
  return true;
}
} // namespace

CaptureLogLine parse_capture_log_line(const char *line, size_t length) {
  CaptureLogLine out;
  if (line == nullptr) return out;
  Text text{line, length};
  if (contains(text, "config in time_base:")) {
    // The input-rate check only reads this config line, never "config out".
    out.kind = CaptureLogLine::Kind::TimeBase;
    parse_time_base(text, out);
    parse_input_rate(text, out);
    return out;
  }
  if (!contains(text, " n:")) {
    out.kind = contains(text, "showinfo") ? CaptureLogLine::Kind::ShowinfoOther : CaptureLogLine::Kind::Other;
    return out;
  }
  out.kind = parse_frame(text, out) ? CaptureLogLine::Kind::Frame : CaptureLogLine::Kind::ShowinfoOther;
  return out;
}

bool wall_clock_to_ticks(int64_t pts, int64_t numerator, int64_t denominator, int64_t anchor_unix_us,
  int64_t anchor_ticks, int64_t frequency, int64_t *ticks) {
  if (numerator <= 0 || denominator <= 0 || frequency <= 0 || ticks == nullptr) return false;
  __int128 unix_us = 0;
  if (!divide_round_even(static_cast<__int128>(pts) * numerator * 1'000'000, denominator, &unix_us)) return false;
  if (unix_us > std::numeric_limits<int64_t>::max() || unix_us < std::numeric_limits<int64_t>::min()) return false;
  // Subtract the epoch before converting to floating point to retain precision.
  __int128 delta_us = unix_us - anchor_unix_us;
  if (delta_us > std::numeric_limits<int64_t>::max() || delta_us < std::numeric_limits<int64_t>::min()) return false;
  double offset = std::nearbyint(static_cast<double>(static_cast<int64_t>(delta_us)) * static_cast<double>(frequency)
    / 1'000'000.0);
  if (!(offset > -9.2e18 && offset < 9.2e18)) return false;
  __int128 result = static_cast<__int128>(anchor_ticks) + static_cast<int64_t>(offset);
  if (result > std::numeric_limits<int64_t>::max() || result < std::numeric_limits<int64_t>::min()) return false;
  *ticks = static_cast<int64_t>(result);
  return true;
}

} // namespace tufreplay::camera
