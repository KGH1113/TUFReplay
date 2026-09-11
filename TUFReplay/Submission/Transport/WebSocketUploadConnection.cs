using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Protocol;

namespace TUFReplay.Submission.Transport;

public sealed class WebSocketUploadConnection : IUploadConnection
{
  private readonly Uri _url;
  private readonly ClientWebSocket _socket = new ClientWebSocket();

  public WebSocketUploadConnection(Uri url, string token)
  {
    _url = url;
    _socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
  }

  public async Task Connect(CancellationToken cancellation)
  {
    using var timeout = Deadline(cancellation);
    await _socket.ConnectAsync(_url, timeout.Token).ConfigureAwait(false);
  }

  public Task SendControl(object control, CancellationToken cancellation) =>
    Send(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(control)), WebSocketMessageType.Text, cancellation);

  public Task SendFrame(UploadFrame frame, CancellationToken cancellation) =>
    Send(frame.Bytes, WebSocketMessageType.Binary, cancellation);

  private async Task Send(byte[] bytes, WebSocketMessageType kind, CancellationToken cancellation)
  {
    using var timeout = Deadline(cancellation);
    await _socket.SendAsync(new ArraySegment<byte>(bytes), kind, true, timeout.Token).ConfigureAwait(false);
  }

  public async Task<JObject> Receive(CancellationToken cancellation)
  {
    using var timeout = Deadline(cancellation);
    var bytes = new byte[4096];
    int total = 0;
    WebSocketReceiveResult message;
    do
    {
      if (total == bytes.Length) throw new InvalidDataException("Oversized upload control.");
      message = await _socket.ReceiveAsync(new ArraySegment<byte>(bytes, total, bytes.Length - total), timeout.Token).ConfigureAwait(false);
      if (message.MessageType != WebSocketMessageType.Text) throw new IOException("Upload socket closed or sent invalid control.");
      total += message.Count;
    } while (!message.EndOfMessage);
    return JObject.Parse(Encoding.UTF8.GetString(bytes, 0, total));
  }

  private static CancellationTokenSource Deadline(CancellationToken cancellation)
  {
    var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
    source.CancelAfter(TimeSpan.FromSeconds(8));
    return source;
  }

  public void Dispose() => _socket.Dispose();
}
