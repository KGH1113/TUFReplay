using System;
using System.Collections.Generic;
using System.IO;

namespace TUFReplay.Activity.Charts;

// Reuses a level file's gameplay hash while its size and write time stay the same.
// Verifying a chart builds ADOFAI's whole level object graph (many times the file
// size in managed garbage), and Boehm GC pauses delay the game's async-input thread.
// Only the 32-byte hash is kept; chart text is read again for every request.
internal sealed class LevelFileVerificationCache
{
  internal delegate bool Verifier(string path, out string source, out byte[] hash);

  private readonly object _gate = new object();
  private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
  private readonly Verifier _verify;
  private readonly Func<string, string> _readText;
  private readonly int _capacity;

  public LevelFileVerificationCache(Verifier verify, Func<string, string> readText, int capacity)
  {
    _verify = verify ?? throw new ArgumentNullException(nameof(verify));
    _readText = readText ?? throw new ArgumentNullException(nameof(readText));
    _capacity = Math.Max(1, capacity);
  }

  public bool TryRead(string path, out string source, out byte[] hash)
  {
    source = null;
    hash = null;
    if (string.IsNullOrEmpty(path) || !TryGetStamp(path, out FileStamp before))
    {
      Forget(path);
      return false;
    }

    if (TryGetCachedHash(path, before, out byte[] cachedHash) && TryReadUnchanged(path, before, out source))
    {
      hash = (byte[])cachedHash.Clone();
      return true;
    }

    if (!_verify(path, out source, out hash) || hash == null)
    {
      Forget(path);
      return false;
    }

    // Cache only when the verified text was read from the stamp observed before it.
    if (TryGetStamp(path, out FileStamp after) && after.Equals(before))
      Store(path, before, hash);
    else
      Forget(path);
    return true;
  }

  private bool TryReadUnchanged(string path, FileStamp expected, out string source)
  {
    source = null;
    try
    {
      string text = _readText(path);
      if (text == null || !TryGetStamp(path, out FileStamp after) || !after.Equals(expected))
        return false;
      source = text;
      return true;
    }
    catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
    {
      return false;
    }
  }

  private bool TryGetCachedHash(string path, FileStamp stamp, out byte[] hash)
  {
    lock (_gate)
    {
      if (_entries.TryGetValue(path, out Entry entry) && entry.Stamp.Equals(stamp))
      {
        hash = entry.Hash;
        return true;
      }
    }
    hash = null;
    return false;
  }

  private void Store(string path, FileStamp stamp, byte[] hash)
  {
    lock (_gate)
    {
      if (_entries.Count >= _capacity && !_entries.ContainsKey(path))
        _entries.Clear();
      _entries[path] = new Entry(stamp, (byte[])hash.Clone());
    }
  }

  private void Forget(string path)
  {
    if (string.IsNullOrEmpty(path))
      return;
    lock (_gate)
      _entries.Remove(path);
  }

  private static bool TryGetStamp(string path, out FileStamp stamp)
  {
    stamp = default;
    try
    {
      var file = new FileInfo(path);
      if (!file.Exists)
        return false;
      stamp = new FileStamp(file.Length, file.LastWriteTimeUtc.Ticks);
      return true;
    }
    catch (Exception exception)
      when (exception is IOException
        || exception is UnauthorizedAccessException
        || exception is ArgumentException
        || exception is NotSupportedException
        || exception is System.Security.SecurityException
      )
    {
      return false;
    }
  }

  private readonly struct FileStamp : IEquatable<FileStamp>
  {
    public readonly long Length;
    public readonly long LastWriteUtcTicks;

    public FileStamp(long length, long lastWriteUtcTicks)
    {
      Length = length;
      LastWriteUtcTicks = lastWriteUtcTicks;
    }

    public bool Equals(FileStamp other) => Length == other.Length && LastWriteUtcTicks == other.LastWriteUtcTicks;

    public override bool Equals(object obj) => obj is FileStamp other && Equals(other);

    public override int GetHashCode() => unchecked((Length.GetHashCode() * 397) ^ LastWriteUtcTicks.GetHashCode());
  }

  private readonly struct Entry
  {
    public readonly FileStamp Stamp;
    public readonly byte[] Hash;

    public Entry(FileStamp stamp, byte[] hash)
    {
      Stamp = stamp;
      Hash = hash;
    }
  }
}
