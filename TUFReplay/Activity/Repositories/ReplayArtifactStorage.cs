using System;
using System.IO;
using System.IO.Compression;

namespace TUFReplay.Activity.Repositories;

// Stores replay CSV blobs gzip-compressed. The replay payload format itself is
// unchanged: a CSV row starts with a digit or '-', never the gzip magic 1F 8B, so
// rows written before compression keep loading as-is without a schema migration.
internal static class ReplayArtifactStorage
{
  private const byte GzipMagic0 = 0x1f;
  private const byte GzipMagic1 = 0x8b;

  public static byte[] Encode(byte[] csv)
  {
    if (csv == null || csv.Length == 0)
      return csv ?? Array.Empty<byte>();
    try
    {
      using var compressed = new MemoryStream(csv.Length / 4 + 64);
      using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        gzip.Write(csv, 0, csv.Length);
      // Tiny payloads can grow; keep whichever form is smaller.
      return compressed.Length < csv.Length ? compressed.ToArray() : csv;
    }
    // A missing native zlib must never lose the run; raw CSV stays readable.
    catch (Exception exception)
    {
      Main.Instance?.Log("[Activity] Replay payload compression unavailable; storing raw CSV. " + exception.Message);
      return csv;
    }
  }

  public static byte[] Decode(byte[] stored)
  {
    if (!IsCompressed(stored))
      return stored;
    using var input = new MemoryStream(stored, writable: false);
    using var gzip = new GZipStream(input, CompressionMode.Decompress);
    using var output = new MemoryStream(stored.Length * 6);
    gzip.CopyTo(output, 64 * 1024);
    return output.ToArray();
  }

  public static bool IsCompressed(byte[] stored) =>
    stored != null && stored.Length >= 2 && stored[0] == GzipMagic0 && stored[1] == GzipMagic1;
}
