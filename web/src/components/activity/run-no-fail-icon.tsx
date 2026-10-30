import { useTranslation } from "react-i18next";
import noFailIcon from "@/shared/assets/activity/no-fail.png";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

export function RunNoFailIcon({ enabled }: { enabled?: boolean }) {
  const { t } = useTranslation("activity");
  if (!enabled) return null;
  const label = t("run.noFail");

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span role="img" aria-label={label} className="grid size-6 shrink-0 place-items-center">
          <img
            src={noFailIcon}
            alt=""
            className="block size-[18px] object-contain"
            draggable={false}
          />
        </span>
      </TooltipTrigger>
      <TooltipContent side="top">{label}</TooltipContent>
    </Tooltip>
  );
}
