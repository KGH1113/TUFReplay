import { Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import type { VisualKind } from "@/models/visual/visual-model";
import { TabsList, TabsTrigger } from "@/shared/ui/tabs";

export function VisualKindTabs({
  disabled = false,
  selectedNames,
}: {
  disabled?: boolean;
  selectedNames?: Partial<Record<VisualKind, string>>;
}) {
  const { t } = useTranslation("submission");
  return (
    <TabsList aria-label={t("visual.libraryTitle")} className="shrink-0">
      {(["keyviewer", "overlay"] as const).map((kind) => (
        <TabsTrigger
          key={kind}
          value={kind}
          disabled={disabled}
          title={selectedNames?.[kind]}
          className="gap-2 px-4 data-[state=active]:bg-background data-[state=active]:text-foreground"
        >
          {t(`visual.${kind}`)}
          {selectedNames?.[kind] ? (
            <>
              <HugeiconsIcon
                aria-hidden="true"
                icon={Tick02Icon}
                className="size-3.5 text-foreground/70"
              />
              <span className="sr-only">{selectedNames[kind]}</span>
            </>
          ) : null}
        </TabsTrigger>
      ))}
    </TabsList>
  );
}
