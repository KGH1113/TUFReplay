using System;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Submission.Api;

namespace TUFReplay.Submission.Transport;

/// <summary>Separate account-authenticated socket survives individual run uploads.</summary>
public sealed class LevelChangeListener : IDisposable
{
  private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
  private int _changed;
  public bool TakeChange() => Interlocked.Exchange(ref _changed, 0) != 0;

  public LevelChangeListener(SubmissionAccount account, int levelId)
  {
    var url = new UriBuilder(account.Server) {
      Scheme = account.Server.Scheme == "https" ? "wss" : "ws", Path = $"/api/v1/levels/{levelId}/changes"
    }.Uri;
    _ = Task.Run(async () => {
      long? generation = null;
      while (!_cancellation.IsCancellationRequested)
      {
        try
        {
          using var socket = new WebSocketUploadConnection(url, await account.AccessToken(_cancellation.Token).ConfigureAwait(false));
          await socket.Connect(_cancellation.Token).ConfigureAwait(false);
          while (!_cancellation.IsCancellationRequested)
          {
            var control = await socket.Receive(_cancellation.Token).ConfigureAwait(false);
            if ((string)control["type"] != "level_state" || (int?)control["level_id"] != levelId)
              throw new InvalidOperationException("Invalid level change response.");
            long next = (long)control["generation"];
            if (generation.HasValue && generation.Value != next) Interlocked.Exchange(ref _changed, 1);
            generation = next;
          }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { return; }
        catch (Exception) { }
        try { await Task.Delay(5000, _cancellation.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }
      }
    });
  }

  public void Dispose() => _cancellation.Cancel();
}
