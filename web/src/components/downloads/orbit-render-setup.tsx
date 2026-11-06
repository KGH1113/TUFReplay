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
import { Progress } from "@/shared/ui/progress";

const licenseUrl = "https://github.com/imnyang/OrbitRender/blob/main/LICENSE.md";
const latestReleaseUrl = "https://github.com/imnyang/OrbitRender/releases/latest";

/**
 * Getting OrbitRender ready as three steps: install, restart ADOFAI, make a video.
 * The step you are on stands out, finished steps are ticked, and the button does that step.
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
  const consent = state.Status === "awaiting-consent";
  const action = renderT("button");

  return (
    <div className="space-y-5">
      <p role="status" aria-live="polite" className="sr-only">
        {t(`status.${state.Status}`)}
      </p>
      <ol aria-label={t("setup.label")}>
        <SetupStep
          index={1}
          state={install}
          title={t(stage === "update" ? "setup.install.updateTitle" : "setup.install.title")}
        >
          <InstallDetail state={state} stage={stage} />
        </SetupStep>
        <SetupStep index={2} state={restart} title={t("setup.restart.title")}>
          {restart === "current" ? (
            <>
              <p>{t("setup.restart.now")}</p>
              <p className="text-muted-foreground/80">{t("setup.restart.stuck")}</p>
            </>
          ) : (
            <p>{t(restart === "done" ? "setup.restart.done" : "setup.restart.about")}</p>
          )}
        </SetupStep>
        <SetupStep index={3} state={create} title={t("setup.create.title")} last>
          <p>
            {create === "current"
              ? t("setup.create.ready", { action })
              : t("setup.create.about", { action })}
          </p>
        </SetupStep>
      </ol>
      <FinePrint state={state} stage={stage} consent={consent} />
      <div className="flex flex-wrap justify-end gap-2 border-t border-border/60 pt-4">
        <SetupActions
          stage={stage}
          consent={consent}
          pending={pending}
          onAction={onAction}
          onClose={onClose}
        />
      </div>
    </div>
  );
}

/** The license before installing and where OrbitRender goes, kept small under the steps. */
function FinePrint({
  state,
  stage,
  consent,
}: {
  state: InstallState;
  stage: OrbitRenderSetupStage;
  consent: boolean;
}) {
  const { t } = useTranslation("downloads");
  const license = stage === "install" || stage === "install-failed";
  const location =
    state.InstallationDirectory && (consent || stage === "restart" || stage === "ready");
  if (!license && !location) return null;
  return (
    <div className="space-y-0.5 text-xs leading-relaxed text-muted-foreground">
      {license ? (
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
      ) : null}
      {location ? (
        <p className="break-all">{t("installLocation", { path: state.InstallationDirectory })}</p>
      ) : null}
    </div>
  );
}

/** What step 1 says: what installing does, how far it got, or why it stopped. */
function InstallDetail({ state, stage }: { state: InstallState; stage: OrbitRenderSetupStage }) {
  const { t, i18n } = useTranslation("downloads");
  if (stage === "checking") return <p>{t("status.checking")}</p>;
  if (stage === "installing") {
    const total = state.TotalBytes;
    return (
      <div className="space-y-2">
        <div className="flex items-baseline justify-between gap-3">
          <p>{t(`status.${state.Status}`)}</p>
          {total ? (
            <p className="shrink-0 tabular-nums">
              {(state.DownloadedBytes / 1048576).toFixed(1)} / {(total / 1048576).toFixed(1)} MB
            </p>
          ) : null}
        </div>
        {total ? (
          <Progress
            aria-label={t("progress", { name: "OrbitRender" })}
            value={Math.min(100, (state.DownloadedBytes / total) * 100)}
            className="h-1.5"
          />
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
        {state.Error || state.InstallationDirectory ? (
          <DetailsToggle label={t("details")}>
            <div className="space-y-1 break-words">
              {state.Error ? <p>{state.Error}</p> : null}
              {state.InstallationDirectory ? (
                <p className="break-all">
                  {t("installLocation", { path: state.InstallationDirectory })}
                </p>
              ) : null}
            </div>
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

/** The buttons always offer the step you are on; closing never loses progress. */
function SetupActions({
  stage,
  consent,
  pending,
  onAction,
  onClose,
}: {
  stage: OrbitRenderSetupStage;
  consent: boolean;
  pending: boolean;
  onAction: (action: DownloadAction) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation("downloads");
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
          {consent ? t("later") : t("close")}
        </Button>
        <Button
          size="sm"
          disabled={pending}
          onClick={() => onAction(consent ? "confirm" : "request")}
        >
          {consent ? t("agreeInstall") : stage === "install-failed" ? t("retry") : t("install")}
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
  title,
  last = false,
  children,
}: {
  index: number;
  state: SetupStepState;
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
      <StepMarker index={index} state={state} />
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

function StepMarker({ index, state }: { index: number; state: SetupStepState }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        "flex size-7 shrink-0 items-center justify-center rounded-full text-xs font-semibold tabular-nums transition-colors duration-300",
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
