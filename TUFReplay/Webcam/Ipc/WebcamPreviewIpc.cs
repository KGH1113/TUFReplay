using System;
using System.Diagnostics;
using System.IO;
using AdofaiIpc;
using TUFReplay.Composition;
using TUFReplay.Shared.Ipc;
using TUFReplay.Shared.Ipc.Generated;
using TUFReplay.Webcam.Capture;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Ipc;

internal sealed class WebcamPreviewIpc
{
  private readonly object _gate = new object();
  private byte[] _pixels;
  private byte[] _bitmap;
  private CameraFrameSize _size;
  private CameraPreviewBuffer _buffer;
  private long _snapshotAt;
  private long _sourceSequence;
  private long _sourceChangedAt;
  private long _lastRequestAt;

  public WebcamPreview Read(string deviceId)
  {
    var state = FeatureRegistry.WebcamRecording?.GetState();
    if (state?.Enabled != true || deviceId != state.SelectedDeviceId)
    {
      throw IpcDomainError.Create("camera_preview_changed", "The camera changed. Open the preview again.");
    }
    lock (_gate)
    {
      CameraPreviewBuffer buffer = FeatureRegistry.WebcamRecording.Preview;
      if (buffer == null)
      {
        return null;
      }
      buffer.RequestBrowserPreview();
      long now = Stopwatch.GetTimestamp();
      bool resumed = _lastRequestAt == 0 || now - _lastRequestAt > Stopwatch.Frequency;
      _lastRequestAt = now;
      if (resumed)
      {
        _sourceChangedAt = now;
        return null;
      }
      long sequence = buffer.Sequence;
      if (_buffer != buffer || sequence != _sourceSequence)
      {
        _sourceSequence = sequence;
        _sourceChangedAt = now;
      }
      else if (now - _sourceChangedAt > 3 * Stopwatch.Frequency)
      {
        throw IpcDomainError.Create(
          "camera_preview_stalled",
          "The camera stopped sending video. Reconnect it and retry the preview."
        );
      }
      if (_buffer != buffer || _bitmap == null || now - _snapshotAt >= Stopwatch.Frequency / 8)
      {
        if (!buffer.TryGetFrameSize(out CameraFrameSize size))
        {
          return null;
        }
        if (_pixels?.Length != size.ByteCount)
          _pixels = new byte[size.ByteCount];
        if (!buffer.TryCopySnapshot(_pixels, out size))
        {
          return null;
        }
        _bitmap = CameraPreviewBitmap.Encode(_pixels, size, out _size);
        _buffer = buffer;
        _snapshotAt = now;
      }
      var stream = new MemoryStream(_bitmap, writable: false);
      return new WebcamPreview
      {
        Width = _size.Width,
        Height = _size.Height,
        File = IpcFile.FromStream(stream, stream.Length, "camera.bmp", "image/bmp"),
      };
    }
  }
}
