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
  if (!difficulty || !difficultyIcons[difficulty]) return null;

  return (
    <span className="grid size-6 shrink-0 place-items-center">
      <img
        src={difficultyIcons[difficulty]}
        alt={`${difficulty} judgment difficulty`}
        title={`${difficulty} judgment difficulty`}
        className="block size-[18px] translate-y-[0.5px]"
      />
    </span>
  );
}
