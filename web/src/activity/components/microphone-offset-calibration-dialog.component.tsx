import {
  Loading03Icon,
  Mic02Icon,
  MusicNote01Icon,
  PlayIcon,
  RefreshIcon,
  StopIcon,
  Tick02Icon,
  WaveSquareIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useMemo, useRef, useState } from "react";

import { Button } from "@/ui/button.component";
import { Dialog, DialogContent, DialogTitle } from "@/ui/dialog.component";
import { cn } from "@/ui/ui-class.utils";
import {
  buildWaveformAreaPath,
  formatMicrophoneOffset,
  keyboardOffsetAdjustment,
  MAX_MICROPHONE_OFFSET_MS,
  MIN_MICROPHONE_OFFSET_MS,
  offsetFromPointerDelta,
} from "../lib/microphone-offset.utils";
import type { MicrophoneOffsetCalibrationPhase } from "../lib/microphone-offset-calibration.reducer";
import type { MockMicrophoneOffsetCalibrationData } from "../mock/microphone-offset.mock";

export function MicrophoneOffsetCalibrationDialog({
  data,
  phase,
  offsetMs,
  playing,
  playbackPositionMs,
  audioError,
  onClose,
  onCommitOffset,
  onResetOffset,
  onTogglePlayback,
}: {
  data: MockMicrophoneOffsetCalibrationData;
  phase: MicrophoneOffsetCalibrationPhase;
  offsetMs: number;
  playing: boolean;
  playbackPositionMs: number;
  audioError: string;
  onClose: () => void;
  onCommitOffset: (offsetMs: number) => void;
  onResetOffset: () => void;
  onTogglePlayback: () => void;
}) {
  const [draftOffsetMs, setDraftOffsetMs] = useState(offsetMs);
  const [dragging, setDragging] = useState(false);

  useEffect(() => {
    if (!dragging) setDraftOffsetMs(offsetMs);
  }, [dragging, offsetMs]);

  const open = phase !== "closed";
  const editing = phase === "editing";

  return (
    <Dialog open={open} onOpenChange={(nextOpen) => !nextOpen && onClose()}>
      <DialogContent
        aria-describedby="microphone-offset-description"
        className={cn(
          "w-[calc(100vw_-_2rem)] overflow-hidden p-0 transition-[max-width] duration-300 motion-reduce:transition-none",
          editing ? "max-h-[calc(100svh-2rem)] min-h-0 max-w-[68rem]" : "max-w-[38rem]",
        )}
      >
        {editing ? (
          <OffsetEditor
            data={data}
            offsetMs={offsetMs}
            draftOffsetMs={draftOffsetMs}
            dragging={dragging}
            playing={playing}
            playbackPositionMs={playbackPositionMs}
            audioError={audioError}
            onDraftOffset={setDraftOffsetMs}
            onDraggingChange={setDragging}
            onCommitOffset={onCommitOffset}
            onResetOffset={onResetOffset}
            onTogglePlayback={onTogglePlayback}
            onClose={onClose}
          />
        ) : (
          <CalibrationProgress phase={phase} onClose={onClose} />
        )}
      </DialogContent>
    </Dialog>
  );
}

function CalibrationProgress({
  phase,
  onClose,
}: {
  phase: MicrophoneOffsetCalibrationPhase;
  onClose: () => void;
}) {
  const waiting = phase === "waiting_for_clear";
  return (
    <section className="p-6 sm:p-8">
      <div className="flex items-start gap-4">
        <span className="grid size-11 shrink-0 place-items-center rounded-full bg-primary/15 text-primary">
          <HugeiconsIcon
            aria-hidden="true"
            icon={waiting ? WaveSquareIcon : Loading03Icon}
            size={21}
            strokeWidth={2}
            className={waiting ? "animate-pulse motion-reduce:animate-none" : "animate-spin"}
          />
        </span>
        <div className="min-w-0">
          <DialogTitle>Calibrate microphone timing</DialogTitle>
          <p
            id="microphone-offset-description"
            aria-live="polite"
            className="mt-1 text-sm text-muted-foreground"
          >
            {waiting
              ? "Playing the calibration run and waiting for its clear."
              : "Opening the built-in calibration level."}
          </p>
        </div>
      </div>

      <div className="mt-8 h-1.5 overflow-hidden rounded-full bg-muted">
        <div
          className="h-full rounded-full bg-primary transition-[width] duration-700 ease-out motion-reduce:transition-none"
          style={{ width: waiting ? "72%" : "34%" }}
        />
      </div>
      <ol className="mt-5 grid grid-cols-3 gap-3 text-xs">
        <ProgressStep label="Open level" active={!waiting} complete={waiting} />
        <ProgressStep label="Clear run" active={waiting} complete={false} />
        <ProgressStep label="Align audio" active={false} complete={false} />
      </ol>

      <div className="mt-8 flex justify-end border-t border-border pt-5">
        <Button type="button" variant="ghost" onClick={onClose}>
          Cancel
        </Button>
      </div>
    </section>
  );
}

