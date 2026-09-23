import { useState } from "react";
import { useTranslation } from "react-i18next";
import { VisualPresetLibrary } from "@/components/submission/visual-preset-library";
import { useSubmissionSettings } from "@/hooks/submission/use-submission";
import { submissionAccountKey } from "@/models/submission/submission-model";
import { TUF_WEB_URL } from "@/shared/config/tuf-web-url";
import { Badge } from "@/shared/ui/badge";
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/shared/ui/tabs";

export function SubmissionDialog({ disabled = false }: { disabled?: boolean }) {
  const [open, setOpen] = useState(() => window.location.pathname === "/oauth/callback");
  const { t } = useTranslation("submission");
  const { status, connect, disconnect, setDisabled } = useSubmissionSettings(!disabled);
  const connected = status.data?.connected === true;
  const username = status.data?.username?.trim();
  const nickname = status.data?.nickname?.trim();
  const accountName =
    nickname && username ? `${nickname} (@${username})` : nickname || username || t("connected");
  const eligibilityKey = status.isError
    ? status.data
      ? "eligibility.stale"
      : "eligibility.unavailable"
    : !status.data || status.data.accountStatus === "checking"
      ? "eligibility.checking"
      : status.data.accountStatus === "stale"
        ? "eligibility.stale"
        : status.data.accountStatus === "unavailable"
          ? "eligibility.unavailable"
          : status.data.canSubmit
            ? "eligibility.allowed"
            : status.data.denialReason === "auto_submission_disabled"
              ? "eligibility.disabledByServer"
              : status.data.denialReason === "auto_submission_tester_required"
                ? "eligibility.testerRequired"
                : "eligibility.notAuthorized";
  const error =
    connect.error ??
    disconnect.error ??
    setDisabled.error ??
    (connected ? undefined : status.error);
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" variant="outline" disabled={disabled}>
          {t("title")}
        </Button>
      </DialogTrigger>
      <DialogContent className="flex h-[min(40rem,calc(100dvh-2rem))] w-[min(44rem,calc(100vw-2rem))] flex-col gap-6 overflow-hidden p-5 sm:p-7">
        <DialogHeader>
          <DialogTitle>{t("title")}</DialogTitle>
          <DialogDescription className="leading-relaxed">{t("description")}</DialogDescription>
        </DialogHeader>
        <Tabs defaultValue="general" className="min-h-0 flex-1 gap-6">
          <TabsList
            variant="line"
            aria-label={t("title")}
            className="w-full shrink-0 justify-start border-b border-border p-0"
          >
            <TabsTrigger value="general" className="flex-none px-4 after:bottom-0">
              {t("settingsTabs.general")}
            </TabsTrigger>
            <TabsTrigger value="visual" className="flex-none px-4 after:bottom-0">
              {t("settingsTabs.visual")}
            </TabsTrigger>
          </TabsList>
          <TabsContent value="general" className="min-h-0 overflow-y-auto">
            {!connected ? (
              <div className="space-y-4 py-2">
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
                    <a href={TUF_WEB_URL} target="_blank" rel="noreferrer">
                      {t("openTuf")}
                    </a>
                  </Button>
                </div>
              </div>
            ) : (
              <div className="space-y-5">
                <div className="flex min-h-16 items-center justify-between gap-4 py-1">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-sm font-medium">{t("account")}</p>
                      <Badge variant="secondary">{t("accountConnected")}</Badge>
                    </div>
                    <p className="mt-2 break-words text-sm text-muted-foreground">{accountName}</p>
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
                <p
                  role="status"
                  aria-live="polite"
                  className="border-l-2 border-border pl-3 text-sm leading-relaxed text-muted-foreground"
                >
                  {t(eligibilityKey)}
                </p>
                <div className="h-px bg-border" />
                <div className="flex min-h-20 items-center justify-between gap-5 py-1">
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
          </TabsContent>
          <TabsContent value="visual" className="min-h-0 overflow-y-auto">
            {connected ? (
              <VisualPresetLibrary
                disabled={!open || disconnect.isPending}
                accountKey={submissionAccountKey(status.data)}
              />
            ) : (
              <p className="flex min-h-72 items-center justify-center text-center text-sm text-muted-foreground">
                {t("visual.connectHint")}
              </p>
            )}
          </TabsContent>
        </Tabs>
        {error && (
          <p role="alert" className="text-sm text-destructive">
            {t(error.message === "tuf_login_required" ? "loginRequired" : "requestFailed")}
          </p>
        )}
        <div className="flex shrink-0 justify-end">
          <DialogClose asChild>
            <Button variant="outline">{t("close")}</Button>
          </DialogClose>
        </div>
      </DialogContent>
    </Dialog>
  );
}
