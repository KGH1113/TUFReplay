import { RotateLeft01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { WebcamSettingsPatch, WebcamState } from "@/models/webcam/webcam-model";
import { Button } from "@/shared/ui/button";
import { Slider } from "@/shared/ui/slider";
import { Switch } from "@/shared/ui/switch";
import { CameraCropEditor } from "./camera-crop-editor";

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
  const captureDisabled = saving || !state.supported || state.captureLocked;
  const fieldClass =
    "h-9 w-full rounded-lg border border-border bg-background/60 px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50 disabled:opacity-50";
  return (
    <div className="grid gap-7 md:grid-cols-2 md:gap-8">
      <section className="min-w-0 space-y-5" aria-label={t("recordingTitle")}>
        <div className="flex items-center justify-between gap-4 rounded-xl border border-border/60 bg-muted/20 p-4">
          <div className="space-y-1">
            <label htmlFor={`${id}-enabled`} className="text-sm font-semibold">
              {t("enabled")}
            </label>
            <p className="max-w-72 text-xs leading-relaxed text-muted-foreground">
              {t("permissionHint")}
            </p>
          </div>
          <Switch
            id={`${id}-enabled`}
            checked={state.enabled}
            disabled={captureDisabled}
            onCheckedChange={(enabled) => onUpdate({ enabled })}
          />
        </div>
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
            <label htmlFor={`${id}-device`} className="text-sm font-medium">
              {t("device")}
            </label>
            <Button
              size="icon-sm"
              variant="ghost"
              aria-label={t("refresh")}
              title={t("refresh")}
              onClick={onRefresh}
            >
              <HugeiconsIcon aria-hidden="true" icon={RotateLeft01Icon} size={15} />
            </Button>
          </div>
          <select
            id={`${id}-device`}
            className={fieldClass}
            value={state.deviceId ?? ""}
            disabled={captureDisabled}
            onChange={(event) => onUpdate({ deviceId: event.target.value || null })}
          >
            <option value="">{t("systemDefault")}</option>
            {state.deviceId && !state.devices.some((device) => device.id === state.deviceId) ? (
              <option value={state.deviceId}>{t("unavailableDevice")}</option>
            ) : null}
            {state.devices.map((device) => (
              <option key={device.id} value={device.id}>
                {device.name}
              </option>
            ))}
          </select>
        </div>
        <div className="space-y-2">
          <p className="text-sm font-medium">{t("qualityLabel")}</p>
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
          label={t("storageLimit")}
          description={t("storageDescription")}
          value={state.storageLimitMb}
          min={64}
          max={8192}
          step={64}
          format={(value) => `${value} MB`}
          disabled={captureDisabled}
          onCommit={(storageLimitMb) => onUpdate({ storageLimitMb })}
        />
        <SettingSlider
          label={t("retention")}
          description={t("retentionDescription")}
          value={state.retentionDays}
          min={1}
          max={30}
          step={1}
          format={(count) => t("days", { count })}
          disabled={captureDisabled}
          onCommit={(retentionDays) => onUpdate({ retentionDays })}
        />
        <p className="text-xs leading-relaxed text-muted-foreground">{t("storageHint")}</p>
        {state.backend === "ffmpeg" ? (
          <p className="text-xs leading-relaxed text-muted-foreground">{t("ffmpegHint")}</p>
        ) : null}
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
        <h3 className="border-t border-border/60 pt-5 text-sm font-semibold">
          {t("playbackTitle")}
        </h3>
        <div className="flex items-center justify-between gap-4">
          <label htmlFor={`${id}-live`} className="text-sm">
            {t("liveVisible")}
          </label>
          <Switch
            id={`${id}-live`}
            checked={state.liveVisible}
            disabled={saving}
            onCheckedChange={(liveVisible) => onUpdate({ liveVisible })}
          />
        </div>
        <div className="flex items-center justify-between gap-4">
          <label htmlFor={`${id}-visible`} className="text-sm">
            {t("playbackVisible")}
          </label>
          <Switch
            id={`${id}-visible`}
            checked={state.playbackVisible}
            disabled={saving}
            onCheckedChange={(playbackVisible) => onUpdate({ playbackVisible })}
          />
        </div>
        <div className="flex items-center justify-between gap-4">
          <label htmlFor={`${id}-mirror`} className="text-sm">
            {t("mirror")}
          </label>
          <Switch
            id={`${id}-mirror`}
            checked={state.mirror}
            disabled={saving}
            onCheckedChange={(mirror) => onUpdate({ mirror })}
          />
        </div>
        <p className="text-xs leading-relaxed text-muted-foreground">{t("inGamePlacementHint")}</p>
        <details className="space-y-4 border-t border-border/60 pt-4">
          <summary className="cursor-pointer text-sm font-medium">{t("syncTitle")}</summary>
          <SettingSlider
            label={t("offset")}
            value={state.offsetMs}
            min={-1000}
            max={1000}
            step={1}
            format={(value) => `${value > 0 ? "+" : ""}${value} ms`}
            disabled={saving}
            onCommit={(offsetMs) => onUpdate({ offsetMs })}
          />
          <p className="text-xs leading-relaxed text-muted-foreground">{t("syncHint")}</p>
          <Button
            size="sm"
            variant="ghost"
            disabled={saving || state.offsetMs === 0}
            onClick={() => onUpdate({ offsetMs: 0 })}
          >
            {t("resetOffset")}
          </Button>
        </details>
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
        <span className="font-medium">{label}</span>
        <span className="font-mono text-xs tabular-nums text-muted-foreground">
          {format(draft)}
        </span>
      </div>
      <Slider
        aria-label={label}
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
      {description ? (
        <p id={id} className="text-xs leading-relaxed text-muted-foreground">
          {description}
        </p>
      ) : null}
    </div>
  );
}
