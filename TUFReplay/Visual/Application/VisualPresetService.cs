using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;
using TUFReplay.Visual.Application.Abstractions;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;

namespace TUFReplay.Visual.Application;

public sealed class VisualPresetService
{
  private readonly Func<SubmissionAccount> _account;
  private readonly IVisualPresetGateway _gateway;
  private readonly VisualBundleFactory _bundles;

  public VisualPresetService(Func<SubmissionAccount> account, IVisualPresetGateway gateway, VisualBundleFactory bundles)
  {
    _account = account ?? throw new ArgumentNullException(nameof(account));
    _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
    _bundles = bundles ?? throw new ArgumentNullException(nameof(bundles));
  }

  public Task<JObject> ListAsync(CancellationToken cancellation = default)
  {
    return _gateway.ListAsync(_account(), cancellation);
  }

  public Task<JObject> RemoveAsync(string id, CancellationToken cancellation = default)
  {
    if (string.IsNullOrWhiteSpace(id))
      throw new VisualImportException("visual_preset_not_found", "The visual preset was not found.");
    return _gateway.RemoveAsync(_account(), id.Trim(), cancellation);
  }

  public Task<JObject> ImportAsync(
    string name,
    VisualKind kind,
    VisualSource source,
    string presetJson,
    CancellationToken cancellation = default,
    IReadOnlyList<VisualAssetUpload> uploads = null
  )
  {
    // Capture the account before disk I/O. An account switch during import
    // must never create the snapshot for the replacement account.
    SubmissionAccount account = _account();
    if (account == null)
      throw new VisualImportException("login_required", "Sign in before importing a visual preset.");
    string cleanName = ValidateName(name);
    VisualBundle bundle = _bundles.Create(
      kind,
      source,
      presetJson,
      new VisualImportOptions { Uploads = uploads ?? Array.Empty<VisualAssetUpload>() }
    );
    var body = new JObject { ["name"] = cleanName, ["bundle"] = JObject.FromObject(bundle) };
    if (Encoding.UTF8.GetByteCount(body.ToString(Formatting.None)) > VisualImportLimits.MaxRequestBytes)
      throw new VisualImportException(
        "visual_payload_too_large",
        "The visual preset exceeds the permitted request size."
      );
    if (!ReferenceEquals(account, _account()))
      throw new VisualImportException(
        "account_changed",
        "The signed-in account changed while the visual preset was being imported."
      );
    return _gateway.CreateAsync(account, body, cancellation);
  }

  public async Task<JObject> RegisterAsync(
    SubmissionAccount account,
    string name,
    VisualKind kind,
    VisualSource source,
    string presetJson,
    IReadOnlyList<VisualAssetUpload> uploads,
    Action<VisualImportProgress> progress,
    CancellationToken cancellation
  )
  {
    if (account == null)
      throw new VisualImportException("login_required", "Sign in before importing a visual preset.");
    string cleanName = ValidateName(name);
    var options = new VisualImportOptions
    {
      Inspect = true,
      Uploads = uploads ?? Array.Empty<VisualAssetUpload>(),
      Progress = progress,
    };
    VisualBundle bundle = _bundles.Create(kind, source, presetJson, options);
    if (options.MissingAssets.Count > 0)
      return new JObject { ["state"] = "needs_assets", ["missing_assets"] = JArray.FromObject(options.MissingAssets) };

    // Inspection and registration use the same snapshot, including large CJK fonts.
    var body = new JObject { ["name"] = cleanName, ["bundle"] = JObject.FromObject(bundle) };
    if (Encoding.UTF8.GetByteCount(body.ToString(Formatting.None)) > VisualImportLimits.MaxRequestBytes)
      throw new VisualImportException(
        "visual_payload_too_large",
        "The visual preset exceeds the permitted request size."
      );
    cancellation.ThrowIfCancellationRequested();
    if (!ReferenceEquals(account, _account()))
      throw new VisualImportException(
        "account_changed",
        "The signed-in account changed while the visual preset was being imported."
      );
    progress(new VisualImportProgress("uploading", completedAssets: bundle.Assets.Count));
    JObject response = await _gateway.CreateAsync(account, body, cancellation).ConfigureAwait(false);
    return new JObject { ["state"] = "completed", ["preset"] = response["preset"]?.DeepClone() };
  }

  private static string ValidateName(string name)
  {
    string value = name?.Trim();
    if (string.IsNullOrWhiteSpace(value) || value.Length > VisualImportLimits.MaxNameLength)
      throw new VisualImportException("visual_name_required", "Enter a name for the visual preset.");
    return value;
  }
}
