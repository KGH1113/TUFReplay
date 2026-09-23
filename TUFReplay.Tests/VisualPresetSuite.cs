using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Infrastructure.Discovery;
using TUFReplay.Visual.Infrastructure.FileSystem;
using TUFReplay.Visual.Sources.DmNote;
using TUFReplay.Visual.Sources.ImplResourcePack;
using TUFReplay.Visual.Sources.JipperKeyViewer;
using TUFReplay.Visual.Sources.JipperResourcePack;

internal static class VisualPresetSuite
{
  public static void RunAll()
  {
    string root = Path.Combine(Path.GetTempPath(), "tufreplay-visual-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      SourceDefinitionsAreCanonical();
      VisualRegistrationSuite.RunAll();
      AdditionalVisualSourcesSuite.Run(root);
      VisualPortabilitySuite.Run();
      JipperResourcePackSettingsAreSnapshotAndSanitized(root);
      SymlinkedVisualAssetsAreRejected(root);
      SvgNamespaceIsAcceptedAndUnsafeSvgIsRejected(root);
      AutomaticModDiscovery(root);
      DmNoteSingleTabPreservesEmbeddedIdentity(root);
      DmNoteImportsSavedTab();
      DmNotePlacementScaleValidation();
      DmNoteRejectsMultipleOrMissingAssets(root);
      AuthenticatedVisualRequestPreservesServerErrors();
      Console.WriteLine("Visual preset import tests passed.");
    }
    finally
    {
      if (Directory.Exists(root))
        Directory.Delete(root, true);
    }
  }

  private static void SourceDefinitionsAreCanonical()
  {
    var wireNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (VisualSourceDefinition definition in VisualSourceDefinitions.All)
    {
      TestFixture.Assert(wireNames.Add(definition.WireName), "Visual source wire names must be unique.");
      TestFixture.Assert(definition.Kinds.Count > 0, "Every visual source must declare a supported kind.");
      TestFixture.Assert(
        VisualSourceDefinitions.TryParse(definition.WireName, out VisualSource parsed) && parsed == definition.Source,
        "Visual source wire names must round-trip through the canonical catalog."
      );
    }
    foreach (VisualKind kind in new[] { VisualKind.Keyviewer, VisualKind.Overlay })
      TestFixture.Assert(
        VisualKindNames.TryParse(VisualKindNames.ToWire(kind), out VisualKind parsed) && parsed == kind,
        "Visual kind wire names must round-trip."
      );
  }