function ProgressStep({
  label,
  active,
  complete,
}: {
  label: string;
  active: boolean;
  complete: boolean;
}) {
  return (
    <li
      className={cn(
        "flex items-center gap-2 text-muted-foreground",
        (active || complete) && "text-foreground",
      )}
    >
      <span
        className={cn(
          "grid size-5 shrink-0 place-items-center rounded-full border border-border text-[10px]",
          active && "border-primary bg-primary/15 text-primary",
          complete && "border-primary bg-primary text-primary-foreground",
        )}
      >
        {complete ? (
          <HugeiconsIcon aria-hidden="true" icon={Tick02Icon} size={12} strokeWidth={2.5} />
        ) : (
          <span className={cn(active && "size-1.5 rounded-full bg-primary")} />
        )}
      </span>
      <span className="truncate">{label}</span>
    </li>
  );
}

function OffsetEditor({
  data,
  offsetMs,
  draftOffsetMs,
  dragging,
  playing,
  playbackPositionMs,
  audioError,
  onDraftOffset,
  onDraggingChange,
  onCommitOffset,
  onResetOffset,
  onTogglePlayback,
  onClose,
}: {
  data: MockMicrophoneOffsetCalibrationData;
  offsetMs: number;
  draftOffsetMs: number;
  dragging: boolean;
  playing: boolean;
  playbackPositionMs: number;
  audioError: string;
  onDraftOffset: (offsetMs: number) => void;
  onDraggingChange: (dragging: boolean) => void;
  onCommitOffset: (offsetMs: number) => void;
  onResetOffset: () => void;
  onTogglePlayback: () => void;
  onClose: () => void;
}) {
  const gamePath = useMemo(() => buildWaveformAreaPath(data.gameWaveform), [data.gameWaveform]);
  const microphonePath = useMemo(
    () => buildWaveformAreaPath(data.microphoneWaveform),
    [data.microphoneWaveform],
  );
  const playheadPercent = Math.min(100, (playbackPositionMs / data.durationMs) * 100);

  return (
    <section className="flex max-h-[calc(100svh-2rem)] min-h-0 flex-col">
      <header className="flex shrink-0 flex-col gap-4 border-b border-border px-5 py-5 sm:flex-row sm:items-start sm:justify-between sm:px-7">
        <div className="min-w-0">
          <DialogTitle>Microphone timing</DialogTitle>
          <p id="microphone-offset-description" className="mt-1 text-sm text-muted-foreground">
            Drag the microphone waveform until its transients line up with the game audio.
          </p>
        </div>
        <div className="shrink-0 text-left sm:text-right">
          <p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
            Microphone offset
          </p>
          <p className="mt-0.5 font-heading text-2xl font-semibold tabular-nums tracking-tight">
            {formatMicrophoneOffset(draftOffsetMs)}
          </p>
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-7 sm:py-6">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-muted-foreground">
            Positive values delay the microphone. Use arrow keys for 1ms or Shift for 10ms.
          </p>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={offsetMs === 0 && draftOffsetMs === 0}
            onClick={() => {
              onDraftOffset(0);
              onResetOffset();
            }}
          >
            <HugeiconsIcon data-icon="inline-start" icon={RefreshIcon} size={14} strokeWidth={2} />
            Reset to 0
          </Button>
        </div>

        <div className="overflow-hidden rounded-xl border border-border bg-background shadow-inner">
          <div className="grid grid-cols-[5.75rem_minmax(0,1fr)] sm:grid-cols-[7.25rem_minmax(0,1fr)]">
            <div className="border-b border-border bg-muted/25" />
            <TimelineRuler durationMs={data.durationMs} />

            <TrackLabel icon={MusicNote01Icon} label="Game audio" description="Reference" />
            <WaveformTrack
              path={gamePath}
              colorClassName="fill-foreground/55"
              playheadPercent={playheadPercent}
              playing={playing}
            />

            <TrackLabel icon={Mic02Icon} label="Microphone" description="Drag to align" />
            <MicrophoneWaveformTrack
              path={microphonePath}
              durationMs={data.durationMs}
              committedOffsetMs={offsetMs}
              draftOffsetMs={draftOffsetMs}
              dragging={dragging}
              playheadPercent={playheadPercent}
              playing={playing}
              onDraftOffset={onDraftOffset}
              onDraggingChange={onDraggingChange}
              onCommitOffset={onCommitOffset}
            />
          </div>
        </div>

        {audioError ? (
          <p
            role="alert"
            className="mt-3 rounded-lg bg-destructive/10 px-3 py-2 text-xs text-destructive"
          >
            {audioError} The visual preview is still running.
          </p>
        ) : null}
      </div>

      <footer className="flex shrink-0 items-center justify-between gap-3 border-t border-border bg-muted/15 px-5 py-4 sm:px-7">
        <Button type="button" variant="outline" onClick={onTogglePlayback}>
          <HugeiconsIcon
            data-icon="inline-start"
            icon={playing ? StopIcon : PlayIcon}
            size={15}
            strokeWidth={2}
          />
          {playing ? "Stop" : playbackPositionMs >= data.durationMs ? "Replay test" : "Play test"}
        </Button>
        <Button
          type="button"
          className="bg-primary text-primary-foreground hover:bg-primary/90"
          onClick={onClose}
        >
          Done
        </Button>
      </footer>
    </section>
  );
}

