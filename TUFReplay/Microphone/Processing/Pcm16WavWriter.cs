using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace TUFReplay.Microphone.Processing;

public sealed class Pcm16WavWriter : IDisposable
{
  private readonly BlockingCollection<Chunk> _queue;
  private readonly string _path;
  private FileStream _stream;
  private readonly Thread _worker;
  private volatile Exception _failure;
  private long _frameCount;
  private volatile bool _completed;

  public Pcm16WavWriter(string path, int queueCapacity = 16)
  {
    _path = path ?? throw new ArgumentNullException(nameof(path));
    _queue = new BlockingCollection<Chunk>(queueCapacity);
    _worker = new Thread(WriteLoop) { IsBackground = true, Name = "TUFReplay microphone WAV writer" };
    _worker.Start();
  }

  public bool TryEnqueue(float[] samples, int sampleCount, int sourceChannels)
  {
    if (_completed || _failure != null || sampleCount <= 0 || sourceChannels <= 0)
      return false;

    float[] copy = ArrayPool<float>.Shared.Rent(sampleCount);
    bool queued = false;
    try
    {
      Array.Copy(samples, copy, sampleCount);
      queued = _queue.TryAdd(new Chunk(copy, sampleCount, sourceChannels));
      return queued;
    }
    catch (InvalidOperationException)
    {
      // Completion or an I/O failure may close the queue after the initial check.
      return false;
    }
    finally
    {
      if (!queued)
        ArrayPool<float>.Shared.Return(copy);
    }
  }

  public long Complete()
  {
    if (_completed)
      return _frameCount;
    _completed = true;
    _queue.CompleteAdding();
    _worker.Join();
    if (_failure != null)
      throw new IOException("Microphone WAV writer failed.", _failure);
    WriteHeader(_stream, _frameCount);
    _stream.Flush(true);
    return _frameCount;
  }

  public void Dispose()
  {
    try
    {
      Complete();
    }
    finally
    {
      _queue.Dispose();
      _stream?.Dispose();
    }
  }

  private void WriteLoop()
  {
    byte[] pcm = ArrayPool<byte>.Shared.Rent(65536);
    try
    {
      Directory.CreateDirectory(Path.GetDirectoryName(_path));
      _stream = new FileStream(_path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 65536);
      WriteHeader(_stream, 0);
      foreach (Chunk chunk in _queue.GetConsumingEnumerable())
      {
        try
        {
          int frames = chunk.Count / chunk.Channels;
          int required = frames * 2;
          if (pcm.Length < required)
          {
            ArrayPool<byte>.Shared.Return(pcm);
            pcm = ArrayPool<byte>.Shared.Rent(required);
          }

          int output = 0;
          for (int frame = 0; frame < frames; frame++)
          {
            float mono = 0f;
            int source = frame * chunk.Channels;
            for (int channel = 0; channel < chunk.Channels; channel++)
              mono += chunk.Samples[source + channel];
            mono /= chunk.Channels;
            mono = Math.Max(-1f, Math.Min(1f, mono));
            short value = mono <= -1f ? short.MinValue : (short)Math.Round(mono * short.MaxValue);
            pcm[output++] = (byte)value;
            pcm[output++] = (byte)(value >> 8);
          }

          _stream.Write(pcm, 0, output);
          Interlocked.Add(ref _frameCount, frames);
        }
        finally
        {
          ArrayPool<float>.Shared.Return(chunk.Samples);
        }
      }
    }
    catch (Exception exception)
    {
      _failure = exception;
      _queue.CompleteAdding();
      while (_queue.TryTake(out Chunk chunk))
        ArrayPool<float>.Shared.Return(chunk.Samples);
    }
    finally
    {
      ArrayPool<byte>.Shared.Return(pcm);
    }
  }

  internal static void WriteHeader(Stream stream, long frameCount)
  {
    long dataLength = checked(frameCount * 2);
    if (dataLength > uint.MaxValue - 36)
      throw new IOException("WAV recording exceeds the RIFF size limit.");

    stream.Position = 0;
    using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
    writer.Write((uint)(36 + dataLength));
    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
    writer.Write((uint)16);
    writer.Write((ushort)1);
    writer.Write((ushort)1);
    writer.Write((uint)48000);
    writer.Write((uint)96000);
    writer.Write((ushort)2);
    writer.Write((ushort)16);
    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
    writer.Write((uint)dataLength);
    stream.Position = stream.Length;
  }

  private readonly struct Chunk
  {
    public readonly float[] Samples;
    public readonly int Count;
    public readonly int Channels;

    public Chunk(float[] samples, int count, int channels)
    {
      Samples = samples;
      Count = count;
      Channels = channels;
    }
  }
}
