using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;
using TUFReplay.Visual.Application;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;
using TUFReplay.Visual.Infrastructure.Api;
using TUFReplay.Visual.Infrastructure.Discovery;
using TUFReplay.Visual.Infrastructure.FileSystem;
using TUFReplay.Visual.Sources.DmNote;
using TUFReplay.Visual.Sources.ImplResourcePack;
using TUFReplay.Visual.Sources.JipperKeyViewer;
using TUFReplay.Visual.Sources.JipperResourcePack;

namespace TUFReplay.Visual.Composition;

/// <summary>Composition facade for the visual-preset feature.</summary>
public sealed class VisualPresetFeature : IDisposable
{
  private readonly VisualBundleFactory _bundles;
  private readonly VisualPresetService _presets;
  private readonly VisualSourceCatalog _sources;
  private readonly Func<SubmissionAccount> _account;
  private readonly VisualRegistrationJobs _registrations = new VisualRegistrationJobs();

  public VisualSourceLocator Locator { get; }

  public VisualPresetFeature(Func<SubmissionAccount> account, SubmissionRecordsClient records, VisualSourceRoots roots)
  {
    if (account == null)
      throw new ArgumentNullException(nameof(account));
    _account = account;
    if (records == null)
      throw new ArgumentNullException(nameof(records));
    var fileSystem = new PhysicalVisualFileSystem();
    Locator = new VisualSourceLocator(fileSystem, roots ?? throw new ArgumentNullException(nameof(roots)));
    var adapters = new VisualAdapterRegistry(
      new IVisualSourceAdapter[]
      {
        new JipperResourcePackVisualAdapter(fileSystem, Locator),
        new DmNoteVisualAdapter(fileSystem),
        new DmNoteVisualAdapter(fileSystem, VisualSource.ImplDmnote),
        new JipperKeyViewerVisualAdapter(fileSystem, Locator),
        new ImplResourcePackVisualAdapter(fileSystem, Locator),
      }
    );
    _bundles = new VisualBundleFactory(adapters);
    _sources = new VisualSourceCatalog(Locator);
    _presets = new VisualPresetService(account, new SubmissionVisualPresetGateway(records), _bundles);
  }

  public object Sources() => new { sources = _sources.List() };

  public Task<JObject> ListAsync(CancellationToken cancellation = default) => _presets.ListAsync(cancellation);

  public Task<JObject> RemoveAsync(string id, CancellationToken cancellation = default) =>
    _presets.RemoveAsync(id, cancellation);

  public Task<JObject> ImportAsync(
    string name,
    VisualKind kind,
    VisualSource source,
    string presetJson,
    CancellationToken cancellation = default,
    IReadOnlyList<VisualAssetUpload> uploads = null
  ) => _presets.ImportAsync(name, kind, source, presetJson, cancellation, uploads);

  public object Inspect(
    VisualKind kind,
    VisualSource source,
    string presetJson,
    IReadOnlyList<VisualAssetUpload> uploads
  )
  {
    var options = new VisualImportOptions { Inspect = true, Uploads = uploads ?? Array.Empty<VisualAssetUpload>() };
    VisualBundle bundle = BuildBundle(kind, source, presetJson, options);
    return new { missing_assets = options.MissingAssets, asset_count = bundle.Assets.Count };
  }

  public VisualBundle BuildBundle(
    VisualKind kind,
    VisualSource source,
    string presetJson = null,
    VisualImportOptions options = null
  ) => _bundles.Create(kind, source, presetJson, options);

  public object StartRegistration(
    string id,
    string name,
    VisualKind kind,
    VisualSource source,
    string presetJson,
    IReadOnlyList<VisualAssetUpload> uploads
  )
  {
    SubmissionAccount account = _account();
    return _registrations.Start(
      id,
      (progress, cancellation) =>
        _presets.RegisterAsync(account, name, kind, source, presetJson, uploads, progress, cancellation)
    );
  }

  public object RegistrationStatus(string id) => _registrations.Get(id);

  public void Dispose() => _registrations.Dispose();
}
