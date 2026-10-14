import { useTranslation } from "react-i18next";
import type { useRenderComingSoon } from "@/hooks/render/use-render-coming-soon";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";

export function RenderComingSoonDialog({
  notice,
}: {
  notice: ReturnType<typeof useRenderComingSoon>;
}) {
  const { t } = useTranslation("render");

  return (
    <Dialog open={notice.open} onOpenChange={notice.setOpen}>
      <DialogContent
        className="min-h-0 space-y-6"
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          notice.restoreFocus();
        }}
      >
        <DialogHeader>
          <DialogTitle>{t("comingSoon.title")}</DialogTitle>
          <DialogDescription className="leading-relaxed">
            {t("comingSoon.description")}
          </DialogDescription>
        </DialogHeader>
        <div className="flex justify-end">
          <Button type="button" onClick={() => notice.setOpen(false)}>
            {t("close")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
