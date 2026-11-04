using TUFReplay.Shared.Database;

internal static class Program
{
  private static int Main(string[] args)
  {
    string root = Path.Combine(Path.GetTempPath(), "tufreplay-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
      if (args.Length == 1 && args[0] == "--renderer")
      {
        ManagedRendererSuite.RunAll(root);
        return 0;
      }
      NativeSqliteLoader.Initialize();
      MicrophonePermissionWarningSuite.RunAll();
      MicrophoneCalibrationSuite.RunAll(root);
      WebcamSuite.RunAll(root);
      FfmpegCameraModeSuite.RunAll();
      FfmpegCameraFrameRateSuite.RunAll();
      CameraEncoderSuite.RunAll();
      FfmpegCameraRecordingSuite.RunAll(root);
      Mp4CameraDurationSuite.RunAll(root);
      ManagedFfmpegSuite.RunAll(root);
      ManagedRendererSuite.RunAll(root);
      WebcamCropSuite.RunAll();
      WebcamOverlayInteractionSuite.RunAll();
      RenderBundleSuite.RunAll();
      RunJudgmentTimelineSuite.RunAll();
      ActivityDatabaseSuite.RunAll(root);
      RunRecordManagementSuite.RunAll(root);
      ActivityChartDownloadSuite.RunAll();
      ReplayNativeInputSuite.RunAll();
      ImplResourcePackAttemptsSuite.RunAll();
      CGEventCaptureRegressionSuite.RunAll();
      PerformanceRegressionSuite.RunAll(root);
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
