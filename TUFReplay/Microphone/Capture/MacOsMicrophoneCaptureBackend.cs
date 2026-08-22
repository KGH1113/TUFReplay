using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Microphone.Capture;
using TUFReplay.Microphone.Models;

namespace TUFReplay.Microphone.Capture;

public sealed class MacOsMicrophoneCaptureBackend : IMicrophoneCaptureBackend
{
  private const int ProtocolVersion = 1;
  private const int ConnectTimeoutMilliseconds = 10000;
  private const int CommandTimeoutMilliseconds = 300000;

  private readonly object _transportGate = new object();
  private readonly object _stateGate = new object();
  private readonly List<MicrophoneDeviceInfo> _cachedDevices = new List<MicrophoneDeviceInfo>();
  private readonly BlockingCollection<Action> _commands = new BlockingCollection<Action>();
  private readonly Thread _commandWorker;
  private TcpClient _client;
  private StreamReader _reader;
  private StreamWriter _writer;
  private ArmState _armState;
  private string _armError;
  private int _armGeneration;
  private PendingRun _run;
  private bool _deviceRefreshQueued;
  private volatile bool _disposed;

  public MacOsMicrophoneCaptureBackend()
  {
    _commandWorker = new Thread(ProcessCommands) { IsBackground = true, Name = "TUFReplay macOS microphone helper" };
    _commandWorker.Start();
    _deviceRefreshQueued = true;
    QueueCommand(RefreshDevices);
  }

  public void RequestPermission()
  {
    QueueCommand(() =>
    {
      try
      {
        Send(new JObject { ["command"] = "authorize" });
        RefreshDevices();
        Main.Instance?.Log("[Microphone] macOS microphone permission is ready.");
      }
      catch (Exception exception)
      {
        Main.Instance?.Log("[Microphone] macOS microphone permission request failed. error=" + exception.Message);
      }
    });
  }

  public List<MicrophoneDeviceInfo> ListDevices()
  {
    lock (_stateGate)
    {
      if (!_deviceRefreshQueued && !_disposed)
      {
        _deviceRefreshQueued = true;
        QueueCommand(RefreshDevices);
      }
      return new List<MicrophoneDeviceInfo>(_cachedDevices);
    }
  }

  public bool Arm(string deviceId, out string error)
  {
    int generation;
    lock (_stateGate)
    {
      generation = ++_armGeneration;
      _armState = ArmState.Arming;
      _armError = null;
    }
    if (!QueueCommand(() => CompleteArm(deviceId, generation)))
    {
      lock (_stateGate)
      {
        _armState = ArmState.Failed;
        _armError = "macOS microphone helper is shutting down.";
      }
      error = _armError;
      return false;
    }
    Main.Instance?.Log("[Microphone] Arming macOS microphone.");
    error = null;
    return true;
  }

  public MicrophoneArmStatus GetArmStatus()
  {
    lock (_stateGate)
    {
      return new MicrophoneArmStatus
      {
        State =
          _armState == ArmState.Arming ? MicrophoneArmState.Arming
          : _armState == ArmState.Armed ? MicrophoneArmState.Armed
          : _armState == ArmState.Failed ? MicrophoneArmState.Failed
          : MicrophoneArmState.Idle,
        Error = _armError,
      };
    }
  }

