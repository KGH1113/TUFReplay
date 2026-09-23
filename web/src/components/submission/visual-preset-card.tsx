import { Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { VisualPreset } from "@/models/visual/visual-model";
import { visualPresetAvatar } from "@/models/visual/visual-model";
import { cn } from "@/shared/lib/cn";
import { Badge } from "@/shared/ui/badge";
import { Card } from "@/shared/ui/card";

export function VisualPresetCard({
  preset,
  action,
  selection,
}: {
  preset: VisualPreset;
  action?: ReactNode;
  selection?: { groupId: string; selected: boolean; onSelect: () => void; disabled: boolean };
}) {
  const content = <VisualPresetCardContent preset={preset} action={action} />;
  return (
    <Card
      className={cn(
        "min-w-0 gap-0 rounded-xl bg-transparent py-0 shadow-none backdrop-blur-none",
        selection &&
          "transition-colors has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring has-[:disabled]:opacity-60 motion-reduce:transition-none",
        selection &&
          (selection.selected
            ? "bg-primary/5 ring-primary/60"
            : "hover:bg-muted/20 hover:ring-foreground/25"),
      )}
    >
      {selection ? (
        <label className="relative block h-full cursor-pointer has-[:disabled]:cursor-wait">
          <input
            type="radio"
            name={selection.groupId}
            value={preset.id}
            checked={selection.selected}
            onChange={selection.onSelect}
            disabled={selection.disabled}
            className="sr-only"
          />
          <span
            aria-hidden="true"
            className={cn(
              "absolute right-3 top-3 grid size-5 place-items-center rounded-full border",
              selection.selected
                ? "border-primary bg-primary text-neutral-950"
                : "border-foreground/25 bg-popover/80",
            )}
          >
            {selection.selected ? (
              <HugeiconsIcon icon={Tick02Icon} className="size-3.5" strokeWidth={2.5} />
            ) : null}
          </span>
          {content}
        </label>
      ) : (
        content
      )}
    </Card>
  );
}

function VisualPresetCardContent({ preset, action }: { preset: VisualPreset; action?: ReactNode }) {
  const { t } = useTranslation("submission");
  return (
    <>
      <span
        aria-hidden="true"
        className="grid aspect-[4/3] place-items-center bg-muted/40 text-5xl font-semibold tracking-tight text-foreground/75"
      >
        {visualPresetAvatar(preset.name)}
      </span>
      <span className="flex items-start gap-1 p-3">
        <span className="min-w-0 flex-1 space-y-2">
          <span className="block break-words text-sm font-medium leading-snug">{preset.name}</span>
          <Badge
            variant="secondary"
            className="h-auto min-h-5 max-w-full whitespace-normal break-words text-left leading-normal"
            title={t("visual.sourceVersion", { version: preset.source_version })}
          >
            {t(`visual.source.${preset.source}`)}
          </Badge>
        </span>
        {action}
      </span>
    </>
  );
}