function TimelineRuler({ durationMs }: { durationMs: number }) {
  const seconds = Math.round(durationMs / 1_000);
  const ticks: Array<{ id: string; label: string; position: number }> = [];
  for (let elapsedSeconds = 0; elapsedSeconds <= seconds; elapsedSeconds += 1) {
    ticks.push({
      id: `time-${elapsedSeconds}s`,
      label: `${elapsedSeconds}s`,
      position: (elapsedSeconds / seconds) * 100,
    });
  }
  return (
    <div className="relative flex h-9 items-end border-b border-border bg-muted/25 px-2 pb-1.5 text-[10px] tabular-nums text-muted-foreground">
      {ticks.map((tick) => (
        <span
          key={tick.id}
          className={cn(
            "absolute",
            tick.position === 0
              ? "translate-x-0"
              : tick.position === 100
                ? "-translate-x-full"
                : "-translate-x-1/2",
          )}
          style={{ left: `${tick.position}%` }}
        >
          {tick.label}
        </span>
      ))}
    </div>
  );
}

function TrackLabel({
  icon,
  label,
  description,
}: {
  icon: typeof MusicNote01Icon;
  label: string;
  description: string;
}) {
  return (
    <div className="flex min-h-28 flex-col justify-center border-b border-border bg-muted/10 px-3 last:border-b-0 sm:px-4">
      <HugeiconsIcon aria-hidden="true" icon={icon} size={16} strokeWidth={2} />
      <p className="mt-2 truncate text-xs font-semibold">{label}</p>
      <p className="mt-0.5 truncate text-[10px] text-muted-foreground">{description}</p>
    </div>
  );
}

function WaveformTrack({
  path,
  colorClassName,
  playheadPercent,
  playing,
}: {
  path: string;
  colorClassName: string;
  playheadPercent: number;
  playing: boolean;
}) {
  return (
    <div className="relative min-h-28 overflow-hidden border-b border-border bg-muted/5">
      <TimelineGrid />
      <svg
        aria-hidden="true"
        viewBox="0 0 1000 100"
        preserveAspectRatio="none"
        className="absolute inset-0 h-full w-full px-0 py-4"
      >
        <path d={path} className={colorClassName} />
      </svg>
      <Playhead percent={playheadPercent} visible={playing || playheadPercent > 0} />
    </div>
  );
}

