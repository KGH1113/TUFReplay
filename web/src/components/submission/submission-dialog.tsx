import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useSubmissionSettings } from "@/hooks/submission/use-submission";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/shared/ui/dialog";
import { Switch } from "@/shared/ui/switch";

export function SubmissionDialog({ disabled = false }: { disabled?: boolean }) {
  const [open, setOpen] = useState(false);
  const { t } = useTranslation("submission");
  const { status, connect, disconnect, setDisabled } = useSubmissionSettings(!disabled);
  const connected = status.data?.connected === true;
  const error = connect.error ?? disconnect.error ?? setDisabled.error ?? status.error;
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline" disabled={disabled}>
          {t("title")}
        </Button>
      </DialogTrigger>
      <DialogContent className="w-[min(30rem,calc(100vw-2rem))] space-y-6 p-6">
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
          <DialogDescription className="leading-relaxed">{t("description")}</DialogDescription>
        </DialogHeader>
        {!connected ? (
          <div className="space-y-4 rounded-xl bg-muted/25 p-4 ring-1 ring-foreground/8">
            <p className="text-sm leading-relaxed text-muted-foreground">
              {t(status.data?.configured === false ? "notConfigured" : "connectDescription")}
            </p>
            <div className="flex flex-wrap gap-3">
              <Button
                onClick={() => connect.mutate()}
                disabled={connect.isPending || status.data?.configured !== true}
              >
                {connect.isPending ? t("connecting") : t("connect")}
              </Button>
              <Button variant="outline" asChild>
                <a href="https://tuforums.com" target="_blank" rel="noreferrer">
                  {t("openTuf")}
                </a>
              </Button>
            </div>
          </div>
        ) : (
          <div className="overflow-hidden rounded-xl bg-muted/25 ring-1 ring-foreground/8">
            <div className="flex min-h-16 items-center justify-between gap-4 px-4 py-3">
              <div className="min-w-0">
                <p className="text-sm font-medium">{t("account")}</p>
                <p className="mt-0.5 text-xs text-muted-foreground">{t("connected")}</p>
              </div>
              <Button
                variant="ghost"
                size="sm"
                disabled={disconnect.isPending}
                onClick={() => disconnect.mutate()}
              >
                {t("disconnect")}
              </Button>
            </div>
            <div className="mx-4 h-px bg-foreground/8" />
            <div className="flex min-h-20 items-center justify-between gap-5 px-4 py-3">
              <label className="min-w-0 cursor-pointer" htmlFor="auto-submission-capture">
                <span className="block text-sm font-medium">{t("capture")}</span>
                <span className="mt-1 block text-xs leading-relaxed text-muted-foreground">
                  {t("captureDescription")}
                </span>
              </label>
              <Switch
                id="auto-submission-capture"
                checked={!status.data?.disabled}
                disabled={setDisabled.isPending}
                onCheckedChange={(checked) => setDisabled.mutate(!checked)}
                aria-label={t("capture")}
              />
            </div>
          </div>
        )}
        {error && (
          <p role="alert" className="text-sm text-destructive">
            {t(error.message === "tuf_login_required" ? "loginRequired" : "requestFailed")}
          </p>
        )}
        <div className="flex justify-end">
          <DialogClose asChild>
            <Button variant="outline">{t("close")}</Button>
          </DialogClose>
        </div>
      </DialogContent>
    </Dialog>
  );
}
