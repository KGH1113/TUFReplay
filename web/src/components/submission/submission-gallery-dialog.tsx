import { ArrowRight02Icon, Loading03Icon, Tick02Icon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { useEffect, useId, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { VisualKindTabs } from "@/components/submission/visual-kind-tabs";
import { VisualPresetCard } from "@/components/submission/visual-preset-card";
import { useVisualLibrary } from "@/hooks/visual/use-visual-library";
import type { SubmissionRun, VisualSelection } from "@/models/submission/submission-model";
import type { VisualKind, VisualPreset } from "@/models/visual/visual-model";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Tabs, TabsContent } from "@/shared/ui/tabs";

export function SubmissionGalleryDialog({
  open,
  run,
  locked,
  initialSelection,
  accountKey,
  pending,
  error,
  onOpenChange,
  onSubmit,
  onCloseAutoFocus,
}: {
  open: boolean;
  run: SubmissionRun | undefined;
  locked: boolean;
  initialSelection: VisualSelection | null;
  accountKey: string | null;
  pending: boolean;
  error: string;
  onOpenChange: (open: boolean) => void;
  onSubmit: (selection: VisualSelection | undefined) => void;
  onCloseAutoFocus?: (event: Event) => void;
}) {
  const { t } = useTranslation("submission");
  const titleRef = useRef<HTMLHeadingElement>(null);
  const [keyviewerId, setKeyviewerId] = useState<string | null>(null);
  const [overlayId, setOverlayId] = useState<string | null>(null);
  const [activeKind, setActiveKind] = useState<VisualKind>("keyviewer");
  const library = useVisualLibrary(open, false, accountKey);
  const presets = library.presets.data?.presets ?? [];
  const keyviewers = useMemo(
    () => presets.filter((preset) => preset.kind === "keyviewer"),
    [presets],
  );
  const overlays = useMemo(() => presets.filter((preset) => preset.kind === "overlay"), [presets]);

  useEffect(() => {
    if (!open) return;
    setKeyviewerId(initialSelection?.keyviewer_id ?? null);
    setOverlayId(initialSelection?.overlay_id ?? null);
    setActiveKind("keyviewer");
  }, [initialSelection, open]);

  const listError = library.presets.isError;
  const firstSelection = { keyviewer_id: keyviewerId, overlay_id: overlayId };
  const selectionCount = Number(keyviewerId !== null) + Number(overlayId !== null);
  const submitLabel = locked
    ? t("visual.galleryRetrySubmit")
    : selectionCount
      ? t("visual.gallerySubmit")
      : t("visual.gallerySubmitPlain");

  return (
    <Dialog open={open} onOpenChange={(nextOpen) => !pending && onOpenChange(nextOpen)}>
      <DialogContent
        className="flex max-h-[calc(100dvh-2rem)] w-[min(48rem,calc(100vw-2rem))] flex-col overflow-hidden rounded-2xl p-0"
        aria-busy={pending}
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          titleRef.current?.focus();
        }}
        onEscapeKeyDown={(event) => pending && event.preventDefault()}
        onPointerDownOutside={(event) => pending && event.preventDefault()}
        onCloseAutoFocus={onCloseAutoFocus}
      >
        <DialogHeader className="shrink-0 space-y-3 px-6 pb-6 pt-7 sm:px-8 sm:pt-8">
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
            {t("visual.galleryEyebrow")}
          </p>
          <DialogTitle
            ref={titleRef}
            tabIndex={-1}
            className="break-keep text-2xl font-semibold tracking-tight outline-none sm:text-[28px]"
          >
            {t("visual.galleryTitle")}
          </DialogTitle>
          <DialogDescription className="max-w-lg break-keep text-sm leading-relaxed">
            {locked ? t("visual.galleryFixed") : t("visual.galleryDescription")}
          </DialogDescription>
        </DialogHeader>

        <div className="flex min-h-0 flex-col px-6 pb-7 sm:px-8">
          {locked ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <SelectionSummary
                kind="keyviewer"
                id={run?.presentation?.keyviewer_id ?? keyviewerId}
                presets={keyviewers}
              />
              <SelectionSummary
                kind="overlay"
                id={run?.presentation?.overlay_id ?? overlayId}
                presets={overlays}
              />
            </div>
          ) : library.presets.isPending ? (
            <p
              role="status"
              className="flex min-h-40 items-center justify-center gap-3 text-sm text-muted-foreground"
            >
              <HugeiconsIcon
                aria-hidden="true"
                icon={Loading03Icon}
                className="size-4 animate-spin"
              />
              {t("visual.galleryLoading")}
            </p>
          ) : listError ? (
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-destructive/8 px-3 py-3 text-sm text-destructive ring-1 ring-destructive/20">
              <p role="alert">{t("visual.galleryListFailed")}</p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => void library.presets.refetch()}
              >
                {t("visual.galleryRetry")}
              </Button>
            </div>
          ) : (
            <Tabs
              value={activeKind}
              onValueChange={(value) => setActiveKind(value as VisualKind)}
              className="h-[24rem] min-h-0 flex-col gap-5"
            >
              <VisualKindTabs
                disabled={pending}
                selectedNames={{
                  keyviewer: keyviewers.find((preset) => preset.id === keyviewerId)?.name,
                  overlay: overlays.find((preset) => preset.id === overlayId)?.name,
                }}
              />
              {(["keyviewer", "overlay"] as const).map((kind) => (
                <TabsContent
                  key={kind}
                  value={kind}
                  className="-m-1 min-h-0 overflow-y-auto overscroll-contain p-1"
                >
                  <PresetPicker
                    kind={kind}
                    presets={kind === "keyviewer" ? keyviewers : overlays}
                    selectedId={kind === "keyviewer" ? keyviewerId : overlayId}
                    onSelect={kind === "keyviewer" ? setKeyviewerId : setOverlayId}
                    disabled={pending}
                  />
                </TabsContent>
              ))}
            </Tabs>
          )}

          {error ? (
            <p
              role="alert"
              aria-live="polite"
              className="mt-5 rounded-xl bg-destructive/8 px-4 py-3 text-sm leading-relaxed text-destructive"
            >
              {error}
            </p>
          ) : null}
        </div>
        <div className="flex shrink-0 flex-col gap-4 border-t border-border/70 bg-muted/15 px-6 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-8">
          <p role="status" className="text-xs text-muted-foreground">
            {locked
              ? t("visual.gallerySaved")
              : library.presets.isPending || listError
                ? t("visual.galleryOptional")
                : selectionCount
                  ? t("visual.gallerySelectionCount", { count: selectionCount })
                  : t("visual.galleryNoVisuals")}
          </p>
          <div className="flex flex-col-reverse items-stretch justify-end gap-2 min-[380px]:flex-row min-[380px]:items-center">
            <Button
              type="button"
              variant="ghost"
              className="h-11 rounded-xl px-4 text-muted-foreground"
              disabled={pending}
              onClick={() => onOpenChange(false)}
            >
              {t("close")}
            </Button>
            <Button
              type="button"
              className="h-11 shrink-0 gap-3 rounded-xl bg-primary px-5 font-semibold text-neutral-950 ring-0 hover:bg-primary/90 min-[380px]:flex-1 sm:flex-none"
              disabled={pending || (!locked && (library.presets.isPending || listError))}
              onClick={() => onSubmit(locked ? undefined : firstSelection)}
            >
              {pending ? t("visual.gallerySubmitting") : submitLabel}
              <HugeiconsIcon
                aria-hidden="true"
                icon={pending ? Loading03Icon : ArrowRight02Icon}
                className={cn("size-4", pending && "animate-spin")}
              />
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function PresetPicker({
  kind,
  presets,
  selectedId,
  onSelect,
  disabled,
}: {
  kind: VisualKind;
  presets: VisualPreset[];
  selectedId: string | null;
  onSelect: (id: string | null) => void;
  disabled: boolean;
}) {
  const { t } = useTranslation("submission");
  const groupId = useId();
  return (
    <fieldset disabled={disabled} aria-describedby={`${groupId}-hint`} className="min-w-0">
      <legend className="sr-only">{t(`visual.${kind}`)}</legend>
      <p id={`${groupId}-hint`} className="mb-1 text-sm leading-relaxed text-muted-foreground">
        {t(kind === "keyviewer" ? "visual.galleryKeyviewerHint" : "visual.galleryOverlayHint")}
      </p>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
        <p className="text-xs font-medium text-muted-foreground">
          {t("visual.galleryPresets", { count: presets.length })}
        </p>
        <NoPresetOption
          groupId={groupId}
          selected={selectedId === null}
          onClick={() => onSelect(null)}
        />
      </div>
      <div className="grid grid-cols-2 items-start gap-4 sm:grid-cols-3">
        {presets.map((preset) => (
          <VisualPresetCard
            key={preset.id}
            preset={preset}
            selection={{
              groupId,
              selected: preset.id === selectedId,
              onSelect: () => onSelect(preset.id),
              disabled,
            }}
          />
        ))}
        {presets.length === 0 ? (
          <p className="col-span-full rounded-xl border border-dashed border-border px-5 py-9 text-center text-sm leading-relaxed text-muted-foreground">
            {t("visual.galleryEmpty")}
          </p>
        ) : null}
      </div>
    </fieldset>
  );
}

