using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using TUFReplay.Webcam.Capture;

// Feeds the native camera relay through real pipes, exactly like the FFmpeg
// capture process does: a showinfo line on stderr, then the raw I420 frame on stdout.
internal sealed class SyntheticCameraCapture : ICameraRelaySource, IDisposable
{
  private readonly AnonymousPipeServerStream _frames;
  private readonly AnonymousPipeServerStream _log;
  private readonly IntPtr _camera;
  private readonly int _width;
  private readonly int _height;
  private long _index;

  public static bool IsAvailable
  {
    get
    {
      string library = Environment.GetEnvironmentVariable("TUFREPLAY_CAMERA_NATIVE_LIBRARY");
      return !string.IsNullOrEmpty(library) && File.Exists(library);
    }
  }

  public SyntheticCameraCapture(int width, int height, string previewPath = null, int maxWidth = 0, int maxHeight = 0)
  {
    _width = width;
    _height = height;
    Relay = CameraRelayLibrary.Load();
    _frames = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
    _log = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None);
    _camera = Relay.Create(
      new CameraRelayConfig
      {
        FrameHandle = _frames.ClientSafePipeHandle.DangerousGetHandle().ToInt64(),
        LogHandle = _log.ClientSafePipeHandle.DangerousGetHandle().ToInt64(),
        MaxWidth = maxWidth > 0 ? maxWidth : width,
        MaxHeight = maxHeight > 0 ? maxHeight : height,
        // Unix time zero on the Stopwatch clock: PTS microseconds become ticks.
        ClockUnixUs = 0,
        ClockTicks = 0,
        TicksFrequency = Stopwatch.Frequency,
        PoolCapacity = 8,
        TimestampTimeoutMs = 10000,
        PreviewMaxWidth = CameraPreviewBuffer.MaxWidth,
        PreviewMaxHeight = CameraPreviewBuffer.MaxHeight,
      },
      previewPath,
      out string failure
    );
    if (_camera == IntPtr.Zero)
      throw new InvalidOperationException("The camera relay did not start: " + failure);
    _frames.DisposeLocalCopyOfClientHandle();
    _log.DisposeLocalCopyOfClientHandle();
    WriteLog("[Parsed_showinfo_4 @ 0] config in time_base: 1/1000000, frame_rate: 60/1");
  }

  public CameraRelayLibrary Relay { get; }
  public string Error => null;

  // The Stopwatch time the run attaches at; the relay reuses a latest frame at
  // most 250 ms older than this.
  public long Now { get; set; }

  public IntPtr AttachRecording(long boundaryTicks, int frameRate, int queueCapacity) =>
    Relay.Attach(_camera, boundaryTicks, Now, frameRate, queueCapacity);

  public CameraRelayState State => Relay.GetState(_camera);

  // Capture times have the microsecond precision of FFmpeg's wall-clock PTS.
  public void Emit(byte[] pixels, long ticks)
  {
    long microseconds = (long)Math.Round(ticks * 1_000_000d / Stopwatch.Frequency);
    WriteLog(
      "[Parsed_showinfo_4 @ 0] n:" + _index++ + " pts:" + microseconds + " s:" + _width + "x" + _height + " i:P"
    );
    _frames.Write(pixels, 0, _width * _height * 3 / 2);
    _frames.Flush();
  }

  public void EmitAndWaitCaptured(byte[] pixels, long ticks)
  {
    long before = State.FramesCaptured;
    Emit(pixels, ticks);
    if (!SpinWait.SpinUntil(() => State.FramesCaptured > before, TimeSpan.FromSeconds(5)))
      throw new TimeoutException("The camera relay did not capture a synthetic frame.");
  }

  public bool WaitReady() => Relay.WaitReady(_camera, 5000) == 1;

  private void WriteLog(string line)
  {
    byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
    _log.Write(bytes, 0, bytes.Length);
    _log.Flush();
  }

  public void Dispose()
  {
    Relay.Stop(_camera);
    _frames.Dispose();
    _log.Dispose();
    Relay.Destroy(_camera, 5000);
  }
}
