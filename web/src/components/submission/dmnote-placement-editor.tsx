import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { useDmnoteRegistration } from "@/hooks/visual/use-dmnote-registration";
import {
  DMNOTE_SCALE_MAX,
  DMNOTE_SCALE_MIN,
  DMNOTE_VIEWPORT_HEIGHT,
  DMNOTE_VIEWPORT_WIDTH,
  dmnoteScaledDimensions,
} from "@/models/visual/dmnote-registration-model";
import { Button } from "@/shared/ui/button";

export function DmnotePlacementEditor({
  editor,
  disabled,
}: {
  editor: ReturnType<typeof useDmnoteRegistration>;
  disabled: boolean;
}) {
  const { t } = useTranslation("submission");
  const { layout, position } = editor;
  if (!layout) return null;
  const dimensions = dmnoteScaledDimensions(layout, position.scale);
  const scalePercent = Math.round(dimensions.scale * 100);
  const maxX = Math.max(0, Math.floor(DMNOTE_VIEWPORT_WIDTH - dimensions.width));
  const maxY = Math.max(0, Math.floor(DMNOTE_VIEWPORT_HEIGHT - dimensions.height));
  return (
    <fieldset disabled={disabled} className="space-y-2">
      <legend className="text-sm font-medium">{t("visual.placementTitle")}</legend>
      <p className="text-xs leading-relaxed text-muted-foreground">{t("visual.placementHint")}</p>
      <svg
        viewBox="0 0 1920 1080"
        role="img"
        aria-label={t("visual.placementCanvas")}
        tabIndex={disabled ? -1 : 0}
        className="aspect-video w-full touch-none rounded-lg border border-border bg-black/70 outline-none focus-visible:ring-2 focus-visible:ring-ring"
        onPointerDown={disabled ? undefined : editor.onPointerDown}
        onPointerMove={disabled ? undefined : editor.onPointerMove}
        onPointerUp={editor.onPointerUp}
        onPointerCancel={editor.onPointerUp}
        onLostPointerCapture={editor.onPointerUp}
        onKeyDown={disabled ? undefined : editor.onKeyDown}
      >
        <title>{t("visual.placementCanvas")}</title>
        <path d="M960 0V1080M0 540H1920" stroke="white" opacity=".12" strokeDasharray="12 12" />
        <g
          transform={`translate(${position.x} ${position.y}) scale(${dimensions.scale})`}
          className="cursor-grab active:cursor-grabbing"
        >
          <rect
            width={layout.width}
            height={layout.height}
            rx="12"
            fill="#b5854e"
            fillOpacity=".08"
            stroke="#d5b287"
            strokeWidth="4"
            strokeDasharray="12 8"
          />
          {layout.elements.map((item) => (
            <g key={item.id}>
              <rect
                x={item.x}
                y={item.y}
                width={item.width}
                height={item.height}
                rx="6"
                fill="#b5854e"
                fillOpacity=".75"
              />
              <text
                x={item.x + item.width / 2}
                y={item.y + item.height / 2}
                dominantBaseline="central"
                textAnchor="middle"
                fill="white"
                fontSize="16"
              >
                {item.label.length > 5 ? `${item.label.slice(0, 4)}…` : item.label}
              </text>
            </g>
          ))}
        </g>
      </svg>
      <div className="space-y-3 text-xs text-muted-foreground">
        <div className="space-y-2">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <label htmlFor="dmnote-placement-scale" className="font-medium text-foreground">
              {t("visual.placementScale")}
            </label>
            <Button
              type="button"
              variant="ghost"
              size="xs"
              onClick={() => editor.move({ ...position, scale: 1 })}
            >
              {t("visual.placementScaleReset")}
            </Button>
          </div>
          <div className="flex items-center gap-3">
            <input
              id="dmnote-placement-scale"
              aria-label={t("visual.placementScaleSlider")}
              type="range"
              min={DMNOTE_SCALE_MIN}
              max={DMNOTE_SCALE_MAX}
              step={0.01}
              value={dimensions.scale}
              onChange={(event) => editor.move({ ...position, scale: Number(event.target.value) })}
              className="min-w-0 flex-1 accent-primary"
            />
            <div className="flex shrink-0 items-center gap-1">
              <span className="sr-only">{t("visual.placementScaleInput")}</span>
              <ScalePercentageInput
                label={t("visual.placementScaleInput")}
                value={scalePercent}
                onCommit={(percent) => editor.move({ ...position, scale: percent / 100 })}
              />
              <span aria-hidden="true">%</span>
            </div>
          </div>
          <p className="text-[11px] leading-relaxed">{t("visual.placementScaleHint")}</p>
        </div>
        <div className="flex flex-wrap gap-4">
          <label className="flex items-center gap-2">
            X{" "}
            <input
              aria-label={t("visual.placementX")}
              type="number"
              min={0}
              max={maxX}
              value={position.x}
              onChange={(event) => editor.move({ ...position, x: Number(event.target.value) })}
              className="w-20 rounded border border-input bg-background px-2 py-1"
            />
          </label>
          <label className="flex items-center gap-2">
            Y{" "}
            <input
              aria-label={t("visual.placementY")}
              type="number"
              min={0}
              max={maxY}
              value={position.y}
              onChange={(event) => editor.move({ ...position, y: Number(event.target.value) })}
              className="w-20 rounded border border-input bg-background px-2 py-1"
            />
          </label>
        </div>
      </div>
    </fieldset>
  );
}

function ScalePercentageInput({
  label,
  value,
  onCommit,
}: {
  label: string;
  value: number;
  onCommit: (value: number) => void;
}) {
  const [draft, setDraft] = useState<string | null>(null);
  return (
    <input
      aria-label={label}
      type="number"
      min={DMNOTE_SCALE_MIN * 100}
      max={DMNOTE_SCALE_MAX * 100}
      step={1}
      value={draft ?? value}
      onChange={(event) => setDraft(event.target.value)}
      onBlur={() => {
        if (draft !== null && draft.trim() !== "" && Number.isFinite(Number(draft)))
          onCommit(Math.round(Number(draft)));
        setDraft(null);
      }}
      onKeyDown={(event) => {
        if (event.key === "Enter") {
          event.preventDefault();
          event.currentTarget.blur();
        }
      }}
      className="w-20 rounded border border-input bg-background px-2 py-1 text-right text-foreground"
    />
  );
}
