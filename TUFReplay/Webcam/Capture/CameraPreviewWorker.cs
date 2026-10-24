using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

// Preview conversion must never hold up DirectShow or recording. At most one
// pending frame is retained and conversion is limited to the display's 30 fps.
internal sealed class CameraPreviewWorker : IDisposable
{
  private readonly CameraPreviewBuffer _preview;
  private readonly CameraPreviewPixels _converter;
  private readonly CameraFrameSize _size;
  private readonly byte[] _pixels;
  private readonly CameraFrameQueue _frames = new CameraFrameQueue(1);
  private readonly Task _worker;
  private long _lastSubmitted;

  public CameraPreviewWorker(CameraPreviewBuffer preview, CameraFrameSize source)
  {
    _preview = preview;
    _size = CameraFrameSize.Fit(source.Width, source.Height, preview.Width, preview.Height);
    _pixels = new byte[_size.ByteCount];
    _converter = new CameraPreviewPixels(source.Width, source.Height, _size.Width, _size.Height);
    _worker = Task.Factory.StartNew(
      Run,
      CancellationToken.None,
      TaskCreationOptions.LongRunning,
      TaskScheduler.Default
    );
  }

  public void Submit(CameraFrame frame)
  {
    long now = Stopwatch.GetTimestamp();
    if (!_preview.ShouldPublish || (_lastSubmitted != 0 && now - _lastSubmitted < Stopwatch.Frequency / 30))
      return;
    if (_frames.Submit(frame))
      _lastSubmitted = now;
  }

  private void Run()
  {
    try
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        try
        {
          Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        }
        catch (ThreadStateException) { }
        catch (System.Security.SecurityException) { }
      }
      CameraFrame frame;
      while ((frame = _frames.Take()) != null)
      {
        try
        {
          if (!_preview.ShouldPublish)
            continue;
          _converter.Convert(frame.Pixels, _pixels);
          _preview.Publish(_pixels, _size.Width, _size.Height);
        }
        finally
        {
          frame.Release();
        }
      }
    }
    catch (ObjectDisposedException) { }
    finally
    {
      _frames.Dispose();
    }
  }

  public void Dispose()
  {
    _frames.Dispose();
    // Do not join a conversion worker from the game/capture thread.
    _ = _worker.ContinueWith(
      completed =>
      {
        _ = completed.Exception;
      },
      TaskScheduler.Default
    );
  }
}
