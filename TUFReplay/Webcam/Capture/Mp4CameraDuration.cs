using System;
using System.IO;

namespace TUFReplay.Webcam.Capture;

// Encoder progress can include packets omitted by the muxer at -fs. Read the
// finalized video track instead, seeking past mdat without loading media bytes.
internal static class Mp4CameraDuration
{
  public static long ReadUs(string path)
  {
    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    return new Reader(stream).ReadUs();
  }

  private sealed class Reader
  {
    private const uint Moov = 0x6D6F6F76;
    private const uint Trak = 0x7472616B;
    private const uint Mdia = 0x6D646961;
    private const uint Mdhd = 0x6D646864;
    private const uint Hdlr = 0x68646C72;
    private const uint Vide = 0x76696465;
    private readonly Stream _stream;
    private readonly byte[] _bytes = new byte[8];
    private int _atomsRemaining = 10000;

    public Reader(Stream stream) => _stream = stream;

    public long ReadUs()
    {
      Atom? movie = null;
      for (long position = 0; position < _stream.Length; )
      {
        Atom atom = ReadAtom(position, _stream.Length);
        if (atom.Type == Moov)
        {
          if (movie.HasValue)
            throw Invalid("The camera MP4 contains duplicate movie headers.");
          movie = atom;
        }
        position = atom.End;
      }
      if (!movie.HasValue)
        throw Invalid("The camera MP4 has no finalized movie header.");
      long? duration = null;
      Atom moov = movie.Value;
      for (long position = moov.Payload; position < moov.End; )
      {
        Atom atom = ReadAtom(position, moov.End);
        if (atom.Type == Trak)
        {
          long? trackDuration = ReadTrack(atom);
          if (trackDuration.HasValue)
          {
            if (duration.HasValue)
              throw Invalid("The camera MP4 contains more than one video track.");
            duration = trackDuration;
          }
        }
        position = atom.End;
      }
      return duration ?? throw Invalid("The camera MP4 has no video duration.");
    }

    private long? ReadTrack(Atom track)
    {
      Atom? media = null;
      for (long position = track.Payload; position < track.End; )
      {
        Atom atom = ReadAtom(position, track.End);
        if (atom.Type == Mdia)
        {
          if (media.HasValue)
            throw Invalid("The camera MP4 track contains duplicate media headers.");
          media = atom;
        }
        position = atom.End;
      }
      if (!media.HasValue)
        return null;
      Atom mdia = media.Value;
      Atom? header = null;
      uint handler = 0;
      bool hasHandler = false;
      for (long position = mdia.Payload; position < mdia.End; )
      {
        Atom atom = ReadAtom(position, mdia.End);
        if (atom.Type == Mdhd)
        {
          if (header.HasValue)
            throw Invalid("The camera MP4 track contains duplicate duration headers.");
          header = atom;
        }
        else if (atom.Type == Hdlr)
        {
          if (hasHandler || atom.End - atom.Payload < 12)
            throw Invalid("The camera MP4 track handler is invalid.");
          handler = ReadUInt32(atom.Payload + 8);
          hasHandler = true;
        }
        position = atom.End;
      }
      if (handler != Vide)
        return null;
      if (!header.HasValue)
        throw Invalid("The camera MP4 video track has no duration header.");
      return ReadDuration(header.Value);
    }

    private long ReadDuration(Atom header)
    {
      long length = header.End - header.Payload;
      if (length < 4)
        throw Invalid("The camera MP4 duration header is truncated.");
      uint version = ReadUInt32(header.Payload) >> 24;
      uint scale;
      ulong duration;
      if (version == 0 && length >= 24)
      {
        scale = ReadUInt32(header.Payload + 12);
        duration = ReadUInt32(header.Payload + 16);
        if (duration == uint.MaxValue)
          throw Invalid("The camera MP4 video duration is unknown.");
      }
      else if (version == 1 && length >= 36)
      {
        scale = ReadUInt32(header.Payload + 20);
        duration = ReadUInt64(header.Payload + 24);
        if (duration == ulong.MaxValue)
          throw Invalid("The camera MP4 video duration is unknown.");
      }
      else
        throw Invalid("The camera MP4 duration header version or size is invalid.");
      if (scale == 0 || duration == 0)
        throw Invalid("The camera MP4 video has an invalid time scale or duration.");
      try
      {
        // The duration can use all 64 bits. Its remainder is smaller than a
        // 32-bit time scale, so split before multiplying to avoid overflow.
        ulong microseconds = checked(duration / scale * 1_000_000 + duration % scale * 1_000_000 / scale);
        return checked((long)microseconds);
      }
      catch (OverflowException)
      {
        throw Invalid("The camera MP4 video duration is too large.");
      }
    }

    private Atom ReadAtom(long position, long end)
    {
      if (--_atomsRemaining < 0 || position < 0 || end - position < 8)
        throw Invalid("The camera MP4 atom structure is invalid.");
      ulong size = ReadUInt32(position);
      uint type = ReadUInt32(position + 4);
      int headerSize = 8;
      if (size == 1)
      {
        if (end - position < 16)
          throw Invalid("The camera MP4 extended atom header is truncated.");
        size = ReadUInt64(position + 8);
        headerSize = 16;
      }
      else if (size == 0)
        size = (ulong)(end - position);
      if (size < (ulong)headerSize || size > (ulong)(end - position))
        throw Invalid("The camera MP4 atom extends beyond its container.");
      return new Atom(position + headerSize, position + (long)size, type);
    }

    private uint ReadUInt32(long position)
    {
      Read(position, 4);
      return (uint)_bytes[0] << 24 | (uint)_bytes[1] << 16 | (uint)_bytes[2] << 8 | _bytes[3];
    }

    private ulong ReadUInt64(long position)
    {
      Read(position, 8);
      ulong value = 0;
      for (int index = 0; index < 8; index++)
        value = value << 8 | _bytes[index];
      return value;
    }

    private void Read(long position, int count)
    {
      _stream.Position = position;
      int read = 0;
      while (read < count)
      {
        int bytes = _stream.Read(_bytes, read, count - read);
        if (bytes == 0)
          throw Invalid("The camera MP4 ended before its header was complete.");
        read += bytes;
      }
    }

    private static InvalidDataException Invalid(string message) => new InvalidDataException(message);

    private readonly struct Atom
    {
      public Atom(long payload, long end, uint type)
      {
        Payload = payload;
        End = end;
        Type = type;
      }

      public long Payload { get; }
      public long End { get; }
      public uint Type { get; }
    }
  }
}
