using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Webcam.Models;

namespace TUFReplay.Webcam.Capture;

// Encoder names in an FFmpeg build do not imply that a usable GPU/driver is present.
// Prepare with a real encode before a run; use precisely the same codec options later.
internal sealed class FfmpegCameraEncoder
{
  private static readonly FfmpegCameraEncoder[] Candidates =
  {
    new FfmpegCameraEncoder(
      "h264_nvenc",
      "-forced-idr",
      "-preset",
      "p1",
      "-tune",
      "ll",
      "-zerolatency",
      "1",
      "-rc",
      "cbr",
      "-rc-lookahead",
      "0"
    ),
    new FfmpegCameraEncoder("h264_qsv", "-forced_idr", "-preset", "veryfast", "-async_depth", "1", "-look_ahead", "0"),
    new FfmpegCameraEncoder("h264_amf", "-forced_idr", "-usage", "ultralowlatency", "-quality", "speed", "-rc", "cbr"),
    new FfmpegCameraEncoder("libx264", "-forced-idr", "-preset", "ultrafast", "-tune", "zerolatency", "-threads", "2"),
  };

  private const int ProbeTimeoutMs = 30000;
  private static readonly FfmpegCameraEncoderCache Cache = new FfmpegCameraEncoderCache(ProbeAsync);
  private readonly string _forcedIdrOption;
  private readonly string[] _options;

  private FfmpegCameraEncoder(string name, string forcedIdrOption, params string[] options)
  {
    Name = name;
    _forcedIdrOption = forcedIdrOption;
    _options = options;
  }

  public string Name { get; }
  public bool IsHardware => Name != "libx264";
  public static FfmpegCameraEncoder Software => Candidates[Candidates.Length - 1];

  public static Task<FfmpegCameraEncoder> SelectAsync(
    string executable,
    WebcamCaptureProfile profile,
    Action<string> log = null
  ) => Cache.SelectAsync(executable, profile, log);

  public static void Invalidate(string executable, WebcamCaptureProfile profile) =>
    Cache.Invalidate(executable, profile);

  public IEnumerable<string> GetArguments(WebcamCaptureProfile profile)
  {
    var arguments = new List<string> { "-c:v", Name };
    arguments.AddRange(_options);
    arguments.AddRange(
      new[]
      {
        "-pix_fmt",
        Name == "h264_qsv" || Name == "h264_amf" ? "nv12" : "yuv420p",
        "-bf",
        "0",
        // Matroska DefaultDuration supplies the nominal rate independently of
        // packet timestamps. FFmpeg rejects output -r with non-CFR fps modes.
        "-fps_mode",
        "vfr",
        "-enc_time_base",
        "1:1000000",
        "-b:v",
        profile.BitRate.ToString(CultureInfo.InvariantCulture),
        "-maxrate",
        profile.BitRate.ToString(CultureInfo.InvariantCulture),
        "-bufsize",
        (profile.BitRate * 2).ToString(CultureInfo.InvariantCulture),
        "-g",
        (profile.FrameRate * 2).ToString(CultureInfo.InvariantCulture),
        // The relay's first frame only opens the encoder session before the run
        // (hardware encoders can take seconds). Drop its packet, start the video
        // on an IDR frame, and rebase timestamps so the run still starts at zero.
        _forcedIdrOption,
        "1",
        "-force_key_frames",
        "expr:eq(n,1)",
        "-bsf:v",
        "noise=drop=not(n),setts=pts=PTS-STARTPTS:dts=DTS-STARTDTS",
      }
    );
    return arguments;
  }

  internal static async Task<FfmpegCameraEncoder> SelectUncachedAsync(
    string executable,
    WebcamCaptureProfile profile,
    Func<string, WebcamCaptureProfile, FfmpegCameraEncoder, Task<FfmpegCameraEncoderProbe>> probe,
    Action<string> log
  )
  {
    foreach (FfmpegCameraEncoder candidate in Candidates)
    {
      FfmpegCameraEncoderProbe result = await probe(executable, profile, candidate).ConfigureAwait(false);
      log?.Invoke("Camera encoder " + candidate.Name + ": " + result.Diagnostic);
      if (result.Success)
        return candidate;
    }
    throw new IOException(
      "Camera recording could not prepare a video encoder. Restart the game and retry FFmpeg installation in Downloads."
    );
  }

  internal static string[] GetProbeArguments(WebcamCaptureProfile profile, FfmpegCameraEncoder encoder)
  {
    var arguments = new List<string>
    {
      "-hide_banner",
      "-loglevel",
      "error",
      "-nostats",
      "-nostdin",
      "-filter_threads",
      "1",
      "-f",
      "lavfi",
      "-i",
      "color=c=black:s=" + profile.Width + "x" + profile.Height + ":r=" + profile.FrameRate,
      // The first frame is the dropped warm-up frame; three are written.
      "-frames:v",
      "4",
      "-an",
    };
    arguments.AddRange(encoder.GetArguments(profile));
    arguments.AddRange(new[] { "-progress", "pipe:1", "-f", "null", "-" });
    return arguments.ToArray();
  }

  private static Task<FfmpegCameraEncoderProbe> ProbeAsync(
    string executable,
    WebcamCaptureProfile profile,
    FfmpegCameraEncoder encoder
  ) => Task.Run(() => Probe(executable, GetProbeArguments(profile, encoder)));

