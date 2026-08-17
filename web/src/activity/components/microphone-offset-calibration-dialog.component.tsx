import {
  KeyboardIcon,
  Loading03Icon,
  Mic02Icon,
  PlayIcon,
  RefreshIcon,
  StopIcon,
  Tick02Icon,
  WaveSquareIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { TFunction } from "i18next";
import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/ui/button.component";
import { Dialog, DialogContent, DialogTitle } from "@/ui/dialog.component";
import { cn } from "@/ui/ui-class.utils";
import type { MicrophoneOffsetCalibrationData } from "../activity.model";
import {
  buildWaveformAreaPath,
  CALIBRATION_TIMELINE_VISIBLE_MS,
  calibrationTimelineScale,
  clampCalibrationTimelineVisibleMs,
  formatMicrophoneOffset,
  keyboardOffsetAdjustment,
  MAX_MICROPHONE_OFFSET_MS,
  MIN_CALIBRATION_TIMELINE_VISIBLE_MS,
  MIN_MICROPHONE_OFFSET_MS,
  offsetFromPointerDelta,
  zoomCalibrationTimelineVisibleMs,
} from "../lib/microphone-offset.utils";
import type { MicrophoneOffsetCalibrationPhase } from "../lib/microphone-offset-calibration.reducer";
import {
  formatMicrophoneVolumeDb,
  MAX_MICROPHONE_VOLUME_DB,
  MIN_MICROPHONE_VOLUME_DB,
} from "../lib/microphone-volume.utils";

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

function TimingSettingsPanel({
  offsetMs,
  microphoneVolumeDb,
  audioError,
  onCommitOffset,
  onCommitMicrophoneVolume,
  onClose,
  onStartCalibration,
}: {
  offsetMs: number;
  microphoneVolumeDb: number;
  audioError: string;
  onCommitOffset: (offsetMs: number) => void;
  onCommitMicrophoneVolume: (volumeDb: number) => void;
  onClose: () => void | Promise<void>;
  onStartCalibration: () => void;
}) {
  const { t } = useTranslation("microphone");
  return (
    <section className="motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-1 motion-safe:duration-200">
      <header className="border-b border-border px-6 py-5 sm:px-7">
        <DialogTitle>{t("timingSettings.title")}</DialogTitle>
        <p id="microphone-offset-description" className="mt-1 text-sm text-muted-foreground">
          {t("timingSettings.description")}
        </p>
      </header>

      <div className="divide-y divide-border px-6 sm:px-7">
        <TimingRangeControl
          id="microphone-timing-offset"
          icon={WaveSquareIcon}
          label={t("calibration.offset")}
          description={t("timingSettings.offsetDescription")}
          min={MIN_MICROPHONE_OFFSET_MS}
          max={MAX_MICROPHONE_OFFSET_MS}
          value={offsetMs}
          valueText={formatMicrophoneOffset(offsetMs)}
          onChange={onCommitOffset}
        />
        <TimingRangeControl
          id="microphone-timing-gain"
          icon={Mic02Icon}
          label={t("calibration.micGain")}
          description={t("timingSettings.gainDescription")}
          min={MIN_MICROPHONE_VOLUME_DB}
          max={MAX_MICROPHONE_VOLUME_DB}
          value={microphoneVolumeDb}
          valueText={formatMicrophoneVolumeDb(microphoneVolumeDb)}
          onChange={onCommitMicrophoneVolume}
        />
      </div>

      <div className="px-6 pb-1 sm:px-7">
        <Button
          type="button"
          variant="ghost"
          size="sm"
          disabled={offsetMs === 0 && microphoneVolumeDb === 0}
          onClick={() => {
            onCommitOffset(0);
            onCommitMicrophoneVolume(0);
          }}
        >
          <HugeiconsIcon data-icon="inline-start" icon={RefreshIcon} size={14} strokeWidth={2} />
          {t("timingSettings.resetBoth")}
        </Button>
      </div>

      {audioError ? (
        <p
          role="alert"
          className="mx-6 mt-3 rounded-lg bg-destructive/10 px-3 py-2 text-xs text-destructive sm:mx-7"
        >
          {audioError}
        </p>
      ) : null}

      <footer className="mt-5 flex flex-col-reverse gap-2 border-t border-border bg-muted/15 px-6 py-4 sm:flex-row sm:justify-end sm:px-7">
        <Button type="button" variant="outline" onClick={() => void onClose()}>
          {t("calibration.done")}
        </Button>
        <Button type="button" onClick={onStartCalibration}>
          <HugeiconsIcon data-icon="inline-start" icon={WaveSquareIcon} size={15} strokeWidth={2} />
          {t("timingSettings.calibrateWithLevel")}
        </Button>
      </footer>
    </section>
  );
}

function TimingRangeControl({
  id,
  icon,
  label,
  description,
  min,
  max,
  value,
  valueText,
  onChange,
}: {
  id: string;
  icon: typeof Mic02Icon;
  label: string;
  description: string;
  min: number;
  max: number;
  value: number;
  valueText: string;
  onChange: (value: number) => void;
}) {
  return (
    <div className="py-5">
      <div className="flex items-start gap-3">
        <span className="mt-0.5 grid size-8 shrink-0 place-items-center rounded-full bg-primary/12 text-primary">
          <HugeiconsIcon aria-hidden="true" icon={icon} size={15} strokeWidth={2} />
        </span>
        <div className="min-w-0 flex-1">
          <div className="flex items-baseline justify-between gap-4">
            <label htmlFor={id} className="text-sm font-medium">
              {label}
            </label>
            <output htmlFor={id} className="shrink-0 text-sm font-semibold tabular-nums">
              {valueText}
            </output>
          </div>
          <p className="mt-0.5 text-xs text-muted-foreground">{description}</p>
          <input
            id={id}
            type="range"
            min={min}
            max={max}
            step={1}
            value={value}
            aria-valuetext={valueText}
            onChange={(event) => onChange(event.currentTarget.valueAsNumber)}
            className="mt-4 h-1.5 w-full cursor-pointer appearance-none rounded-full bg-muted accent-primary outline-none focus-visible:ring-2 focus-visible:ring-primary/60 focus-visible:ring-offset-2 focus-visible:ring-offset-background"
          />
        </div>
      </div>
    </div>
  );
}

function CalibrationProgress({
  phase,
  error,
  onClose,
}: {
  phase: MicrophoneOffsetCalibrationPhase;
  error: string;
  onClose: () => void;
}) {
  const { t } = useTranslation("microphone");
  const { t: commonT } = useTranslation("common");
  const waiting = phase === "waiting_for_clear";
  const failed = phase === "error";
  return (
    <section className="p-6 motion-safe:animate-in motion-safe:fade-in motion-safe:slide-in-from-bottom-1 motion-safe:duration-200 sm:p-8">
      <div className="flex items-start gap-4">
        <span
          className={cn(
            "grid size-11 shrink-0 place-items-center rounded-full",
            failed ? "bg-destructive/15 text-destructive" : "bg-primary/15 text-primary",
          )}
        >
          <HugeiconsIcon
            aria-hidden="true"
            icon={waiting || failed ? WaveSquareIcon : Loading03Icon}
            size={21}
            strokeWidth={2}
            className={
              failed
                ? undefined
                : waiting
                  ? "animate-pulse motion-reduce:animate-none"
                  : "animate-spin"
            }
          />
        </span>
        <div className="min-w-0">
          <DialogTitle>{t("calibration.launchTitle")}</DialogTitle>
          <p
            id="microphone-offset-description"
            aria-live="polite"
            className="mt-1 text-sm text-muted-foreground"
          >
            {failed
              ? error || t("calibration.failedToStart")
              : waiting
                ? t("calibration.waitingForClear")
                : t("calibration.openingLevel")}
          </p>
        </div>
      </div>

      <div className="mt-8 h-1.5 overflow-hidden rounded-full bg-muted">
        <div
          className={cn(
            "h-full rounded-full transition-[width] duration-700 ease-out motion-reduce:transition-none",
            failed ? "bg-destructive" : "bg-primary",
          )}
          style={{ width: waiting || failed ? "72%" : "34%" }}
        />
      </div>
      <ol className="mt-5 grid grid-cols-3 gap-3 text-xs">
        <ProgressStep
          label={t("calibration.openLevel")}
          active={!waiting && !failed}
          complete={waiting || failed}
        />
        <ProgressStep
          label={t("calibration.clearRun")}
          active={waiting || failed}
          complete={false}
        />
        <ProgressStep label={t("calibration.alignAudio")} active={false} complete={false} />
      </ol>

      <div className="mt-8 flex justify-end border-t border-border pt-5">
        <Button type="button" variant="ghost" onClick={onClose}>
          {commonT("actions.cancel")}
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
  microphoneVolumeDb,
  draftOffsetMs,
  dragging,
  playing,
  playbackPositionMs,
  getPlaybackPositionMs,
  audioError,
  onDraftOffset,
  onDraggingChange,
  onCommitOffset,
  onCommitMicrophoneVolume,
  onResetOffset,
  onTogglePlayback,
  onClose,
}: {
  data: MicrophoneOffsetCalibrationData;
  offsetMs: number;
  microphoneVolumeDb: number;
  draftOffsetMs: number;
  dragging: boolean;
  playing: boolean;
  playbackPositionMs: number;
  getPlaybackPositionMs: () => number;
  audioError: string;
  onDraftOffset: (offsetMs: number) => void;
  onDraggingChange: (dragging: boolean) => void;
  onCommitOffset: (offsetMs: number) => void;
  onCommitMicrophoneVolume: (volumeDb: number) => void;
  onResetOffset: () => void;
  onTogglePlayback: () => void;
  onClose: () => void;
}) {
  const { t } = useTranslation("microphone");
  const { t: commonT, i18n } = useTranslation("common");
  const songPath = useMemo(() => buildWaveformAreaPath(data.songWaveform), [data.songWaveform]);
  const microphonePath = useMemo(
    () => buildWaveformAreaPath(data.microphoneWaveform),
    [data.microphoneWaveform],
  );
  const timelineViewportRef = useRef<HTMLDivElement>(null);
  const timelineContentRef = useRef<HTMLDivElement>(null);
  const pendingZoomCenterRef = useRef<number | null>(null);
  const [timelineVisibleMs, setTimelineVisibleMs] = useState(() =>
    clampCalibrationTimelineVisibleMs(data.durationMs, CALIBRATION_TIMELINE_VISIBLE_MS),
  );
  const timelineScale = calibrationTimelineScale(data.durationMs, timelineVisibleMs);

  useEffect(() => {
    const timeline = timelineContentRef.current;
    if (!timeline) return undefined;
    let frame = 0;
    let timelineWidth = timeline.clientWidth;
    const updatePlayhead = () => {
      const positionMs = Math.max(
        0,
        Math.min(data.durationMs, playing ? getPlaybackPositionMs() : playbackPositionMs),
      );
      const x = data.durationMs > 0 ? (positionMs / data.durationMs) * timelineWidth : 0;
      timeline.style.setProperty("--calibration-playhead-x", `${x}px`);
    };
    const resizeObserver = new ResizeObserver((entries) => {
      timelineWidth = entries[0]?.contentRect.width ?? timeline.clientWidth;
      updatePlayhead();
    });
    resizeObserver.observe(timeline);
    updatePlayhead();
    if (playing) {
      const animate = () => {
        updatePlayhead();
        frame = requestAnimationFrame(animate);
      };
      frame = requestAnimationFrame(animate);
    }
    return () => {
      resizeObserver.disconnect();
      if (frame) cancelAnimationFrame(frame);
    };
  }, [data.durationMs, getPlaybackPositionMs, playbackPositionMs, playing]);

  useEffect(() => {
    pendingZoomCenterRef.current = null;
    setTimelineVisibleMs(
      clampCalibrationTimelineVisibleMs(data.durationMs, CALIBRATION_TIMELINE_VISIBLE_MS),
    );
    if (timelineViewportRef.current) timelineViewportRef.current.scrollLeft = 0;
  }, [data.durationMs]);

  useLayoutEffect(() => {
    const viewport = timelineViewportRef.current;
    const centerRatio = pendingZoomCenterRef.current;
    if (!viewport || centerRatio === null || timelineScale <= 0) return;
    viewport.scrollLeft = centerRatio * viewport.scrollWidth - viewport.clientWidth / 2;
    pendingZoomCenterRef.current = null;
  }, [timelineScale]);

  const changeTimelineZoom = (direction: "in" | "out") => {
    const viewport = timelineViewportRef.current;
    if (viewport?.scrollWidth)
      pendingZoomCenterRef.current =
        (viewport.scrollLeft + viewport.clientWidth / 2) / viewport.scrollWidth;
    setTimelineVisibleMs((currentVisibleMs) =>
      zoomCalibrationTimelineVisibleMs(data.durationMs, currentVisibleMs, direction),
    );
  };

  return (
    <section className="flex max-h-[calc(100svh-2rem)] min-h-0 flex-col">
      <header className="flex shrink-0 flex-col gap-4 border-b border-border px-5 py-5 sm:flex-row sm:items-start sm:justify-between sm:px-7">
        <div className="min-w-0">
          <DialogTitle>{t("calibration.title")}</DialogTitle>
          <p id="microphone-offset-description" className="mt-1 text-sm text-muted-foreground">
            {t("calibration.description")}
          </p>
        </div>
        <div className="shrink-0 text-left sm:text-right">
          <p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
            {t("calibration.offset")}
          </p>
          <p className="mt-0.5 font-heading text-2xl font-semibold tabular-nums tracking-tight">
            {formatMicrophoneOffset(draftOffsetMs)}
          </p>
        </div>
      </header>

      <div className="min-h-0 flex-1 overflow-y-auto px-5 py-5 sm:px-7 sm:py-6">
        <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
          <p className="text-xs text-muted-foreground">{t("calibration.offsetHelp")}</p>
          <div className="flex items-center gap-2">
            <fieldset className="flex items-center rounded-full border border-border bg-muted/20 p-0.5">
              <legend className="sr-only">{t("calibration.timelineZoom")}</legend>
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                aria-label={t("calibration.zoomOut")}
                disabled={timelineVisibleMs >= data.durationMs}
                onClick={() => changeTimelineZoom("out")}
              >
                <span aria-hidden="true" className="text-base leading-none">
                  −
                </span>
              </Button>
              <output className="w-12 text-center text-[10px] font-semibold tabular-nums text-muted-foreground">
                {formatTimelineDuration(timelineVisibleMs, i18n.resolvedLanguage ?? "en", commonT)}
              </output>
              <Button
                type="button"
                variant="ghost"
                size="icon-xs"
                aria-label={t("calibration.zoomIn")}
                disabled={
                  timelineVisibleMs <=
                  Math.min(data.durationMs, MIN_CALIBRATION_TIMELINE_VISIBLE_MS)
                }
                onClick={() => changeTimelineZoom("in")}
              >
                <span aria-hidden="true" className="text-base leading-none">
                  +
                </span>
              </Button>
            </fieldset>
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
              <HugeiconsIcon
                data-icon="inline-start"
                icon={RefreshIcon}
                size={14}
                strokeWidth={2}
              />
              {t("calibration.reset")}
            </Button>
          </div>
        </div>

        <div className="overflow-hidden rounded-xl border border-border bg-background shadow-inner">
          <div className="grid grid-cols-[5.75rem_minmax(0,1fr)] sm:grid-cols-[7.25rem_minmax(0,1fr)]">
            <div className="grid grid-rows-[2.25rem_7rem_7rem]">
              <div className="border-b border-border bg-muted/25" />
              <TrackLabel
                icon={KeyboardIcon}
                label={t("calibration.keyInput")}
                description={t("calibration.recordedTiming")}
              />
              <TrackLabel
                icon={Mic02Icon}
                label={t("calibration.microphone")}
                description={t("calibration.dragToAlign")}
              />
            </div>

            <div ref={timelineViewportRef} className="min-w-0 overflow-x-auto overscroll-x-contain">
              <div
                ref={timelineContentRef}
                className="min-w-full"
                style={{ width: `${timelineScale * 100}%` }}
              >
                <TimelineRuler durationMs={data.durationMs} visibleMs={timelineVisibleMs} />
                <WaveformTrack
                  path={songPath}
                  colorClassName="fill-sky-400/55"
                  playheadVisible={playing || playbackPositionMs > 0}
                />
                <MicrophoneWaveformTrack
                  path={microphonePath}
                  durationMs={data.durationMs}
                  committedOffsetMs={offsetMs}
                  draftOffsetMs={draftOffsetMs}
                  dragging={dragging}
                  playheadVisible={playing || playbackPositionMs > 0}
                  onDraftOffset={onDraftOffset}
                  onDraggingChange={onDraggingChange}
                  onCommitOffset={onCommitOffset}
                />
              </div>
            </div>
          </div>
        </div>

        {audioError ? (
          <p
            role="alert"
            className="mt-3 rounded-lg bg-destructive/10 px-3 py-2 text-xs text-destructive"
          >
            {audioError}
          </p>
        ) : null}
      </div>

      <footer className="flex shrink-0 flex-col gap-3 border-t border-border bg-muted/15 px-5 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-7">
        <div className="flex w-full flex-wrap items-center gap-x-5 gap-y-3 sm:w-auto">
          <Button type="button" variant="outline" onClick={onTogglePlayback}>
            <HugeiconsIcon
              data-icon="inline-start"
              icon={playing ? StopIcon : PlayIcon}
              size={15}
              strokeWidth={2}
            />
            {playing
              ? t("calibration.stop")
              : playbackPositionMs >= data.durationMs
                ? t("calibration.replayTest")
                : t("calibration.playTest")}
          </Button>
          <MicrophoneVolumeControl
            volumeDb={microphoneVolumeDb}
            onChange={onCommitMicrophoneVolume}
          />
        </div>
        <Button
          type="button"
          className="w-full bg-primary text-primary-foreground hover:bg-primary/90 sm:w-auto"
          onClick={onClose}
        >
          {t("calibration.done")}
        </Button>
      </footer>
    </section>
  );
}

function MicrophoneVolumeControl({
  volumeDb,
  onChange,
}: {
  volumeDb: number;
  onChange: (volumeDb: number) => void;
}) {
  const { t } = useTranslation("microphone");
  return (
    <div className="flex min-w-0 flex-1 items-center gap-2.5 sm:min-w-64 sm:flex-none">
      <HugeiconsIcon
        aria-hidden="true"
        icon={Mic02Icon}
        size={15}
        strokeWidth={2}
        className="shrink-0 text-muted-foreground"
      />
      <label
        htmlFor="microphone-preview-volume"
        className="shrink-0 text-xs font-medium text-muted-foreground"
      >
        {t("calibration.micGain")}
      </label>
      <input
        id="microphone-preview-volume"
        type="range"
        min={MIN_MICROPHONE_VOLUME_DB}
        max={MAX_MICROPHONE_VOLUME_DB}
        step={1}
        value={volumeDb}
        aria-valuetext={formatMicrophoneVolumeDb(volumeDb)}
        onChange={(event) => onChange(event.currentTarget.valueAsNumber)}
        className="h-1.5 min-w-20 flex-1 cursor-pointer appearance-none rounded-full bg-muted accent-primary outline-none focus-visible:ring-2 focus-visible:ring-primary/60 focus-visible:ring-offset-2 focus-visible:ring-offset-background sm:w-28 sm:flex-none"
      />
      <output
        htmlFor="microphone-preview-volume"
        className="w-14 shrink-0 text-right text-xs font-semibold tabular-nums"
      >
        {formatMicrophoneVolumeDb(volumeDb)}
      </output>
    </div>
  );
}

function TimelineRuler({ durationMs, visibleMs }: { durationMs: number; visibleMs: number }) {
  const { t, i18n } = useTranslation("common");
  const locale = i18n.resolvedLanguage ?? "en";
  const tickIntervalMs = visibleMs <= 1_000 ? 250 : visibleMs <= 2_000 ? 500 : 1_000;
  const ticks: Array<{ id: string; label: string; position: number }> = [];
  for (let elapsedMs = 0; elapsedMs <= durationMs; elapsedMs += tickIntervalMs) {
    ticks.push({
      id: `time-${elapsedMs}ms`,
      label: formatTimelineTimestamp(elapsedMs, locale, t),
      position: (elapsedMs / durationMs) * 100,
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

function formatTimelineDuration(durationMs: number, locale: string, t: TFunction<"common">) {
  if (durationMs >= 1_000)
    return t("units.seconds", {
      value: new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(durationMs / 1_000),
    });
  return t("units.milliseconds", { value: Math.round(durationMs).toLocaleString(locale) });
}

function formatTimelineTimestamp(elapsedMs: number, locale: string, t: TFunction<"common">) {
  return t("units.seconds", {
    value: new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(elapsedMs / 1_000),
  });
}

function TrackLabel({
  icon,
  label,
  description,
}: {
  icon: typeof KeyboardIcon;
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
  playheadVisible,
}: {
  path: string;
  colorClassName: string;
  playheadVisible: boolean;
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
      <Playhead visible={playheadVisible} />
    </div>
  );
}

function MicrophoneWaveformTrack({
  path,
  durationMs,
  committedOffsetMs,
  draftOffsetMs,
  dragging,
  playheadVisible,
  onDraftOffset,
  onDraggingChange,
  onCommitOffset,
}: {
  path: string;
  durationMs: number;
  committedOffsetMs: number;
  draftOffsetMs: number;
  dragging: boolean;
  playheadVisible: boolean;
  onDraftOffset: (offsetMs: number) => void;
  onDraggingChange: (dragging: boolean) => void;
  onCommitOffset: (offsetMs: number) => void;
}) {
  const { t } = useTranslation("microphone");
  const dragRef = useRef<{
    pointerId: number;
    startX: number;
    startOffsetMs: number;
    timelineWidth: number;
  } | null>(null);
  const draftOffsetRef = useRef(draftOffsetMs);
  const keyboardAdjustingRef = useRef(false);
  draftOffsetRef.current = draftOffsetMs;
  const translationPercent = (-draftOffsetMs / durationMs) * 100;

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
      aria-label={t("calibration.offsetSlider")}
      aria-valuemin={MIN_MICROPHONE_OFFSET_MS}
      aria-valuemax={MAX_MICROPHONE_OFFSET_MS}
      aria-valuenow={draftOffsetMs}
      aria-valuetext={formatMicrophoneOffset(draftOffsetMs)}
      style={{ WebkitTouchCallout: "none" }}
      onPointerDown={(event) => {
        if (event.button !== 0) return;
        event.preventDefault();
        event.currentTarget.focus({ preventScroll: true });
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
        if (dragRef.current?.pointerId !== event.pointerId) return;
        event.preventDefault();
        updatePointerOffset(event.clientX);
      }}
      onPointerUp={(event) => {
        if (dragRef.current?.pointerId !== event.pointerId) return;
        event.preventDefault();
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
      onContextMenu={(event) => event.preventDefault()}
      onDragStart={(event) => event.preventDefault()}
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
        "relative min-h-28 touch-none select-none overflow-hidden bg-primary/[0.035] outline-none transition-[transform,box-shadow,background-color] duration-150 focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-primary/60",
        dragging
          ? "z-10 -translate-y-1 cursor-grabbing bg-primary/[0.07] shadow-[0_14px_36px_rgba(0,0,0,0.22)]"
          : "cursor-grab hover:bg-primary/[0.055]",
      )}
    >
      <TimelineGrid />
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-0 will-change-transform"
        style={{ transform: `translate3d(${translationPercent}%, 0, 0)` }}
      >
        <svg
          aria-hidden="true"
          viewBox="0 0 1000 100"
          preserveAspectRatio="none"
          className="absolute inset-0 h-full w-full select-none py-4"
        >
          <path d={path} className="fill-primary/75" />
        </svg>
      </div>
      <Playhead visible={playheadVisible} />
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

function Playhead({ visible }: { visible: boolean }) {
  return (
    <div
      aria-hidden="true"
      className={cn(
        "pointer-events-none absolute inset-y-0 left-0 z-20 w-px bg-primary shadow-[0_0_0_1px_color-mix(in_oklab,var(--primary)_25%,transparent)] transition-opacity will-change-transform",
        visible ? "opacity-100" : "opacity-0",
      )}
      style={{ transform: "translate3d(var(--calibration-playhead-x, 0px), 0, 0)" }}
    />
  );
}
