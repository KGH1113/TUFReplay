"""Losslessly wrap the inspected ADOFAI CJK font in WOFF, entirely locally.

The generated font is checked in. Ordinary builds and imports need neither
AssetRipper nor a sibling checkout. No glyph subsetting or metric changes occur.
"""

import argparse
import hashlib
from pathlib import Path
import struct
import zlib


SOURCE_SHA256 = "f4890beb5284b31b92e5f3e408cccc51b59971d8431c19e66ffda1589002fec2"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="Local cjkFonts-regular-normalized font exported by AssetRipper")
    args = parser.parse_args()
    source = args.source.read_bytes()
    if hashlib.sha256(source).hexdigest() != SOURCE_SHA256:
        parser.error("The source differs from the inspected game CJK font; review its provenance before updating the pinned hash.")
    count = struct.unpack_from(">H", source, 4)[0]
    offset = 44 + 20 * count
    sfnt_size = 12 + 16 * count
    directory, payload, original_tables = [], [], {}
    for index in range(count):
        tag, checksum, start, length = struct.unpack_from(">4sIII", source, 12 + 16 * index)
        raw = source[start:start + length]
        compressed = zlib.compress(raw, 9)
        data = compressed if len(compressed) < length else raw
        directory.append(struct.pack(">4sIIII", tag, offset, len(data), length, checksum))
        padded = data + b"\0" * (-len(data) % 4)
        payload.append(padded)
        offset += len(padded)
        sfnt_size += (length + 3) & ~3
        original_tables[tag] = raw
    header = struct.pack(">4s4sIHHIHHIIIII", b"wOFF", source[:4], offset, count, 0, sfnt_size, 1, 0, 0, 0, 0, 0, 0)
    woff = header + b"".join(directory) + b"".join(payload)
    assert len(woff) == offset
    # Verify every table after decompression, including outlines, cmap and metrics.
    for index in range(count):
        tag, start, compressed_size, raw_size, _ = struct.unpack_from(">4sIIII", woff, 44 + 20 * index)
        table = woff[start:start + compressed_size]
        if compressed_size < raw_size:
            table = zlib.decompress(table)
        assert table == original_tables[tag], tag
    output = Path(__file__).resolve().parents[3] / "TUFReplay/Visual/Assets/Fonts/adofai-cjk.woff"
    output.write_bytes(woff)
    print(f"{output}: {len(woff)} bytes, {count} original tables verified, SHA-256 {hashlib.sha256(woff).hexdigest()}")


if __name__ == "__main__":
    main()
