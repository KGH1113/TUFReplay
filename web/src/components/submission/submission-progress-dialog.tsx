import { Cancel01Icon, Loading03Icon, Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useRef } from "react";
import { useTranslation } from "react-i18next";
import type { SubmissionRun } from "@/models/submission/submission-model";
import {
  submissionProgress,
  submissionReason,
  submissionSteps,
} from "@/models/submission/submission-progress";
import { TUF_WEB_URL } from "@/shared/config/tuf-web-url";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";

export function SubmissionProgressDialog({
  open,
  run,
  runIndex,
  ready,
  pending,
  queryFailed,
  disconnected,
  refreshing,
  error,
  onOpenChange,
  onSubmit,
  onRefresh,
  onCloseAutoFocus,
}: {
  open: boolean;
  run: SubmissionRun | undefined;
  runIndex: number;
  ready: boolean;
  pending: boolean;
  queryFailed: boolean;
  disconnected: boolean;
  refreshing: boolean;
  error: string;
  onOpenChange: (open: boolean) => void;
  onSubmit: () => void;
  onRefresh: () => void;
  onCloseAutoFocus: (event: Event) => void;
}) {
  const { t, i18n } = useTranslation("submission");
  const titleRef = useRef<HTMLHeadingElement>(null);
  const progress = submissionProgress(run);
  const reason = submissionReason(run?.reason);
  const loading = !run && !queryFailed && !disconnected;
  const canAct = ready && !pending && !queryFailed && !disconnected;
  const retry = ["validationError", "registrationError", "unavailable"].includes(progress.phase);
  const notice = disconnected
    ? t("progress.disconnected")
    : queryFailed
      ? t("progress.refreshFailed")
      : progress.processing || progress.phase === "submitted"
        ? ""
        : error;
  const expiresAt = run?.evidence_expires_at ? Date.parse(run.evidence_expires_at) : Number.NaN;
  const description = loading
    ? t("progress.loading")
    : reason && progress.phase !== "submitted" && !progress.processing
      ? t(`progress.reasons.${reason}`)
      : t(`progress.description.${progress.phase}`);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent
        className="flex max-h-[calc(100dvh-2rem)] min-h-[min(30rem,calc(100dvh-2rem))] w-[min(25rem,calc(100vw-2rem))] flex-col overflow-y-auto p-6"
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          titleRef.current?.focus();
        }}
        onCloseAutoFocus={onCloseAutoFocus}
      >
        <DialogHeader className="pr-7">
          <DialogTitle ref={titleRef} tabIndex={-1} className="outline-none">
            {t("progress.title")}
          </DialogTitle>
          <DialogDescription>{t("progress.run", { index: runIndex })}</DialogDescription>
        </DialogHeader>
        <DialogClose asChild>
          <Button
            variant="ghost"
            size="icon-sm"
            className="absolute right-4 top-4"
            aria-label={t("close")}
          >
            <HugeiconsIcon icon={Cancel01Icon} className="size-4" aria-hidden="true" />
          </Button>
        </DialogClose>

        <div role="status" aria-live="polite" aria-atomic="true" className="mt-6 min-h-24">
          <p className={cn("font-medium", progress.failed && "text-destructive")}>
            {loading ? t("progress.loading") : t(`progress.heading.${progress.phase}`)}
          </p>
          <p className="mt-2 text-sm leading-relaxed text-muted-foreground">{description}</p>
        </div>

        <ol className="my-5 space-y-3" aria-label={t("progress.stepsLabel")}>
          {submissionSteps.map((step, index) => {
            const done = index < progress.step || (progress.phase === "ready" && index === 2);
            const current = index === progress.step && !done;
            const active = current && progress.processing;
            const state = done
              ? "done"
              : current
                ? progress.failed
                  ? "failed"
                  : "current"
                : "waiting";
            return (
              <li
                key={step}
                aria-current={current ? "step" : undefined}
                className={cn(
                  "flex items-center gap-3 text-sm",
                  !done && !current && "text-muted-foreground",
                )}
              >
                <span
                  aria-hidden="true"
                  className={cn(
                    "grid size-6 shrink-0 place-items-center rounded-full border border-border text-xs tabular-nums",
                    done && "border-primary/25 bg-primary/10 text-primary",
                    current && "border-primary/40 text-primary",
                    current && progress.failed && "border-destructive/40 text-destructive",
                  )}
                >
                  {done || active || (current && progress.failed) ? (
                    <HugeiconsIcon
                      icon={done ? Tick02Icon : active ? Loading03Icon : Cancel01Icon}
                      className={cn("size-4", active && "animate-spin motion-reduce:animate-none")}
                    />
                  ) : (
                    index + 1
                  )}
                </span>
                {t(`progress.steps.${step}`)}
                <span className="sr-only">{t(`progress.states.${state}`)}</span>
              </li>
            );
          })}
        </ol>

        <div className="mt-auto space-y-4 pt-2">
          <p role="status" className="min-h-10 text-xs leading-relaxed text-muted-foreground">
            {notice ||
              (pending
                ? t("progress.requesting")
                : progress.processing
                  ? t("progress.canClose")
                  : ready && !canAct
                    ? t("progress.checkingPermission")
                    : (progress.phase === "ready" || retry) && !ready
                      ? t("progress.checkPermission")
                      : "")}
          </p>
          {Number.isFinite(expiresAt) && progress.phase !== "submitted" ? (
            <p className="text-xs text-muted-foreground">
              {t("evidenceExpires", {
                date: new Date(expiresAt).toLocaleString(i18n.resolvedLanguage),
              })}
            </p>
          ) : null}
          <div className="flex flex-wrap justify-end gap-2">
            <DialogClose asChild>
              <Button variant="outline" size="sm">
                {t("close")}
              </Button>
            </DialogClose>
            {queryFailed || (!run && !loading) ? (
              <Button
                size="sm"
                variant="outline"
                disabled={refreshing || disconnected}
                onClick={onRefresh}
              >
                {t("progress.refresh")}
              </Button>
            ) : null}
            {run?.external_pass_id != null ? (
              <Button size="sm" asChild>
                <a
                  href={`${TUF_WEB_URL}/passes/${run.external_pass_id}`}
                  target="_blank"
                  rel="noreferrer"
                >
                  {t("viewPass")}
                </a>
              </Button>
            ) : ready || progress.phase === "ready" || retry ? (
              <Button size="sm" disabled={!canAct} onClick={onSubmit}>
                {pending ? (
                  <HugeiconsIcon
                    icon={Loading03Icon}
                    className="size-4 animate-spin motion-reduce:animate-none"
                    aria-hidden="true"
                  />
                ) : null}
                {t(retry ? "progress.retry" : "progress.submit")}
              </Button>
            ) : null}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
