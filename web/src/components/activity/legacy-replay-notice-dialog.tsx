import { ReplayIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useTranslation } from "react-i18next";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";

export function LegacyReplayNoticeDialog({
  open,
  onConfirm,
}: {
  open: boolean;
  onConfirm: () => void;
}) {
  const { t } = useTranslation("activity");

  return (
    <Dialog open={open} onOpenChange={() => undefined}>
      <DialogContent
        className="min-h-0 w-[min(29rem,calc(100vw-2rem))] overflow-hidden border-foreground/15 bg-popover/95 p-0 shadow-2xl backdrop-blur-2xl"
        onEscapeKeyDown={(event) => event.preventDefault()}
        onPointerDownOutside={(event) => event.preventDefault()}
        onInteractOutside={(event) => event.preventDefault()}
      >
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-x-0 top-0 h-28 bg-gradient-to-b from-primary/10 to-transparent"
        />
        <div className="relative space-y-5 p-6 pb-5">
          <DialogHeader className="space-y-4">
            <div className="flex items-center gap-3">
              <div className="grid size-11 shrink-0 place-items-center rounded-2xl bg-primary/12 text-primary ring-1 ring-primary/20 shadow-sm">
                <HugeiconsIcon aria-hidden="true" icon={ReplayIcon} size={22} strokeWidth={2} />
              </div>
              <div className="min-w-0">
                <p className="mb-1 text-xs font-semibold tracking-wide text-primary">
                  {t("legacyReplayNotice.label")}
                </p>
                <DialogTitle className="text-xl leading-tight tracking-tight">
                  {t("legacyReplayNotice.title")}
                </DialogTitle>
              </div>
            </div>
            <DialogDescription className="rounded-xl border border-foreground/8 bg-background/35 p-4 leading-6 text-foreground/70 shadow-inner">
              {t("legacyReplayNotice.description")}
            </DialogDescription>
          </DialogHeader>
        </div>
        <div className="relative flex justify-end border-t border-foreground/8 bg-muted/20 px-6 py-4">
          <Button
            size="lg"
            className="min-w-24 bg-primary text-primary-foreground shadow-sm ring-1 ring-primary/30 hover:bg-primary/90"
            onClick={onConfirm}
          >
            {t("legacyReplayNotice.confirm")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