  private static void JipperResourcePackSettingsAreSnapshotAndSanitized(string root)
  {
    string install = Path.Combine(root, "JipperResourcePack");
    Directory.CreateDirectory(Path.Combine(install, "Images"));
    Directory.CreateDirectory(Path.Combine(install, "Fonts"));
    File.WriteAllText(Path.Combine(install, "Info.json"), "{\"Id\":\"JipperResourcePack\",\"Version\":\"1.5.2.0\"}");
    File.WriteAllBytes(Path.Combine(install, "Images", "test.png"), PngBytes());
    File.WriteAllBytes(Path.Combine(install, "Fonts", "test.ttf"), TtfBytes());
    File.WriteAllText(
      Path.Combine(install, "Settings.json"),
      "{\"Setting\":{\"width\":1280,\"height\":720,\"FontName\":\"Fonts/test.ttf\"},\"Feature\":{"
        + "\"KeyViewer\":{\"Enabled\":true,\"Setting\":{\"FontName\":\"Fonts/test.ttf\",\"Image\":\"Images/test.png\",\"customJS\":{\"content\":\"void 0\"}}},"
        + "\"Status\":{\"Enabled\":true,\"Setting\":{\"FontName\":\"Arial\"}},"
        + "\"ResourceChanger\":{\"Enabled\":false,\"Setting\":{\"FontName\":\"Arial\"}}}}"
    );

    var fileSystem = new PhysicalVisualFileSystem();
    var roots = new VisualSourceRoots(new[] { install });
    var locator = new VisualSourceLocator(fileSystem, roots);
    VisualBundle bundle = new JipperResourcePackVisualAdapter(fileSystem, locator).Import(VisualKind.Keyviewer);

    TestFixture.Assert(bundle.Source == "jipper-resourcepack", "Jipper Resourcepack wire identity was not preserved.");
    TestFixture.Assert(bundle.SourceVersion == "1.5.2.0", "JipperResourcePack metadata version was not preserved.");
    TestFixture.Assert(
      bundle.Viewport.Width == 1280 && bundle.Viewport.Height == 720,
      "JipperResourcePack saved viewport was not captured."
    );
    TestFixture.Assert(
      bundle.Files.ContainsKey("KeyViewer.json"),
      "JipperResourcePack keyviewer logical file is missing."
    );
    string json = ((JToken)bundle.Files["KeyViewer.json"]).ToString(Newtonsoft.Json.Formatting.None);
    TestFixture.Assert(
      !json.Contains("customJS", StringComparison.Ordinal),
      "JipperResourcePack JavaScript was not stripped."
    );
    TestFixture.Assert(
      json.Contains("\"Feature\"", StringComparison.Ordinal),
      "JipperResourcePack feature namespace was not preserved."
    );
    TestFixture.Assert(
      json.Contains("\"KeyViewer\"", StringComparison.Ordinal),
      "JipperResourcePack keyviewer feature name was not preserved."
    );
    TestFixture.Assert(
      json.Contains("\"Enabled\":true", StringComparison.Ordinal),
      "JipperResourcePack feature enabled state was not preserved."
    );
    TestFixture.Assert(
      json.Contains("\"Setting\"", StringComparison.Ordinal),
      "JipperResourcePack feature setting namespace was not preserved."
    );
    TestFixture.Assert(
      bundle.Assets.Count == 5,
      "JipperResourcePack custom assets and three native sprites were not collected."
    );

    var overlayAdapter = new JipperResourcePackVisualAdapter(fileSystem, locator);
    var inspection = new VisualImportOptions { Inspect = true };
    overlayAdapter.Import(VisualKind.Overlay, options: inspection);
    TestFixture.Assert(
      inspection.MissingAssets.Count == 1 && inspection.MissingAssets[0].Reference == "Arial",
      "Inspection must identify the original missing font, without silently replacing it."
    );
    AssertCode(() => overlayAdapter.Import(VisualKind.Overlay), "visual_asset_missing");
    VisualBundle overlay = overlayAdapter.Import(
      VisualKind.Overlay,
      options: new VisualImportOptions
      {
        Uploads = new[]
        {
          new VisualAssetUpload { Reference = "Arial", DataBase64 = Convert.ToBase64String(TtfBytes()) },
        },
      }
    );
    string overlayJson = ((JToken)overlay.Files["ResourcePack.json"]).ToString(Newtonsoft.Json.Formatting.None);
    TestFixture.Assert(
      overlayJson.Contains("\"Status\"", StringComparison.Ordinal),
      "JipperResourcePack overlay feature name was not preserved."
    );
    TestFixture.Assert(
      overlayJson.Contains("\"Enabled\":true", StringComparison.Ordinal),
      "JipperResourcePack overlay enabled state was not preserved."
    );
    TestFixture.Assert(
      overlayJson.Contains("\"ResourceChanger\"", StringComparison.Ordinal),
      "JipperResourcePack resource changer feature name was not preserved."
    );
    TestFixture.Assert(
      overlayJson.Contains("\"Enabled\":false", StringComparison.Ordinal),
      "JipperResourcePack disabled feature state was not preserved."
    );
    TestFixture.Assert(
      !overlayJson.Contains("sans-serif", StringComparison.Ordinal),
      "The user's font was replaced with a system fallback."
    );
    TestFixture.Assert(
      overlay.Assets.Count == 3 && overlayJson.Contains(overlay.Assets[1].Path, StringComparison.Ordinal),
      "Uploaded font bytes and the rewritten logical reference must agree alongside the progress sprite."
    );
    TestFixture.Assert(
      File.ReadAllText(Path.Combine(install, "Settings.json")).Contains("\"Arial\"", StringComparison.Ordinal),
      "Import modified the original saved settings."
    );
  }

