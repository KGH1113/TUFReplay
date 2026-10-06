using System;
using System.Diagnostics;
using System.IO;
using AdofaiIpc;
using Newtonsoft.Json.Linq;
using TUFReplay.Composition;
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

  public void Read(AdofaiIpcNamespace ipc, IpcCommand command)
  {
    var state = FeatureRegistry.WebcamRecording?.GetState();
    var payload = command.Payload as JObject;
    if (
      payload == null
      || !payload.ContainsKey("deviceId")
      || (payload["deviceId"].Type != JTokenType.Null && payload["deviceId"].Type != JTokenType.String)
    )
    {
      command.Reject("invalid_preview_request", "Choose a camera before opening the preview.");
      return;
    }
    if (state?.Enabled != true || (string)payload["deviceId"] != state.SelectedDeviceId)
    {
      command.Reject("camera_preview_changed", "The camera changed. Open the preview again.");
      return;
    }
    lock (_gate)
    {
      CameraPreviewBuffer buffer = FeatureRegistry.WebcamRecording.Preview;
      if (buffer == null)
      {
        command.Reply("webcam.preview.frame", new { ready = false });
        return;
      }
      buffer.RequestBrowserPreview();
      long now = Stopwatch.GetTimestamp();
      bool resumed = _lastRequestAt == 0 || now - _lastRequestAt > Stopwatch.Frequency;
      _lastRequestAt = now;
      if (resumed)
      {
        _sourceChangedAt = now;
        command.Reply("webcam.preview.frame", new { ready = false });
        return;
      }
      long sequence = buffer.Sequence;
      if (_buffer != buffer || sequence != _sourceSequence)
      {
        _sourceSequence = sequence;
        _sourceChangedAt = now;
      }
      else if (now - _sourceChangedAt > 3 * Stopwatch.Frequency)
      {
        command.Reject(
          "camera_preview_stalled",
          "The camera stopped sending video. Reconnect it and retry the preview."
        );
        return;
      }
      if (_buffer != buffer || _bitmap == null || now - _snapshotAt >= Stopwatch.Frequency / 8)
      {
        if (!buffer.TryGetFrameSize(out CameraFrameSize size))
        {
          command.Reply("webcam.preview.frame", new { ready = false });
          return;
        }
        if (_pixels?.Length != size.ByteCount)
          _pixels = new byte[size.ByteCount];
        if (!buffer.TryCopySnapshot(_pixels, out size))
        {
          command.Reply("webcam.preview.frame", new { ready = false });
          return;
        }
        _bitmap = CameraPreviewBitmap.Encode(_pixels, size, out _size);
        _buffer = buffer;
        _snapshotAt = now;
      }
      var stream = new MemoryStream(_bitmap, writable: false);
      ipc.ReplyDownload(
        command,
        "webcam.preview.frame",
        new IpcDownloadSource(stream, stream.Length, "camera.bmp", "image/bmp"),
        new { width = _size.Width, height = _size.Height }
      );
    }
  }
}
