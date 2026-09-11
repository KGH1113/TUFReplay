import type { KeyboardIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { TFunction } from "i18next";
import { useRef } from "react";
import { useTranslation } from "react-i18next";
import {
  formatMicrophoneOffset,
  keyboardOffsetAdjustment,
  MAX_MICROPHONE_OFFSET_MS,
  MIN_MICROPHONE_OFFSET_MS,
  offsetFromPointerDelta,
} from "@/models/calibration/microphone-offset";
import { cn } from "@/shared/lib/cn";

export function TimelineRuler({
  durationMs,
  visibleMs,
}: {
  durationMs: number;
  visibleMs: number;
}) {
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

export function formatTimelineDuration(durationMs: number, locale: string, t: TFunction<"common">) {
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

export function TrackLabel({
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

export function WaveformTrack({
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

export function MicrophoneWaveformTrack({
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

export function TimelineGrid() {
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

export function Playhead({ visible }: { visible: boolean }) {
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
