using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;
using TUFReplay.Visual.Importing;

namespace TUFReplay.Visual.Application;

/// <summary>Short IPC calls observe background work; the IPC listener must never wait for fonts or uploads.</summary>
public sealed class VisualRegistrationJobs : IDisposable
{
  private sealed class Job
  {
    public JObject Snapshot = new JObject
    {
      ["state"] = "running",
      ["progress"] = JObject.FromObject(new VisualImportProgress("reading_settings")),
    };
    public DateTime? FinishedAt;
  }

  private readonly object _gate = new object();
  private readonly Dictionary<string, Job> _jobs = new Dictionary<string, Job>();
  private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
  private bool _disposed;

  public object Start(string id, Func<Action<VisualImportProgress>, CancellationToken, Task<JObject>> work)
  {
    if (!Guid.TryParseExact(id, "D", out _))
      throw new VisualImportException("visual_bundle_invalid", "The registration request is invalid.");
    Job job;
    lock (_gate)
    {
      if (_disposed)
        throw new ObjectDisposedException(nameof(VisualRegistrationJobs));
      foreach (
        string expired in _jobs
          .Where(pair => pair.Value.FinishedAt < DateTime.UtcNow.AddMinutes(-5))
          .Select(pair => pair.Key)
          .ToArray()
      )
        _jobs.Remove(expired);
      // Retrying the same start must never create a second server preset.
      if (_jobs.ContainsKey(id))
        return new { operation_id = id };
      if (_jobs.Count >= 32 || _jobs.Count(pair => !pair.Value.FinishedAt.HasValue) >= 2)
        throw new VisualImportException(
          "visual_registration_busy",
          "Another preset is being registered. Wait for it to finish and retry."
        );
      job = new Job();
      _jobs.Add(id, job);
    }
    _ = Task.Run(async () =>
    {
      JObject result;
      try
      {
        result = await work(
            progress =>
            {
              _shutdown.Token.ThrowIfCancellationRequested();
              lock (_gate)
                job.Snapshot["progress"] = JObject.FromObject(progress);
            },
            _shutdown.Token
          )
          .ConfigureAwait(false);
      }
      catch (Exception exception)
      {
        string code =
          (exception as VisualImportException)?.Code
          ?? (exception as SubmissionRequestException)?.Code
          ?? "visual_request_failed";
        result = new JObject
        {
          ["state"] = "failed",
          ["error"] = new JObject { ["code"] = code },
        };
      }
      lock (_gate)
      {
        result["progress"] = job.Snapshot["progress"].DeepClone();
        job.Snapshot = result;
        job.FinishedAt = DateTime.UtcNow;
      }
    });
    return new { operation_id = id };
  }

  public JObject Get(string id)
  {
    lock (_gate)
    {
      if (id == null || !_jobs.TryGetValue(id, out Job job))
        throw new VisualImportException(
          "visual_registration_expired",
          "The registration status is unavailable. Refresh the preset list before retrying."
        );
      return (JObject)job.Snapshot.DeepClone();
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      _disposed = true;
      _jobs.Clear();
    }
    _shutdown.Cancel();
  }
}
