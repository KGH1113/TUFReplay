import {
  FolderSearchIcon,
  Loading03Icon,
  Mic01Icon,
  MusicNote01Icon,
  PlayIcon,
  StopIcon,
  VideoReplayIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { TFunction } from "i18next";
import { useEffect, useId, useState } from "react";
import { useTranslation } from "react-i18next";

import { Button } from "@/ui/button.component";
import { Dialog, DialogContent, DialogTitle } from "@/ui/dialog.component";
import { Switch } from "@/ui/switch.component";

import type {
  ActivityRun,
  RenderCapabilities,
  RenderRequestOptions,
  RenderStatus,
  ReplayLevelFilePickerResult,
} from "../activity.model";
import { translatedDomainError } from "../lib/localized-error";
import type { RenderPreviewLevels } from "../lib/render-preview-player";

type ResolutionPreset = { label: string; width: number; height: number };

const RESOLUTION_PRESETS: ResolutionPreset[] = [
  { label: "720p", width: 1280, height: 720 },
  { label: "1080p", width: 1920, height: 1080 },
  { label: "1440p", width: 2560, height: 1440 },
  { label: "4K", width: 3840, height: 2160 },
];

const FPS_PRESETS = [30, 60, 120];

export function RenderRunDialog({
  run,
  capabilities,
  status,
  pendingRunId,
  error,
  errorRunId,
  pickerResult,
  pickingRunId,
  onClose,
  onStart,
  onCancel,
  onPickLevelFile,
  onResetPicker,
  previewState,
  onRenderPreview,
  onPlayPreview,
  onStopPreviewPlayback,
  onPreviewLevels,
  onPreviewMicTiming,
  onCancelPreviewRender,
}: {
  run: ActivityRun | null;
  capabilities: RenderCapabilities | null;
  status: RenderStatus;
  pendingRunId: string | null;
  error: string;
  errorRunId: string | null;
  pickerResult: ReplayLevelFilePickerResult | null;
  pickingRunId: string | null;
  onClose: () => void;
  onStart: (runId: string, options?: RenderRequestOptions) => Promise<boolean>;
  onCancel: () => Promise<boolean>;
  onPickLevelFile: (runId: string) => Promise<boolean>;
  onResetPicker: () => void;
  previewState: "idle" | "rendering" | "ready" | "playing";
  onRenderPreview: (runId: string, options?: RenderRequestOptions) => Promise<boolean>;
  onPlayPreview: (levels: RenderPreviewLevels) => boolean;
  onStopPreviewPlayback: () => void;
  onPreviewLevels: (levels: RenderPreviewLevels) => void;
  onPreviewMicTiming: (timingMs: number) => void;
  onCancelPreviewRender: () => Promise<boolean>;
}) {
  const { t } = useTranslation("replay");
  const [resolution, setResolution] = useState<ResolutionPreset>(RESOLUTION_PRESETS[1]);
  const [fps, setFps] = useState(60);
  const [includeMicrophone, setIncludeMicrophone] = useState(true);
  const [step, setStep] = useState<"level" | "settings">("level");
  const [musicVolume, setMusicVolume] = useState(100);
  const [hitsoundVolume, setHitsoundVolume] = useState(100);
  const [microphoneVolume, setMicrophoneVolume] = useState(100);
  const [microphoneTiming, setMicrophoneTiming] = useState(0);
  const [levelPath, setLevelPath] = useState<string | undefined>(undefined);
  const microphoneToggleId = useId();

  const open = run !== null;
  const currentPicker = pickerResult?.RunId === run?.Id ? pickerResult : null;
  const isPicking = pickingRunId === run?.Id;
  const isThisRun = status.RunId === run?.Id;
  const starting = pendingRunId === run?.Id;
  const active =
    isThisRun &&
    (status.State === "preparing" ||
      status.State === "opening_level" ||
      status.State === "capturing" ||
      status.State === "finalizing");
  const completed = isThisRun && status.State === "completed";
  const displayedError =
    errorRunId === run?.Id && error
      ? error
      : isThisRun && status.State === "error"
        ? (translatedDomainError(status.ErrorCode ?? "") ?? status.Message ?? t("render.failed"))
        : "";

  useEffect(() => {
    if (!open) return;
    setIncludeMicrophone(run?.HasMicrophoneRecording ?? false);
    setStep("level");
    setLevelPath(undefined);
    setMusicVolume(100);
    setHitsoundVolume(100);
    setMicrophoneVolume(100);
    setMicrophoneTiming(0);
  }, [open, run?.HasMicrophoneRecording]);

  // The preview stems belong to this dialog's run; drop them when it closes.
  useEffect(() => {
    if (open) return;
    void onCancelPreviewRender();
  }, [open, onCancelPreviewRender]);

  useEffect(() => {
    if (!run || currentPicker?.Outcome !== "selected" || !currentPicker.LevelPath) return;
    setLevelPath(currentPicker.LevelPath);
    setStep("settings");
  }, [currentPicker, run]);

  const unavailable = capabilities !== null && !capabilities.Available;
  const busy = starting || active;

  const renderOptions = (): RenderRequestOptions => ({
    levelPath,
    width: resolution.width,
    height: resolution.height,
    renderFps: fps,
    videoFps: fps,
    includeMicrophone,
    musicVolumePercent: musicVolume,
    hitsoundVolumePercent: hitsoundVolume,
    microphoneVolumePercent: microphoneVolume,
    microphoneTimingMs: microphoneTiming,
  });

  const previewLevels = (): RenderPreviewLevels => ({
    music: musicVolume / 100,
    hitsound: hitsoundVolume / 100,
    microphone: includeMicrophone ? microphoneVolume / 100 : 0,
  });

  const start = () => {
    if (!run || busy) return;
    onStopPreviewPlayback();
    void onStart(run.Id, renderOptions());
  };

  const renderPreview = () => {
    if (!run) return;
    void onRenderPreview(run.Id, renderOptions()).then((ready) => {
      // Auto-play the freshly rendered window once.
      if (ready) onPlayPreview(previewLevels());
    });
  };

  const setVolume = (which: "music" | "hitsound" | "microphone", value: number) => {
    if (which === "music") setMusicVolume(value);
    else if (which === "hitsound") setHitsoundVolume(value);
    else setMicrophoneVolume(value);
    const levels = previewLevels();
    if (which === "music") levels.music = value / 100;
    else if (which === "hitsound") levels.hitsound = value / 100;
    else levels.microphone = includeMicrophone ? value / 100 : 0;
    // Live: an active preview hears the change immediately.
    onPreviewLevels(levels);
  };

  const close = () => {
    if (isPicking) return;
    onResetPicker();
    onClose();
  };

  const pickerFailed = currentPicker?.Outcome === "mismatch" || currentPicker?.Outcome === "error";
  const pickerMessage = pickerFailed ? currentPicker?.Message : null;
  const chosenFileName = levelPath ? (levelPath.split("/").pop() ?? levelPath) : null;

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        // Closing the dialog does not stop the render; it keeps going in the background.
        if (!next) close();
      }}
    >
      <DialogContent
        className="max-w-md"
        onEscapeKeyDown={(event) => isPicking && event.preventDefault()}
        onPointerDownOutside={(event) => isPicking && event.preventDefault()}
      >
        <DialogTitle className="flex items-center gap-2">
          <HugeiconsIcon aria-hidden="true" icon={VideoReplayIcon} className="size-4" />
          {t("render.title")}
        </DialogTitle>

        {unavailable ? (
          <p className="text-sm text-muted-foreground">
            {capabilities?.UnavailableReason || t("render.unavailable")}
          </p>
        ) : step === "level" ? (
          // Same level choice as playing a replay: render the recorded level, or a different file
          // whose gameplay matches. The choice has to happen before the render starts, because it
          // decides which chart is captured.
          <div className="flex flex-col gap-3">
            <p className="text-sm text-muted-foreground">{t("render.levelDescription")}</p>

            <Button
              type="button"
              variant="outline"
              className="h-auto justify-start gap-3 p-3 text-left"
              disabled={isPicking}
              onClick={() => {
                setLevelPath(undefined);
                setStep("settings");
              }}
            >
              <HugeiconsIcon
                aria-hidden="true"
                icon={MusicNote01Icon}
                className="size-4 shrink-0"
              />
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-medium">{t("dialog.original")}</span>
                <span className="mt-0.5 block text-xs text-muted-foreground">
                  {t("dialog.useRecorded")}
                </span>
              </span>
            </Button>

            <Button
              type="button"
              variant="outline"
              className="h-auto justify-start gap-3 p-3 text-left"
              disabled={isPicking}
              onClick={() => {
                if (run) void onPickLevelFile(run.Id);
              }}
            >
              <HugeiconsIcon
                aria-hidden="true"
                icon={isPicking ? Loading03Icon : FolderSearchIcon}
                className={isPicking ? "size-4 shrink-0 animate-spin" : "size-4 shrink-0"}
              />
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-medium">{t("dialog.chooseMatching")}</span>
                <span className="mt-0.5 block text-xs text-muted-foreground">
                  {isPicking ? t("dialog.choosingAndVerifying") : t("dialog.useAnother")}
                </span>
              </span>
            </Button>

            {pickerMessage ? (
              <p aria-live="polite" className="text-sm text-destructive">
                {pickerMessage}
              </p>
            ) : null}
          </div>
        ) : (
          <div className="flex flex-col gap-4">
            <p className="text-sm text-muted-foreground">
              {t("render.description", { runIndex: run?.RunIndex ?? 0 })}
            </p>

            {chosenFileName ? (
              <p className="truncate text-xs text-muted-foreground">
                {t("render.usingLevel", { file: chosenFileName })}
              </p>
            ) : null}

            <fieldset className="flex flex-col gap-2" disabled={busy}>
              <legend className="text-xs font-medium text-muted-foreground">
                {t("render.resolution")}
              </legend>
              <div className="flex flex-wrap gap-2">
                {RESOLUTION_PRESETS.map((preset) => (
                  <Button
                    key={preset.label}
                    type="button"
                    size="sm"
                    variant={preset.label === resolution.label ? "default" : "outline"}
                    onClick={() => setResolution(preset)}
                  >
                    {preset.label}
                  </Button>
                ))}
              </div>
            </fieldset>

            <fieldset className="flex flex-col gap-2" disabled={busy}>
              <legend className="text-xs font-medium text-muted-foreground">
                {t("render.frameRate")}
              </legend>
              <div className="flex flex-wrap gap-2">
                {FPS_PRESETS.map((preset) => (
                  <Button
                    key={preset}
                    type="button"
                    size="sm"
                    variant={preset === fps ? "default" : "outline"}
                    onClick={() => setFps(preset)}
                  >
                    {t("render.fpsValue", { value: preset })}
                  </Button>
                ))}
              </div>
            </fieldset>

            {run?.HasMicrophoneRecording ? (
              <div className="flex items-center justify-between gap-3 text-sm">
                <label htmlFor={microphoneToggleId} className="flex items-center gap-2">
                  <HugeiconsIcon aria-hidden="true" icon={Mic01Icon} className="size-4" />
                  {t("render.includeMicrophone")}
                </label>
                <Switch
                  id={microphoneToggleId}
                  checked={includeMicrophone}
                  disabled={busy}
                  onCheckedChange={setIncludeMicrophone}
                />
              </div>
            ) : null}

            <fieldset className="flex flex-col gap-3" disabled={busy}>
              <legend className="text-xs font-medium text-muted-foreground">
                {t("render.audioLevels")}
              </legend>
              <VolumeSlider
                label={t("render.musicVolume")}
                value={musicVolume}
                onChange={(value) => setVolume("music", value)}
                disabled={busy}
              />
              <VolumeSlider
                label={t("render.hitsoundVolume")}
                value={hitsoundVolume}
                onChange={(value) => setVolume("hitsound", value)}
                disabled={busy}
              />
              {run?.HasMicrophoneRecording && includeMicrophone ? (
                <>
                  <VolumeSlider
                    label={t("render.microphoneVolume")}
                    value={microphoneVolume}
                    onChange={(value) => setVolume("microphone", value)}
                    disabled={busy}
                  />
                  <TimingSlider
                    label={t("render.microphoneTiming")}
                    value={microphoneTiming}
                    onChange={(value) => {
                      setMicrophoneTiming(value);
                      onPreviewMicTiming(value);
                    }}
                    disabled={busy}
                  />
                </>
              ) : null}

              <div className="flex items-center justify-between gap-3">
                <span className="text-xs text-muted-foreground">
                  {previewState === "rendering"
                    ? t("render.preview.rendering")
                    : previewState === "idle"
                      ? t("render.preview.hint")
                      : t("render.preview.readyHint")}
                </span>
                <div className="flex gap-2">
                  {previewState === "ready" || previewState === "playing" ? (
                    <>
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={() =>
                          previewState === "playing"
                            ? onStopPreviewPlayback()
                            : onPlayPreview(previewLevels())
                        }
                      >
                        <HugeiconsIcon
                          aria-hidden="true"
                          icon={previewState === "playing" ? StopIcon : PlayIcon}
                          className="size-4"
                        />
                        {previewState === "playing"
                          ? t("render.preview.stop")
                          : t("render.preview.playCached")}
                      </Button>
                      <Button type="button" size="sm" variant="ghost" onClick={renderPreview}>
                        {t("render.preview.newWindow")}
                      </Button>
                    </>
                  ) : (
                    <Button
                      type="button"
                      size="sm"
                      variant="outline"
                      disabled={previewState === "rendering"}
                      onClick={renderPreview}
                    >
                      <HugeiconsIcon
                        aria-hidden="true"
                        icon={previewState === "rendering" ? Loading03Icon : PlayIcon}
                        className={previewState === "rendering" ? "size-4 animate-spin" : "size-4"}
                      />
                      {t("render.preview.play")}
                    </Button>
                  )}
                </div>
              </div>
            </fieldset>

            {active ? <RenderProgress status={status} t={t} /> : null}

            {completed ? (
              <div className="rounded-md border border-border bg-muted/40 p-3">
                <p className="text-sm font-medium">{t("render.completed")}</p>
                <p className="mt-1 break-all text-xs text-muted-foreground">{status.OutputPath}</p>
              </div>
            ) : null}

            {displayedError ? (
              <p aria-live="polite" className="text-sm text-destructive">
                {displayedError}
              </p>
            ) : null}

            <div className="flex justify-end gap-2">
              {!busy && !completed ? (
                <Button type="button" variant="ghost" onClick={() => setStep("level")}>
                  {t("render.back")}
                </Button>
              ) : null}
              {active ? (
                <Button type="button" variant="outline" onClick={() => void onCancel()}>
                  {t("render.cancel")}
                </Button>
              ) : null}
              <Button type="button" disabled={busy} onClick={start}>
                {busy ? (
                  <HugeiconsIcon
                    aria-hidden="true"
                    icon={Loading03Icon}
                    className="size-4 animate-spin"
                  />
                ) : null}
                {completed ? t("render.again") : t("render.start")}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

/** Percentage slider for one audio source, 0-200% of the render's normal level. */
function VolumeSlider({
  label,
  value,
  onChange,
  disabled,
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  disabled: boolean;
}) {
  const id = useId();
  return (
    <div className="flex items-center gap-3">
      <label htmlFor={id} className="w-24 shrink-0 text-sm">
        {label}
      </label>
      <input
        id={id}
        type="range"
        min={0}
        max={200}
        step={5}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(Number(event.target.value))}
        className="h-1 flex-1 cursor-pointer appearance-none rounded-full bg-muted accent-primary disabled:cursor-not-allowed disabled:opacity-50"
      />
      <span className="w-12 shrink-0 text-right text-xs tabular-nums text-muted-foreground">
        {value}%
      </span>
    </div>
  );
}

/** Millisecond trim slider (-400..+400); positive = source earlier. */
function TimingSlider({
  label,
  value,
  onChange,
  disabled,
}: {
  label: string;
  value: number;
  onChange: (value: number) => void;
  disabled: boolean;
}) {
  const id = useId();
  return (
    <div className="flex items-center gap-3">
      <label htmlFor={id} className="w-24 shrink-0 text-sm">
        {label}
      </label>
      <input
        id={id}
        type="range"
        min={-400}
        max={400}
        step={5}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(Number(event.target.value))}
        className="h-1 flex-1 cursor-pointer appearance-none rounded-full bg-muted accent-primary disabled:cursor-not-allowed disabled:opacity-50"
      />
      <span className="w-12 shrink-0 text-right text-xs tabular-nums text-muted-foreground">
        {value > 0 ? `+${value}` : value}
      </span>
    </div>
  );
}

function RenderProgress({ status, t }: { status: RenderStatus; t: TFunction<"replay"> }) {
  const stateLabel =
    status.State === "capturing"
      ? t("render.status.capturing")
      : status.State === "finalizing"
        ? t("render.status.finalizing")
        : status.State === "opening_level"
          ? t("render.status.openingLevel")
          : t("render.status.preparing");

  return (
    <div className="rounded-md border border-border bg-muted/40 p-3">
      <p className="text-sm font-medium">{stateLabel}</p>
      <p className="mt-1 text-xs text-muted-foreground">
        {t("render.progress", {
          frames: status.FramesEncoded,
          seconds: status.EncodedSeconds.toFixed(1),
        })}
        {status.EncoderName ? ` · ${status.EncoderName}` : ""}
      </p>
    </div>
  );
}
