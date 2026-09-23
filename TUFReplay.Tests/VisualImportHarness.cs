using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;
using TUFReplay.Visual.Infrastructure.Discovery;
using TUFReplay.Visual.Infrastructure.FileSystem;
using TUFReplay.Visual.Sources.DmNote;
using TUFReplay.Visual.Sources.ImplResourcePack;
using TUFReplay.Visual.Sources.JipperKeyViewer;
using TUFReplay.Visual.Sources.JipperResourcePack;

// Test-only stdin bridge. It exercises the same adapters as the Unity IPC
// without starting the game or changing its installed files.
internal static class VisualImportHarness
{
  public static int Run()
  {
    try
    {
      JObject input = JObject.Parse(Console.In.ReadToEnd());
      var options = new VisualImportOptions
      {
        Inspect = (bool?)input["inspect"] ?? false,
        Uploads = input["assets"]?.ToObject<VisualAssetUpload[]>() ?? Array.Empty<VisualAssetUpload>(),
      };
      var files = new PhysicalVisualFileSystem();
      var paths = new[] { (string)input["installation"] ?? "" };
      var roots = new VisualSourceRoots(paths, paths, paths);
      var locator = new VisualSourceLocator(files, roots);
      string source = (string)input["source"];
      IVisualSourceAdapter adapter = source switch
      {
        "dmnote" => new DmNoteVisualAdapter(files),
        "impl-dmnote" => new DmNoteVisualAdapter(files, VisualSource.ImplDmnote),
        "jipper-keyviewer" => new JipperKeyViewerVisualAdapter(files, locator),
        "impl-resourcepack" => new ImplResourcePackVisualAdapter(files, locator),
        "jipper-resourcepack" => new JipperResourcePackVisualAdapter(files, locator),
        _ => throw new ArgumentException("Unsupported source"),
      };
      var bundle = adapter.Import(
        (string)input["kind"] == "overlay" ? VisualKind.Overlay : VisualKind.Keyviewer,
        (string)input["presetJson"],
        options
      );
      object result = options.Inspect
        ? new { missing_assets = options.MissingAssets, asset_count = bundle.Assets.Count }
        : bundle;
      Console.WriteLine(JsonConvert.SerializeObject(result));
      return 0;
    }
    catch (Exception exception)
    {
      Console.WriteLine(
        JsonConvert.SerializeObject(
          new
          {
            error = new
            {
              code = (exception as VisualImportException)?.Code ?? "visual_bundle_invalid",
              message = exception.Message,
            },
          }
        )
      );
      return 0;
    }
  }
}
