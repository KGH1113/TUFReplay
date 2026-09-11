using TUFReplay.Microphone.Capture;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Permissions;
using static TestFixture;

internal static class MicrophonePermissionWarningSuite
{
  internal static void RunAll()
  {
    TestWarningStateClassification();
    TestOneShotPolicy();
    TestHelperStatusMapping();
    TestNonMacBackendStatus();
  }

  private static void TestWarningStateClassification()
  {
    Assert(
      MicrophonePermissionWarningPolicy.IsWarningState(MicrophonePermissionState.Denied),
      "Denied microphone permission did not request a warning."
    );
    Assert(
      MicrophonePermissionWarningPolicy.IsWarningState(MicrophonePermissionState.Restricted),
      "Restricted microphone permission did not request a warning."
    );
    Assert(
      MicrophonePermissionWarningPolicy.IsWarningState(MicrophonePermissionState.Failed),
      "Failed microphone permission request did not request a warning."
    );
    Assert(
      !MicrophonePermissionWarningPolicy.IsWarningState(MicrophonePermissionState.Authorized),
      "Authorized microphone permission requested a warning."
    );
    Assert(
      !MicrophonePermissionWarningPolicy.IsWarningState(MicrophonePermissionState.Requesting),
      "An in-flight microphone permission request requested a warning."
    );
    Assert(
      !MicrophonePermissionWarningPolicy.IsWarningState(MicrophonePermissionState.Checking),
      "An in-flight microphone permission check requested a warning."
    );
    Assert(
      !MicrophonePermissionWarningPolicy.IsWarningState(MicrophonePermissionState.NotApplicable),
      "A non-macOS microphone backend requested a warning."
    );
  }

  private static void TestOneShotPolicy()
  {
    var policy = new MicrophonePermissionWarningPolicy();
    Assert(policy.ShouldShow(MicrophonePermissionState.Denied), "The first denied level was suppressed.");
    policy.MarkShown();
    Assert(!policy.ShouldShow(MicrophonePermissionState.Denied), "The warning repeated in one session.");
    policy.Reset();
    Assert(policy.ShouldShow(MicrophonePermissionState.Failed), "Reset did not restore the warning opportunity.");
  }

  private static void TestHelperStatusMapping()
  {
    Assert(
      MacOsMicrophoneCaptureBackend.ParsePermissionState("authorized", out string authorizedError)
        == MicrophonePermissionState.Authorized
        && authorizedError == null,
      "The helper authorized state was not mapped."
    );
    Assert(
      MacOsMicrophoneCaptureBackend.ParsePermissionState("denied", out _) == MicrophonePermissionState.Denied,
      "The helper denied state was not mapped."
    );
    Assert(
      MacOsMicrophoneCaptureBackend.ParsePermissionState("restricted", out _) == MicrophonePermissionState.Restricted,
      "The helper restricted state was not mapped."
    );
    Assert(
      MacOsMicrophoneCaptureBackend.ParsePermissionState("notDetermined", out string undeterminedError)
        == MicrophonePermissionState.Failed
        && !string.IsNullOrEmpty(undeterminedError),
      "An unresolved helper permission request was not treated as failed."
    );
    Assert(
      MacOsMicrophoneCaptureBackend.ParsePermissionState(null, out string missingError)
        == MicrophonePermissionState.Failed
        && !string.IsNullOrEmpty(missingError),
      "A missing helper permission state was not treated as failed."
    );
  }

  private static void TestNonMacBackendStatus()
  {
    var backend = new UnityMicrophoneCaptureBackend();
    Assert(
      backend.GetPermissionStatus().State == MicrophonePermissionState.NotApplicable,
      "The non-macOS backend exposed an actionable permission state."
    );
  }
}
