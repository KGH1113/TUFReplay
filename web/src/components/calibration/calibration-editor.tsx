import {
  KeyboardIcon,
  Mic02Icon,
  PlayIcon,
  RefreshIcon,
  StopIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  formatTimelineDuration,
  MicrophoneWaveformTrack,
  TimelineRuler,
  TrackLabel,
  WaveformTrack,
} from "@/components/calibration/calibration-waveform";
import {
  buildWaveformAreaPath,
  CALIBRATION_TIMELINE_VISIBLE_MS,
  calibrationTimelineScale,
  clampCalibrationTimelineVisibleMs,
  formatMicrophoneOffset,
  MIN_CALIBRATION_TIMELINE_VISIBLE_MS,
  zoomCalibrationTimelineVisibleMs,
} from "@/models/calibration/microphone-offset";
import type { MicrophoneOffsetCalibrationData } from "@/models/calibration/microphone-offset-calibration-data";
import {
  formatMicrophoneVolumeDb,
  MAX_MICROPHONE_VOLUME_DB,
  MIN_MICROPHONE_VOLUME_DB,
} from "@/models/calibration/microphone-volume";
import { Button } from "@/shared/ui/button";
import { DialogTitle } from "@/shared/ui/dialog";
export function OffsetEditor({
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

export function MicrophoneVolumeControl({
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
