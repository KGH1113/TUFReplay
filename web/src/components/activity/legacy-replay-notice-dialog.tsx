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
        className="min-h-0 w-[min(28rem,calc(100vw-2rem))] space-y-6 p-6"
        onEscapeKeyDown={(event) => event.preventDefault()}
        onPointerDownOutside={(event) => event.preventDefault()}
        onInteractOutside={(event) => event.preventDefault()}
      >
        <DialogHeader>
          <DialogTitle>{t("legacyReplayNotice.title")}</DialogTitle>
          <DialogDescription className="leading-relaxed">
            {t("legacyReplayNotice.description")}
          </DialogDescription>
        </DialogHeader>
        <div className="flex justify-end">
          <Button onClick={onConfirm}>{t("legacyReplayNotice.confirm")}</Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