  private static void SymlinkedVisualAssetsAreRejected(string root)
  {
    string outside = Path.Combine(root, "outside-assets");
    Directory.CreateDirectory(outside);
    string outsideFile = Path.Combine(outside, "escape.png");
    File.WriteAllBytes(outsideFile, PngBytes());

    try
    {
      string fileLinkInstall = Path.Combine(root, "JipperSymlinkFile");
      Directory.CreateDirectory(Path.Combine(fileLinkInstall, "Images"));
      File.WriteAllText(
        Path.Combine(fileLinkInstall, "Info.json"),
        "{\"Id\":\"JipperResourcePack\",\"Version\":\"1.5.2.0\"}"
      );
      File.WriteAllText(
        Path.Combine(fileLinkInstall, "Settings.json"),
        "{\"Feature\":{\"KeyViewer\":{\"Setting\":{\"Image\":\"Images/escape.png\"}}}}"
      );
      File.CreateSymbolicLink(Path.Combine(fileLinkInstall, "Images", "escape.png"), outsideFile);
      AssertCode(
        () =>
          new JipperResourcePackVisualAdapter(
            new PhysicalVisualFileSystem(),
            new VisualSourceLocator(new PhysicalVisualFileSystem(), new VisualSourceRoots(new[] { fileLinkInstall }))
          ).Import(VisualKind.Keyviewer),
        "visual_asset_missing"
      );

      string directoryLinkInstall = Path.Combine(root, "JipperSymlinkDirectory");
      Directory.CreateDirectory(directoryLinkInstall);
      File.WriteAllText(
        Path.Combine(directoryLinkInstall, "Info.json"),
        "{\"Id\":\"JipperResourcePack\",\"Version\":\"1.5.2.0\"}"
      );
      File.WriteAllText(
        Path.Combine(directoryLinkInstall, "Settings.json"),
        "{\"Feature\":{\"KeyViewer\":{\"Setting\":{\"Image\":\"Images/escape.png\"}}}}"
      );
      Directory.CreateSymbolicLink(Path.Combine(directoryLinkInstall, "Images"), outside);
      AssertCode(
        () =>
          new JipperResourcePackVisualAdapter(
            new PhysicalVisualFileSystem(),
            new VisualSourceLocator(
              new PhysicalVisualFileSystem(),
              new VisualSourceRoots(new[] { directoryLinkInstall })
            )
          ).Import(VisualKind.Keyviewer),
        "visual_asset_missing"
      );

      string configLinkInstall = Path.Combine(root, "JipperSymlinkConfig");
      Directory.CreateDirectory(configLinkInstall);
      File.WriteAllText(
        Path.Combine(configLinkInstall, "Info.json"),
        "{\"Id\":\"JipperResourcePack\",\"Version\":\"1.5.2.0\"}"
      );
      string outsideSettings = Path.Combine(outside, "escape-settings.json");
      File.WriteAllText(outsideSettings, "{\"Feature\":{\"KeyViewer\":{\"Setting\":{}}}}");
      File.CreateSymbolicLink(Path.Combine(configLinkInstall, "Settings.json"), outsideSettings);
      AssertCode(
        () =>
          new JipperResourcePackVisualAdapter(
            new PhysicalVisualFileSystem(),
            new VisualSourceLocator(new PhysicalVisualFileSystem(), new VisualSourceRoots(new[] { configLinkInstall }))
          ).Import(VisualKind.Keyviewer),
        "visual_source_unsupported"
      );
    }
    catch (PlatformNotSupportedException)
    {
      Console.WriteLine("Visual symlink rejection tests skipped: symbolic links are not supported.");
    }
    catch (UnauthorizedAccessException)
    {
      Console.WriteLine("Visual symlink rejection tests skipped: symbolic links are not permitted.");
    }
  }

