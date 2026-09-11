using TUFReplay.Shared.Database;

internal static class Program
{
  private static int Main()
  {
    string root = Path.Combine(Path.GetTempPath(), "tufreplay-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      NativeSqliteLoader.Initialize();
      MicrophonePermissionWarningSuite.RunAll();
      MicrophoneCalibrationSuite.RunAll(root);
      ActivityDatabaseSuite.RunAll(root);
      ReplayNativeInputSuite.RunAll();
      SubmissionCaptureSuite.RunAll();
      SubmissionUploadSuite.RunAll();
      SubmissionRecoverySuite.RunAll();
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
