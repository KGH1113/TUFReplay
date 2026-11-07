import { Folder01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ChoiceCards,
  OptionGroup,
  Segmented,
  SettingRow,
  SliderSetting,
  SwitchSetting,
} from "@/components/render/render-option-controls";
import type { useRenderControl } from "@/hooks/render/use-render-control";
import {
  describeFolder,
  formatChoices,
  formatNames,
  maxVideoSize,
  megabytesPerMinute,
  precisionChoices,
  proResChoices,
  proResNames,
  qualityChoices,
  qualityOf,
  smoothnessChoices,
  withQuality,
  withVideoCodec,
  withVideoFps,
} from "@/models/render/render-choices";
import type { RenderOptions } from "@/models/render/render-model";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";

type Control = ReturnType<typeof useRenderControl>;
type Update = (change: (current: RenderOptions) => RenderOptions) => void;

/** Where OrbitRender saves videos when no folder is chosen. */
const defaultFolder = "~/Videos/A Dance of Fire and Ice/TUFReplay";

/**
 * Every video setting on one screen: how the video looks on the left, the
 * file it becomes on the right.
 */
export function RenderOptionsForm({ control }: { control: Control }) {
  const update: Update = (change) => control.setOptions((current) => change(current));
  return (
    <div className="grid gap-x-8 gap-y-6 md:grid-cols-2">
      <div className="min-w-0 space-y-6">
        <QualitySettings control={control} update={update} />
        <SmoothnessSettings control={control} update={update} />
        <IncludeSettings control={control} update={update} />
      </div>
      <div className="min-w-0 space-y-6">
        <FormatSettings control={control} update={update} />
        <PlaybackSettings control={control} update={update} />
      </div>
    </div>
  );
}

function QualitySettings({ control, update }: { control: Control; update: Update }) {
  const { t } = useTranslation("render");
  const options = control.options;
  const quality = qualityOf(options);
  const [typing, setTyping] = useState(false);
  const issues = control.optionValidation.success ? [] : control.optionValidation.error.issues;
  const invalid = issues.some((issue) => issue.path[0] === "width" || issue.path[0] === "height");
  const exact = typing || quality === null;

  return (
    <OptionGroup
      label={t("quality.label")}
      aside={
        <Button
          type="button"
          variant="ghost"
          size="xs"
          aria-expanded={exact}
          className="-mr-2 text-muted-foreground hover:text-foreground"
          onClick={() => setTyping((value) => !value)}
          disabled={quality === null}
        >
          {t("quality.exact")}
        </Button>
      }
    >
      <ChoiceCards
        label={t("quality.label")}
        className="grid-cols-4"
        value={quality}
        choices={qualityChoices.map((choice) => ({
          value: choice.id,
          title: choice.label,
          detail: t(`quality.${choice.id}`),
        }))}
        onChange={(id) => {
          setTyping(false);
          update((current) => withQuality(current, id));
        }}
      />
      {exact ? (
        <div className="space-y-1.5 pt-1">
          <div className="flex items-center gap-2">
            {(["width", "height"] as const).map((field, index) => (
              <span key={field} className="flex items-center gap-2">
                {index > 0 ? (
                  <span aria-hidden="true" className="text-xs text-muted-foreground">
                    ×
                  </span>
                ) : null}
                <Input
                  aria-label={t(`quality.${field}`)}
                  inputMode="numeric"
                  autoComplete="off"
                  className="h-8 w-20 text-center tabular-nums"
                  value={options[field] > 0 ? String(options[field]) : ""}
                  aria-invalid={invalid}
                  onChange={(event) => {
                    const digits = event.target.value.replace(/\D/g, "").slice(0, 4);
                    update((current) => ({ ...current, [field]: digits ? Number(digits) : 0 }));
                  }}
                />
              </span>
            ))}
          </div>
          <p className={`text-xs ${invalid ? "text-destructive" : "text-muted-foreground"}`}>
            {t("quality.exactHint", maxVideoSize)}
          </p>
        </div>
      ) : null}
    </OptionGroup>
  );
}

