using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Shared.Capture;

// Each capture backend owns a connection and helper instance. All commands run
// on the recording worker; permission prompts and file finalization never block Unity.
internal sealed class MacOsCaptureHelperConnection : IDisposable
{
  private TcpClient _client;
  private StreamReader _reader;
  private StreamWriter _writer;
  private volatile bool _disposed;
  private readonly string _connectionId = Guid.NewGuid().ToString("N");
  private long _requestId;
  private int? _helperPid;
  private string _diagnosticsLogPath;

  public JObject Send(JObject command)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(MacOsCaptureHelperConnection));
    long requestId = Interlocked.Increment(ref _requestId);
    var elapsed = Stopwatch.StartNew();
    JObject response = null;
    CaptureDiagnostics.Record(
      "helper.command.begin",
      new
      {
        connectionId = _connectionId,
        requestId,
        command,
      }
    );
    try
    {
      EnsureConnection();
      _writer.WriteLine(command.ToString(Formatting.None));
      string line = _reader.ReadLine();
      response = line == null ? null : JObject.Parse(line);
      if ((bool?)response?["ok"] != true)
        throw new IOException((string)response?["error"] ?? "The camera helper disconnected. Restart the game.");
      CaptureDiagnostics.Record(
        "helper.command.complete",
        new
        {
          connectionId = _connectionId,
          requestId,
          elapsedMs = elapsed.Elapsed.TotalMilliseconds,
          helperPid = _helperPid,
          response,
        }
      );
      return response;
    }
    catch (Exception exception)
    {
      CaptureDiagnostics.Record(
        "helper.command.failed",
        new
        {
          connectionId = _connectionId,
          requestId,
          command,
          elapsedMs = elapsed.Elapsed.TotalMilliseconds,
          helperPid = _helperPid,
          diagnosticsLogPath = _diagnosticsLogPath,
          response,
        },
        exception
      );
      Reset();
      throw;
    }
  }

  private void EnsureConnection()
  {
    if (_client != null)
      return;
    string app = Path.Combine(Main.Instance.PayloadPath, "Helpers", "mac", "TUFReplayMicrophoneCapture.app");
    string binary = Path.Combine(app, "Contents", "MacOS", "TUFReplayMicrophoneCapture");
    CaptureDiagnostics.Record(
      "helper.launch.begin",
      new
      {
        connectionId = _connectionId,
        app,
        appExists = Directory.Exists(app),
        binaryExists = File.Exists(binary),
        binaryModifiedUtc = File.Exists(binary) ? File.GetLastWriteTimeUtc(binary).ToString("O") : null,
        connectTimeoutMs = 10000,
        responseTimeoutMs = 150000,
      }
    );
    if (!Directory.Exists(app))
      throw new DirectoryNotFoundException("The camera helper is missing. Reinstall TUFReplay.");
    string token = Guid.NewGuid().ToString("N");
    var listener = new TcpListener(IPAddress.Loopback, 0);
    try
    {
      listener.Start(1);
      int port = ((IPEndPoint)listener.LocalEndpoint).Port;
      using var launch = new Process
      {
        StartInfo = new ProcessStartInfo
        {
          FileName = "/usr/bin/open",
          Arguments =
            "-n " + CaptureProcessArguments.Quote(app) + " --args --connect-port " + port + " --token " + token,
          UseShellExecute = false,
          RedirectStandardError = true,
          CreateNoWindow = true,
        },
      };
      var launchError = new StringBuilder();
      launch.ErrorDataReceived += (_, args) =>
      {
        if (args.Data != null)
          lock (launchError)
            if (launchError.Length < 4096)
              launchError.AppendLine(args.Data);
      };
      if (!launch.Start())
        throw new IOException("The camera helper could not start. Restart the game.");
      launch.BeginErrorReadLine();
      bool exited = launch.WaitForExit(10000);
      if (exited)
        launch.WaitForExit();
      string stderr;
      lock (launchError)
        stderr = launchError.ToString();
      CaptureDiagnostics.Record(
        "helper.launch.result",
        new
        {
          connectionId = _connectionId,
          launcherPid = launch.Id,
          exited,
          exitCode = exited ? (int?)launch.ExitCode : null,
          stderr,
        }
      );
      if (!exited || launch.ExitCode != 0)
        throw new IOException("The camera helper could not start. Restart the game.");
      IAsyncResult accept = listener.BeginAcceptTcpClient(null, null);
      using (accept.AsyncWaitHandle)
      {
        if (!accept.AsyncWaitHandle.WaitOne(10000))
          throw new TimeoutException("The camera helper did not connect. Restart the game.");
        _client = listener.EndAcceptTcpClient(accept);
      }
      if (_disposed)
        throw new ObjectDisposedException(nameof(MacOsCaptureHelperConnection));
      _client.NoDelay = true;
      NetworkStream stream = _client.GetStream();
      stream.ReadTimeout = 150000;
      _reader = new StreamReader(stream, new UTF8Encoding(false), false, 4096, true);
      _writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
      string line = _reader.ReadLine();
      var handshake = line == null ? null : JObject.Parse(line);
      _helperPid = (int?)handshake?["processId"];
      _diagnosticsLogPath = (string)handshake?["diagnosticsLogPath"];
      // Authentication tokens are never written to either diagnostic log.
      CaptureDiagnostics.Record(
        "helper.handshake",
        new
        {
          connectionId = _connectionId,
          received = handshake != null,
          tokenMatched = (string)handshake?["token"] == token,
          protocolVersion = (int?)handshake?["protocolVersion"],
          helperPid = _helperPid,
          diagnosticsLogPath = _diagnosticsLogPath,
        }
      );
      if ((string)handshake?["token"] != token || (int?)handshake?["protocolVersion"] != 3)
        throw new IOException("The camera helper is incompatible. Reinstall TUFReplay.");
    }
    finally
    {
      listener.Stop();
    }
  }

  public void Dispose()
  {
    _disposed = true;
    // Closing the socket wakes a worker that is waiting for permission or a reply.
    _client?.Close();
  }

  private void Reset()
  {
    CaptureDiagnostics.Record(
      "helper.connection.reset",
      new
      {
        connectionId = _connectionId,
        helperPid = _helperPid,
        disposed = _disposed,
      }
    );
    _reader?.Dispose();
    _writer?.Dispose();
    _client?.Dispose();
    _reader = null;
    _writer = null;
    _client = null;
  }
}
