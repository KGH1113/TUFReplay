using System;
using System.IO;
using System.Threading;

namespace TUFReplay.Microphone.Playback;

internal sealed class Pcm16PrefetchBuffer : IDisposable
{
  private readonly object _gate = new object();
  private readonly Stream _stream;
  private readonly long _dataOffset;
  private readonly long _dataLength;
  private readonly byte[] _ring;
  private readonly byte[] _workerBuffer;
  private readonly Thread _worker;
  private int _readIndex;
  private int _writeIndex;
  private int _bufferedBytes;
  private int _generation;
  private long _seekOffset;
  private bool _seekPending = true;
  private bool _endOfStream;
  private bool _disposed;
  private Exception _failure;

  public Pcm16PrefetchBuffer(Stream stream, long dataOffset, long dataLength, int capacity)
  {
    _stream = stream ?? throw new ArgumentNullException(nameof(stream));
    if (!stream.CanRead || !stream.CanSeek)
      throw new ArgumentException("The PCM prefetch stream must support reading and seeking.", nameof(stream));
    if (dataOffset < 0)
      throw new ArgumentOutOfRangeException(nameof(dataOffset));
    if (dataLength < 0)
      throw new ArgumentOutOfRangeException(nameof(dataLength));
    if (capacity <= 0)
      throw new ArgumentOutOfRangeException(nameof(capacity));

    _dataOffset = dataOffset;
    _dataLength = dataLength;
    _ring = new byte[capacity];
    _workerBuffer = new byte[Math.Min(capacity, 64 * 1024)];
    _worker = new Thread(PrefetchLoop) { IsBackground = true, Name = "TUFReplay PCM Prefetch" };
    _worker.Start();
  }

  public Exception Failure
  {
    get
    {
      lock (_gate)
        return _failure;
    }
  }

  public int Generation
  {
    get
    {
      lock (_gate)
        return _generation;
    }
  }

  public int BufferedBytes
  {
    get
    {
      lock (_gate)
        return _bufferedBytes;
    }
  }

  public int Capacity => _ring.Length;

  public bool IsReady(int generation, int minimumBytes)
  {
    lock (_gate)
    {
      return !_disposed
        && _failure == null
        && generation == _generation
        && !_seekPending
        && _bufferedBytes >= Math.Max(0, minimumBytes);
    }
  }

  public int Read(byte[] destination, int offset, int count, int alignment)
  {
    if (destination == null)
      throw new ArgumentNullException(nameof(destination));
    if (offset < 0 || count < 0 || offset > destination.Length - count)
      throw new ArgumentOutOfRangeException();
    if (alignment <= 0 || count % alignment != 0)
      throw new ArgumentOutOfRangeException(nameof(alignment));

    int total = 0;
    lock (_gate)
    {
      while (total < count)
      {
        int copyCount = Math.Min(count - total, _bufferedBytes);
        copyCount -= copyCount % alignment;
        if (copyCount > 0)
        {
          CopyFromRing(destination, offset + total, copyCount);
          total += copyCount;
          Monitor.PulseAll(_gate);
          continue;
        }

        break;
      }
    }

    return total;
  }

  public int Seek(long byteOffset)
  {
    lock (_gate)
    {
      if (_disposed)
        return _generation;

      _generation++;
      _seekOffset = Math.Max(0L, Math.Min(byteOffset, _dataLength));
      _seekPending = true;
      _endOfStream = false;
      _readIndex = 0;
      _writeIndex = 0;
      _bufferedBytes = 0;
      Monitor.PulseAll(_gate);
      return _generation;
    }
  }

  public bool TrySkip(int generation, int byteCount, int alignment)
  {
    if (byteCount < 0 || alignment <= 0 || byteCount % alignment != 0)
      throw new ArgumentOutOfRangeException(nameof(byteCount));

    lock (_gate)
    {
      if (
        _disposed
        || _failure != null
        || generation != _generation
        || _seekPending
        || byteCount > _bufferedBytes
      )
        return false;

      _readIndex = (_readIndex + byteCount) % _ring.Length;
      _bufferedBytes -= byteCount;
      Monitor.PulseAll(_gate);
      return true;
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      _disposed = true;
      Monitor.PulseAll(_gate);
    }

    try
    {
      _stream.Dispose();
    }
    catch { }

    if (Thread.CurrentThread != _worker)
      _worker.Join(1000);
  }

  private void PrefetchLoop()
  {
    long streamOffset = 0;
    int generation = 0;

    try
    {
      while (true)
      {
        int requestedBytes;
        bool seek;
        lock (_gate)
        {
          while (!_disposed && !_seekPending && (_bufferedBytes == _ring.Length || _endOfStream))
            Monitor.Wait(_gate);
          if (_disposed)
            return;

          seek = _seekPending;
          if (seek)
          {
            streamOffset = _seekOffset;
            generation = _generation;
            _seekPending = false;
          }

          long remainingBytes = _dataLength - streamOffset;
          int freeBytes = _ring.Length - _bufferedBytes;
          requestedBytes = (int)Math.Min(_workerBuffer.Length, Math.Min(freeBytes, remainingBytes));
          if (requestedBytes <= 0)
          {
            _endOfStream = true;
            Monitor.PulseAll(_gate);
            continue;
          }
        }

        if (seek)
          _stream.Position = _dataOffset + streamOffset;
        int bytesRead = _stream.Read(_workerBuffer, 0, requestedBytes);

        lock (_gate)
        {
          if (_disposed)
            return;
          if (generation != _generation || _seekPending)
            continue;
          if (bytesRead <= 0)
          {
            _endOfStream = true;
            Monitor.PulseAll(_gate);
            continue;
          }

          CopyToRing(_workerBuffer, bytesRead);
          streamOffset += bytesRead;
          Monitor.PulseAll(_gate);
        }
      }
    }
    catch (Exception exception)
    {
      lock (_gate)
      {
        if (!_disposed)
          _failure = exception;
        Monitor.PulseAll(_gate);
      }
    }
  }

  private void CopyFromRing(byte[] destination, int destinationOffset, int count)
  {
    int first = Math.Min(count, _ring.Length - _readIndex);
    Buffer.BlockCopy(_ring, _readIndex, destination, destinationOffset, first);
    int second = count - first;
    if (second > 0)
      Buffer.BlockCopy(_ring, 0, destination, destinationOffset + first, second);
    _readIndex = (_readIndex + count) % _ring.Length;
    _bufferedBytes -= count;
  }

  private void CopyToRing(byte[] source, int count)
  {
    int first = Math.Min(count, _ring.Length - _writeIndex);
    Buffer.BlockCopy(source, 0, _ring, _writeIndex, first);
    int second = count - first;
    if (second > 0)
      Buffer.BlockCopy(source, first, _ring, 0, second);
    _writeIndex = (_writeIndex + count) % _ring.Length;
    _bufferedBytes += count;
  }
}
