import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import type { RenderOptions } from "@/models/render/render-model";
import { Input } from "@/shared/ui/input";
import { NativeSelect, NativeSelectOption } from "@/shared/ui/native-select";
import { Switch } from "@/shared/ui/switch";

type Control = ReturnType<typeof useRenderControl>;
type NumericOption =
  | "width"
  | "height"
  | "videoFps"
  | "simulationFps"
  | "bitrateMbps"
  | "audioGainDb"
  | "endDelaySeconds";
type ToggleOption = "includeWebcam" | "includeMicrophone" | "captureAudio";

export function RenderOptionsForm({ control }: { control: Control }) {
  const { t } = useTranslation("render");
  const id = useId();
  const options = control.options;
  const [customResolution, setCustomResolution] = useState(false);
  const disabled = control.busy;
  const issues = control.optionValidation.success ? [] : control.optionValidation.error.issues;
  const invalid = (field: keyof RenderOptions) => issues.some((issue) => issue.path[0] === field);

  const number = (
    field: NumericOption,
    min: number,
    max: number,
    step = 1,
    help?: string,
    fieldDisabled = false,
  ) => (
    <label htmlFor={`${id}-${field}`} className="space-y-1.5 text-sm">
      <span className="font-medium">{t(`options.${field}`)}</span>
      <Input
        id={`${id}-${field}`}
        type="number"
        inputMode={step < 1 ? "decimal" : "numeric"}
        value={Number.isFinite(options[field]) ? options[field] : ""}
        min={min}
        max={max}
        step={step}
        disabled={disabled || fieldDisabled}
        aria-invalid={invalid(field)}
        onChange={(event) => {
          const value = event.currentTarget.valueAsNumber;
          control.setOptions((current) => ({ ...current, [field]: value }));
        }}
      />
      {help ? (
        <span className="block text-xs leading-relaxed text-muted-foreground">{help}</span>
      ) : null}
      {invalid(field) ? (
        <span className="block text-xs text-destructive">
          {(field === "width" || field === "height") && options[field] % 2 !== 0
            ? t("validation.evenDimensions")
            : t("validation.numberRange", { min, max })}
        </span>
      ) : null}
    </label>
  );

  const toggle = (field: ToggleOption, fieldDisabled = false, hint?: string) => (
    <div key={field} className="flex items-center justify-between gap-4 py-1">
      <label htmlFor={`${id}-${field}`} className="text-sm">
        {t(`options.${field}`)}
        {hint ? (
          <span className="mt-0.5 block text-xs leading-relaxed text-muted-foreground">{hint}</span>
        ) : null}
      </label>
      <Switch
        id={`${id}-${field}`}
        checked={options[field] && !fieldDisabled}
        disabled={disabled || fieldDisabled}
        onCheckedChange={(checked) =>
          control.setOptions((current) => ({ ...current, [field]: checked }))
        }
      />
    </div>
  );

  const resolution =
    !customResolution &&
    ["1280x720", "1920x1080", "2560x1440", "3840x2160", "7680x4320"].includes(
      `${options.width}x${options.height}`,
    )
      ? `${options.width}x${options.height}`
      : "custom";

  return (
    <fieldset disabled={disabled} className="space-y-6 disabled:opacity-60">
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">{t("sections.video")}</h3>
        <div className="grid grid-cols-2 gap-x-4 gap-y-3">
          <label htmlFor={`${id}-resolution`} className="space-y-1.5 text-sm">
            <span className="font-medium">{t("resolution")}</span>
            <NativeSelect
              id={`${id}-resolution`}
              value={resolution}
              onChange={(event) => {
                if (event.target.value === "custom") {
                  setCustomResolution(true);
                  return;
                }
                setCustomResolution(false);
                const [width, height] = event.target.value.split("x").map(Number);
                control.setOptions((current) => ({ ...current, width, height }));
              }}
            >
              <NativeSelectOption value="custom">{t("customResolution")}</NativeSelectOption>
              {["1280x720", "1920x1080", "2560x1440", "3840x2160", "7680x4320"].map((size) => (
                <NativeSelectOption key={size} value={size}>
                  {size.replace("x", " × ")}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </label>
          {number("videoFps", 15, 240)}
        </div>
        {resolution === "custom" ? (
          <div className="grid grid-cols-2 gap-4">
            {number("width", 320, 7680, 2)}
            {number("height", 180, 4320, 2)}
          </div>
        ) : null}
        <div className="grid gap-4 sm:grid-cols-2">
          {number("simulationFps", options.videoFps, 1024, 1, t("simulationFpsHelp"))}
        </div>
        {options.simulationFps < options.videoFps ? (
          <p role="alert" className="text-xs text-destructive">
            {t("validation.simulation_fps_below_video")}
          </p>
        ) : null}
      </section>

      <section className="space-y-2">
        <h3 className="text-sm font-semibold">{t("sections.recordings")}</h3>
        {toggle("captureAudio")}
        {toggle("includeMicrophone")}
        {toggle("includeWebcam")}
      </section>

      <details
        className="group rounded-lg border border-border"
        open={
          issues.some((issue) =>
            ["bitrateMbps", "audioGainDb", "endDelaySeconds"].includes(String(issue.path[0])),
          ) || undefined
        }
      >
        <summary className="cursor-pointer rounded-lg px-4 py-3 text-sm font-semibold outline-none focus-visible:ring-2 focus-visible:ring-ring">
          {t("advancedOptions")}
        </summary>
        <div className="space-y-6 border-t border-border px-4 py-4">
          <section className="space-y-3">
            <h3 className="text-sm font-semibold">{t("sections.encoding")}</h3>
            <div className="grid grid-cols-2 gap-4">
              <label htmlFor={`${id}-codec`} className="space-y-1.5 text-sm">
                <span className="font-medium">{t("videoCodec")}</span>
                <NativeSelect
                  id={`${id}-codec`}
                  value={options.videoCodec}
                  onChange={(event) =>
                    control.setOptions((current) => ({
                      ...current,
                      videoCodec: event.target.value as RenderOptions["videoCodec"],
                      bitDepth: event.target.value === "ProRes" ? 10 : current.bitDepth,
                    }))
                  }
                >
                  {(["H264", "H265", "VP9", "AV1", "ProRes"] as const).map((codec) => (
                    <NativeSelectOption key={codec} value={codec}>
                      {t(`codecs.${codec}`)}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
              </label>
              {number("bitrateMbps", 1, 200)}
              <label htmlFor={`${id}-bitDepth`} className="space-y-1.5 text-sm">
                <span className="font-medium">{t("bitDepth")}</span>
                <NativeSelect
                  id={`${id}-bitDepth`}
                  value={options.bitDepth}
                  disabled={options.videoCodec === "ProRes"}
                  onChange={(event) =>
                    control.setOptions((current) => ({
                      ...current,
                      bitDepth: Number(event.target.value) as 8 | 10,
                    }))
                  }
                >
                  {[8, 10].map((depth) => (
                    <NativeSelectOption key={depth} value={depth}>
                      {t("bitDepthValue", { depth })}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
              </label>
            </div>
          </section>
          <section className="space-y-3">
            <h3 className="text-sm font-semibold">{t("sections.playback")}</h3>
            <div className="grid gap-4 sm:grid-cols-2">
              {number("endDelaySeconds", 0, 30, 0.1, t("endDelayHelp"))}
              {number("audioGainDb", -60, 12, 0.5, t("audioGainHelp"), !options.captureAudio)}
            </div>
          </section>
        </div>
      </details>
    </fieldset>
  );
}