function NoPresetOption({
  groupId,
  selected,
  onClick,
}: {
  groupId: string;
  selected: boolean;
  onClick: () => void;
}) {
  const { t } = useTranslation("submission");
  return (
    <label className="relative flex min-h-11 cursor-pointer items-center gap-3 rounded-lg px-2 text-left has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring has-[:disabled]:cursor-wait has-[:disabled]:opacity-60">
      <input
        type="radio"
        name={groupId}
        value=""
        checked={selected}
        onChange={onClick}
        className="sr-only"
      />
      <span className="text-sm font-medium">{t("visual.galleryNone")}</span>
      <span
        aria-hidden="true"
        className={cn(
          "grid size-5 shrink-0 place-items-center rounded-full border",
          selected ? "border-primary bg-primary text-neutral-950" : "border-foreground/25",
        )}
      >
        {selected ? (
          <HugeiconsIcon icon={Tick02Icon} className="size-3.5" strokeWidth={2.5} />
        ) : null}
      </span>
    </label>
  );
}

function SelectionSummary({
  kind,
  id,
  presets,
}: {
  kind: VisualKind;
  id: string | null;
  presets: VisualPreset[];
}) {
  const { t } = useTranslation("submission");
  const preset = id ? presets.find((candidate) => candidate.id === id) : null;
  return (
    <div className="rounded-xl bg-muted/25 px-3 py-3 ring-1 ring-foreground/8">
      <p className="text-xs font-semibold uppercase tracking-[0.12em] text-muted-foreground">
        {t(`visual.${kind}`)}
      </p>
      <p className="mt-1 truncate text-sm font-medium">
        {preset?.name ?? t(id ? "visual.gallerySavedPreset" : "visual.galleryNone")}
      </p>
    </div>
  );
}
