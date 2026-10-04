using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

// A single, bounded frame shared with the native helper. The sequence counter
// prevents torn frames while Unity keeps the last complete preview texture.
public sealed class CameraPreviewBuffer : IDisposable
{
  public const int HeaderSize = 64;
  public const int MaxWidth = 960;
  public const int MaxHeight = 720;
  private const int PreviewRequestedOffset = 16;
  private readonly object _gate = new object();
  private readonly MemoryMappedFile _mapping;
  private MemoryMappedViewAccessor _view;
  private long _lastRead;
  private volatile bool _hasFrame;

  public CameraPreviewBuffer(int width, int height)
  {
    if (width < 1 || width > MaxWidth || height < 1 || height > MaxHeight)
      throw new ArgumentOutOfRangeException(nameof(width));
    Width = width;
    Height = height;
    ByteCount = width * height * 4;
    Path = System.IO.Path.Combine(
      System.IO.Path.GetTempPath(),
      "tufreplay-camera-" + Guid.NewGuid().ToString("N") + ".frame"
    );
    using (
      var file = new FileStream(Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)
    )
      file.SetLength(HeaderSize + ByteCount);
    _mapping = MemoryMappedFile.CreateFromFile(
      Path,
      FileMode.Open,
      null,
      HeaderSize + ByteCount,
      MemoryMappedFileAccess.ReadWrite
    );
    _view = _mapping.CreateViewAccessor();
    _view.Write(8, Width);
    _view.Write(12, Height);
    _view.Write(PreviewRequestedOffset, 1);
  }

  public string Path { get; }
  public int Width { get; }
  public int Height { get; }
  public int ByteCount { get; }

  internal object DiagnosticState()
  {
    lock (_gate)
      return new
      {
        path = Path,
        disposed = _view == null,
        capacityWidth = Width,
        capacityHeight = Height,
        sequence = _view?.ReadInt64(0),
        width = _view?.ReadInt32(8),
        height = _view?.ReadInt32(12),
        requested = _view?.ReadInt32(PreviewRequestedOffset),
        lastReadSequence = _lastRead,
        hasFrame = _hasFrame,
      };
  }

  public bool HasFrame
  {
    get
    {
      if (!Monitor.TryEnter(_gate))
        return _hasFrame;
      try
      {
        if (_view == null)
          return false;
        // Readiness survives the next write. The displayed texture still holds
        // the last complete frame while the shared buffer is being refreshed.
        if (!_hasFrame)
          TryGetFrameSizeLocked(out _);
        return _hasFrame;
      }
      finally
      {
        Monitor.Exit(_gate);
      }
    }
  }

  public bool TryGetFrameSize(out CameraFrameSize size)
  {
    size = default;
    if (!Monitor.TryEnter(_gate))
      return false;
    try
    {
      return TryGetFrameSizeLocked(out size);
    }
    finally
    {
      Monitor.Exit(_gate);
    }
  }

  public void SetPreviewRequested(bool requested)
  {
    if (!Monitor.TryEnter(_gate))
      return;
    try
    {
      _view?.Write(PreviewRequestedOffset, requested ? 1 : 0);
      Thread.MemoryBarrier();
    }
    finally
    {
      Monitor.Exit(_gate);
    }
  }

  public bool ShouldPublish
  {
    get
    {
      lock (_gate)
        return _view != null && (_view.ReadInt64(0) == 0 || _view.ReadInt32(PreviewRequestedOffset) != 0);
    }
  }

  private bool TryGetFrameSizeLocked(out CameraFrameSize size)
  {
    size = default;
    if (_view == null)
      return false;
    long sequence = _view.ReadInt64(0);
    if (sequence <= 0 || (sequence & 1) != 0)
      return false;
    Thread.MemoryBarrier();
    int width = _view.ReadInt32(8);
    int height = _view.ReadInt32(12);
    Thread.MemoryBarrier();
    if (_view.ReadInt64(0) != sequence || width < 1 || width > Width || height < 1 || height > Height)
      return false;
    size = new CameraFrameSize(width, height);
    _hasFrame = true;
    return true;
  }

  public bool TryCopy(byte[] destination) => TryCopy(destination, out _);

  public bool TryCopy(byte[] destination, out CameraFrameSize size)
  {
    size = default;
    if (destination == null)
      return false;
    if (!Monitor.TryEnter(_gate))
      return false;
    try
    {
      if (_view == null)
        return false;
      long sequence = _view.ReadInt64(0);
      if (sequence <= 0 || sequence == _lastRead || (sequence & 1) != 0)
        return false;
      Thread.MemoryBarrier();
      if (!TryGetFrameSizeLocked(out CameraFrameSize copiedSize) || destination.Length != copiedSize.ByteCount)
        return false;
      CopyPixels(destination);
      Thread.MemoryBarrier();
      if (_view.ReadInt64(0) != sequence)
        return false;
      _lastRead = sequence;
      size = copiedSize;
      return true;
    }
    finally
    {
      Monitor.Exit(_gate);
    }
  }

  private unsafe void CopyPixels(byte[] destination)
  {
    // Unity's Mono SafeBuffer.ReadArray<byte> copies one element at a time.
    // The gate excludes Dispose, and the acquired handle keeps the mapping
    // alive until the whole frame has been copied in one native operation.
    var handle = _view.SafeMemoryMappedViewHandle;
    byte* pointer = null;
    handle.AcquirePointer(ref pointer);
    try
    {
      Marshal.Copy(new IntPtr(pointer + _view.PointerOffset + HeaderSize), destination, 0, destination.Length);
    }
    finally
    {
      handle.ReleasePointer();
    }
  }

  public void Publish(byte[] rgba)
  {
    Publish(rgba, Width, Height);
  }

  public void Publish(byte[] rgba, int width, int height)
  {
    if (width < 1 || width > Width || height < 1 || height > Height || rgba.Length != width * height * 4)
      throw new ArgumentException("Invalid camera preview dimensions.", nameof(rgba));
    lock (_gate)
    {
      if (_view == null)
        return;
      long sequence = _view.ReadInt64(0);
      _view.Write(0, sequence + 1);
      Thread.MemoryBarrier();
      _view.Write(8, width);
      _view.Write(12, height);
      _view.WriteArray(HeaderSize, rgba, 0, rgba.Length);
      Thread.MemoryBarrier();
      _view.Write(0, sequence + 2);
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      _view?.Dispose();
      _view = null;
      _hasFrame = false;
      _mapping.Dispose();
      try
      {
        File.Delete(Path);
      }
      catch (IOException) { }
    }
  }
}
