using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;
using TUFReplay.Visual.Application;
using TUFReplay.Visual.Application.Abstractions;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;

internal static class VisualRegistrationSuite
{
  public static void RunAll()
  {
    BackgroundJobsRemainObservable().GetAwaiter().GetResult();
    RegistrationBuildsOnlyOnce().GetAwaiter().GetResult();
  }

  private static async Task BackgroundJobsRemainObservable()
  {
    using var jobs = new VisualRegistrationJobs();
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    string id = Guid.NewGuid().ToString();
    int calls = 0;
    Func<Action<VisualImportProgress>, CancellationToken, Task<JObject>> work = async (report, token) =>
    {
      Interlocked.Increment(ref calls);
      report(new VisualImportProgress("processing_assets", "cjk.otf", 2));
      started.SetResult(true);
      await release.Task;
      return new JObject { ["state"] = "completed" };
    };
    jobs.Start(id, work);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
    jobs.Start(id, work);
    JObject status = jobs.Get(id);
    TestFixture.Assert(
      (string)status["state"] == "running" && (string)status["progress"]["asset_name"] == "cjk.otf",
      "Progress must remain readable while the background worker is blocked."
    );
    status["progress"]["stage"] = "tampered";
    TestFixture.Assert(
      (string)jobs.Get(id)["progress"]["stage"] == "processing_assets",
      "Progress snapshots must be isolated."
    );
    release.SetResult(true);
    await WaitUntilFinished(jobs, id);
    TestFixture.Assert(calls == 1, "Repeated starts must not register the preset twice.");
    string failed = Guid.NewGuid().ToString();
    jobs.Start(failed, (_, _) => throw new VisualImportException("visual_asset_missing", "missing"));
    await WaitUntilFinished(jobs, failed);
    TestFixture.Assert(
      (string)jobs.Get(failed)["error"]["code"] == "visual_asset_missing",
      "Background failures must retain their domain error."
    );
  }

  private static async Task WaitUntilFinished(VisualRegistrationJobs jobs, string id)
  {
    for (int retry = 0; retry < 1000; retry++)
    {
      if ((string)jobs.Get(id)["state"] != "running")
        return;
      await Task.Delay(5);
    }
    throw new Exception("Registration job did not finish.");
  }

  private static async Task RegistrationBuildsOnlyOnce()
  {
    var account = new SubmissionAccount(new Uri("https://example.test"), _ => Task.FromResult("token"));
    var adapter = new Adapter();
    var gateway = new Gateway();
    var service = new VisualPresetService(
      () => account,
      gateway,
      new VisualBundleFactory(new VisualAdapterRegistry(new[] { adapter }))
    );
    var stages = new List<string>();
    JObject result = await service.RegisterAsync(
      account,
      "test",
      VisualKind.Keyviewer,
      adapter.Source,
      null,
      null,
      progress => stages.Add(progress.Stage),
      CancellationToken.None
    );
    TestFixture.Assert(
      (string)result["state"] == "completed" && adapter.Calls == 1 && gateway.Calls == 1,
      "Registration must inspect and upload one bundle without rebuilding fonts."
    );
    TestFixture.Assert(
      stages.SequenceEqual(new[] { "reading_settings", "processing_assets", "validating", "uploading" }),
      "Progress must follow actual settings, assets, validation and server work."
    );
    JObject renamed = await service.RenameAsync(" registered ", " 새 이름 ");
    TestFixture.Assert(
      (string)renamed["preset"]["name"] == "새 이름"
        && gateway.RenameCalls == 1
        && gateway.RenamedId == "registered"
        && adapter.Calls == 1,
      "Renaming must use the existing account and preset ID without rebuilding its bundle."
    );
    try
    {
      await service.RenameAsync("registered", " ");
      throw new Exception("Empty rename must be rejected before reaching the server.");
    }
    catch (VisualImportException error)
    {
      TestFixture.Assert(
        error.Code == "visual_name_required" && gateway.RenameCalls == 1,
        "Rename must validate the name."
      );
    }
    adapter.Missing = true;
    result = await service.RegisterAsync(
      account,
      "test",
      VisualKind.Keyviewer,
      adapter.Source,
      null,
      null,
      _ => { },
      CancellationToken.None
    );
    TestFixture.Assert(
      (string)result["state"] == "needs_assets" && gateway.Calls == 1,
      "Missing assets must return attachment requirements without contacting the server."
    );
    adapter.Missing = false;
    SubmissionAccount previous = account;
    account = new SubmissionAccount(new Uri("https://other.test"), _ => Task.FromResult("other"));
    try
    {
      await service.RegisterAsync(
        previous,
        "test",
        VisualKind.Keyviewer,
        adapter.Source,
        null,
        null,
        _ => { },
        CancellationToken.None
      );
      throw new Exception("An account switch must block registration.");
    }
    catch (VisualImportException error)
    {
      TestFixture.Assert(
        error.Code == "account_changed" && gateway.Calls == 1,
        "Account identity must be checked before upload."
      );
    }
  }

  private sealed class Adapter : IVisualSourceAdapter
  {
    public VisualSource Source => VisualSource.JipperKeyviewer;
    public int Calls;
    public bool Missing;

    public VisualBundle Import(VisualKind kind, string presetJson = null, VisualImportOptions options = null)
    {
      Calls++;
      options.Progress?.Invoke(new VisualImportProgress("processing_assets", "cjk.otf"));
      if (Missing)
        options.MissingAssets.Add(new VisualAssetRequirement { Reference = "missing.otf", Kind = "font" });
      options.Progress?.Invoke(new VisualImportProgress("validating"));
      return new VisualBundle { Assets = new List<VisualAsset>() };
    }
  }

  private sealed class Gateway : IVisualPresetGateway
  {
    public int Calls;
    public int RenameCalls;
    public string RenamedId;

    public Task<JObject> CreateAsync(SubmissionAccount account, JObject body, CancellationToken cancellation)
    {
      Calls++;
      return Task.FromResult(new JObject { ["preset"] = new JObject { ["id"] = "registered" } });
    }

    public Task<JObject> ListAsync(SubmissionAccount account, CancellationToken cancellation) =>
      throw new NotSupportedException();

    public Task<JObject> RenameAsync(SubmissionAccount account, string id, string name, CancellationToken cancellation)
    {
      RenameCalls++;
      RenamedId = id;
      return Task.FromResult(
        new JObject
        {
          ["preset"] = new JObject { ["id"] = id, ["name"] = name },
        }
      );
    }

    public Task<JObject> RemoveAsync(SubmissionAccount account, string id, CancellationToken cancellation) =>
      throw new NotSupportedException();
  }
}