function MicrophoneWaveformTrack({
  path,
  durationMs,
  committedOffsetMs,
  draftOffsetMs,
  dragging,
  playheadPercent,
  playing,
  onDraftOffset,
  onDraggingChange,
  onCommitOffset,
}: {
  path: string;
  durationMs: number;
  committedOffsetMs: number;
  draftOffsetMs: number;
  dragging: boolean;
  playheadPercent: number;
  playing: boolean;
  onDraftOffset: (offsetMs: number) => void;
  onDraggingChange: (dragging: boolean) => void;
  onCommitOffset: (offsetMs: number) => void;
}) {
  const dragRef = useRef<{
    pointerId: number;
    startX: number;
    startOffsetMs: number;
    timelineWidth: number;
  } | null>(null);
  const draftOffsetRef = useRef(draftOffsetMs);
  const keyboardAdjustingRef = useRef(false);
  draftOffsetRef.current = draftOffsetMs;
  const translation = (draftOffsetMs / durationMs) * 1_000;

  const updatePointerOffset = (clientX: number) => {
    const drag = dragRef.current;
    if (!drag) return;
    const nextOffsetMs = offsetFromPointerDelta(
      drag.startOffsetMs,
      clientX - drag.startX,
      drag.timelineWidth,
      durationMs,
    );
    draftOffsetRef.current = nextOffsetMs;
    onDraftOffset(nextOffsetMs);
  };

  const commitKeyboardOffset = () => {
    if (!keyboardAdjustingRef.current) return;
    keyboardAdjustingRef.current = false;
    onCommitOffset(draftOffsetRef.current);
  };

  return (
    <div
      role="slider"
      tabIndex={0}
      aria-label="Microphone timing offset"
      aria-valuemin={MIN_MICROPHONE_OFFSET_MS}
      aria-valuemax={MAX_MICROPHONE_OFFSET_MS}
      aria-valuenow={draftOffsetMs}
      aria-valuetext={formatMicrophoneOffset(draftOffsetMs)}
      title="Drag left or right to adjust microphone timing"
      onPointerDown={(event) => {
        if (event.button !== 0) return;
        const timelineWidth = event.currentTarget.getBoundingClientRect().width;
        event.currentTarget.setPointerCapture(event.pointerId);
        dragRef.current = {
          pointerId: event.pointerId,
          startX: event.clientX,
          startOffsetMs: draftOffsetRef.current,
          timelineWidth,
        };
        onDraggingChange(true);
      }}
      onPointerMove={(event) => {
        if (dragRef.current?.pointerId === event.pointerId) updatePointerOffset(event.clientX);
      }}
      onPointerUp={(event) => {
        if (dragRef.current?.pointerId !== event.pointerId) return;
        updatePointerOffset(event.clientX);
        dragRef.current = null;
        if (event.currentTarget.hasPointerCapture(event.pointerId))
          event.currentTarget.releasePointerCapture(event.pointerId);
        onDraggingChange(false);
        onCommitOffset(draftOffsetRef.current);
      }}
      onPointerCancel={(event) => {
        if (dragRef.current?.pointerId !== event.pointerId) return;
        dragRef.current = null;
        onDraftOffset(committedOffsetMs);
        onDraggingChange(false);
      }}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.preventDefault();
          keyboardAdjustingRef.current = false;
          draftOffsetRef.current = committedOffsetMs;
          onDraftOffset(committedOffsetMs);
          return;
        }
        const nextOffsetMs = keyboardOffsetAdjustment(
          draftOffsetRef.current,
          event.key,
          event.shiftKey,
        );
        if (nextOffsetMs === null) return;
        event.preventDefault();
        keyboardAdjustingRef.current = true;
        draftOffsetRef.current = nextOffsetMs;
        onDraftOffset(nextOffsetMs);
      }}
      onKeyUp={(event) => {
        if (event.key === "ArrowLeft" || event.key === "ArrowRight") commitKeyboardOffset();
      }}
      onBlur={commitKeyboardOffset}
      className={cn(
        "relative min-h-28 touch-none overflow-hidden bg-primary/[0.035] outline-none transition-[transform,box-shadow,background-color] duration-150 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/60",
        dragging
          ? "z-10 -translate-y-1 scale-[1.008] cursor-grabbing bg-primary/[0.07] shadow-[0_14px_36px_rgba(0,0,0,0.22)]"
          : "cursor-grab hover:bg-primary/[0.055]",
      )}
    >
      <TimelineGrid />
      <svg
        aria-hidden="true"
        viewBox="0 0 1000 100"
        preserveAspectRatio="none"
        className="absolute inset-0 h-full w-full py-4"
      >
        <path
          d={path}
          transform={`translate(${translation} 0)`}
          className="fill-primary/75 transition-transform duration-75 motion-reduce:transition-none"
        />
      </svg>
      <Playhead percent={playheadPercent} visible={playing || playheadPercent > 0} />
    </div>
  );
}

function TimelineGrid() {
  return (
    <div
      aria-hidden="true"
      className="absolute inset-0 opacity-50"
      style={{
        backgroundImage:
          "linear-gradient(to right, var(--border) 1px, transparent 1px), linear-gradient(to bottom, transparent 49.5%, var(--border) 50%, transparent 50.5%)",
        backgroundSize: "10% 100%, 100% 100%",
      }}
    />
  );
}

function Playhead({ percent, visible }: { percent: number; visible: boolean }) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        "pointer-events-none absolute inset-y-0 z-20 w-px bg-primary shadow-[0_0_0_1px_color-mix(in_oklab,var(--primary)_25%,transparent)] transition-opacity",
        visible ? "opacity-100" : "opacity-0",
      )}
      style={{ left: `${percent}%` }}
    />
  );
}
