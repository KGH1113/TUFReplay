import { Delete02Icon, Loading03Icon, SquareLock02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { type ReactNode, useEffect, useId, useRef, useState } from "react";
import { Trans, useTranslation } from "react-i18next";
import { dayStampParts } from "@/models/activity/activity-date";
import type { ActivityDay } from "@/models/activity/activity-model";
import { summarizeDayDeletion } from "@/models/activity/day-deletion";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import { Checkbox } from "@/shared/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/shared/ui/dialog";

export interface DayDeleteOutcome {
  deletedRunCount: number;
  keptLockedRunCount: number;
  keptInUseRunCount: number;
}

// Long enough that a normal delete finishes first; past it the player is likely mid-attempt.
const SLOW_DELETE_HINT_MS = 2_500;

export function DayDeleteDialog({
  day,
  open,
  onOpenChange,
  onDelete,
  onDeleted,
  restoreFocus,
}: {
  day: ActivityDay | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onDelete: (day: ActivityDay, keepClears: boolean) => Promise<DayDeleteOutcome>;
  onDeleted: (day: ActivityDay, outcome: DayDeleteOutcome) => void;
  restoreFocus: () => void;
}) {
  const { t, i18n } = useTranslation("activity");
  const { t: commonT } = useTranslation("common");
  const locale = i18n.resolvedLanguage ?? "en";
  const [keepClears, setKeepClears] = useState(true);
  const [deleting, setDeleting] = useState(false);
  const [slow, setSlow] = useState(false);
  const [error, setError] = useState("");
  const keepClearsId = useId();
  const cancelRef = useRef<HTMLButtonElement>(null);

  useEffect(() => {
    if (!open) return;
    setKeepClears(true);
    setError("");
  }, [open]);
  useEffect(() => {
    if (!deleting) {
      setSlow(false);
      return;
    }
    const timer = setTimeout(() => setSlow(true), SLOW_DELETE_HINT_MS);
    return () => clearTimeout(timer);
  }, [deleting]);

  if (!day) return null;
  const summary = summarizeDayDeletion(day, keepClears);
  const label = dayStampParts(day.date, locale).fullLabel;
  const count = summary.deleteRunCount;

  const confirm = async () => {
    if (deleting || count === 0) return;
    setDeleting(true);
    setError("");
    try {
      const outcome = await onDelete(day, keepClears);
      onDeleted(day, outcome);
      onOpenChange(false);
    } catch (cause) {
      const code = (cause as { code?: unknown } | null)?.code;
      setError(
        code === "ipc_timeout"
          ? t("dayDelete.timeout")
          : localizedErrorMessage(cause, t("dayDelete.failed")),
      );
    } finally {
      setDeleting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={(next) => !deleting && onOpenChange(next)}>
      <DialogContent
        role="alertdialog"
        className="min-h-0 w-[min(27rem,calc(100vw-2rem))]"
        onEscapeKeyDown={(event) => deleting && event.preventDefault()}
        onPointerDownOutside={(event) => deleting && event.preventDefault()}
        onOpenAutoFocus={(event) => {
          // Destructive dialogs focus the safe choice first.
          event.preventDefault();
          cancelRef.current?.focus();
        }}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          restoreFocus();
        }}
      >
        <DialogTitle className="text-balance leading-snug">
          {t("dayDelete.title", { count, date: label })}
        </DialogTitle>
        <DialogDescription asChild>
          <ul className="mt-3 grid gap-2 text-sm leading-relaxed text-muted-foreground">
            <Fact icon={Delete02Icon} tone="destructive">
              <Trans
                t={t}
                i18nKey="dayDelete.deletes"
                count={count}
                components={{ strong: <strong className="font-semibold text-foreground" /> }}
              />
            </Fact>
            {summary.keepRunCount > 0 ? (
              <Fact icon={SquareLock02Icon} tone="foreground">
                <Trans
                  t={t}
                  i18nKey="dayDelete.keeps"
                  count={summary.keepRunCount}
                  components={{ strong: <strong className="font-semibold text-foreground" /> }}
                />
              </Fact>
            ) : null}
            {summary.hiddenRunCount > 0 ? (
              <li className="pl-7 text-xs">
                {t("dayDelete.hidden", { count: summary.hiddenRunCount })}
              </li>
            ) : null}
          </ul>
        </DialogDescription>
        {summary.unlockedClearRunCount > 0 ? (
          <label
            htmlFor={keepClearsId}
            className={cn(
              "mt-4 grid cursor-pointer grid-cols-[auto_minmax(0,1fr)] items-start gap-3 rounded-xl border px-3 py-2.5 text-sm transition-colors",
              keepClears ? "border-primary/25 bg-primary/8" : "border-amber-500/30 bg-amber-500/8",
            )}
          >
            <Checkbox
              id={keepClearsId}
              checked={keepClears}
              disabled={deleting}
              onCheckedChange={(checked) => setKeepClears(checked === true)}
              className="mt-0.5"
            />
            <span className="grid gap-0.5">
              <span className="font-medium">
                {t("dayDelete.keepClears", { count: summary.unlockedClearRunCount })}
              </span>
              <span
                className={cn("text-xs", keepClears ? "text-muted-foreground" : "text-amber-500")}
              >
                {keepClears
                  ? t("dayDelete.keepClearsOn")
                  : t("dayDelete.keepClearsOff", { count: summary.unlockedClearRunCount })}
              </span>
            </span>
          </label>
        ) : null}
        <p className="mt-4 text-xs text-muted-foreground">{t("dayDelete.irreversible")}</p>
        {slow && !error ? (
          <p role="status" className="mt-2 text-xs text-muted-foreground">
            {t("dayDelete.slow")}
          </p>
        ) : null}
        {error ? (
          <p role="alert" className="mt-3 text-sm text-destructive">
            {error}
          </p>
        ) : null}
        <div className="mt-6 flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            ref={cancelRef}
            disabled={deleting}
            onClick={() => onOpenChange(false)}
          >
            {commonT("actions.cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            disabled={deleting || count === 0}
            onClick={() => void confirm()}
          >
            {deleting ? (
              <HugeiconsIcon
                aria-hidden="true"
                icon={Loading03Icon}
                className="size-4 animate-spin motion-reduce:animate-none"
              />
            ) : null}
            {count === 0
              ? t("dayDelete.nothing")
              : deleting
                ? t("dayDelete.deleting", { count })
                : t("dayDelete.confirm", { count })}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function Fact({
  icon,
  tone,
  children,
}: {
  icon: typeof Delete02Icon;
  tone: "destructive" | "foreground";
  children: ReactNode;
}) {
  return (
    <li className="grid grid-cols-[1rem_minmax(0,1fr)] gap-3">
      <HugeiconsIcon
        aria-hidden="true"
        icon={icon}
        className={cn(
          "mt-0.5 size-4",
          tone === "destructive" ? "text-destructive" : "text-foreground",
        )}
      />
      <span>{children}</span>
    </li>
  );
}