  public bool BeginRun(string runId, string tempPath, out string error)
  {
    lock (_stateGate)
    {
      if (_armState != ArmState.Armed)
      {
        error =
          _armState == ArmState.Arming
            ? "Microphone authorization is still pending. This run will continue without microphone audio."
            : _armError ?? "Microphone is not armed.";
        return false;
      }
    }

    var run = new PendingRun(runId, tempPath);
    try
    {
      Send(
        new JObject
        {
          ["command"] = "begin",
          ["runId"] = run.RunId,
          ["path"] = run.TempPath,
        }
      );
      run.BeginSucceeded = true;
      lock (_stateGate)
        _run = run;
      error = null;
      return true;
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] macOS helper failed to begin capture. error=" + exception.Message);
      error = exception.Message;
      return false;
    }
  }

  public Task<CapturedMicrophoneRecording> EndRunAsync()
  {
    PendingRun run;
    lock (_stateGate)
    {
      run = _run;
      _run = null;
    }
    if (run == null)
      return Task.FromResult<CapturedMicrophoneRecording>(null);

    var completion = new TaskCompletionSource<CapturedMicrophoneRecording>(
      TaskCreationOptions.RunContinuationsAsynchronously
    );
    if (
      !QueueCommand(() =>
      {
        try
        {
          if (!run.BeginSucceeded)
          {
            DeleteTemp(run.TempPath);
            completion.TrySetResult(null);
            return;
          }
          JObject response = Send(new JObject { ["command"] = "end" });
          if ((long?)response["frameCount"] <= 0)
          {
            DeleteTemp(run.TempPath);
            completion.TrySetResult(null);
            return;
          }
          completion.TrySetResult(
            new CapturedMicrophoneRecording
            {
              RunId = run.RunId,
              TempPath = run.TempPath,
              DeviceId = (string)response["deviceId"],
              SampleRate = (int?)response["sampleRate"] ?? 48000,
              Channels = (int?)response["channels"] ?? 1,
              FrameCount = (long)response["frameCount"],
              CaptureStartOffsetUs = (long?)response["captureStartOffsetUs"] ?? 0,
            }
          );
        }
        catch (Exception exception)
        {
          Main.Instance?.Log("[Microphone] macOS helper failed to finalize capture. error=" + exception.Message);
          DeleteTemp(run.TempPath);
          completion.TrySetResult(null);
        }
      })
    )
    {
      DeleteTemp(run.TempPath);
      completion.TrySetResult(null);
    }
    return completion.Task;
  }

  public void Tick() { }

  public void Disarm()
  {
    bool shouldDisarm;
    int generation;
    PendingRun run;
    lock (_stateGate)
    {
      generation = ++_armGeneration;
      shouldDisarm = _armState == ArmState.Armed;
      _armState = ArmState.Idle;
      _armError = null;
      run = _run;
      _run = null;
    }
    if (shouldDisarm)
      QueueDisarm(generation, run?.TempPath);
    else if (!QueueCommand(() => DeleteTemp(run?.TempPath)))
      DeleteTemp(run?.TempPath);
  }

  public void Dispose()
  {
    if (_disposed)
      return;
    _disposed = true;
    lock (_stateGate)
    {
      ++_armGeneration;
      _armState = ArmState.Idle;
      _armError = null;
    }
    try
    {
      _client?.Close();
    }
    catch { }
    _commands.CompleteAdding();
  }

  private void CompleteArm(string deviceId, int generation)
  {
    string error = null;
    try
    {
      Send(new JObject { ["command"] = "arm", ["deviceId"] = deviceId == null ? JValue.CreateNull() : deviceId });
    }
    catch (Exception exception)
    {
      error = exception.Message;
    }

    bool stale;
    bool shouldDisarm;
    int currentGeneration;
    lock (_stateGate)
    {
      stale = generation != _armGeneration;
      currentGeneration = _armGeneration;
      shouldDisarm = stale && _armState == ArmState.Idle;
      if (!stale)
      {
        _armError = error;
        _armState = error == null ? ArmState.Armed : ArmState.Failed;
      }
    }

    if (stale)
    {
      if (error == null && shouldDisarm)
        QueueDisarm(currentGeneration);
      return;
    }

    Main.Instance?.Log(
      error == null ? "[Microphone] macOS microphone is armed." : "[Microphone] Arm failed. error=" + error
    );
  }

  private void QueueDisarm(int generation, string tempPath = null)
  {
    if (
      !QueueCommand(() =>
      {
        lock (_stateGate)
        {
          if (generation != _armGeneration || _armState != ArmState.Idle)
            return;
        }
        try
        {
          Send(new JObject { ["command"] = "disarm" });
        }
        catch (Exception exception)
        {
          Main.Instance?.Log("[Microphone] macOS helper disarm failed. error=" + exception.Message);
        }
        finally
        {
          DeleteTemp(tempPath);
        }
      })
    )
    {
      DeleteTemp(tempPath);
    }
  }

  private bool QueueCommand(Action command)
  {
    if (_disposed || _commands.IsAddingCompleted)
      return false;
    try
    {
      _commands.Add(command);
      return true;
    }
    catch (InvalidOperationException)
    {
      return false;
    }
  }

  private void ProcessCommands()
  {
    try
    {
      foreach (Action command in _commands.GetConsumingEnumerable())
      {
        try
        {
          command();
        }
        catch (Exception exception)
        {
          Main.Instance?.Log("[Microphone] macOS helper command failed. error=" + exception.Message);
        }
      }
    }
    finally
    {
      lock (_transportGate)
        ResetConnection();
    }
  }

  private void RefreshDevices()
  {
    try
    {
      JObject response = Send(new JObject { ["command"] = "devices" });
      var result = new List<MicrophoneDeviceInfo>();
      foreach (JToken item in response["devices"] ?? new JArray())
      {
        result.Add(
          new MicrophoneDeviceInfo
          {
            Id = (string)item["id"],
            Name = (string)item["name"],
            MinFrequency = (int?)item["minFrequency"] ?? 48000,
            MaxFrequency = (int?)item["maxFrequency"] ?? 48000,
          }
        );
      }
      lock (_stateGate)
      {
        _cachedDevices.Clear();
        _cachedDevices.AddRange(result);
      }
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] macOS device refresh failed. error=" + exception.Message);
    }
    finally
    {
      lock (_stateGate)
        _deviceRefreshQueued = false;
    }
  }

  private JObject Send(JObject command)
  {
    lock (_transportGate)
    {
      try
      {
        EnsureConnection();
        _writer.WriteLine(command.ToString(Formatting.None));
        _writer.Flush();
        string line = _reader.ReadLine();
        if (line == null)
          throw new IOException("macOS microphone helper closed its connection.");
        JObject response = JObject.Parse(line);
        if ((bool?)response["ok"] != true)
          throw new IOException((string)response["error"] ?? "macOS microphone helper rejected the command.");
        return response;
      }
      catch
      {
        ResetConnection();
        throw;
      }
    }
  }

  private void EnsureConnection()
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(MacOsMicrophoneCaptureBackend));
    if (_client != null)
      return;

    string appPath = Path.Combine(Main.Instance.PayloadPath, "Helpers", "mac", "TUFReplayMicrophoneCapture.app");
    if (!Directory.Exists(appPath))
      throw new DirectoryNotFoundException("macOS microphone helper app is missing: " + appPath);

    string token = Guid.NewGuid().ToString("N");
    var listener = new TcpListener(IPAddress.Loopback, 0);
    try
    {
      listener.Start(1);
      int port = ((IPEndPoint)listener.LocalEndpoint).Port;
      LaunchApp(appPath, port, token);

      IAsyncResult accept = listener.BeginAcceptTcpClient(null, null);
      if (!accept.AsyncWaitHandle.WaitOne(ConnectTimeoutMilliseconds))
        throw new TimeoutException("macOS microphone helper did not connect after LaunchServices started it.");

      TcpClient client = listener.EndAcceptTcpClient(accept);
      if (!IPAddress.IsLoopback(((IPEndPoint)client.Client.RemoteEndPoint).Address))
      {
        client.Dispose();
        throw new IOException("macOS microphone helper connection was not local.");
      }

      client.NoDelay = true;
      NetworkStream stream = client.GetStream();
      stream.ReadTimeout = ConnectTimeoutMilliseconds;
      var reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, true);
      var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
      string handshakeLine = reader.ReadLine();
      JObject handshake = handshakeLine == null ? null : JObject.Parse(handshakeLine);
      if (
        handshake == null
        || !string.Equals((string)handshake["token"], token, StringComparison.Ordinal)
        || (int?)handshake["protocolVersion"] != ProtocolVersion
      )
      {
        reader.Dispose();
        writer.Dispose();
        client.Dispose();
        throw new IOException("macOS microphone helper handshake was invalid.");
      }

      stream.ReadTimeout = CommandTimeoutMilliseconds;
      _client = client;
      _reader = reader;
      _writer = writer;
    }
    finally
    {
      listener.Stop();
    }
  }

  private static void LaunchApp(string appPath, int port, string token)
  {
    using var process = new Process
    {
      StartInfo = new ProcessStartInfo
      {
        FileName = "/usr/bin/open",
        Arguments = "-n " + QuoteArgument(appPath) + " --args --connect-port " + port + " --token " + token,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
      },
    };
    if (!process.Start())
      throw new IOException("Could not ask LaunchServices to start the macOS microphone helper.");
    if (!process.WaitForExit(ConnectTimeoutMilliseconds))
    {
      process.Kill();
      throw new TimeoutException("LaunchServices did not finish starting the macOS microphone helper.");
    }
    if (process.ExitCode != 0)
      throw new IOException(
        "LaunchServices failed to start the macOS microphone helper: " + process.StandardError.ReadToEnd()
      );
  }

  private static string QuoteArgument(string value)
  {
    return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
  }

  private void ResetConnection()
  {
    try
    {
      _reader?.Dispose();
    }
    catch { }
    try
    {
      _writer?.Dispose();
    }
    catch { }
    try
    {
      _client?.Dispose();
    }
    catch { }
    _reader = null;
    _writer = null;
    _client = null;
  }

  private static void DeleteTemp(string path)
  {
    try
    {
      if (!string.IsNullOrEmpty(path) && File.Exists(path))
        File.Delete(path);
    }
    catch { }
  }

  private sealed class PendingRun
  {
    public readonly string RunId;
    public readonly string TempPath;
    public bool BeginSucceeded;

    public PendingRun(string runId, string tempPath)
    {
      RunId = runId;
      TempPath = tempPath;
    }
  }

  private enum ArmState
  {
    Idle,
    Arming,
    Armed,
    Failed,
  }
}