  private static FfmpegCameraEncoderProbe Probe(string executable, string[] arguments)
  {
    using Process process = FfmpegCameraProcess.Create(executable, arguments);
    var messages = new Queue<string>();
    var outputClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var errorClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    int frames = 0;
    process.ErrorDataReceived += (_, args) =>
    {
      if (args.Data == null)
      {
        errorClosed.TrySetResult(true);
        return;
      }
      lock (messages)
      {
        messages.Enqueue(args.Data.Length > 400 ? args.Data.Substring(0, 400) : args.Data);
        while (messages.Count > 12)
          messages.Dequeue();
      }
    };
    process.OutputDataReceived += (_, args) =>
    {
      if (args.Data == null)
        outputClosed.TrySetResult(true);
      else if (
        args.Data.StartsWith("frame=", StringComparison.Ordinal)
        && int.TryParse(args.Data.Substring(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
      )
        Interlocked.Exchange(ref frames, count);
    };
    try
    {
      if (!process.Start())
        return new FfmpegCameraEncoderProbe(false, "FFmpeg did not start.");
      process.StandardInput.Close();
      process.BeginErrorReadLine();
      process.BeginOutputReadLine();
      // Quick Sync took over 8 s to open while the game was loading; a timeout
      // here records with the software encoder for the rest of the session.
      if (!process.WaitForExit(ProbeTimeoutMs))
        return new FfmpegCameraEncoderProbe(false, "Probe timed out after 30 seconds.");
      // WaitForExit() without a timeout also waits for redirected streams, and can
      // block forever with a broken child. Bound the final progress drain instead.
      Task.WaitAll(new Task[] { outputClosed.Task, errorClosed.Task }, 1000);
      if (process.ExitCode == 0 && Volatile.Read(ref frames) >= 3)
        return new FfmpegCameraEncoderProbe(true, "Encoded 3 test frames successfully.");
      lock (messages)
        return new FfmpegCameraEncoderProbe(
          false,
          "Exit " + process.ExitCode + ", frames=" + Volatile.Read(ref frames) + ". " + string.Join(" | ", messages)
        );
    }
    catch (Exception exception)
      when (exception is Win32Exception
        || exception is IOException
        || exception is InvalidOperationException
        || exception is UnauthorizedAccessException
      )
    {
      return new FfmpegCameraEncoderProbe(false, exception.Message);
    }
    finally
    {
      try
      {
        if (!process.HasExited)
        {
          process.Kill();
          process.WaitForExit(1000);
        }
      }
      catch (InvalidOperationException) { }
      catch (Win32Exception) { }
    }
  }
}

internal readonly struct FfmpegCameraEncoderProbe
{
  public FfmpegCameraEncoderProbe(bool success, string diagnostic)
  {
    Success = success;
    Diagnostic = diagnostic;
  }

  public bool Success { get; }
  public string Diagnostic { get; }
}

// Separate cache/probe policy keeps GPU absence, driver failures, retries, and
// concurrent preparation testable without either hardware or a child process.
internal sealed class FfmpegCameraEncoderCache
{
  private readonly object _gate = new object();
  private readonly Dictionary<string, Task<FfmpegCameraEncoder>> _entries = new Dictionary<
    string,
    Task<FfmpegCameraEncoder>
  >(StringComparer.Ordinal);
  private readonly Func<string, WebcamCaptureProfile, FfmpegCameraEncoder, Task<FfmpegCameraEncoderProbe>> _probe;

  public FfmpegCameraEncoderCache(
    Func<string, WebcamCaptureProfile, FfmpegCameraEncoder, Task<FfmpegCameraEncoderProbe>> probe
  ) => _probe = probe ?? throw new ArgumentNullException(nameof(probe));

  public async Task<FfmpegCameraEncoder> SelectAsync(
    string executable,
    WebcamCaptureProfile profile,
    Action<string> log = null
  )
  {
    string key = GetKey(executable, profile);
    Task<FfmpegCameraEncoder> selection;
    lock (_gate)
    {
      if (!_entries.TryGetValue(key, out selection))
      {
        if (_entries.Count >= 16)
          _entries.Clear();
        selection = FfmpegCameraEncoder.SelectUncachedAsync(executable, profile, _probe, log);
        _entries.Add(key, selection);
      }
    }
    try
    {
      return await selection.ConfigureAwait(false);
    }
    catch
    {
      lock (_gate)
        if (_entries.TryGetValue(key, out Task<FfmpegCameraEncoder> current) && ReferenceEquals(current, selection))
          _entries.Remove(key);
      throw;
    }
  }

  public void Invalidate(string executable, WebcamCaptureProfile profile)
  {
    lock (_gate)
      _entries.Remove(GetKey(executable, profile));
  }

  private static string GetKey(string executable, WebcamCaptureProfile profile)
  {
    var file = new FileInfo(executable);
    return file.FullName
      + "|"
      + (file.Exists ? file.Length + ":" + file.LastWriteTimeUtc.Ticks : "missing")
      + "|"
      + profile.Width
      + "x"
      + profile.Height
      + "@"
      + profile.FrameRate
      + ":"
      + profile.BitRate;
  }
}
