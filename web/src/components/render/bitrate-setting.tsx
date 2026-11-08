import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { SettingRow } from "@/components/render/render-option-controls";
import {
  type BitrateView,
  readBitrateView,
  saveBitrateView,
} from "@/hooks/render/render-option-storage";
import {
  bitrateForMegabytesPerMinute,
  bitrateRangeKbps,
  megabytesPerMinute,
  readTypedNumber,
  settleBitrate,
} from "@/models/render/render-choices";
import { Button } from "@/shared/ui/button";
import { Input } from "@/shared/ui/input";
import { Slider } from "@/shared/ui/slider";

/** True for a typed number, where Esc only puts the value back and the dialog stays open. */
export function isTypedNumber(target: EventTarget | null) {
  return target instanceof HTMLElement && target.dataset.typedNumber !== undefined;
}

// Splits "1분에 {{size}}MB" around the number, so the number can be a field.
const numberMark = "\u0000";

/**
 * The video's bitrate on one line. Pressing the label switches between the file it
 * makes each minute and the bitrate in Kbps; the number itself can be typed over.
 */
export function BitrateSetting({
  value,
  stepKbps,
  onChange,
}: {
  value: number;
  /** 1, or 1,000 when the renderer rounds to whole Mbps. */
  stepKbps: number;
  onChange: (bitrateKbps: number) => void;
}) {
  const { t, i18n } = useTranslation("render");
  const [view, setView] = useState<BitrateView>(readBitrateView);
  const settled = settleBitrate(value, stepKbps);
  const [draft, setDraft] = useState(settled);
  const dragging = useRef(false);
  useEffect(() => {
    if (!dragging.current) setDraft(settled);
  }, [settled]);

  const fileSize = view === "fileSize";
  const numbers = new Intl.NumberFormat(i18n.language);
  const shown = fileSize ? megabytesPerMinute(draft) : draft;
  const describe = (number: string) =>
    fileSize
      ? t("format.fileSizeValue", { size: number })
      : t("format.bitrateValue", { kbps: number });
  const [before = "", after = ""] = describe(numberMark).split(numberMark);
  const label = fileSize ? t("format.fileSize") : t("format.bitrate");

  const switchView = () => {
    const next = fileSize ? "bitrate" : "fileSize";
    setView(next);
    saveBitrateView(next);
  };
  const commit = (bitrateKbps: number) => {
    const next = settleBitrate(bitrateKbps, stepKbps);
    setDraft(next);
    onChange(next);
  };

  return (
    <SettingRow
      label={
        <Button
          type="button"
          variant="link"
          className="h-auto p-0 text-sm font-normal text-foreground underline decoration-muted-foreground/50 decoration-dotted underline-offset-4 hover:decoration-foreground"
          onClick={switchView}
        >
          {label}
          <span className="sr-only">
            {fileSize ? t("format.showBitrate") : t("format.showFileSize")}
          </span>
        </Button>
      }
    >
      <Slider
        aria-label={label}
        aria-valuetext={describe(numbers.format(shown))}
        min={bitrateRangeKbps.min}
        max={bitrateRangeKbps.max}
        step={1_000}
        value={[draft]}
        className="min-w-20 max-w-56 flex-1"
        onValueChange={([next]) => {
          dragging.current = true;
          setDraft(next);
        }}
        onValueCommit={([next]) => {
          dragging.current = false;
          commit(next);
        }}
      />
      <span className="w-32 shrink-0 text-right text-xs whitespace-nowrap tabular-nums text-muted-foreground">
        {before}
        <TypedNumber
          value={numbers.format(shown)}
          label={fileSize ? t("format.fileSizeInput") : t("format.bitrateInput")}
          onCommit={(typed) => commit(fileSize ? bitrateForMegabytesPerMinute(typed) : typed)}
        />
        {after}
      </span>
    </SettingRow>
  );
}

/** A number that reads as text and can be typed over. Enter or leaving keeps it; Esc puts it back. */
function TypedNumber({
  value,
  label,
  onCommit,
}: {
  value: string;
  label: string;
  onCommit: (value: number) => void;
}) {
  const [typing, setTyping] = useState<string | null>(null);
  const puttingBack = useRef(false);
  const text = typing ?? value;
  return (
    <Input
      type="text"
      inputMode="decimal"
      aria-label={label}
      data-typed-number=""
      value={text}
      style={{ width: `calc(${Math.max(text.length, 1)}ch + 0.625rem)` }}
      className="inline-block h-6 min-w-0 rounded-md border-transparent bg-transparent px-1 py-0 text-right align-baseline text-xs text-foreground tabular-nums shadow-none hover:bg-muted focus-visible:border-ring focus-visible:bg-background focus-visible:ring-2 md:text-xs"
      onFocus={(event) => {
        puttingBack.current = false;
        setTyping(value);
        event.currentTarget.select();
      }}
      onChange={(event) => setTyping(event.target.value)}
      onKeyDown={(event) => {
        if (event.key === "Enter") event.currentTarget.blur();
        if (event.key === "Escape") {
          puttingBack.current = true;
          event.currentTarget.blur();
        }
      }}
      onBlur={() => {
        const typed = typing === null ? null : readTypedNumber(typing);
        if (!puttingBack.current && typed !== null) onCommit(typed);
        setTyping(null);
      }}
    />
  );
}
