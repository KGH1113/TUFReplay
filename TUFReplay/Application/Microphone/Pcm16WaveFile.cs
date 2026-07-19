using System;
using System.IO;
using System.Text;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Application.Microphone;

public sealed class Pcm16WaveInfo
{
  public long DataOffset;
  public long DataLength;
  public int SampleRate;
  public int Channels;
  public long FrameCount;
}

public static class Pcm16WaveFile
{
  public static Pcm16WaveInfo ReadAndValidate(StoredMicrophoneRecording recording)
  {
    if (recording == null)
      throw new ArgumentNullException(nameof(recording));
    if (!string.Equals(recording.Format, "wav/pcm16", StringComparison.OrdinalIgnoreCase))
      throw new InvalidDataException("The microphone recording format is not PCM16 WAV.");

    using var stream = new FileStream(recording.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
    if (recording.ByteLength != stream.Length)
      throw new InvalidDataException("The microphone WAV length does not match its database metadata.");
    if (stream.Length < 12)
      throw new InvalidDataException("The microphone WAV header is truncated.");

    using var reader = new BinaryReader(stream, Encoding.ASCII, true);
    if (ReadFourCc(reader) != "RIFF")
      throw new InvalidDataException("The microphone recording is not a RIFF file.");
    reader.ReadUInt32();
    if (ReadFourCc(reader) != "WAVE")
      throw new InvalidDataException("The microphone recording is not a WAVE file.");

    bool hasFormat = false;
    ushort audioFormat = 0;
    ushort channels = 0;
    uint sampleRate = 0;
    ushort blockAlign = 0;
    ushort bitsPerSample = 0;
    long dataOffset = -1;
    long dataLength = -1;

    while (stream.Position + 8 <= stream.Length)
    {
      string chunkId = ReadFourCc(reader);
      uint chunkLength = reader.ReadUInt32();
      long chunkOffset = stream.Position;
      long chunkEnd = checked(chunkOffset + chunkLength);
      if (chunkEnd > stream.Length)
        throw new InvalidDataException("The microphone WAV contains a truncated chunk.");

      if (chunkId == "fmt ")
      {
        if (chunkLength < 16)
          throw new InvalidDataException("The microphone WAV format chunk is truncated.");
        audioFormat = reader.ReadUInt16();
        channels = reader.ReadUInt16();
        sampleRate = reader.ReadUInt32();
        reader.ReadUInt32();
        blockAlign = reader.ReadUInt16();
        bitsPerSample = reader.ReadUInt16();
        hasFormat = true;
      }
      else if (chunkId == "data" && dataOffset < 0)
      {
        dataOffset = chunkOffset;
        dataLength = chunkLength;
      }

      long nextChunk = chunkEnd + (chunkLength & 1u);
      if (nextChunk > stream.Length)
        throw new InvalidDataException("The microphone WAV chunk padding is truncated.");
      stream.Position = nextChunk;
    }

    if (!hasFormat || dataOffset < 0)
      throw new InvalidDataException("The microphone WAV is missing its format or data chunk.");
    if (audioFormat != 1 || bitsPerSample != 16)
      throw new InvalidDataException("The microphone WAV is not uncompressed PCM16 audio.");
    if (channels == 0 || sampleRate == 0 || blockAlign != channels * 2)
      throw new InvalidDataException("The microphone WAV format metadata is invalid.");
    if (dataLength % blockAlign != 0)
      throw new InvalidDataException("The microphone WAV data is not frame-aligned.");

    long frameCount = dataLength / blockAlign;
    if (frameCount <= 0)
      throw new InvalidDataException("The microphone WAV contains no audio frames.");
    if (recording.SampleRate != sampleRate || recording.Channels != channels || recording.FrameCount != frameCount)
      throw new InvalidDataException("The microphone WAV format does not match its database metadata.");

    return new Pcm16WaveInfo
    {
      DataOffset = dataOffset,
      DataLength = dataLength,
      SampleRate = checked((int)sampleRate),
      Channels = channels,
      FrameCount = frameCount,
    };
  }

  private static string ReadFourCc(BinaryReader reader)
  {
    byte[] bytes = reader.ReadBytes(4);
    if (bytes.Length != 4)
      throw new EndOfStreamException("The microphone WAV chunk identifier is truncated.");
    return Encoding.ASCII.GetString(bytes);
  }
}
