using System;
using System.IO;
using System.Threading;
using TUFReplay.Application.Calibration;
using TUFReplay.Application.Microphone;
using TUFReplay.Application.Replay;
using TUFReplay.Bootstrap;
using TUFReplay.Domain.Microphone;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Unity;

namespace TUFReplay.Features.Calibration;

internal sealed class MicrophoneCalibrationPreview
{
  private readonly MicrophoneCalibrationState _state;
  private StoredReplayRun _run;
  private CapturedMicrophoneRecording _recording;
  private string _replayOperationId;
  private bool _originalRunInBackground;
  private bool _changedRunInBackground;

  public MicrophoneCalibrationPreview(MicrophoneCalibrationState state)
  {
    _state = state;
  }

  public void SetSource(StoredReplayRun run, CapturedMicrophoneRecording recording)
  {
    _run = run;
    _recording = recording;
  }

  public MicrophoneCalibrationStatus Play(string operationId, string levelPath)
  {
    if (_run == null || _recording == null || !_state.HasResult)
      return _state.Error(
        "calibration_result_unavailable",
        "Clear the calibration level before playing a test.",
        operationId
      );
    Stop();
    _state.Update(MicrophoneCalibrationStates.PreviewStarting, "Preparing calibration replay.");
    string copyPath = ReplayMicrophonePlaybackFiles.ForOperation("calibration-" + Guid.NewGuid().ToString("N"));
    CapturedMicrophoneRecording source = _recording;
    ThreadPool.QueueUserWorkItem(_ => Prepare(operationId, levelPath, source, copyPath));
    return _state.GetStatus();
  }

  public MicrophoneCalibrationStatus Stop()
  {
    if (_replayOperationId != null)
      ReplayPlaybackCoordinator.Cancel("Calibration preview stopped.");
    _replayOperationId = null;
    RestoreRunInBackground();
    if (_state.HasResult && _state.Active)
    {
      _state.Update(MicrophoneCalibrationStates.Editing, "Calibration preview stopped.");
      _state.SetPlaybackPosition(0d);
    }
    return _state.GetStatus();
  }

  public void Tick()
  {
    ReplayPlaybackStatus replay = ReplayPlaybackCoordinator.GetStatus();
    if (_replayOperationId == null)
      return;
    if (!string.Equals(replay.OperationId, _replayOperationId, StringComparison.Ordinal))
    {
      Stop();
      return;
    }
    if (replay.State == ReplayPlaybackStates.Error)
    {
      _replayOperationId = null;
      RestoreRunInBackground();
      _state.Error(replay.ErrorCode, replay.Message, _state.GetStatus().OperationId);
      return;
    }
    if (replay.State == ReplayPlaybackStates.Completed || replay.State == ReplayPlaybackStates.Cancelled)
    {
      bool completed = replay.State == ReplayPlaybackStates.Completed;
      _replayOperationId = null;
      RestoreRunInBackground();
      _state.Update(MicrophoneCalibrationStates.Editing, completed ? "Calibration preview finished." : replay.Message);
      _state.SetPlaybackPosition(completed ? _state.GetStatus().DurationMs : 0d);
      return;
    }

    if (replay.State == ReplayPlaybackStates.Playing)
      _state.Update(MicrophoneCalibrationStates.PreviewPlaying, "Playing calibration preview in ADOFAI.");
    if (ReplaySessionService.TryGetPlaybackSnapshot(out long replayTimeUs, out double timelineRate))
      _state.SetPlaybackPosition(replayTimeUs / Math.Max(0.0001d, timelineRate) / 1000d);
  }

  public void Reset()
  {
    Stop();
    if (_recording != null)
      FeatureRegistry.MicrophoneRecording?.Discard(_recording);
    _recording = null;
    _run = null;
  }

  private void Prepare(string operationId, string levelPath, CapturedMicrophoneRecording source, string copyPath)
  {
    try
    {
      File.Copy(source.TempPath, copyPath, true);
      var stored = new StoredMicrophoneRecording
      {
        RunId = source.RunId,
        FilePath = copyPath,
        Format = "wav/pcm16",
        SampleRate = source.SampleRate,
        Channels = source.Channels,
        FrameCount = source.FrameCount,
        DeviceId = source.DeviceId,
        CaptureStartOffsetUs = source.CaptureStartOffsetUs,
        ByteLength = new FileInfo(copyPath).Length,
      };
      Pcm16WaveInfo wave = Pcm16WaveFile.ReadAndValidate(stored);
      Pcm16LimiterEnvelope limiterEnvelope = Pcm16WaveAnalyzer.Analyze(
        stored,
        wave,
        CancellationToken.None
      );
      UnityMainThread.Post(() => StartPrepared(operationId, levelPath, stored, wave, limiterEnvelope));
    }
    catch (Exception exception)
    {
      ReplayMicrophonePlaybackFiles.Delete(copyPath);
      UnityMainThread.Post(() =>
      {
        if (_state.IsState(operationId, MicrophoneCalibrationStates.PreviewStarting))
          _state.Error("calibration_preview_prepare_failed", exception.Message, operationId);
      });
    }
  }

  private void StartPrepared(
    string operationId,
    string levelPath,
    StoredMicrophoneRecording recording,
    Pcm16WaveInfo wave,
    Pcm16LimiterEnvelope limiterEnvelope
  )
  {
    if (!_state.IsState(operationId, MicrophoneCalibrationStates.PreviewStarting) || _run == null)
    {
      ReplayMicrophonePlaybackFiles.Delete(recording.FilePath);
      return;
    }
    _originalRunInBackground = UnityEngine.Application.runInBackground;
    _changedRunInBackground = true;
    UnityEngine.Application.runInBackground = true;
    ReplayPlaybackStatus replayStatus = ReplayPlaybackCoordinator.PlayEphemeral(
      _run,
      levelPath,
      recording,
      wave,
      limiterEnvelope
    );
    if (replayStatus.State == ReplayPlaybackStates.Error)
    {
      RestoreRunInBackground();
      _state.Error(replayStatus.ErrorCode, replayStatus.Message, operationId);
      return;
    }
    _replayOperationId = replayStatus.OperationId;
  }

  private void RestoreRunInBackground()
  {
    if (_changedRunInBackground)
      UnityEngine.Application.runInBackground = _originalRunInBackground;
    _changedRunInBackground = false;
  }
}
