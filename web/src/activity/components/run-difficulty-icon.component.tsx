import { useTranslation } from "react-i18next";
import type { JudgmentDifficulty } from "../activity.model";

import lenientBullseye from "../assets/lenient-bullseye.png";
import normalBullseye from "../assets/normal-bullseye.png";
import strictBullseye from "../assets/strict-bullseye.png";

const difficultyIcons: Record<JudgmentDifficulty, string> = {
  Lenient: lenientBullseye,
  Normal: normalBullseye,
  Strict: strictBullseye,
};

export function RunDifficultyIcon({ difficulty }: { difficulty?: JudgmentDifficulty | null }) {
  const { t } = useTranslation("activity");
  if (!difficulty || !difficultyIcons[difficulty]) return null;
  const label = t("run.difficulty", { difficulty });

  return (
    <span className="grid size-6 shrink-0 place-items-center">
      <img
        src={difficultyIcons[difficulty]}
        alt={label}
        title={label}
        className="block size-[18px] translate-y-[0.5px]"
      />
    </span>
  );
}
