using System;
using System.Threading;
using System.Threading.Tasks;
using TUFReplay.Submission.Api;
using TUFReplay.Submission.Catalog;

namespace TUFReplay.Submission.Sessions;

/// <summary>Background installation lookup and issuance; publication is cancellation guarded.</summary>
public sealed class SubmissionPreflight : IDisposable
{
  private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
  private IssuedRun _ready;
  private string _error;
  public string Error => Volatile.Read(ref _error);
  private int _finished;
  public bool Finished => Volatile.Read(ref _finished) != 0;

  public SubmissionPreflight(
    RunIssuanceClient api,
    SubmissionAccount account,
    string path,
    int levelId,
    string gameVersion,
    string modVersion,
    byte[] submissionGameplayHash = null
  )
  {
    byte[] hash = submissionGameplayHash == null ? null : (byte[])submissionGameplayHash.Clone();
    _ = Task.Run(async () =>
    {
      try
      {
        var level = InstalledLevelReader.Read(path, levelId);
        var run = await api.Issue(account, level, gameVersion, modVersion, _cancellation.Token, hash)
          .ConfigureAwait(false);
        if (!_cancellation.IsCancellationRequested)
          Volatile.Write(ref _ready, run);
      }
      catch (OperationCanceledException) { }
      catch (SubmissionRequestException exception)
      {
        Volatile.Write(ref _error, exception.Code);
      }
      catch (Exception)
      {
        Volatile.Write(ref _error, "level_preparation_failed");
      }
      finally
      {
        Volatile.Write(ref _finished, 1);
      }
    });
  }

  public IssuedRun Take() => Interlocked.Exchange(ref _ready, null);

  public bool IsReady => Volatile.Read(ref _ready)?.HasRemainingLease(TimeSpan.FromSeconds(5)) == true;

  public void Dispose() => _cancellation.Cancel();
}
