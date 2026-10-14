import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/shared/ui/button";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/shared/ui/tooltip";

export function CameraSettingHelp({ label, children }: { label: string; children: ReactNode }) {
  const { t } = useTranslation("webcam");
  return (
    <TooltipProvider delayDuration={200}>
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            type="button"
            variant="ghost"
            size="icon-xs"
            aria-label={t("settingInfo", { setting: label })}
            className="size-7 shrink-0 rounded-md p-1.5 text-muted-foreground transition-[color,background-color] duration-150 ease-out hover:text-foreground motion-reduce:transition-none"
          >
            <span
              aria-hidden="true"
              className="grid size-3.5 place-items-center rounded-full border border-current text-[10px] font-semibold leading-none"
            >
              i
            </span>
          </Button>
        </TooltipTrigger>
        <TooltipContent side="top" className="max-w-80 space-y-2 text-pretty leading-relaxed">
          {children}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
