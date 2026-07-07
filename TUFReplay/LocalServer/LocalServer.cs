using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using JALib.Tools;
using Newtonsoft.Json;
using TUFReplay.LocalServer.Controllers;
using TUFReplay.LocalServer.Http;
using TUFReplay.LocalServer.Routing;

namespace TUFReplay.LocalServer;

public class LocalServer
{
  private readonly HttpListener _listener = new HttpListener();
  private readonly LocalRouter _router = LocalApi.CreateRouter();
  private Task _listenTask;
  private bool _running;

  public string Url { get; } = "http://127.0.0.1:32145/";

  public void Start()
  {
    if (_running) return;

    if (_listener.Prefixes.Count == 0) _listener.Prefixes.Add(Url);

    _listener.Start();
    _running = true;
    _listenTask = JATask.Run(new JAction(Main.Instance, ListenLoop));

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
      _listenTask?.Wait(500);
    }
    catch
    {
    }

    _listenTask = null;
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
      object result = _router.Dispatch(context);
      ResponseWriter.Write(context, result);
    }
    catch (Exception e)
    {
      Main.Instance?.LogException(e);
      ResponseWriter.WriteError(context, e);
    }
  }
}
