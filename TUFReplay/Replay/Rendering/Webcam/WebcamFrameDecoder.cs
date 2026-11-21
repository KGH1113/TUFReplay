using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using TUFReplay.Webcam.Capture;

namespace TUFReplay.Replay.Rendering.Webcam;

/// <summary>
/// Decodes the webcam recording with FFmpeg into RGBA frames already cropped and scaled to their
/// place in the video. Two threads read a few frames ahead into buffers that are reused, so a
/// render allocates nothing per frame. The render asks for camera times in order; a jump back,
/// or far ahead, starts FFmpeg again at that time.
/// </summary>
internal sealed class WebcamFrameDecoder : IDisposable
{
  // The frame shown, the next one, and the ones FFmpeg fills meanwhile.
  private const int Buffers = 5;

  // Decoding that far is slower than starting FFmpeg again at the time.
  private const double SeekAheadUs = 3_000_000;

  // Started a little early, so the frame before the time is decoded too.
  private const double SeekLeadUs = 250_000;

  private static readonly TimeSpan FrameWait = TimeSpan.FromSeconds(15);

  private readonly string _ffmpeg;
  private readonly string _path;
  private readonly string _filter;
  private readonly int _frameBytes;
  private Session _session;
  private Frame _current;
  private Frame _next;

  public WebcamFrameDecoder(string ffmpeg, string path, string filter, int width, int height)
  {
    _ffmpeg = ffmpeg ?? throw new ArgumentNullException(nameof(ffmpeg));
    _path = path ?? throw new ArgumentNullException(nameof(path));
    _filter = filter ?? throw new ArgumentNullException(nameof(filter));
    if (width <= 0 || height <= 0)
      throw new ArgumentOutOfRangeException(nameof(width));
    _frameBytes = checked(width * height * 4);
  }

  /// <summary>The frame shown: RGBA rows from the top, or null before the first one.</summary>
  public byte[] Current => _current?.Pixels;

  /// <summary>The shown frame's camera time in microseconds.</summary>
  public long CurrentTimeUs => _current?.TimeUs ?? 0;

  /// <summary>
  /// Moves to the last frame at or before <paramref name="sourceUs"/>, or the first frame when
  /// none comes before it. True when the shown frame changed.
  /// </summary>
  public bool Advance(double sourceUs)
  {
    Frame previous = null;
    if (
      _session == null
      || (_current != null && (sourceUs < _current.TimeUs || sourceUs - _current.TimeUs > SeekAheadUs))
    )
    {
      previous = _current;
      Restart(sourceUs);
    }
    bool changed = false;
    while (true)
    {
      _next ??= _session.Take(FrameWait);
      if (_next == null || _next.TimeUs > sourceUs)
        break;
      _session.Release(_current);
      _current = _next;
      _next = null;
      changed = true;
    }
    // A time before the first decoded frame shows that frame, as OrbitRender's trim did.
    if (_current == null && _next != null)
    {
      _current = _next;
      _next = null;
      changed = true;
    }
    // A jump past the last frame keeps the frame shown before it.
    if (_current == null && previous != null && _session.Failure == null)
      _current = previous;
    if (_current == null && _session.Failure != null)
      throw new IOException("FFmpeg could not decode the webcam recording: " + _session.Failure);
    return changed;
  }

  private void Restart(double sourceUs)
  {
    _session?.Dispose();
    _current = null;
    _next = null;
    _session = new Session(_ffmpeg, _path, _filter, _frameBytes, Math.Max(0, sourceUs - SeekLeadUs));
  }

  public void Dispose()
  {
    _session?.Dispose();
    _session = null;
    _current = null;
    _next = null;
  }

  private sealed class Frame
  {
    public Frame(byte[] pixels, long timeUs)
    {
      Pixels = pixels;
      TimeUs = timeUs;
    }

    public byte[] Pixels { get; }
    public long TimeUs { get; }
  }

  /// <summary>One FFmpeg run from a start time to the end of the recording.</summary>
  private sealed class Session : IDisposable
  {
    // showinfo writes one line per frame, in the order the frames come out.
    private static readonly Regex FrameTime = new Regex(
      @"Parsed_showinfo.*\bpts_time:\s*(-?[0-9.]+(?:[eE][-+]?[0-9]+)?)",
      RegexOptions.CultureInvariant
    );

    private readonly Process _process;
    private readonly CancellationTokenSource _stop = new CancellationTokenSource();
    private readonly BlockingCollection<byte[]> _free = new BlockingCollection<byte[]>();
    private readonly BlockingCollection<Frame> _ready = new BlockingCollection<Frame>();
    private readonly BlockingCollection<long> _times = new BlockingCollection<long>();
    private readonly int _frameBytes;
    private readonly Thread _pixels;
    private readonly Thread _log;
    private string _lastError;

