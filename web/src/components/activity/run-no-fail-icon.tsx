import { useTranslation } from "react-i18next";
import { useGameIcon } from "@/hooks/activity/use-game-icon";
import noFailIcon from "@/shared/assets/activity/no-fail.png";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

export function RunNoFailIcon({ enabled }: { enabled?: boolean }) {
  if (!enabled) return null;
  return <NoFailIcon />;
}

function NoFailIcon() {
  const { t } = useTranslation("activity");
  const src = useGameIcon("noFail", noFailIcon);
  const label = t("run.noFail");

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span role="img" aria-label={label} className="grid size-6 shrink-0 place-items-center">
          {src ? (
            <img src={src} alt="" className="block size-[18px] object-contain" draggable={false} />
          ) : null}
        </span>
      </TooltipTrigger>
      <TooltipContent side="top">{label}</TooltipContent>
    </Tooltip>
  );
}
