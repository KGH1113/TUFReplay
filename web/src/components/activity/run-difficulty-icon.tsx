import { useTranslation } from "react-i18next";
import { useGameIcon } from "@/hooks/activity/use-game-icon";
import type { JudgmentDifficulty } from "@/models/activity/activity-model";
import type { GameIconName } from "@/models/activity/game-icon";
import lenientBullseye from "@/shared/assets/activity/lenient-bullseye.png";
import normalBullseye from "@/shared/assets/activity/normal-bullseye.png";
import strictBullseye from "@/shared/assets/activity/strict-bullseye.png";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

const difficultyIcons: Record<JudgmentDifficulty, { name: GameIconName; fallback: string }> = {
  Lenient: { name: "lenientBullseye", fallback: lenientBullseye },
  Normal: { name: "normalBullseye", fallback: normalBullseye },
  Strict: { name: "strictBullseye", fallback: strictBullseye },
};

export function RunDifficultyIcon({ difficulty }: { difficulty?: JudgmentDifficulty | null }) {
  if (!difficulty || !difficultyIcons[difficulty]) return null;
  return <DifficultyIcon difficulty={difficulty} />;
}

function DifficultyIcon({ difficulty }: { difficulty: JudgmentDifficulty }) {
  const { t } = useTranslation("activity");
  const icon = difficultyIcons[difficulty];
  const src = useGameIcon(icon.name, icon.fallback);
  const label = t("run.difficulty", { difficulty: t(`run.difficultyName.${difficulty}`) });

  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span role="img" aria-label={label} className="grid size-6 shrink-0 place-items-center">
          {src ? <img src={src} alt="" className="block size-[17px]" draggable={false} /> : null}
        </span>
      </TooltipTrigger>
      <TooltipContent side="top">{label}</TooltipContent>
    </Tooltip>
  );
}
