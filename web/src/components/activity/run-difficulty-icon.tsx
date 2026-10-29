import { useTranslation } from "react-i18next";
import type { JudgmentDifficulty } from "@/models/activity/activity-model";
import lenientBullseye from "@/shared/assets/activity/lenient-bullseye.png";
import normalBullseye from "@/shared/assets/activity/normal-bullseye.png";
import strictBullseye from "@/shared/assets/activity/strict-bullseye.png";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

const difficultyIcons: Record<JudgmentDifficulty, string> = {
  Lenient: lenientBullseye,
  Normal: normalBullseye,
  Strict: strictBullseye,
};

export function RunDifficultyIcon({ difficulty }: { difficulty?: JudgmentDifficulty | null }) {
  const { t } = useTranslation("activity");
  if (!difficulty || !difficultyIcons[difficulty]) return null;
  const label = t("run.difficulty", { difficulty: t(`run.difficultyName.${difficulty}`) });

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span role="img" aria-label={label} className="grid size-6 shrink-0 place-items-center">
          <img
            src={difficultyIcons[difficulty]}
            alt=""
            className="block size-[17px]"
            draggable={false}
          />
        </span>
      </TooltipTrigger>
      <TooltipContent side="top">{label}</TooltipContent>
    </Tooltip>
  );
}
