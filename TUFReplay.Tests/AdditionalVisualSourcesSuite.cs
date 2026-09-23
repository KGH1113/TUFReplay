using System;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Infrastructure.Discovery;
using TUFReplay.Visual.Infrastructure.FileSystem;
using TUFReplay.Visual.Sources.DmNote;
using TUFReplay.Visual.Sources.ImplResourcePack;
using TUFReplay.Visual.Sources.JipperKeyViewer;
using TUFReplay.Visual.Sources.JipperResourcePack;

internal static class AdditionalVisualSourcesSuite
{
  public static void Run(string root)
  {
    var files = new PhysicalVisualFileSystem();
    string mods = Path.Combine(root, "additional", "Mods");
    string jkv = Path.Combine(mods, "renamed-jkv-1.7.2");
    string impl = Path.Combine(mods, "renamed-impl");
    string jrp = Path.Combine(mods, "old-jrp");
    Directory.CreateDirectory(Path.Combine(jkv, "config", "profiles"));
    Directory.CreateDirectory(Path.Combine(jkv, "assets"));
    Directory.CreateDirectory(Path.Combine(jkv, "CustomImages"));
    Directory.CreateDirectory(impl);
    Directory.CreateDirectory(jrp);
    File.WriteAllText(Path.Combine(jkv, "Info.json"), "{\"Id\":\"JipperKeyViewer\",\"Version\":\"1.7.2\"}");
    File.WriteAllText(Path.Combine(impl, "Info.json"), "{\"Id\":\"ImplResourcePack\",\"Version\":\"0.1.0\"}");
    File.WriteAllText(Path.Combine(jrp, "Info.json"), "{\"Id\":\"JipperResourcePack\",\"Version\":\"1.5.2.0\"}");
    File.WriteAllText(Path.Combine(jrp, "Settings.json"), "{}");
    File.WriteAllText(Path.Combine(jkv, "config", "settings.json"), "{\"Version\":6,\"CurrentProfile\":\"Chosen\"}");
    File.WriteAllText(Path.Combine(jkv, "config", "profiles", "Default.json"), "{\"FontName\":\"missing-font\"}");
    File.WriteAllText(
      Path.Combine(jkv, "config", "profiles", "Chosen.json"),
      "{\"FontName\":\"MapleStory\",\"KeyViewerStyle\":3,\"CustomNodes\":[{\"ImagePath\":\"key.png\"}],\"customJS\":\"ignored\"}"
    );
    File.WriteAllBytes(
      Path.Combine(jkv, "assets", "MAPLESTORY_OTF_BOLD.OTF"),
      new byte[] { 79, 84, 84, 79, 0, 0, 0, 0 }
    );
    File.WriteAllBytes(
      Path.Combine(jkv, "assets", "cjkFonts-regular-normalized.otf"),
      new byte[] { 79, 84, 84, 79, 0, 0, 0, 1 }
    );
    File.WriteAllBytes(Path.Combine(jkv, "CustomImages", "key.png"), new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var locator = new VisualSourceLocator(files, VisualSourceRoots.Discover(Path.Combine(mods, "TUFReplay")));
    TestFixture.Assert(
      locator.Find(VisualSource.JipperResourcePack)?.RootPath == jrp,
      "JRP identity was confused with standalone JKV."
    );
    TestFixture.Assert(
      locator.Find(VisualSource.JipperKeyviewer)?.RootPath == jkv,
      "Standalone JKV metadata discovery failed."
    );
    TestFixture.Assert(
      locator.Find(VisualSource.ImplResourcePack)?.RootPath == impl,
      "ImplResourcePack defaults were not discoverable."
    );
    TestFixture.Assert(locator.Find(VisualSource.Dmnote) == null, "File imports must not resolve an installed source.");

    var jrpAdapter = new JipperResourcePackVisualAdapter(files, locator);
    var jrpInspect = new VisualImportOptions { Inspect = true };
    var jrpBundle = jrpAdapter.Import(VisualKind.Keyviewer, options: jrpInspect);
    TestFixture.Assert(
      jrpInspect.MissingAssets.Count == 0,
      "Jipper Resourcepack's default font must not require an upload."
    );
    string nativePlatform =
      RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macos"
      : RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
      : null;
    TestFixture.Assert(
      (string)((JObject)jrpBundle.Files["KeyViewer.json"])["nativeKeyPlatform"] == nativePlatform,
      "JRP snapshots must preserve the native key binding platform."
    );
    TestFixture.Assert(
      jrpBundle.Assets.Count == 4 && jrpBundle.Assets[0].MediaType == "font/otf",
      "Jipper Resourcepack's portable font and three sprites were not included."
    );
    File.WriteAllText(Path.Combine(jrp, "Settings.json"), "{\"Setting\":{\"FontName\":\"Font/custom.otf\"}}");
    jrpInspect = new VisualImportOptions { Inspect = true };
    jrpAdapter.Import(VisualKind.Keyviewer, options: jrpInspect);
    TestFixture.Assert(
      jrpInspect.MissingAssets.Count == 1 && jrpInspect.MissingAssets[0].Reference == "Font/custom.otf",
      "An unavailable custom font must still request an upload."
    );

    var jkvAdapter = new JipperKeyViewerVisualAdapter(files, locator);
    var jkvProgress = new System.Collections.Generic.List<VisualImportProgress>();
    var jkvBundle = jkvAdapter.Import(
      VisualKind.Keyviewer,
      options: new VisualImportOptions { Progress = jkvProgress.Add }
    );
    TestFixture.Assert(
      jkvProgress.Exists(item => item.AssetName == "cjkFonts-regular-normalized.otf")
        && jkvProgress.Exists(item => item.Stage == "validating" && item.CompletedAssets == jkvBundle.Assets.Count),
      "Real Jipper imports must report the CJK font and measured completed asset count."
    );
    var selected = (JObject)jkvBundle.Files["JipperKeyViewer.json"];
    TestFixture.Assert(
      jkvBundle.Source == "jipper-keyviewer" && (int)selected["Data"]["KeyViewerStyle"] == 3,
      "The saved current profile was not snapshotted."
    );
    TestFixture.Assert(
      jkvBundle.Assets.Count == 6 && selected["Data"]["customJS"] == null,
      "JKV sprites, CJK fallback or sanitization failed."
    );
    string chosenPath = Path.Combine(jkv, "config", "profiles", "Chosen.json");
    JObject custom = JObject.Parse(File.ReadAllText(chosenPath));
    custom["CustomNodes"][0]["ImagePath"] = Path.Combine(root, "external.png");
    File.WriteAllText(chosenPath, custom.ToString());
    var externalInspect = new VisualImportOptions { Inspect = true };
    jkvAdapter.Import(VisualKind.Keyviewer, options: externalInspect);
    TestFixture.Assert(externalInspect.MissingAssets.Count == 1, "External images must request explicit attachment.");
    Reject(() => jkvAdapter.Import(VisualKind.Keyviewer), "visual_asset_missing");
    var attached = jkvAdapter.Import(
      VisualKind.Keyviewer,
      options: new VisualImportOptions
      {
        Uploads = new[]
        {
          new VisualAssetUpload { Reference = Path.Combine(root, "external.png"), DataBase64 = "iVBORw0KGgo=" },
        },
      }
    );
    TestFixture.Assert(attached.Assets.Count == 6, "External JKV image attachment was not preserved.");
    Reject(() => jkvAdapter.Import(VisualKind.Overlay), "visual_source_unsupported");
    Reject(() => jkvAdapter.Import(VisualKind.Keyviewer, "{}"), "visual_source_unsupported");
    File.WriteAllText(chosenPath, "{\"FontIndex\":7}");
    var fontInspect = new VisualImportOptions { Inspect = true };
    jkvAdapter.Import(VisualKind.Keyviewer, options: fontInspect);
    TestFixture.Assert(
      fontInspect.MissingAssets.Count == 1 && fontInspect.MissingAssets[0].Reference == "Jipper KeyViewer font index 7",
      "An unnamed runtime font index must never silently select MapleStory."
    );
    var indexedFont = jkvAdapter.Import(
      VisualKind.Keyviewer,
      options: new VisualImportOptions
      {
        Uploads = new[]
        {
          new VisualAssetUpload { Reference = "Jipper KeyViewer font index 7", DataBase64 = "T1RUTwAAAAI=" },
        },
      }
    );
    TestFixture.Assert(
      indexedFont.Assets.Count == 5,
      "A manually resolved primary font must retain the independent CJK fallback and three sprites."
    );
    var runtimeCjk = new JipperKeyViewerVisualAdapter(files, locator, index => index == 7 ? "CJK (Default)" : null);
    var runtimeCjkInspect = new VisualImportOptions { Inspect = true };
    var runtimeCjkBundle = runtimeCjk.Import(VisualKind.Keyviewer, options: runtimeCjkInspect);
    TestFixture.Assert(
      runtimeCjkInspect.MissingAssets.Count == 0
        && (string)((JObject)runtimeCjkBundle.Files["JipperKeyViewer.json"])["Data"]["FontName"]
          == "assets/cjkFonts-regular-normalized.otf",
      "A runtime CJK font index must use the installed original without an upload."
    );
    File.WriteAllText(chosenPath, "{\"FontIndex\":1,\"FontName\":\"\"}");
    var runtimeCjkName = new JipperKeyViewerVisualAdapter(
      files,
      locator,
      index => index == 1 ? "cjkFonts-regular-normalized" : null
    );
    var runtimeCjkNameInspect = new VisualImportOptions { Inspect = true };
    var runtimeCjkNameBundle = runtimeCjkName.Import(VisualKind.Keyviewer, options: runtimeCjkNameInspect);
    TestFixture.Assert(
      runtimeCjkNameInspect.MissingAssets.Count == 0
        && (string)((JObject)runtimeCjkNameBundle.Files["JipperKeyViewer.json"])["Data"]["FontName"]
          == "assets/cjkFonts-regular-normalized.otf"
        && runtimeCjkNameBundle.Assets.Count == 4,
      "The game's cjkFonts runtime name must reuse the installed CJK font instead of asking for it twice."
    );
    File.WriteAllText(chosenPath, "{\"FontIndex\":7}");
    var runtimeMaple = new JipperKeyViewerVisualAdapter(files, locator, index => index == 7 ? "MapleStory" : null);
    var runtimeMapleInspect = new VisualImportOptions { Inspect = true };
    var runtimeMapleBundle = runtimeMaple.Import(VisualKind.Keyviewer, options: runtimeMapleInspect);
    TestFixture.Assert(
      runtimeMapleInspect.MissingAssets.Count == 0 && runtimeMapleBundle.Assets.Count == 5,
      "A runtime MapleStory font index must bundle the selected font and CJK fallback automatically."
    );
    Directory.CreateDirectory(Path.Combine(jkv, "CustomFont"));
    File.WriteAllBytes(Path.Combine(jkv, "CustomFont", "chosen.ttf"), new byte[] { 0, 1, 0, 0, 0, 1, 0, 0 });
    var runtimeCustom = new JipperKeyViewerVisualAdapter(files, locator, index => index == 7 ? "Custom: chosen" : null);
    var runtimeCustomInspect = new VisualImportOptions { Inspect = true };
    var runtimeCustomBundle = runtimeCustom.Import(VisualKind.Keyviewer, options: runtimeCustomInspect);
    TestFixture.Assert(
      runtimeCustomInspect.MissingAssets.Count == 0
        && runtimeCustomBundle.Assets.Count == 5
        && (string)((JObject)runtimeCustomBundle.Files["JipperKeyViewer.json"])["Data"]["FontName"]
          == "CustomFont/chosen.ttf",
      "A runtime custom font index must include its original installed file."
    );
    File.WriteAllText(chosenPath, "{\"FontIndex\":1,\"FontName\":\"\"}");
    var runtimeSystem = new JipperKeyViewerVisualAdapter(files, locator, index => index == 1 ? "Arial" : null);
    var runtimeSystemInspect = new VisualImportOptions { Inspect = true };
    var runtimeSystemBundle = runtimeSystem.Import(VisualKind.Keyviewer, options: runtimeSystemInspect);
    TestFixture.Assert(
      runtimeSystemInspect.MissingAssets.Count == 0
        && (string)((JObject)runtimeSystemBundle.Files["JipperKeyViewer.json"])["Data"]["FontName"] == "Arial",
      "A runtime system font index must keep its CSS family without requesting a file upload."
    );
    File.WriteAllText(chosenPath, "{\"FontIndex\":7}");
    var runtimeUnknown = new JipperKeyViewerVisualAdapter(
      files,
      locator,
      index => index == 7 ? "UnmatchedGameFont" : null
    );
    var runtimeUnknownInspect = new VisualImportOptions { Inspect = true };
    runtimeUnknown.Import(VisualKind.Keyviewer, options: runtimeUnknownInspect);
    TestFixture.Assert(
      runtimeUnknownInspect.MissingAssets.Count == 1
        && runtimeUnknownInspect.MissingAssets[0].Reference == "UnmatchedGameFont",
      "An unknown game font must not be silently replaced with a different face."
    );
    File.WriteAllText(Path.Combine(jkv, "config", "settings.json"), "{\"Version\":5}");
    Reject(() => jkvAdapter.Import(VisualKind.Keyviewer), "visual_source_unsupported");

    var dmnote = new DmNoteVisualAdapter(files, VisualSource.ImplDmnote);
    var dmnoteBundle = dmnote.Import(
      VisualKind.Keyviewer,
      "{\"keys\":{\"one\":[\"A\"],\"two\":[\"B\"]},\"selectedKeyType\":\"one\",\"keyPositions\":{\"one\":[],\"two\":[{\"activeImage\":\"missing.png\"}]}}"
    );
    TestFixture.Assert(
      dmnoteBundle.Source == "impl-dmnote" && dmnoteBundle.SourceVersion == "0.1.0",
      "Impl DMNote identity/version was lost."
    );
    TestFixture.Assert(
      ((JObject)((JObject)dmnoteBundle.Files["preset.json"])["keys"]).Count == 1,
      "Impl DMNote selected-tab projection failed."
    );
    Reject(() => dmnote.Import(VisualKind.Overlay, "{}"), "visual_source_unsupported");

    var implAdapter = new ImplResourcePackVisualAdapter(files, locator);
    var inspect = new VisualImportOptions { Inspect = true };
    implAdapter.Import(VisualKind.Overlay, options: inspect);
    TestFixture.Assert(
      inspect.MissingAssets.Count == 0,
      "Default Impl registration must never request a CJK font upload."
    );
    File.WriteAllText(
      Path.Combine(impl, "Settings.xml"),
      "<ImplResourcePackSettings><HidePerfectJudgmentText>false</HidePerfectJudgmentText><RecordMode>true</RecordMode></ImplResourcePackSettings>"
    );
    var implBundle = implAdapter.Import(VisualKind.Overlay);
    var overlay = (JObject)implBundle.Files["ImplResourcePack.json"];
    TestFixture.Assert(
      implBundle.Source == "impl-resourcepack"
        && !(bool)overlay["HidePerfectJudgmentText"]
        && (bool)overlay["RecordMode"],
      "Impl visual options were not snapshotted."
    );
    TestFixture.Assert(
      implBundle.Assets.Count == 2 && implBundle.Assets[0].MediaType == "font/otf" && implBundle.Viewport.Width == 1920,
      "Impl overlay contract lost its portable font or viewport."
    );
    TestFixture.Assert(
      (string)overlay["Setting"]["FallbackFontName"] == implBundle.Assets[1].Path
        && implBundle.Assets[1].Path == "assets/builtin/adofai-cjk.woff"
        && implBundle.Assets[1].MediaType == "font/woff"
        && Convert.FromBase64String(implBundle.Assets[1].DataBase64).Length == 31959108,
      "Impl must carry the complete original game CJK font without an upload or another installed mod."
    );
    Reject(() => implAdapter.Import(VisualKind.Keyviewer), "visual_source_unsupported");
    File.WriteAllText(
      Path.Combine(impl, "Settings.xml"),
      "<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///etc/passwd'>]><x>&a;</x>"
    );
    Reject(() => implAdapter.Import(VisualKind.Overlay), "visual_bundle_invalid");
    Console.WriteLine("Additional visual source tests passed.");
  }

  private static void Reject(Action action, string code)
  {
    try
    {
      action();
    }
    catch (VisualImportException exception)
    {
      TestFixture.Assert(exception.Code == code, "Unexpected visual failure: " + exception.Code);
      return;
    }
    throw new Exception("Expected visual failure: " + code);
  }
}
