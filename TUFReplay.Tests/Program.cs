using TUFReplay.Shared.Database;

internal static class Program
{
  private static int Main(string[] args)
  {
    if (args.Length == 1 && args[0] == "--visual-import")
      return VisualImportHarness.Run();
    string root = Path.Combine(Path.GetTempPath(), "tufreplay-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      NativeSqliteLoader.Initialize();
      MicrophonePermissionWarningSuite.RunAll();
      MicrophoneCalibrationSuite.RunAll(root);
      ActivityDatabaseSuite.RunAll(root);
      ReplayNativeInputSuite.RunAll();
      VisualPresetSuite.RunAll();
      SubmissionCaptureSuite.RunAll();
      SubmissionPreflightSuite.RunAll(root);
      SubmissionUploadSuite.RunAll();
      SubmissionRecoverySuite.RunAll();
      LevelSubmissionSessionSuite.RunAll();
      SubmissionOAuthSuite.RunAll();
      UpdaterTests.RunAll();
      Console.WriteLine("TUFReplay C# tests passed.");
      return 0;
    }
    catch (Exception exception)
    {
      Console.Error.WriteLine(exception);
      return 1;
    }
    finally
    {
      Directory.Delete(root, true);
    }
  }
}