function SmoothnessSettings({ control, update }: { control: Control; update: Update }) {
  const { t } = useTranslation("render");
  const fps = control.options.videoFps;
  const known = (smoothnessChoices as readonly number[]).includes(fps);
  return (
    <OptionGroup
      label={t("smoothness.label")}
      aside={
        known ? null : (
          <span className="text-xs text-muted-foreground">{t("smoothness.value", { fps })}</span>
        )
      }
    >
      <ChoiceCards
        label={t("smoothness.label")}
        className="grid-cols-3"
        value={known ? String(fps) : null}
        choices={smoothnessChoices.map((choice) => ({
          value: String(choice),
          title: t("smoothness.value", { fps: choice }),
          detail: t(`smoothness.${choice}`),
        }))}
        onChange={(choice) => update((current) => withVideoFps(current, Number(choice)))}
      />
    </OptionGroup>
  );
}

function IncludeSettings({ control, update }: { control: Control; update: Update }) {
  const { t } = useTranslation("render");
  const options = control.options;
  const run = control.run;
  return (
    <OptionGroup label={t("include.label")}>
      <div className="divide-y divide-border/60">
        <SwitchSetting
          label={t("include.captureAudio")}
          checked={options.captureAudio}
          onChange={(captureAudio) => update((current) => ({ ...current, captureAudio }))}
        />
        {options.captureAudio ? (
          <SliderSetting
            label={t("include.volume")}
            value={options.audioGainDb}
            min={-30}
            max={12}
            step={1}
            format={(db) =>
              db === 0
                ? t("include.volumeSame")
                : t("include.decibels", { db: db > 0 ? `+${db}` : db })
            }
            onChange={(audioGainDb) => update((current) => ({ ...current, audioGainDb }))}
          />
        ) : null}
        {/* Only recordings this play has; there is nothing to add otherwise. */}
        {run?.hasMicrophoneRecording ? (
          <SwitchSetting
            label={t("include.includeMicrophone")}
            checked={options.includeMicrophone}
            onChange={(includeMicrophone) =>
              update((current) => ({ ...current, includeMicrophone }))
            }
          />
        ) : null}
        {run?.hasCameraRecording ? (
          <SwitchSetting
            label={t("include.includeWebcam")}
            checked={options.includeWebcam}
            onChange={(includeWebcam) => update((current) => ({ ...current, includeWebcam }))}
          />
        ) : null}
      </div>
    </OptionGroup>
  );
}

/** The video format, with the settings that format has. */
function FormatSettings({ control, update }: { control: Control; update: Update }) {
  const { t } = useTranslation("render");
  const options = control.options;
  const codec = options.videoCodec;
  return (
    <OptionGroup label={t("format.label")}>
      <Segmented
        stretch
        label={t("format.label")}
        value={codec}
        choices={formatChoices.map((value) => ({ value, title: formatNames[value] }))}
        onChange={(value) => update((current) => withVideoCodec(current, value))}
      />
      <p className="text-xs text-muted-foreground">{t(`format.about.${codec}`)}</p>
      {codec === "ProRes" ? (
        <div className="space-y-2 pt-2">
          <ChoiceCards
            label={t("format.proResLabel")}
            className="grid-cols-3"
            value={options.proResProfile}
            choices={proResChoices.map((profile) => ({
              value: profile,
              title: proResNames[profile],
              detail: t(`format.proRes.${profile}`),
            }))}
            onChange={(proResProfile) => update((current) => ({ ...current, proResProfile }))}
          />
          <p className="text-xs leading-relaxed text-muted-foreground">
            {t("format.proResHardware")}
          </p>
        </div>
      ) : (
        <div className="divide-y divide-border/60">
          <SliderSetting
            label={t("format.fileSize")}
            value={options.bitrateMbps}
            min={1}
            max={200}
            step={1}
            format={(value) => t("format.fileSizeValue", { size: megabytesPerMinute(value) })}
            onChange={(bitrateMbps) => update((current) => ({ ...current, bitrateMbps }))}
          />
          <SettingRow label={t("format.colors")}>
            <Segmented
              label={t("format.colors")}
              value={String(options.bitDepth) as "8" | "10"}
              choices={[
                { value: "8", title: t("format.colors8") },
                { value: "10", title: t("format.colors10") },
              ]}
              onChange={(bits) =>
                update((current) => ({
                  ...current,
                  bitDepth: Number(bits) as 8 | 10,
                  pixelFormat: "auto",
                }))
              }
            />
          </SettingRow>
        </div>
      )}
    </OptionGroup>
  );
}

