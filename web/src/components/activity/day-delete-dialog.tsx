import { Delete02Icon, Loading03Icon, SquareLock02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { type ReactNode, useEffect, useId, useRef, useState } from "react";
import { Trans, useTranslation } from "react-i18next";
import { dayStampParts } from "@/models/activity/activity-date";
import type { ActivityDay } from "@/models/activity/activity-model";
import { summarizeDayDeletion } from "@/models/activity/day-deletion";
import { localizedErrorMessage } from "@/models/activity/localized-error";
import { cn } from "@/shared/lib/cn";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogTitle,
} from "@/shared/ui/alert-dialog";
import { Button } from "@/shared/ui/button";
import { Switch } from "@/shared/ui/switch";

export interface DayDeleteOutcome {
  deletedRunCount: number;
  keptLockedRunCount: number;
  keptInUseRunCount: number;
}

// Long enough that a normal delete finishes first; past it the player is likely mid-attempt.
const SLOW_DELETE_HINT_MS = 2_500;

/** Confirms a day delete with the two numbers that matter: what goes and what stays. */
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
  const shortDate = dayStampParts(day.date, locale).shortLabel;
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
    <AlertDialog open={open} onOpenChange={(next) => !deleting && onOpenChange(next)}>
      <AlertDialogContent
        className="max-w-[26rem] gap-0 overflow-hidden p-0"
        onEscapeKeyDown={(event) => deleting && event.preventDefault()}
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
        <div className="px-6 pt-6">
          <AlertDialogTitle className="text-xl">
            {t("dayDelete.heading", { date: shortDate })}
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            <ul className="mt-4 grid gap-3 text-sm leading-relaxed text-foreground">
              <Fact icon={Delete02Icon} tone="destructive">
                <Trans
                  t={t}
                  i18nKey="dayDelete.deletesFact"
                  count={count}
                  components={{ strong: <strong className="font-semibold" /> }}
                />
                {summary.hiddenRunCount > 0 ? (
                  <span className="mt-0.5 block text-xs text-muted-foreground">
                    {t("dayDelete.hidden", { count: summary.hiddenRunCount })}
                  </span>
                ) : null}
              </Fact>
              {summary.keepRunCount > 0 ? (
                <Fact icon={SquareLock02Icon}>
                  <Trans
                    t={t}
                    i18nKey="dayDelete.keepsFact"
                    count={summary.keepRunCount}
                    components={{ strong: <strong className="font-semibold" /> }}
                  />
                </Fact>
              ) : null}
            </ul>
          </AlertDialogDescription>
        </div>

        {summary.unlockedClearRunCount > 0 ? (
          <label
            htmlFor={keepClearsId}
            className="mx-6 mt-4 flex cursor-pointer items-center gap-3 rounded-xl border border-border px-3.5 py-3"
          >
            <span className="min-w-0 flex-1">
              <span className="block text-sm font-medium">
                {t("dayDelete.keepClears", { count: summary.unlockedClearRunCount })}
              </span>
              <span
                className={cn(
                  "mt-0.5 block text-xs leading-relaxed",
                  keepClears ? "text-muted-foreground" : "text-warning",
                )}
              >
                {keepClears
                  ? t("dayDelete.keepClearsOn")
                  : t("dayDelete.keepClearsOff", { count: summary.unlockedClearRunCount })}
              </span>
            </span>
            <Switch
              id={keepClearsId}
              checked={keepClears}
              disabled={deleting}
              onCheckedChange={setKeepClears}
            />
          </label>
        ) : null}

        <p className="mx-6 mt-4 text-xs text-muted-foreground">{t("dayDelete.irreversible")}</p>
        {slow && !error ? (
          <p role="status" className="mx-6 mt-3 text-xs leading-relaxed text-muted-foreground">
            {t("dayDelete.slow")}
          </p>
        ) : null}
        {error ? (
          <p role="alert" className="mx-6 mt-3 text-sm leading-relaxed text-destructive">
            {error}
          </p>
        ) : null}

        <div className="mt-6 flex justify-end gap-2 border-t border-border px-6 py-4">
          <Button
            ref={cancelRef}
            type="button"
            variant="ghost"
            disabled={deleting}
            onClick={() => onOpenChange(false)}
          >
            {commonT("actions.cancel")}
          </Button>
          <Button
            type="button"
            variant="destructive"
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
      </AlertDialogContent>
    </AlertDialog>
  );
}

function Fact({
  icon,
  tone = "neutral",
  children,
}: {
  icon: typeof Delete02Icon;
  tone?: "destructive" | "neutral";
  children: ReactNode;
}) {
  return (
    <li className="grid grid-cols-[1.25rem_minmax(0,1fr)] gap-3">
      <HugeiconsIcon
        aria-hidden="true"
        icon={icon}
        size={18}
        strokeWidth={1.8}
        className={cn(
          "mt-px",
          tone === "destructive" ? "text-destructive" : "text-muted-foreground",
        )}
      />
      <span>{children}</span>
    </li>
  );
}