  private static void SvgNamespaceIsAcceptedAndUnsafeSvgIsRejected(string root)
  {
    string install = Path.Combine(root, "JipperSvg");
    Directory.CreateDirectory(Path.Combine(install, "Images"));
    Directory.CreateDirectory(Path.Combine(install, "Font"));
    File.WriteAllBytes(Path.Combine(install, "Font", "MAPLESTORY_OTF_BOLD.OTF"), TtfBytes());
    File.WriteAllText(Path.Combine(install, "Info.json"), "{\"Id\":\"JipperResourcePack\",\"Version\":\"1.5.2.0\"}");
    File.WriteAllText(
      Path.Combine(install, "Settings.json"),
      "{\"Feature\":{\"KeyViewer\":{\"Setting\":{\"Image\":\"Images/safe.svg\"}}}}"
    );
    File.WriteAllText(
      Path.Combine(install, "Images", "safe.svg"),
      "<svg xmlns=\"http://www.w3.org/2000/svg\"><rect width=\"10\" height=\"10\"/></svg>"
    );
    var fileSystem = new PhysicalVisualFileSystem();
    var locator = new VisualSourceLocator(fileSystem, new VisualSourceRoots(new[] { install }));
    VisualBundle safe = new JipperResourcePackVisualAdapter(fileSystem, locator).Import(VisualKind.Keyviewer);
    TestFixture.Assert(
      safe.Assets.Any(asset => asset.MediaType == "image/svg+xml"),
      "A standard SVG namespace was rejected."
    );

    File.WriteAllText(
      Path.Combine(install, "Images", "evil.svg"),
      "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"
    );
    File.WriteAllText(
      Path.Combine(install, "Settings.json"),
      "{\"Feature\":{\"KeyViewer\":{\"Setting\":{\"Image\":\"Images/evil.svg\"}}}}"
    );
    AssertCode(
      () => new JipperResourcePackVisualAdapter(fileSystem, locator).Import(VisualKind.Keyviewer),
      "visual_bundle_invalid"
    );
  }

  private static void AutomaticModDiscovery(string root)
  {
    string game = Path.Combine(root, "discovery-game");
    string mods = Path.Combine(game, "Mods");
    string replay = Path.Combine(mods, "TUFReplay");
    string jipper = Path.Combine(mods, "JipperResourcePack_1.5.2");
    string backup = Path.Combine(game, "backup", "JipperResourcePack");
    foreach (string path in new[] { replay, jipper, backup })
      Directory.CreateDirectory(path);
    File.WriteAllText(Path.Combine(jipper, "Info.json"), "{'Id':'JipperResourcePack','Version':'1.5.2.0'}");
    File.WriteAllText(
      Path.Combine(jipper, "Settings.json"),
      "{'Setting':{'FontName':'sans-serif'},'Feature':{'KeyViewer':{'Enabled':true,'Setting':{}},'Combo':{'Enabled':true,'Setting':{}}}}"
    );
    File.WriteAllText(Path.Combine(backup, "Info.json"), "{'Id':'JipperResourcePack','Version':'99.0.0'}");
    File.WriteAllText(Path.Combine(backup, "Settings.json"), "{}");

    var fs = new PhysicalVisualFileSystem();
    var roots = VisualSourceRoots.Discover(replay + Path.DirectorySeparatorChar);
    var locator = new VisualSourceLocator(fs, roots);
    TestFixture.Assert(
      locator.Find(VisualSource.JipperResourcePack)?.RootPath == jipper,
      "Renamed JipperResourcePack directory was not detected or a backup was preferred."
    );
    var handler = new RequestHandler(HttpStatusCode.OK, "{\"preset\":{\"id\":\"automatic\"}}");
    var account = new SubmissionAccount(new Uri("https://example.test/"), _ => Task.FromResult("token"));
    using var client = new SubmissionRecordsClient(handler);
    using var feature = new TUFReplay.Visual.Composition.VisualPresetFeature(() => account, client, roots);
    foreach (VisualSource source in new[] { VisualSource.JipperResourcePack })
    foreach (VisualKind kind in new[] { VisualKind.Keyviewer, VisualKind.Overlay })
    {
      VisualBundle bundle = feature.BuildBundle(kind, source);
      TestFixture.Assert(bundle.Files.Count > 0, "Automatic import produced no saved files.");
      feature.ImportAsync(source + " " + kind, kind, source, null).GetAwaiter().GetResult();
      TestFixture.Assert(handler.Body.Contains("bundle"), "Automatic import did not reach authenticated registration.");
      AssertCode(() => feature.BuildBundle(kind, source, "{}"), "visual_source_unsupported");
    }
    File.WriteAllText(
      Path.Combine(jipper, "Settings.json"),
      "{'Setting':{'FontName':'sans-serif','Size':2},'Feature':{'KeyViewer':{'Setting':{}}}}"
    );
    var latest = (JToken)
      feature.BuildBundle(VisualKind.Keyviewer, VisualSource.JipperResourcePack).Files["KeyViewer.json"];
    TestFixture.Assert(latest["Setting"]?["Size"]?.Value<int>() == 2, "Automatic import used stale settings.");

    // Source identity comes from metadata; an arbitrary folder name is insufficient.
    File.WriteAllText(Path.Combine(jipper, "Info.json"), "{'Id':'DifferentMod','Version':'99.0.0'}");
    TestFixture.Assert(
      locator.Find(VisualSource.JipperResourcePack) == null,
      "A different mod was mistaken for JipperResourcePack."
    );
  }

