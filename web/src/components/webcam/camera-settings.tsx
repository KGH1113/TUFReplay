import { ArrowDown01Icon, RefreshIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { WebcamSettingsPatch, WebcamState } from "@/models/webcam/webcam-model";
import { Button } from "@/shared/ui/button";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/shared/ui/select";
import { Slider } from "@/shared/ui/slider";
import { Switch } from "@/shared/ui/switch";
import { CameraCropEditor } from "./camera-crop-editor";
import { CameraSettingHelp } from "./camera-setting-help";

const defaultCamera = "__tufreplay_system_default__";

export function CameraSettings({
  state,
  saving,
  previewActive,
  onUpdate,
  onRefresh,
}: {
  state: WebcamState;
  saving: boolean;
  previewActive: boolean;
  onUpdate: (patch: WebcamSettingsPatch) => void;
  onRefresh: () => void;
}) {
  const { t } = useTranslation("webcam");
  const id = useId();
  const [syncOpen, setSyncOpen] = useState(false);
  const captureDisabled = saving || !state.supported || state.captureLocked;

  return (
    <div className="grid gap-7 md:grid-cols-2 md:gap-8">
      <section className="min-w-0 space-y-5" aria-label={t("recordingTitle")}>
        {state.status === "recording" || state.status === "saving" ? (
          <p role="status" className="flex items-center gap-2 text-xs text-muted-foreground">
            <span className="size-1.5 rounded-full bg-primary" aria-hidden="true" />
            {t(`status.${state.status}`)}
          </p>
        ) : null}
        {!state.supported ? <p className="text-sm text-amber-500">{t("unsupported")}</p> : null}
        {state.captureLocked ? (
          <p className="text-xs text-muted-foreground">{t("captureLocked")}</p>
        ) : null}
        {state.captureError ? (
          <div
            role="alert"
            className="space-y-2 rounded-xl border border-amber-500/20 bg-amber-500/5 p-3 text-sm"
          >
            <p>{t("captureError")}</p>
            <details className="text-xs text-muted-foreground">
              <summary className="cursor-pointer">{t("details")}</summary>
              <p className="mt-2 break-words">{state.captureError}</p>
            </details>
            <Button
              size="sm"
              disabled={captureDisabled}
              onClick={() => onUpdate({ enabled: state.enabled })}
            >
              {t("retryCamera")}
            </Button>
          </div>
        ) : null}
        <div className="space-y-2">
          <div className="flex items-center justify-between gap-3">
            <div className="flex items-center gap-1">
              <label htmlFor={`${id}-device`} className="text-sm font-medium">
                {t("device")}
              </label>
              <CameraSettingHelp label={t("device")}>
                <p>{t("deviceDescription")}</p>
                {state.backend === "ffmpeg" ? <p>{t("ffmpegHint")}</p> : null}
              </CameraSettingHelp>
            </div>
            <Button
              type="button"
              size="xs"
              variant="ghost"
              className="h-7 rounded-md px-2"
              disabled={saving}
              onClick={onRefresh}
            >
              <HugeiconsIcon aria-hidden="true" icon={RefreshIcon} size={14} />
              {t("refresh")}
            </Button>
          </div>
          <Select
            value={state.deviceId ?? defaultCamera}
            disabled={captureDisabled}
            onValueChange={(value) =>
              onUpdate({ deviceId: value === defaultCamera ? null : value })
            }
          >
            <SelectTrigger
              id={`${id}-device`}
              aria-label={t("device")}
              className="w-full rounded-lg"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent position="popper" align="start" className="rounded-xl">
              <SelectGroup>
                <SelectItem value={defaultCamera} className="rounded-lg">
                  {t("systemDefault")}
                </SelectItem>
                {state.deviceId && !state.devices.some((device) => device.id === state.deviceId) ? (
                  <SelectItem value={state.deviceId} className="rounded-lg" disabled>
                    {t("unavailableDevice")}
                  </SelectItem>
                ) : null}
                {state.devices.map((device) => (
                  <SelectItem key={device.id} value={device.id} className="rounded-lg">
                    {device.name}
                  </SelectItem>
                ))}
              </SelectGroup>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <div className="flex items-center gap-1">
            <p className="text-sm font-medium">{t("qualityLabel")}</p>
            <CameraSettingHelp label={t("qualityLabel")}>
              {t("qualityDescription")}
            </CameraSettingHelp>
          </div>
          <div className="grid grid-cols-3 gap-2">
            {(["compact", "balanced", "quality"] as const).map((quality) => {
              const selected = state.quality === quality;
              return (
                <Button
                  key={quality}
                  variant="outline"
                  aria-pressed={selected}
                  disabled={captureDisabled}
                  onClick={() => onUpdate({ quality })}
                  className={`relative h-auto min-w-0 justify-start rounded-xl border-2 px-3 py-3 active:scale-[0.98] ${selected ? "border-primary bg-primary/15 text-foreground shadow-[0_0_0_1px_var(--color-primary)] hover:bg-primary/20" : "border-transparent bg-muted/30 hover:border-border"}`}
                >
                  <span className="min-w-0 space-y-2 text-left">
                    <span className="block pr-3 font-semibold">{t(`quality.${quality}`)}</span>
                    <span className="block whitespace-pre-line text-[11px] font-normal leading-relaxed text-muted-foreground">
                      {t(`quality.${quality}Detail`)}
                    </span>
                  </span>
                  {selected ? (
                    <span
                      aria-hidden="true"
                      className="absolute right-2 top-2 grid size-4 place-items-center rounded-full bg-primary text-[10px] text-primary-foreground"
                    >
                      ✓
                    </span>
                  ) : null}
                </Button>
              );
            })}
          </div>
        </div>
        <SettingSlider
          label={t("recordingLimit")}
          description={t("recordingLimitDescription")}
          value={state.recordingLimitMb}
          min={64}
          max={8192}
          step={64}
          format={(value) => `${value} MB`}
          disabled={captureDisabled}
          onCommit={(recordingLimitMb) => onUpdate({ recordingLimitMb })}
        />
        <SettingSlider
          label={t("retention")}
          description={t("retentionDescription")}
          value={state.retentionDays === 0 ? 31 : state.retentionDays}
          min={1}
          max={31}
          step={1}
          format={(count) => (count === 31 ? t("forever") : t("days", { count }))}
          endLabels={[t("days", { count: 1 }), t("forever")]}
          disabled={captureDisabled}
          onCommit={(value) => onUpdate({ retentionDays: value === 31 ? 0 : value })}
        />
        <div className="space-y-4 border-t border-border/60 pt-4">
          <div className="flex items-center gap-1">
            <Button
              type="button"
              id={`${id}-sync-trigger`}
              variant="ghost"
              size="sm"
              aria-expanded={syncOpen}
              aria-controls={`${id}-sync`}
              className="h-7 justify-start rounded-md px-2 text-xs"
              onClick={() => setSyncOpen((open) => !open)}
            >
              <HugeiconsIcon
                aria-hidden="true"
                icon={ArrowDown01Icon}
                size={16}
                className={syncOpen ? "rotate-180" : ""}
              />
              {t("syncTitle")}
            </Button>
            <CameraSettingHelp label={t("syncTitle")}>{t("syncHint")}</CameraSettingHelp>
          </div>
          <section
            id={`${id}-sync`}
            aria-labelledby={`${id}-sync-trigger`}
            hidden={!syncOpen}
            className="ml-3 space-y-4 border-l border-border/70 py-1 pl-4"
          >
            <SettingSlider
              label={t("offset")}
              description={t("offsetDescription")}
              value={state.offsetMs}
              min={-1000}
              max={1000}
              step={1}
              format={(value) => `${value > 0 ? "+" : ""}${value} ms`}
              disabled={saving}
              onCommit={(offsetMs) => onUpdate({ offsetMs })}
            />
            <Button
              type="button"
              size="sm"
              variant="ghost"
              disabled={saving || state.offsetMs === 0}
              className="-ml-2 h-7 rounded-md px-2 text-xs"
              onClick={() => onUpdate({ offsetMs: 0 })}
            >
              {t("resetOffset")}
            </Button>
          </section>
        </div>
      </section>
      <section
        className="min-w-0 space-y-4 border-t border-border/60 pt-5 md:border-l md:border-t-0 md:pl-8 md:pt-0"
        aria-label={t("playbackTitle")}
      >
        <CameraCropEditor
          state={state}
          active={previewActive}
          saving={saving}
          onUpdate={onUpdate}
        />
        <div className="flex items-center gap-1 border-t border-border/60 pt-4">
          <h3 className="text-sm font-semibold">{t("playbackTitle")}</h3>
          <CameraSettingHelp label={t("playbackTitle")}>
            {t("inGamePlacementHint")}
          </CameraSettingHelp>
        </div>
        <div className="flex items-center justify-between gap-4">
          <div className="flex items-center gap-1">
            <label htmlFor={`${id}-live`} className="text-sm">
              {t("liveVisible")}
            </label>
            <CameraSettingHelp label={t("liveVisible")}>
              {t("liveVisibleDescription")}
            </CameraSettingHelp>
          </div>
          <Switch
            id={`${id}-live`}
            checked={state.liveVisible}
            disabled={saving}
            onCheckedChange={(liveVisible) => onUpdate({ liveVisible })}
          />
        </div>
        <div className="flex items-center justify-between gap-4">
          <div className="flex items-center gap-1">
            <label htmlFor={`${id}-visible`} className="text-sm">
              {t("playbackVisible")}
            </label>
            <CameraSettingHelp label={t("playbackVisible")}>
              {t("playbackVisibleDescription")}
            </CameraSettingHelp>
          </div>
          <Switch
            id={`${id}-visible`}
            checked={state.playbackVisible}
            disabled={saving}
            onCheckedChange={(playbackVisible) => onUpdate({ playbackVisible })}
          />
        </div>
      </section>
    </div>
  );
}

function SettingSlider({
  label,
  description,
  value,
  format,
  min,
  max,
  step,
  endLabels,
  disabled,
  onCommit,
}: {
  label: string;
  description?: string;
  value: number;
  format: (value: number) => string;
  min: number;
  max: number;
  step: number;
  endLabels?: [string, string];
  disabled: boolean;
  onCommit: (value: number) => void;
}) {
  const id = useId();
  const [draft, setDraft] = useState(value);
  const dragging = useRef(false);
  useEffect(() => {
    if (!disabled && !dragging.current) setDraft(value);
  }, [value, disabled]);
  return (
    <div className="space-y-2.5">
      <div className="flex items-center justify-between gap-3 text-sm">
        <div className="flex items-center gap-1">
          <span className="font-medium">{label}</span>
          {description ? <CameraSettingHelp label={label}>{description}</CameraSettingHelp> : null}
        </div>
        <span className="font-mono text-xs tabular-nums text-muted-foreground">
          {format(draft)}
        </span>
      </div>
      <Slider
        aria-label={label}
        aria-valuetext={format(draft)}
        aria-describedby={description ? id : undefined}
        min={min}
        max={max}
        step={step}
        value={[draft]}
        disabled={disabled}
        onValueChange={([next]) => {
          dragging.current = true;
          setDraft(next);
        }}
        onValueCommit={([next]) => {
          dragging.current = false;
          onCommit(next);
        }}
      />
      {endLabels ? (
        <div aria-hidden="true" className="flex justify-between text-xs text-muted-foreground">
          <span>{endLabels[0]}</span>
          <span>{endLabels[1]}</span>
        </div>
      ) : null}
      {description ? (
        <p id={id} className="sr-only">
          {description}
        </p>
      ) : null}
    </div>
  );
}
