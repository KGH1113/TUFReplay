using System;
using System.Collections.Generic;
using System.Diagnostics;
using TUFReplay.Activity.Models;
using TUFReplay.Recording.Input;
using TUFReplay.Replay.Models;

using static TUFReplay.Recording.Telemetry.RecordingRuntimeTelemetry;

namespace TUFReplay.Recording.Sessions;

public partial class RecordingSession
{
  public void AddInputAtCurrentTime(int key, RecordInputFlags flags)
  {
    lock (_lock)
    {
      if (!IsRecording)
        return;

      if (!Data.GameplayStartSongPosition.HasValue)
      {
        _pendingNativeInputs.Add(
          new NativeInputTransition(
            Stopwatch.GetTimestamp(),
            0L,
            key,
            (flags & RecordInputFlags.Down) != 0,
            (flags & RecordInputFlags.ExtendedKey) != 0
          )
        );
        return;
      }

      AddInputLocked(CurrentTimelineTimeUsLocked(), key, flags);
    }
  }

  internal int AddInputBatch(ReadOnlySpan<NativeInputTransition> inputs)
  {
    if (inputs.Length == 0)
      return 0;

    lock (_lock)
    {
      if (!IsRecording)
        return 0;

      for (int i = 0; i < inputs.Length; i++)
        _pendingNativeInputs.Add(inputs[i]);

      Data.InputPendingMax = Math.Max(Data.InputPendingMax, _pendingNativeInputs.Count);

      return inputs.Length;
    }
  }

  internal void ObserveInputAnchor(
    long prefixCaptureTicks,
    long postfixCaptureTicks,
    double songPosition,
    double timelineRate,
    bool ready
  )
  {
    lock (_lock)
    {
      if (!IsRecording || !IsCapturingInput)
        return;

      long durationTicks = Math.Max(0L, postfixCaptureTicks - prefixCaptureTicks);
      long durationUs = CaptureTicksToMicroseconds(durationTicks);
      Data.InputAnchorMaxDurationUs = Math.Max(Data.InputAnchorMaxDurationUs, durationUs);
      long captureTicks = prefixCaptureTicks + durationTicks / 2L;
      ObserveInputAnchorLocked(captureTicks, songPosition, timelineRate, ready, forceSegmentBreak: false);
    }
  }

  internal void BreakInputTimeline(string reason)
  {
    lock (_lock)
    {
      WriteEvidenceStateLocked(TUFReplay.Recording.Capture.RecordingStateKind.Discontinuity);
      if (_previousInputAnchor.HasValue)
      {
        FlushPendingNativeInputsLocked();
        _previousInputAnchor = null;
        Data.InputDiscontinuities++;
        Data.InputLastDiscontinuity = reason;
      }
    }
  }

  public void AddHitContext(RecordedHitContext hitContext)
  {
    lock (_lock)
    {
      if (!IsRecording)
        return;
      hitContext.TimeUs = Math.Max(0L, CurrentTimelineTimeUsLocked());
      RefreshNoFailModeLocked();
      CommitEvidenceHitLocked();
      Data.HitContexts.Add(hitContext);
    }
  }

  public void RemoveLastHitContext()
  {
    lock (_lock)
    {
      if (!IsRecording || Data.HitContexts.Count == 0)
        return;
      if (Data.HitContexts.Count <= _evidenceCommittedHits)
        AbortEvidenceLocked("committed_hit_removed");
      Data.HitContexts.RemoveAt(Data.HitContexts.Count - 1);
    }
  }

  public void SetLastHitContextMargin(int hitMargin)
  {
    lock (_lock)
    {
      if (!IsRecording || Data.HitContexts.Count == 0)
        return;

      int index = Data.HitContexts.Count - 1;
      if (index < _evidenceCommittedHits) AbortEvidenceLocked("committed_hit_changed");
      RecordedHitContext context = Data.HitContexts[index];
      context.ResolvedHitMargin = hitMargin;
      Data.HitContexts[index] = context;
    }
  }
}