  private static void DmNoteSingleTabPreservesEmbeddedIdentity(string root)
  {
    string image = Convert.ToBase64String(PngBytes());
    string font = Convert.ToBase64String(TtfBytes());
    string preset =
      "{"
      + "\"keys\":{\"4key\":[\"A\"]},"
      + "\"keyPositions\":{\"4key\":[{\"activeImage\":\"dmnote-local-image://image-1\"}]},"
      + "\"selectedKeyType\":\"4key\",\"customTabs\":[],"
      + "\"tufReplayPlacement\":{\"x\":60,\"y\":420},\"keyCounterEnabled\":false,"
      + "\"customCSS\":{\"path\":\"/private/user/custom.css\",\"enabled\":true,\"content\":\".key{background-image:url(dmnote-local-image://image-1)}\"},"
      + "\"tabCssOverrides\":{\"4key\":{\"path\":\"/private/user/tab.css\",\"enabled\":true,\"content\":\".key{background-image:url(dmnote-local-image://image-1)}\"}},"
      + "\"fontSettings\":{\"customFonts\":[{\"id\":\"font-1\",\"type\":\"local\",\"name\":\"Local\",\"localPath\":\"/private/user/font.ttf\",\"enabled\":true}]},"
      + "\"useCustomJS\":true,\"customJS\":{\"content\":\"globalThis.bad = true\"},"
      + "\"embeddedLocalImages\":[{\"imageId\":\"image-1\",\"extension\":\"png\",\"dataBase64\":\""
      + image
      + "\"}],"
      + "\"embeddedLocalFonts\":[{\"fontId\":\"font-1\",\"extension\":\"ttf\",\"dataBase64\":\""
      + font
      + "\"}],"
      + "\"embeddedLocalSounds\":[{\"soundId\":\"sound-1\",\"extension\":\"wav\",\"dataBase64\":\"not-used\"}]"
      + "}";

    VisualBundle bundle = new DmNoteVisualAdapter(new PhysicalVisualFileSystem()).Import(VisualKind.Keyviewer, preset);
    TestFixture.Assert(bundle.SourceVersion == "2.0.2", "DMNote used its schema version as the source version.");
    var saved = (JToken)bundle.Files["preset.json"];
    TestFixture.Assert(
      saved["tufReplayPlacement"]?["x"]?.Value<int>() == 60 && saved["tufReplayPlacement"]?["y"]?.Value<int>() == 420,
      "DMNote placement was not preserved."
    );
    TestFixture.Assert(
      saved["keyCounterEnabled"]?.Value<bool>() == false,
      "DMNote global counter setting was not preserved."
    );
    string json = ((JToken)bundle.Files["preset.json"]).ToString(Newtonsoft.Json.Formatting.None);
    TestFixture.Assert(!json.Contains("customJS", StringComparison.Ordinal), "DMNote JavaScript was not stripped.");
    TestFixture.Assert(
      !json.Contains("embeddedLocalSounds", StringComparison.Ordinal),
      "DMNote sounds were not stripped."
    );
    TestFixture.Assert(
      !json.Contains("localPath", StringComparison.Ordinal),
      "DMNote local font paths were not stripped."
    );
    TestFixture.Assert(
      !json.Contains("/private/user/", StringComparison.Ordinal),
      "DMNote CSS machine-local paths were not stripped."
    );
    TestFixture.Assert(json.Contains("customCSS", StringComparison.Ordinal), "DMNote custom CSS was not preserved.");
    TestFixture.Assert(
      bundle.Assets.Any(asset => asset.Path == "assets/dmnote-image/image-1.png"),
      "DMNote image identity was lost."
    );
    TestFixture.Assert(
      bundle.Assets.Any(asset => asset.Path == "assets/dmnote-font/font-1.ttf"),
      "DMNote font identity was lost."
    );
  }

