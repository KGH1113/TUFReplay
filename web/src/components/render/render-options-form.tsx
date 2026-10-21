import { useId } from "react";
import { useTranslation } from "react-i18next";
import { RenderQualitySettings } from "@/components/render/render-quality-settings";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import {
  availableEncoders,
  availablePixelFormats,
  type RenderOptions,
} from "@/models/render/render-model";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { NativeSelect, NativeSelectOption } from "@/shared/ui/native-select";
import { Switch } from "@/shared/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/shared/ui/tabs";

type Control = ReturnType<typeof useRenderControl>;
type NumericOption =
  | "width"
  | "height"
  | "videoFps"
  | "simulationFps"
  | "bitrateMbps"
  | "audioGainDb"
  | "endDelaySeconds";
type ToggleOption =
  | "includeWebcam"
  | "includeMicrophone"
  | "includeDmNote"
  | "captureAudio"
  | "showRenderPreview"
  | "bgaMode"
  | "showPlanetRings"
  | "showSongTitle"
  | "showCountdown"
  | "showResultText"
  | "showHitJudgments";

export function RenderOptionsForm({ control }: { control: Control }) {
  const { t } = useTranslation("render");
  const id = useId();
  const options = control.options;
  const capabilities = control.settings?.capabilities;
  const encoders = availableEncoders(
    options.videoCodec,
    capabilities?.encoders ?? ["Software"],
    control.settings?.engineOptions,
  );
  const pixels = availablePixelFormats(options);
  const supportsCrf = options.encoder === "Software" && options.videoCodec !== "ProRes";
  const crfMax = options.videoCodec === "H264" || options.videoCodec === "H265" ? 51 : 63;
  const disabled = control.busy || control.phase === "checking";
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
        disabled={fieldDisabled}
        aria-invalid={invalid(field)}
        onChange={(event) => {
          const value = event.currentTarget.valueAsNumber;
          control.setOptions((current) => ({
            ...current,
            [field]: value,
          }));
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
  const changeCodec = (videoCodec: RenderOptions["videoCodec"]) =>
    control.setOptions((current) => {
      const allowed = availableEncoders(
        videoCodec,
        capabilities?.encoders ?? ["Software"],
        control.settings?.engineOptions,
      );
      return {
        ...current,
        videoCodec,
        encoder: allowed.includes(current.encoder)
          ? current.encoder
          : allowed.includes("Software")
            ? "Software"
            : allowed[0],
        bitDepth: videoCodec === "ProRes" ? 10 : current.bitDepth,
        crf: null,
        pixelFormat: "auto",
      };
    });
  const resolution = ["1280x720", "1920x1080", "2560x1440", "3840x2160", "7680x4320"].includes(
    `${options.width}x${options.height}`,
  )
    ? `${options.width}x${options.height}`
    : "custom";

  return (
    <fieldset disabled={disabled} className="space-y-6 disabled:opacity-60">
      <section className="space-y-3">
        <h3 className="text-sm font-semibold">{t("sections.save")}</h3>
        <label htmlFor={`${id}-outputDirectory`} className="block space-y-1.5 text-sm">
          <span>{t("outputDirectory")}</span>
          <div className="flex gap-2">
            <Input
              id={`${id}-outputDirectory`}
              value={options.outputDirectory}
              placeholder={t("outputDirectoryPlaceholder")}
              onChange={(event) =>
                control.setOptions((current) => ({
                  ...current,
                  outputDirectory: event.target.value,
                }))
              }
            />
            <Button
              type="button"
              variant="outline"
              disabled={disabled || control.choosingDirectory}
              onClick={() => void control.chooseOutputDirectory()}
            >
              {control.choosingDirectory ? t("choosingDirectory") : t("chooseDirectory")}
            </Button>
          </div>
        </label>
        <p className="text-xs leading-relaxed text-muted-foreground">{t("outputDirectoryHelp")}</p>
      </section>
      <Tabs
        value={control.settingsMode}
        onValueChange={(value) =>
          control.setSettingsMode(value === "advanced" ? "advanced" : "recommended")
        }
        className="gap-5"
      >
        <TabsList aria-label={t("settingsMode.label")}>
          <TabsTrigger value="recommended" disabled={disabled}>
            {t("settingsMode.recommended")}
          </TabsTrigger>
          <TabsTrigger value="advanced" disabled={disabled}>
            {t("settingsMode.advanced")}
          </TabsTrigger>
        </TabsList>
        <TabsContent value="recommended">
          <RenderQualitySettings control={control} />
        </TabsContent>
        <TabsContent value="advanced" className="space-y-6">
          <section className="space-y-3">
            <h3 className="text-sm font-semibold">{t("sections.video")}</h3>
            <div className="grid grid-cols-2 gap-x-4 gap-y-3">
              <label htmlFor={`${id}-resolution`} className="space-y-1.5 text-sm">
                <span className="font-medium">{t("resolution")}</span>
                <NativeSelect
                  id={`${id}-resolution`}
                  value={resolution}
                  onChange={(event) => {
                    if (event.target.value === "custom") return;
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
              <label htmlFor={`${id}-codec`} className="space-y-1.5 text-sm">
                <span className="font-medium">{t("videoCodec")}</span>
                <NativeSelect
                  id={`${id}-codec`}
                  value={options.videoCodec}
                  onChange={(event) =>
                    changeCodec(event.target.value as RenderOptions["videoCodec"])
                  }
                >
                  {(capabilities?.codecs ?? ["H264", "H265", "VP9", "AV1", "ProRes"]).map(
                    (codec) => (
                      <NativeSelectOption key={codec} value={codec}>
                        {t(`codecs.${codec}`)}
                      </NativeSelectOption>
                    ),
                  )}
                </NativeSelect>
              </label>
              {number("width", 320, 7680, 2)}
              {number("height", 180, 4320, 2)}
              {number("videoFps", 15, 240)}
              {number("simulationFps", options.videoFps, 1024, 1, t("simulationFpsHelp"))}
            </div>
            {options.simulationFps < options.videoFps ? (
              <p role="alert" className="text-xs text-destructive">
                {t("validation.simulation_fps_below_video")}
              </p>
            ) : null}
          </section>
          <section className="space-y-3">
            <h3 className="text-sm font-semibold">{t("sections.encoding")}</h3>
            <div className="grid grid-cols-2 gap-x-4 gap-y-3">
              <label htmlFor={`${id}-encoder`} className="space-y-1.5 text-sm">
                <span className="font-medium">{t("encoder")}</span>
                <NativeSelect
                  id={`${id}-encoder`}
                  value={options.encoder}
                  onChange={(event) =>
                    control.setOptions((current) => ({
                      ...current,
                      encoder: event.target.value as RenderOptions["encoder"],
                      crf: null,
                      pixelFormat: "auto",
                    }))
                  }
                >
                  {encoders.map((encoder) => (
                    <NativeSelectOption key={encoder} value={encoder}>
                      {t(`encoders.${encoder}`)}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
              </label>
              <label htmlFor={`${id}-encoding`} className="space-y-1.5 text-sm">
                <span className="font-medium">{t("encoding")}</span>
                <NativeSelect
                  id={`${id}-encoding`}
                  value={options.encoding}
                  onChange={(event) =>
                    control.setOptions((current) => ({
                      ...current,
                      encoding: event.target.value as RenderOptions["encoding"],
                    }))
                  }
                >
                  {(["Maximum", "Balanced", "Quality"] as const).map((speed) => (
                    <NativeSelectOption key={speed} value={speed}>
                      {t(`encodingSpeeds.${speed}`)}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
              </label>
              <label htmlFor={`${id}-rateControl`} className="space-y-1.5 text-sm">
                <span className="font-medium">{t("rateControl")}</span>
                <NativeSelect
                  id={`${id}-rateControl`}
                  value={options.crf === null ? "bitrate" : "crf"}
                  disabled={options.videoCodec === "ProRes"}
                  onChange={(event) =>
                    control.setOptions((current) => ({
                      ...current,
                      crf: event.target.value === "crf" ? (crfMax === 51 ? 23 : 32) : null,
                    }))
                  }
                >
                  <NativeSelectOption value="bitrate">{t("bitrate")}</NativeSelectOption>
                  {supportsCrf ? (
                    <NativeSelectOption value="crf">{t("constantQuality")}</NativeSelectOption>
                  ) : null}
                </NativeSelect>
              </label>
              {options.crf === null ? (
                number("bitrateMbps", 1, 200, 1, undefined, options.videoCodec === "ProRes")
              ) : (
                <label htmlFor={`${id}-crf`} className="space-y-1.5 text-sm">
                  <span className="font-medium">{t("crf")}</span>
                  <Input
                    id={`${id}-crf`}
                    type="number"
                    min={0}
                    max={crfMax}
                    value={Number.isFinite(options.crf) ? options.crf : ""}
                    aria-invalid={invalid("crf")}
                    onChange={(event) => {
                      const value = event.currentTarget.valueAsNumber;
                      control.setOptions((current) => ({
                        ...current,
                        crf: value,
                      }));
                    }}
                  />
                  <span className="block text-xs text-muted-foreground">
                    {t("crfHelp", { max: crfMax })}
                  </span>
                </label>
              )}
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
                      pixelFormat: "auto",
                    }))
                  }
                >
                  {(capabilities?.bitDepths ?? [8, 10]).map((depth) => (
                    <NativeSelectOption key={depth} value={depth}>
                      {t("bitDepthValue", { depth })}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
              </label>
              <label htmlFor={`${id}-pixelFormat`} className="space-y-1.5 text-sm">
                <span className="font-medium">{t("pixelFormat")}</span>
                <NativeSelect
                  id={`${id}-pixelFormat`}
                  value={options.pixelFormat}
                  onChange={(event) =>
                    control.setOptions((current) => ({
                      ...current,
                      pixelFormat: event.target.value as RenderOptions["pixelFormat"],
                    }))
                  }
                >
                  {pixels
                    .filter(
                      (format) =>
                        format === "auto" ||
                        !capabilities ||
                        capabilities.pixelFormats.includes(format),
                    )
                    .map((format) => (
                      <NativeSelectOption key={format} value={format}>
                        {format === "auto" ? t("automatic") : format}
                      </NativeSelectOption>
                    ))}
                </NativeSelect>
              </label>
              {options.videoCodec === "ProRes" ? (
                <label htmlFor={`${id}-proResProfile`} className="col-span-2 space-y-1.5 text-sm">
                  <span className="font-medium">{t("proResProfile")}</span>
                  <NativeSelect
                    id={`${id}-proResProfile`}
                    value={options.proResProfile}
                    onChange={(event) =>
                      control.setOptions((current) => ({
                        ...current,
                        proResProfile: event.target.value as RenderOptions["proResProfile"],
                        pixelFormat: "auto",
                      }))
                    }
                  >
                    {(
                      capabilities?.proResProfiles ?? [
                        "Proxy",
                        "LT",
                        "Standard",
                        "HQ",
                        "FourFourFourFour",
                        "FourFourFourFourXQ",
                      ]
                    ).map((profile) => (
                      <NativeSelectOption key={profile} value={profile}>
                        {t(`proResProfiles.${profile}`)}
                      </NativeSelectOption>
                    ))}
                  </NativeSelect>
                  <span className="block text-xs text-muted-foreground">{t("proResHelp")}</span>
                </label>
              ) : null}
            </div>
          </section>
        </TabsContent>
      </Tabs>
      <section className="space-y-2">
        <h3 className="text-sm font-semibold">{t("sections.recordings")}</h3>
        {toggle("captureAudio")}
        {toggle("includeMicrophone")}
        {toggle("includeWebcam")}
        {toggle(
          "includeDmNote",
          !control.health?.dmNoteConfigured,
          !control.health?.dmNoteConfigured ? t("dmNoteUnavailable") : undefined,
        )}
      </section>
      <div className="grid gap-3 sm:grid-cols-2">
        {number("endDelaySeconds", 0, 30, 0.1, t("endDelayHelp"))}
      </div>
      {control.settingsMode === "advanced" ? (
        <details className="group rounded-lg border border-border p-3">
          <summary className="cursor-pointer text-sm font-semibold outline-none focus-visible:ring-2 focus-visible:ring-ring">
            {t("advancedOptions")}
          </summary>
          <div className="space-y-4 pt-4">
            <div className="grid grid-cols-2 gap-4">
              {number("audioGainDb", -60, 12, 0.5, t("audioGainHelp"), !options.captureAudio)}
            </div>
            {toggle("showRenderPreview")}
            {toggle("bgaMode", false, t("bgaModeHelp"))}
            {toggle("showPlanetRings")}
            {toggle("showSongTitle")}
            {toggle("showCountdown")}
            {toggle("showResultText")}
            {toggle("showHitJudgments")}
          </div>
        </details>
      ) : null}
      <div className="flex items-center justify-between gap-4 rounded-lg bg-muted/40 p-3">
        <label htmlFor={`${id}-saveAsDefault`} className="text-sm">
          {t("saveAsDefault")}
        </label>
        <Switch
          id={`${id}-saveAsDefault`}
          checked={control.saveAsDefault}
          disabled={disabled}
          onCheckedChange={control.setSaveAsDefault}
        />
      </div>
    </fieldset>
  );
}
