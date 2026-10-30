import {
  Delete02Icon,
  FolderLibraryIcon,
  PlayIcon,
  SquareLock02Icon,
  SquareUnlock02Icon,
  Upload04Icon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { ActivityRun } from "@/models/activity/activity-model";
import {
  ContextMenuItem,
  ContextMenuSeparator,
  ContextMenuShortcut,
} from "@/shared/ui/context-menu";
import {
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuShortcut,
} from "@/shared/ui/dropdown-menu";

export interface RunMenuState {
  readOnly: boolean;
  /** Replay of this run is pending or active, which blocks deleting it. */
  replayActive: boolean;
  /** Any replay is pending or active, which blocks starting or rendering another. */
  replayBusy: boolean;
}

export interface RunMenuHandlers {
  onPlay: (run: ActivityRun) => void;
  onShowRecordings: (run: ActivityRun) => void;
  onRender?: (run: ActivityRun) => void;
  onToggleLock: (run: ActivityRun) => void;
  onDelete: (run: ActivityRun) => void;
}

/** The same run commands for the ⋯ button and the row's right-click menu. */
export function RunMenuItems({
  kind,
  run,
  state,
  handlers,
}: {
  kind: "dropdown" | "context";
  run: ActivityRun;
  state: RunMenuState;
  handlers: RunMenuHandlers;
}) {
  const { t } = useTranslation("activity");
  const { t: replayT } = useTranslation("replay");
  const { t: renderT } = useTranslation("render");
  const Item = kind === "dropdown" ? DropdownMenuItem : ContextMenuItem;
  const Separator = kind === "dropdown" ? DropdownMenuSeparator : ContextMenuSeparator;
  const Shortcut = kind === "dropdown" ? DropdownMenuShortcut : ContextMenuShortcut;
  const hasRecordings = run.hasMicrophoneRecording || run.hasCameraRecording;
  const deleteBlocked = state.readOnly || state.replayActive || run.locked;

  return (
    <>
      <Item
        disabled={state.readOnly || !run.replayPlayable || state.replayBusy}
        onSelect={() => handlers.onPlay(run)}
      >
        <HugeiconsIcon aria-hidden="true" icon={PlayIcon} className="size-4" />
        {t("run.playReplay")}
        <Shortcut>↵</Shortcut>
      </Item>
      <Item disabled={!hasRecordings} onSelect={() => handlers.onShowRecordings(run)}>
        <HugeiconsIcon aria-hidden="true" icon={FolderLibraryIcon} className="size-4" />
        {t("recordings.label")}
      </Item>
      {handlers.onRender ? (
        <Item
          disabled={state.readOnly || Boolean(run.replayUnavailableReason) || state.replayBusy}
          onSelect={() => handlers.onRender?.(run)}
        >
          <RenderSlateIcon />
          {renderT("button")}
        </Item>
      ) : null}
      <Item disabled>
        <HugeiconsIcon aria-hidden="true" icon={Upload04Icon} className="size-4" />
        <MenuLabel hint={t("run.comingSoon")}>{replayT("run.submit")}</MenuLabel>
      </Item>
      <Separator />
      <Item disabled={state.readOnly} onSelect={() => handlers.onToggleLock(run)}>
        <HugeiconsIcon
          aria-hidden="true"
          icon={run.locked ? SquareUnlock02Icon : SquareLock02Icon}
          className="size-4"
        />
        <MenuLabel hint={run.locked ? null : t("lock.lockHint")}>
          {t(run.locked ? "lock.unlock" : "lock.lock")}
        </MenuLabel>
      </Item>
      <Item
        className="text-destructive data-highlighted:bg-destructive/10 data-highlighted:text-destructive"
        disabled={deleteBlocked}
        onSelect={() => handlers.onDelete(run)}
      >
        <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="size-4" />
        <MenuLabel
          hint={
            run.locked
              ? t("lock.deleteBlocked")
              : state.replayActive
                ? t("run.deleteWhileReplaying")
                : null
          }
        >
          {replayT("run.delete")}…
        </MenuLabel>
      </Item>
    </>
  );
}

function MenuLabel({ hint, children }: { hint: string | null; children: ReactNode }) {
  return (
    <span className="grid min-w-0 gap-0.5">
      <span>{children}</span>
      {hint ? <span className="text-xs text-muted-foreground">{hint}</span> : null}
    </span>
  );
}

function RenderSlateIcon() {
  return (
    <svg
      aria-hidden="true"
      viewBox="0 0 24 24"
      className="size-4"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      <path d="m3 10-.7-3.8 17.6-3.3.7 3.9L3 10Z" />
      <path d="m6.7 5.4 2.7 3.4m3.5-4.6 2.7 3.5M3 10h18v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-9Z" />
    </svg>
  );
}