  private static void DmNotePlacementScaleValidation()
  {
    var adapter = new DmNoteVisualAdapter(new PhysicalVisualFileSystem());
    var preset = JObject.Parse(@"{'keys':{'one':['A']},'tufReplayPlacement':{'x':60,'y':420}}");
    var placement = (JObject)preset["tufReplayPlacement"];
    foreach (double scale in new[] { 0.1, 0.5, 1.0, 1.75, 4.0 })
    {
      placement["scale"] = scale;
      var saved = (JToken)adapter.Import(VisualKind.Keyviewer, preset.ToString()).Files["preset.json"];
      TestFixture.Assert(
        saved["tufReplayPlacement"]?["scale"]?.Value<double>() == scale,
        "DMNote scale was not preserved through sanitization."
      );
      TestFixture.Assert(
        saved["tufReplayPlacement"]?["x"]?.Value<int>() == 60,
        "Scaling changed the saved screen position."
      );
    }
    foreach (JToken invalid in new JToken[] { -1, 0, 0.09, 4.01, "1.5", true, JValue.CreateNull() })
    {
      placement["scale"] = invalid;
      AssertCode(() => adapter.Import(VisualKind.Keyviewer, preset.ToString()), "visual_bundle_invalid");
    }
    placement.Remove("scale");
    var legacy = (JToken)adapter.Import(VisualKind.Keyviewer, preset.ToString()).Files["preset.json"];
    TestFixture.Assert(
      legacy["tufReplayPlacement"]?["scale"] == null,
      "Legacy placement must remain valid without a scale."
    );
  }

