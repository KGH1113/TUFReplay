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
        className="min-h-0 w-[min(28rem,calc(100vw-2rem))] p-6"
        onEscapeKeyDown={(event) => event.preventDefault()}
        onPointerDownOutside={(event) => event.preventDefault()}
        onInteractOutside={(event) => event.preventDefault()}
      >
        <DialogHeader className="space-y-3">
          <div className="grid size-11 place-items-center rounded-2xl bg-primary/12 text-primary">
            <HugeiconsIcon aria-hidden="true" icon={ReplayIcon} size={22} strokeWidth={2} />
          </div>
          <div>
            <p className="text-xs font-semibold text-primary">{t("legacyReplayNotice.label")}</p>
            <DialogTitle className="mt-1 text-lg leading-snug tracking-tight">
              {t("legacyReplayNotice.title")}
            </DialogTitle>
          </div>
          <DialogDescription className="leading-relaxed">
            {t("legacyReplayNotice.description")}
          </DialogDescription>
        </DialogHeader>
        <div className="mt-6 flex justify-end">
          <Button className="min-w-24" onClick={onConfirm}>
            {t("legacyReplayNotice.confirm")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
