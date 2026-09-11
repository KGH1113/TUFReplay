import { useEffect, useState } from "react";
import { OffsetEditor } from "@/components/calibration/calibration-editor";
import {
  CalibrationProgress,
  TimingSettingsPanel,
} from "@/components/calibration/calibration-progress";
import type { MicrophoneOffsetCalibrationData } from "@/models/calibration/microphone-offset-calibration-data";
import { cn } from "@/shared/lib/cn";
import { Dialog, DialogContent } from "@/shared/ui/dialog";
import type { MicrophoneOffsetCalibrationPhase } from "@/state/calibration/microphone-offset-reducer";
export function MicrophoneOffsetCalibrationDialog({
  data,
  phase,
  offsetMs,
  microphoneVolumeDb,
  playing,
  playbackPositionMs,
  getPlaybackPositionMs,
  audioError,
  onClose,
  onStartCalibration,
  onCommitOffset,
  onCommitMicrophoneVolume,
  onResetOffset,
  onTogglePlayback,
}: {
  data: MicrophoneOffsetCalibrationData;
  phase: MicrophoneOffsetCalibrationPhase;
  offsetMs: number;
  microphoneVolumeDb: number;
  playing: boolean;
  playbackPositionMs: number;
  getPlaybackPositionMs: () => number;
  audioError: string;
  onClose: () => void | Promise<void>;
  onStartCalibration: () => void;
  onCommitOffset: (offsetMs: number) => void;
  onCommitMicrophoneVolume: (volumeDb: number) => void;
  onResetOffset: () => void;
  onTogglePlayback: () => void;
}) {
  const [draftOffsetMs, setDraftOffsetMs] = useState(offsetMs);
  const [dragging, setDragging] = useState(false);

  useEffect(() => {
    if (!dragging) setDraftOffsetMs(offsetMs);
  }, [dragging, offsetMs]);

  const open = phase !== "closed";
  const settings = phase === "settings";
  const editing = phase === "editing";

  return (
    <Dialog open={open} onOpenChange={(nextOpen) => !nextOpen && void onClose()}>
      <DialogContent
        aria-describedby="microphone-offset-description"
        className={cn(
          "w-[calc(100vw_-_2rem)] overflow-hidden p-0 transition-[max-width] duration-300 ease-out motion-reduce:transition-none",
          editing
            ? "max-h-[calc(100svh-2rem)] min-h-0 max-w-[68rem]"
            : settings
              ? "max-w-[32rem]"
              : "max-w-[38rem]",
        )}
      >
        {settings ? (
          <TimingSettingsPanel
            offsetMs={offsetMs}
            microphoneVolumeDb={microphoneVolumeDb}
            audioError={audioError}
            onCommitOffset={onCommitOffset}
            onCommitMicrophoneVolume={onCommitMicrophoneVolume}
            onClose={onClose}
            onStartCalibration={onStartCalibration}
          />
        ) : editing ? (
          <OffsetEditor
            data={data}
            offsetMs={offsetMs}
            microphoneVolumeDb={microphoneVolumeDb}
            draftOffsetMs={draftOffsetMs}
            dragging={dragging}
            playing={playing}
            playbackPositionMs={playbackPositionMs}
            getPlaybackPositionMs={getPlaybackPositionMs}
            audioError={audioError}
            onDraftOffset={setDraftOffsetMs}
            onDraggingChange={setDragging}
            onCommitOffset={onCommitOffset}
            onCommitMicrophoneVolume={onCommitMicrophoneVolume}
            onResetOffset={onResetOffset}
            onTogglePlayback={onTogglePlayback}
            onClose={onClose}
          />
        ) : (
          <CalibrationProgress phase={phase} error={audioError} onClose={onClose} />
        )}
      </DialogContent>
    </Dialog>
  );
}