  private static void DmNoteImportsSavedTab()
  {
    var preset = JObject.Parse(
      @"{
      'keys': {'other':['A'], 'numpad':['B']},
      'selectedKeyType':'numpad',
      'selectedViewerTabs': {'hand':'numpad', 'foot':'other'},
      'tabs': [{'id':'other'}, {'id':'numpad','name':'Numpad'}],
      'tabCount':2,
      'keyPositions': {'other':[{'activeImage':'dmnote-local-image://missing'}], 'numpad':[{'dx':20,'dy':40}]},
      'statPositions': {'other':[]}, 'graphPositions': {'numpad':[]}, 'knobPositions': {'other':[]},
      'tabCssOverrides': {'other':{'content':'url(dmnote-local-image://missing)'}, 'numpad':{'content':'.key{color:red}'}},
      'tabNoteOverrides': {'other':{'trackHeight':500}, 'numpad':{'trackHeight':100}},
      'fontSettings': {'customFonts':[]}
    }"
    );
    var adapter = new DmNoteVisualAdapter(new PhysicalVisualFileSystem());
    JToken saved = (JToken)adapter.Import(VisualKind.Keyviewer, preset.ToString()).Files["preset.json"];
    TestFixture.Assert(
      !saved.ToString().Contains("other", StringComparison.Ordinal),
      "DMNote retained an unselected tab."
    );
    TestFixture.Assert(
      saved["keys"]?["numpad"]?[0]?.Value<string>() == "B",
      "DMNote did not import the saved selection."
    );
    TestFixture.Assert(
      saved["tabCssOverrides"]?["numpad"]?["content"]?.Value<string>() == ".key{color:red}",
      "DMNote lost selected CSS."
    );
    TestFixture.Assert(
      saved["tabNoteOverrides"]?["numpad"]?["trackHeight"]?.Value<int>() == 100,
      "DMNote lost selected rain settings."
    );
    TestFixture.Assert(
      saved["tabCount"]?.Value<int>() == 1 && ((JArray)saved["tabs"]).Count == 1,
      "DMNote tab metadata was not projected."
    );
    TestFixture.Assert(saved["fontSettings"] != null, "DMNote lost shared font settings.");
    preset.Remove("selectedKeyType");
    preset["selected_key_type"] = "numpad";
    JToken legacy = (JToken)adapter.Import(VisualKind.Keyviewer, preset.ToString()).Files["preset.json"];
    TestFixture.Assert(legacy["selectedKeyType"]?.Value<string>() == "numpad", "DMNote legacy selection was ignored.");
  }

  private static void DmNoteRejectsMultipleOrMissingAssets(string root)
  {
    string image = Convert.ToBase64String(PngBytes());
    string oneTab =
      "{\"keys\":{\"4key\":[\"A\"]},\"keyPositions\":{\"4key\":[{\"activeImage\":\"dmnote-local-image://image-1\"}]},\"embeddedLocalImages\":[{\"imageId\":\"image-1\",\"extension\":\"png\",\"dataBase64\":\""
      + image
      + "\"}]}";
    string missingImage = oneTab.Replace(
      ",\"embeddedLocalImages\":[{\"imageId\":\"image-1\",\"extension\":\"png\",\"dataBase64\":\"" + image + "\"}]",
      "",
      StringComparison.Ordinal
    );
    AssertCode(
      () => new DmNoteVisualAdapter(new PhysicalVisualFileSystem()).Import(VisualKind.Keyviewer, missingImage),
      "visual_asset_missing"
    );

    string multiple = "{\"keys\":{\"4key\":[],\"6key\":[]}}";
    AssertCode(
      () => new DmNoteVisualAdapter(new PhysicalVisualFileSystem()).Import(VisualKind.Keyviewer, multiple),
      "visual_selected_tab_missing"
    );
    AssertCode(
      () =>
        new DmNoteVisualAdapter(new PhysicalVisualFileSystem()).Import(
          VisualKind.Keyviewer,
          "{\"keys\":{\"4key\":[]},\"selectedKeyType\":\"deleted\"}"
        ),
      "visual_selected_tab_missing"
    );
    string invalidPlacement = "{\"keys\":{\"4key\":[]},\"tufReplayPlacement\":{\"x\":-1,\"y\":0}}";
    AssertCode(
      () => new DmNoteVisualAdapter(new PhysicalVisualFileSystem()).Import(VisualKind.Keyviewer, invalidPlacement),
      "visual_bundle_invalid"
    );

    string nestedMultiple = "{\"keys\":{\"4key\":[]},\"layout\":{\"tabDefinitions\":[{},{}]}}";
    AssertCode(
      () => new DmNoteVisualAdapter(new PhysicalVisualFileSystem()).Import(VisualKind.Keyviewer, nestedMultiple),
      "visual_multiple_tabs"
    );

    string missingCss =
      "{\"keys\":{\"4key\":[]},\"customCSS\":{\"content\":\".key{background:url(dmnote-local-image://missing-css-image)}\"}}";
    AssertCode(
      () => new DmNoteVisualAdapter(new PhysicalVisualFileSystem()).Import(VisualKind.Keyviewer, missingCss),
      "visual_asset_missing"
    );
  }

  private static void AuthenticatedVisualRequestPreservesServerErrors()
  {
    var successHandler = new RequestHandler(HttpStatusCode.OK, "{\"preset\":{\"id\":\"p\"}}");
    var account = new SubmissionAccount(new Uri("https://example.test/"), _ => Task.FromResult("secret-token"));
    using (var client = new SubmissionRecordsClient(successHandler))
    {
      JObject body = new JObject { ["name"] = "saved" };
      JObject result = client.Send(account, HttpMethod.Post, "api/v1/visual-presets", body).GetAwaiter().GetResult();
      TestFixture.Assert((string)result["preset"]?["id"] == "p", "Authenticated visual response was not parsed.");
      TestFixture.Assert(
        successHandler.Request.Headers.Authorization.Scheme == "Bearer",
        "Visual request did not use bearer authentication."
      );
      TestFixture.Assert(
        successHandler.Request.Headers.Authorization.Parameter == "secret-token",
        "Visual request exposed the wrong bearer token."
      );
      TestFixture.Assert(
        successHandler.Body.Contains("saved", StringComparison.Ordinal),
        "Visual request body was not forwarded."
      );
    }

    var errorHandler = new RequestHandler(
      HttpStatusCode.BadRequest,
      "{\"error\":\"Bad Request\",\"description\":\"visual_name_taken\"}"
    );
    bool sawError = false;
    using (var client = new SubmissionRecordsClient(errorHandler))
    {
      try
      {
        client
          .Send(account, HttpMethod.Post, "api/v1/visual-presets", new JObject { ["name"] = "saved" })
          .GetAwaiter()
          .GetResult();
      }
      catch (SubmissionRequestException exception)
      {
        TestFixture.Assert(exception.Code == "visual_name_taken", "Visual server error code was not preserved.");
        sawError = true;
      }
    }
    if (!sawError)
      throw new InvalidOperationException("Expected the visual server error to be preserved.");

    var compactErrorHandler = new RequestHandler(
      HttpStatusCode.BadRequest,
      "{\"error\":\"visual_source_unsupported\"}"
    );
    using (var client = new SubmissionRecordsClient(compactErrorHandler))
    {
      try
      {
        client
          .Send(account, HttpMethod.Post, "api/v1/visual-presets", new JObject { ["name"] = "saved" })
          .GetAwaiter()
          .GetResult();
      }
      catch (SubmissionRequestException exception)
      {
        TestFixture.Assert(
          exception.Code == "visual_source_unsupported",
          "Compact visual server error code was not preserved."
        );
        return;
      }
    }
    throw new InvalidOperationException("Expected the compact visual server error to be preserved.");
  }

  private sealed class RequestHandler : HttpMessageHandler
  {
    private readonly HttpStatusCode _status;
    private readonly string _body;
    public HttpRequestMessage Request { get; private set; }
    public string Body { get; private set; }

    public RequestHandler(HttpStatusCode status, string body)
    {
      _status = status;
      _body = body;
    }

    protected override Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken
    )
    {
      Request = request;
      Body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
      return Task.FromResult(
        new HttpResponseMessage(_status) { Content = new StringContent(_body), RequestMessage = request }
      );
    }
  }

  private static void AssertCode(Action action, string expected)
  {
    try
    {
      action();
    }
    catch (VisualImportException exception)
    {
      TestFixture.Assert(exception.Code == expected, "Expected " + expected + " but received " + exception.Code + ".");
      return;
    }
    throw new InvalidOperationException("Expected visual import error " + expected + ".");
  }

  private static byte[] PngBytes() => new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };

  private static byte[] TtfBytes() => new byte[] { 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
}
