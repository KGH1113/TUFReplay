using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Newtonsoft.Json;
using TUFReplay.Microphone.Models;
using TUFReplay.Microphone.Playback;
using TUFReplay.Microphone.Processing;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.NativeInput;
using TUFReplay.Replay.Playback;
using UnityEngine;

namespace TUFReplay.Replay.Preparation;

public static partial class ReplayPlaybackCoordinator
{
  private sealed class PendingReplay
  {
    public readonly string OperationId;
    public readonly StoredReplayRun Run;
    public string PlaybackLevelPath;
    public readonly ReplayMetadata Meta;
    public readonly List<RecordedInput> Inputs;
    public readonly List<ReplayHitContext> HitContexts;
    public readonly long TerminalTimeUs;
    public readonly CancellationTokenSource PreparationCancellation = new CancellationTokenSource();
    public double LevelOpenStartedAt;
    public object LevelDataBeforeOpen;
    public bool LevelOpenRequested;
    public bool LevelOpenObservedTransition;
    public bool HasLoadedLevelValidation;
    public object ValidatedLevelData;
    public string ValidatedLevelPath;
    public bool LoadedLevelValidationPassed;
    public string LoadedLevelValidationCode;
    public string LoadedLevelValidationMessage;
    public byte[] ValidatedLoadedGameplayHash;
    public int ValidatedLoadedGameplayHashVersion;
    public bool PathEditingLockApplied;
    public INativeInputFocusGuard NativeInputFocusGuard;
    public StoredMicrophoneRecording MicrophoneRecording;
    public Pcm16WaveInfo MicrophoneWave;
    public Pcm16LimiterEnvelope MicrophoneLimiterEnvelope;
    public int? MicrophoneOffsetMs;
    public int? MicrophoneVolumeDb;
    public bool AllowBackground;

    public PendingReplay(
      string operationId,
      StoredReplayRun run,
      string playbackLevelPath,
      ReplayMetadata meta,
      List<RecordedInput> inputs,
      List<ReplayHitContext> hitContexts,
      long terminalTimeUs
    )
    {
      OperationId = operationId;
      Run = run;
      PlaybackLevelPath = playbackLevelPath;
      Meta = meta;
      Inputs = inputs;
      HitContexts = hitContexts;
      TerminalTimeUs = terminalTimeUs;
    }

    public void TransferMicrophoneOwnership()
    {
      MicrophoneRecording = null;
      MicrophoneWave = null;
      MicrophoneLimiterEnvelope = null;
    }

    public void CleanupPreparedMicrophone()
    {
      string path = MicrophoneRecording?.FilePath;
      MicrophoneRecording = null;
      MicrophoneWave = null;
      MicrophoneLimiterEnvelope = null;
      ReplayMicrophonePlaybackFiles.Delete(path);
    }
  }

  private sealed class AlwaysReadyFocusGuard : INativeInputFocusGuard
  {
    public static readonly AlwaysReadyFocusGuard Instance = new AlwaysReadyFocusGuard();

    public bool IsStable(out string reason)
    {
      reason = null;
      return true;
    }

    public bool IsForegroundTarget(out string reason) => IsStable(out reason);

    public string Describe() => "calibration-background";
  }

  private sealed class NullNativeInputEmitter : INativeInputEmitter
  {
    public static readonly NullNativeInputEmitter Instance = new NullNativeInputEmitter();

    public bool IsSupported(int key) => true;

    public bool EmitBatch(NativeInputEmission[] emissions, int count) => true;
  }
}
