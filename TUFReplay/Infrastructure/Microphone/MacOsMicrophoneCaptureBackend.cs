using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Application.Microphone;
using TUFReplay.Domain.Microphone;

namespace TUFReplay.Infrastructure.Microphone;

public sealed class MacOsMicrophoneCaptureBackend : IMicrophoneCaptureBackend
{
  private const int ProtocolVersion = 1;
  private const int ConnectTimeoutMilliseconds = 10000;
  private const int CommandTimeoutMilliseconds = 300000;

  private readonly object _transportGate = new object();
  private readonly object _stateGate = new object();
  private readonly List<MicrophoneDeviceInfo> _cachedDevices = new List<MicrophoneDeviceInfo>();
  private TcpClient _client;
  private StreamReader _reader;
  private StreamWriter _writer;
  private ArmState _armState;
  private string _armError;
  private int _armGeneration;
  private string _runId;
  private string _tempPath;

  public void RequestPermission()
  {
    ThreadPool.QueueUserWorkItem(_ =>
    {
      try
      {
        Send(new JObject { ["command"] = "authorize" });
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
      if (_armState == ArmState.Arming)
        return new List<MicrophoneDeviceInfo>(_cachedDevices);
    }

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
    return result;
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
    ThreadPool.QueueUserWorkItem(_ => CompleteArm(deviceId, generation));
    Main.Instance?.Log("[Microphone] Arming macOS microphone.");
    error = null;
    return true;
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

    try
    {
      Send(
        new JObject
        {
          ["command"] = "begin",
          ["runId"] = runId,
          ["path"] = tempPath,
        }
      );
      _runId = runId;
      _tempPath = tempPath;
      error = null;
      return true;
    }
    catch (Exception exception)
    {
      error = exception.Message;
      return false;
    }
  }

  public CapturedMicrophoneRecording EndRun()
  {
    if (_runId == null)
      return null;
    try
    {
      JObject response = Send(new JObject { ["command"] = "end" });
      if ((long?)response["frameCount"] <= 0)
      {
        DeleteTemp();
        return null;
      }
      return new CapturedMicrophoneRecording
      {
        RunId = _runId,
        TempPath = _tempPath,
        DeviceId = (string)response["deviceId"],
        SampleRate = (int?)response["sampleRate"] ?? 48000,
        Channels = (int?)response["channels"] ?? 1,
        FrameCount = (long)response["frameCount"],
        CaptureStartOffsetUs = (long?)response["captureStartOffsetUs"] ?? 0,
      };
    }
    catch (Exception exception)
    {
      Main.Instance?.Log("[Microphone] macOS helper failed to finalize capture. error=" + exception.Message);
      DeleteTemp();
      return null;
    }
    finally
    {
      _runId = null;
      _tempPath = null;
    }
  }

  public void Tick() { }

  public void Disarm()
  {
    bool shouldDisarm;
    int generation;
    lock (_stateGate)
    {
      generation = ++_armGeneration;
      shouldDisarm = _armState == ArmState.Armed;
      _armState = ArmState.Idle;
      _armError = null;
    }
    _runId = null;
    DeleteTemp();
    _tempPath = null;
    if (shouldDisarm)
      QueueDisarm(generation);
  }

  public void Dispose()
  {
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
    lock (_transportGate)
      ResetConnection();
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

  private void QueueDisarm(int generation)
  {
    ThreadPool.QueueUserWorkItem(_ =>
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
    });
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
    if (_client != null)
      return;

    string appPath = Path.Combine(Main.Instance.Path, "Helpers", "mac", "TUFReplayMicrophoneCapture.app");
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

  private void DeleteTemp()
  {
    try
    {
      if (!string.IsNullOrEmpty(_tempPath) && File.Exists(_tempPath))
        File.Delete(_tempPath);
    }
    catch { }
  }

  private enum ArmState
  {
    Idle,
    Arming,
    Armed,
    Failed,
  }
}
