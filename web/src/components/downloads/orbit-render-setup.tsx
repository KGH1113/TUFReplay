import { Alert02Icon, Loading03Icon, Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { DownloadAction, InstallState } from "@/api/downloads/downloads-api";
import {
  type OrbitRenderSetupStage,
  orbitRenderSetupStage,
  orbitRenderSetupSteps,
  type SetupStepState,
} from "@/models/downloads/orbit-render-setup";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { DetailsToggle } from "@/shared/ui/details-toggle";

const licenseUrl = "https://github.com/imnyang/OrbitRender/blob/main/LICENSE.md";
const latestReleaseUrl = "https://github.com/imnyang/OrbitRender/releases/latest";

/**
 * Getting OrbitRender ready as three steps: install, restart ADOFAI, make a video.
 * The step you are on stands out, finished steps are ticked, and the button does that step.
 * Every step keeps the same number of lines from start to finish, so the dialog does not
 * jump while installing; only an install error adds lines.
 */
export function OrbitRenderSetup({
  state,
  pending,
  onAction,
  onClose,
}: {
  state: InstallState;
  pending: boolean;
  onAction: (action: DownloadAction) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation("downloads");
  const { t: renderT } = useTranslation("render");
  const stage = orbitRenderSetupStage(state);
  const [install, restart, create] = orbitRenderSetupSteps(stage);
  const downloaded =
    state.Status === "downloading" && state.TotalBytes
      ? Math.min(100, (state.DownloadedBytes / state.TotalBytes) * 100)
      : undefined;

  return (
    <div className="space-y-5">
      <p role="status" aria-live="polite" className="sr-only">
        {t(`status.${state.Status}`)}
      </p>
      <ol aria-label={t("setup.label")}>
        <SetupStep
          index={1}
          state={install}
          progress={downloaded}
          title={t(stage === "update" ? "setup.install.updateTitle" : "setup.install.title")}
        >
          <InstallDetail state={state} stage={stage} />
        </SetupStep>
        <SetupStep index={2} state={restart} title={t("setup.restart.title")}>
          {restart === "done" ? (
            <p>{t("setup.restart.done")}</p>
          ) : (
            <>
              <p>{t("setup.restart.about")}</p>
              <p className="text-muted-foreground/80">{t("setup.restart.stuck")}</p>
            </>
          )}
        </SetupStep>
        <SetupStep index={3} state={create} title={t("setup.create.title")} last>
          <p>{t("setup.create.about", { action: renderT("button") })}</p>
        </SetupStep>
      </ol>
      <FinePrint state={state} />
      <div className="flex flex-wrap justify-end gap-2 border-t border-border/60 pt-4">
        <SetupActions
          state={state}
          stage={stage}
          pending={pending}
          onAction={onAction}
          onClose={onClose}
        />
      </div>
    </div>
  );
}

/** The license and where OrbitRender goes, kept small under the steps at every stage. */
function FinePrint({ state }: { state: InstallState }) {
  const { t } = useTranslation("downloads");
  return (
    <div className="space-y-0.5 text-xs leading-relaxed text-muted-foreground">
      <p>
        {t("setup.license")}{" "}
        <a
          href={licenseUrl}
          target="_blank"
          rel="noreferrer"
          className="text-primary underline underline-offset-2"
        >
          {t("license")}
        </a>
      </p>
      {state.InstallationDirectory ? (
        <p className="break-all">{t("installLocation", { path: state.InstallationDirectory })}</p>
      ) : null}
    </div>
  );
}

/** What step 1 says, in one line: what installing does, how far it got, or what is installed. */
function InstallDetail({ state, stage }: { state: InstallState; stage: OrbitRenderSetupStage }) {
  const { t, i18n } = useTranslation("downloads");
  if (stage === "checking") return <p>{t("status.checking")}</p>;
  if (stage === "installing") {
    const total = state.Status === "downloading" ? state.TotalBytes : null;
    return (
      <div className="flex items-baseline justify-between gap-3">
        <p className="truncate">{t(`status.${state.Status}`)}</p>
        {total ? (
          <p className="shrink-0 tabular-nums">
            {(state.DownloadedBytes / 1048576).toFixed(1)} / {(total / 1048576).toFixed(1)} MB
          </p>
        ) : null}
      </div>
    );
  }
  if (stage === "install-failed") {
    const known = state.ErrorCode && i18n.exists(`errors.${state.ErrorCode}`, { ns: "downloads" });
    return (
      <div role="alert" className="space-y-1">
        <p className="text-destructive">
          {known ? t(`errors.${state.ErrorCode}` as never) : t("errors.installFailed")}
        </p>
        {state.Error ? (
          <DetailsToggle label={t("details")}>
            <p className="break-words">{state.Error}</p>
          </DetailsToggle>
        ) : null}
      </div>
    );
  }
  if (stage === "update")
    return (
      <p>
        {state.Version
          ? t("setup.install.update", { version: state.Version })
          : t("setup.install.updateUnknown")}{" "}
        <a
          href={latestReleaseUrl}
          target="_blank"
          rel="noreferrer"
          className="text-primary underline underline-offset-2"
        >
          {t("setup.install.latestRelease")}
        </a>
      </p>
    );
  if (stage === "restart" || stage === "ready")
    return <p>{[t("setup.install.done"), state.Version].filter(Boolean).join(" · ")}</p>;
  return <p>{t("setup.install.about")}</p>;
}

/**
 * The buttons always offer the step you are on, and keep their labels while the game
 * catches up, so they do not change under the pointer right after the dialog opens.
 */
function SetupActions({
  state,
  stage,
  pending,
  onAction,
  onClose,
}: {
  state: InstallState;
  stage: OrbitRenderSetupStage;
  pending: boolean;
  onAction: (action: DownloadAction) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation("downloads");
  // Agreeing here is the consent; ask for it first when the game has not yet.
  const install = () => {
    if (state.Status !== "awaiting-consent") onAction("request");
    onAction("confirm");
  };
  if (stage === "installing")
    return (
      <>
        <Button size="sm" variant="ghost" disabled={pending} onClick={() => onAction("cancel")}>
          {t("cancel")}
        </Button>
        <Button size="sm" variant="outline" onClick={onClose}>
          {t("background")}
        </Button>
      </>
    );
  if (stage === "install" || stage === "install-failed")
    return (
      <>
        <Button size="sm" variant="outline" onClick={onClose}>
          {stage === "install" ? t("later") : t("close")}
        </Button>
        <Button size="sm" disabled={pending} onClick={install}>
          {stage === "install" ? t("agreeInstall") : t("retry")}
        </Button>
      </>
    );
  if (stage === "restart" || stage === "ready")
    return (
      <Button size="sm" onClick={onClose}>
        {stage === "ready" ? t("done") : t("setup.gotIt")}
      </Button>
    );
  return (
    <Button size="sm" variant="outline" onClick={onClose}>
      {t("close")}
    </Button>
  );
}

function SetupStep({
  index,
  state,
  progress,
  title,
  last = false,
  children,
}: {
  index: number;
  state: SetupStepState;
  progress?: number;
  title: string;
  last?: boolean;
  children: ReactNode;
}) {
  const { t } = useTranslation("downloads");
  return (
    <li
      aria-current={state === "upcoming" || state === "done" ? undefined : "step"}
      className={cn("relative flex gap-3.5", !last && "pb-5")}
    >
      {last ? null : (
        <span
          aria-hidden="true"
          className={cn(
            "absolute top-8 bottom-1 left-3.5 w-px -translate-x-1/2 transition-colors duration-300",
            state === "done" ? "bg-primary/50" : "bg-border",
          )}
        />
      )}
      <StepMarker index={index} state={state} progress={progress} />
      <div className="min-w-0 flex-1 space-y-1 pt-1">
        <p
          className={cn(
            "text-sm leading-5 font-medium transition-colors duration-300",
            state === "upcoming" && "text-muted-foreground",
          )}
        >
          {title}
          <span className="sr-only"> · {t(`setup.state.${state}`)}</span>
        </p>
        <div
          className={cn(
            "space-y-1 text-xs leading-relaxed text-muted-foreground",
            state === "upcoming" && "text-muted-foreground/80",
          )}
        >
          {children}
        </div>
      </div>
    </li>
  );
}

const ringRadius = 12;
const ringLength = 2 * Math.PI * ringRadius;

/** A number, a tick, a spinner, a warning, or while downloading, a ring that fills up. */
function StepMarker({
  index,
  state,
  progress,
}: {
  index: number;
  state: SetupStepState;
  progress?: number;
}) {
  const { t } = useTranslation("downloads");
  const base =
    "flex size-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold tabular-nums transition-colors duration-300";
  if (state === "working" && progress !== undefined)
    return (
      <span
        role="progressbar"
        aria-label={t("progress", { name: "OrbitRender" })}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(progress)}
        className={cn(base, "text-primary")}
      >
        <svg viewBox="0 0 28 28" aria-hidden="true" className="size-7 -rotate-90">
          <circle
            cx="14"
            cy="14"
            r={ringRadius}
            fill="none"
            stroke="currentColor"
            strokeOpacity={0.2}
            strokeWidth={2.5}
          />
          <circle
            cx="14"
            cy="14"
            r={ringRadius}
            fill="none"
            stroke="currentColor"
            strokeWidth={2.5}
            strokeLinecap="round"
            strokeDasharray={ringLength}
            strokeDashoffset={ringLength * (1 - progress / 100)}
            className="transition-[stroke-dashoffset] duration-300 ease-out motion-reduce:transition-none"
          />
        </svg>
      </span>
    );
  return (
    <span
      aria-hidden="true"
      className={cn(
        base,
        state === "upcoming" && "border border-border text-muted-foreground",
        state === "current" && "bg-primary text-primary-foreground",
        (state === "working" || state === "done") && "bg-primary/15 text-primary",
        state === "attention" && "bg-destructive/15 text-destructive",
      )}
    >
      {state === "done" ? (
        <HugeiconsIcon icon={Tick02Icon} size={15} strokeWidth={2.25} />
      ) : state === "working" ? (
        <HugeiconsIcon
          icon={Loading03Icon}
          size={15}
          strokeWidth={2}
          className="animate-spin motion-reduce:animate-none"
        />
      ) : state === "attention" ? (
        <HugeiconsIcon icon={Alert02Icon} size={15} strokeWidth={2} />
      ) : (
        index
      )}
    </span>
  );
}