    public Session(string ffmpeg, string path, string filter, int frameBytes, double startUs)
    {
      _frameBytes = frameBytes;
      for (int i = 0; i < Buffers; i++)
        _free.Add(new byte[frameBytes]);
      // -copyts -start_at_zero keep the recording's own timestamps from its first frame, the
      // times the render asks for, also after -ss.
      _process = FfmpegCameraProcess.Create(
        ffmpeg,
        new[]
        {
          "-nostdin",
          "-hide_banner",
          "-nostats",
          "-loglevel",
          "info",
          "-copyts",
          "-start_at_zero",
          "-ss",
          (startUs / 1_000_000d).ToString("0.######", CultureInfo.InvariantCulture),
          "-i",
          path,
          "-an",
          "-sn",
          "-dn",
          "-vf",
          filter + ",format=rgba,showinfo",
          "-fps_mode",
          "passthrough",
          "-f",
          "rawvideo",
          "-pix_fmt",
          "rgba",
          "pipe:1",
        }
      );
      if (!_process.Start())
        throw new IOException("FFmpeg did not start for the webcam recording.");
      _process.StandardInput.Close();
      _pixels = new Thread(ReadPixels) { IsBackground = true, Name = "TUFReplay render webcam frames" };
      _log = new Thread(ReadLog) { IsBackground = true, Name = "TUFReplay render webcam log" };
      _pixels.Start();
      _log.Start();
    }

    /// <summary>Why FFmpeg stopped early, once it has; otherwise null.</summary>
    public string Failure { get; private set; }

    /// <summary>The next frame, or null at the end of the recording.</summary>
    public Frame Take(TimeSpan wait)
    {
      // A stuck FFmpeg costs the wait once, not on every later frame.
      if (Failure != null && _ready.Count == 0)
        return null;
      try
      {
        if (_ready.TryTake(out Frame frame, (int)wait.TotalMilliseconds, _stop.Token))
          return frame;
      }
      catch (OperationCanceledException)
      {
        return null;
      }
      if (!_ready.IsCompleted)
        Failure ??= "no frame came within " + wait.TotalSeconds + " s";
      return null;
    }

    /// <summary>Hands a frame's buffer back for FFmpeg to fill again.</summary>
    public void Release(Frame frame)
    {
      if (frame != null && !_free.IsAddingCompleted)
        _free.Add(frame.Pixels);
    }

    private void ReadPixels()
    {
      try
      {
        Stream output = _process.StandardOutput.BaseStream;
        while (true)
        {
          byte[] buffer = _free.Take(_stop.Token);
          if (!ReadFrame(output, buffer))
            break;
          long timeUs = _times.Take(_stop.Token);
          _ready.Add(new Frame(buffer, timeUs), _stop.Token);
        }
      }
      catch (OperationCanceledException) { }
      catch (InvalidOperationException) { }
      catch (IOException exception)
      {
        Failure ??= exception.Message;
      }
      finally
      {
        _ready.CompleteAdding();
        if (!_stop.IsCancellationRequested && Failure == null && _process.HasExited && _process.ExitCode != 0)
          Failure = _lastError ?? "FFmpeg exited with code " + _process.ExitCode;
      }
    }

    private bool ReadFrame(Stream output, byte[] buffer)
    {
      int read = 0;
      while (read < _frameBytes)
      {
        int count = output.Read(buffer, read, _frameBytes - read);
        if (count <= 0)
        {
          if (read > 0)
            throw new IOException("FFmpeg stopped in the middle of a webcam frame.");
          return false;
        }
        read += count;
      }
      return true;
    }

    private void ReadLog()
    {
      try
      {
        string line;
        while ((line = _process.StandardError.ReadLine()) != null)
        {
          Match match = FrameTime.Match(line);
          if (match.Success)
            _times.Add(
              (long)Math.Round(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * 1_000_000d),
              _stop.Token
            );
          else if (line.IndexOf("rror", StringComparison.Ordinal) >= 0)
            _lastError = line.Trim();
        }
      }
      catch (OperationCanceledException) { }
      catch (InvalidOperationException) { }
      catch (IOException) { }
      finally
      {
        _times.CompleteAdding();
      }
    }

    public void Dispose()
    {
      _stop.Cancel();
      _free.CompleteAdding();
      try
      {
        if (!_process.HasExited)
        {
          _process.Kill();
          // The bundle deletes the recording next; FFmpeg must have let go of it.
          _process.WaitForExit(5000);
        }
      }
      catch (InvalidOperationException) { }
      catch (System.ComponentModel.Win32Exception) { }
      _pixels.Join(2000);
      _log.Join(2000);
      _process.Dispose();
      _stop.Dispose();
    }
  }
}
