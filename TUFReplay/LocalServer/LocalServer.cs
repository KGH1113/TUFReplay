using System;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace TUFReplay.LocalServer;

public class LocalServer
{
  private readonly HttpListener _listener = new HttpListener();
  private Thread _thread;
  private bool _running;

  public string Url { get; } = "http://127.0.0.1:32145/";

  public void Start()
  {
    if (_running) return;

    if (_listener.Prefixes.Count == 0) _listener.Prefixes.Add(Url);

    _listener.Start();
    _running = true;
    _thread = new Thread(ListenLoop)
    {
      Name = "TUFReplay Local HTTP Server",
      IsBackground = true
    };
    _thread.Start();

    Main.Instance.Log("Local server started: " + Url);
  }

  public void Stop()
  {
    _running = false;

    try
    {
      _listener.Stop();
      _listener.Close();
    }
    catch
    {
    }

    try
    {
      _thread?.Join(500);
    }
    catch
    {
    }

    _thread = null;
  }

  private void ListenLoop()
  {
    while (_running)
    {
      try
      {
        HttpListenerContext context = _listener.GetContext();
        Handle(context);
      }
      catch (HttpListenerException)
      {
        return;
      }
      catch (ObjectDisposedException)
      {
        return;
      }
      catch (Exception e)
      {
        Main.Instance?.LogException(e);
      }
    }
  }

  private void Handle(HttpListenerContext context)
  {
    try
    {
      LocalApi.Handle(context);
    }
    catch (Exception e)
    {
      Main.Instance?.LogException(e);
      WriteJson(context.Response, 500, new { error = "internal_error" });
    }
  }

  public static void WriteJson(HttpListenerResponse response, int statusCode, object body)
  {
    string json = JsonConvert.SerializeObject(body);
    byte[] bytes = Encoding.UTF8.GetBytes(json);

    response.StatusCode = statusCode;
    response.ContentType = "application/json; charset=utf-8";
    response.ContentLength64 = bytes.Length;
    response.OutputStream.Write(bytes, 0, bytes.Length);
    response.OutputStream.Close();
  }
}