function PlaybackSettings({ control, update }: { control: Control; update: Update }) {
  const { t } = useTranslation("render");
  const options = control.options;
  const precisions = precisionChoices.filter((value) => value >= options.videoFps);
  const issues = control.optionValidation.success ? [] : control.optionValidation.error.issues;
  const invalidPrecision = issues.some((issue) => issue.path[0] === "simulationFps");
  return (
    <div className="divide-y divide-border/60 border-t border-border/60">
      <SettingRow
        label={t("precision.label")}
        hint={
          invalidPrecision ? t("precision.invalid", { fps: options.videoFps }) : t("precision.hint")
        }
      >
        <Segmented
          label={t("precision.label")}
          value={
            (precisions as readonly number[]).includes(options.simulationFps)
              ? String(options.simulationFps)
              : null
          }
          choices={precisions.map((fps) => ({ value: String(fps), title: String(fps) }))}
          onChange={(fps) => update((current) => ({ ...current, simulationFps: Number(fps) }))}
        />
      </SettingRow>
      <SliderSetting
        label={t("ending.label")}
        hint={t("ending.hint")}
        value={options.endDelaySeconds}
        min={0}
        max={10}
        step={0.5}
        format={(seconds) => t("ending.seconds", { seconds })}
        onChange={(endDelaySeconds) => update((current) => ({ ...current, endDelaySeconds }))}
      />
    </div>
  );
}

/**
 * Where the video is saved, kept apart from how it looks: the folder as one
 * button that changes it. The folder window opens on the computer running
 * ADOFAI; if it cannot, the path can be typed instead.
 */
export function RenderSaveLocation({ control }: { control: Control }) {
  const { t } = useTranslation("render");
  const [choosing, setChoosing] = useState(false);
  const [typing, setTyping] = useState(false);
  const folder = describeFolder(
    control.options.outputDirectory || control.settings?.outputDirectory || defaultFolder,
  );
  const choose = async () => {
    setChoosing(true);
    try {
      await control.chooseOutputDirectory();
      setTyping(false);
    } catch {
      // The folder window could not open; let the player type the path instead.
      setTyping(true);
    } finally {
      setChoosing(false);
    }
  };

  return (
    <div className="flex min-w-0 flex-1 items-center gap-2.5">
      <span className="shrink-0 text-xs text-muted-foreground">{t("saveTo.label")}</span>
      {typing ? (
        <Input
          aria-label={t("saveTo.pathLabel")}
          value={control.options.outputDirectory}
          placeholder={defaultFolder}
          maxLength={4096}
          spellCheck={false}
          autoComplete="off"
          className="h-8 min-w-0 flex-1 text-xs"
          onChange={(event) => {
            const outputDirectory = event.target.value;
            control.setOptions((current) => ({ ...current, outputDirectory }));
          }}
        />
      ) : (
        <button
          type="button"
          disabled={choosing}
          onClick={() => void choose()}
          className="flex h-8 min-w-0 items-center gap-2 rounded-lg border border-border bg-foreground/[0.03] pr-2.5 pl-2 text-left transition-[background-color,transform] outline-none hover:bg-foreground/[0.07] focus-visible:ring-3 focus-visible:ring-ring/50 active:scale-[0.98] disabled:opacity-70"
        >
          <HugeiconsIcon
            aria-hidden="true"
            icon={Folder01Icon}
            size={15}
            className="shrink-0 text-muted-foreground"
          />
          {choosing ? (
            <span role="status" className="truncate text-xs text-muted-foreground">
              {t("saveTo.choosingHelp")}
            </span>
          ) : (
            <>
              <span className="shrink-0 text-sm font-medium">{folder.name || folder.path}</span>
              {folder.parent ? (
                <span className="min-w-0 truncate text-xs text-muted-foreground">
                  {folder.parent}
                </span>
              ) : null}
              <span className="shrink-0 pl-1 text-xs font-medium text-primary">
                {t("saveTo.change")}
              </span>
            </>
          )}
        </button>
      )}
      {typing ? (
        <Button
          type="button"
          variant="ghost"
          size="xs"
          className="shrink-0"
          disabled={choosing}
          onClick={() => void choose()}
        >
          {t(choosing ? "saveTo.choosing" : "saveTo.change")}
        </Button>
      ) : null}
    </div>
  );
}
