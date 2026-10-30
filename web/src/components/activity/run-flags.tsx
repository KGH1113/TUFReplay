import { Mic01Icon, Video01Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { RunDifficultyIcon } from "@/components/activity/run-difficulty-icon";
import { RunNoFailIcon } from "@/components/activity/run-no-fail-icon";
import type { ActivityRun } from "@/models/activity/activity-model";
import { cn } from "@/shared/lib/cn";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/shared/ui/tooltip";

/** Judgment difficulty, No-Fail and saved media for a run, in a fixed order so rows line up. */
export function RunFlags({ run, className }: { run: ActivityRun; className?: string }) {
  const { t } = useTranslation("activity");
  return (
    <span className={cn("flex items-center gap-0.5", className)}>
      <RunDifficultyIcon difficulty={run.judgmentDifficulty} />
      <RunNoFailIcon enabled={run.noFailMode} />
      {run.hasMicrophoneRecording ? (
        <MediaFlag icon={Mic01Icon} label={t("run.microphoneAvailable")} />
      ) : null}
      {run.hasCameraRecording ? (
        <MediaFlag icon={Video01Icon} label={t("run.cameraAvailable")} />
      ) : null}
    </span>
  );
}

function MediaFlag({ icon, label }: { icon: typeof Mic01Icon; label: string }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span
          role="img"
          aria-label={label}
          className="grid size-6 shrink-0 place-items-center text-muted-foreground"
        >
          <HugeiconsIcon aria-hidden="true" icon={icon} size={15} strokeWidth={1.8} />
        </span>
      </TooltipTrigger>
      <TooltipContent side="top">{label}</TooltipContent>
    </Tooltip>
  );
}
