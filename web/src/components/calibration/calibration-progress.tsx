import {
  Loading03Icon,
  Mic02Icon,
  RefreshIcon,
  Tick02Icon,
  WaveSquareIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import {
  formatMicrophoneOffset,
  MAX_MICROPHONE_OFFSET_MS,
  MIN_MICROPHONE_OFFSET_MS,
} from "@/models/calibration/microphone-offset";
import {
  formatMicrophoneVolumeDb,
  MAX_MICROPHONE_VOLUME_DB,
  MIN_MICROPHONE_VOLUME_DB,
} from "@/models/calibration/microphone-volume";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { DialogTitle } from "@/shared/ui/dialog";
import type { MicrophoneOffsetCalibrationPhase } from "@/state/calibration/microphone-offset-reducer";

export function TimingSettingsPanel({
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

export function CalibrationProgress({
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
