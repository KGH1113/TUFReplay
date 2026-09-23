import { Delete02Icon, Loading03Icon, PlusSignIcon } from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import type { TFunction } from "i18next";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VisualPresetImport } from "@/api/visual/visual-api";
import { DmnotePlacementEditor } from "@/components/submission/dmnote-placement-editor";
import { VisualKindTabs } from "@/components/submission/visual-kind-tabs";
import { VisualPresetCard } from "@/components/submission/visual-preset-card";
import { VisualRegistrationProgressView } from "@/components/submission/visual-registration-progress";
import { useDmnoteRegistration } from "@/hooks/visual/use-dmnote-registration";
import { useVisualLibrary } from "@/hooks/visual/use-visual-library";
import type {
  VisualInspection,
  VisualKind,
  VisualPreset,
  VisualSource,
  VisualSourceInfo,
} from "@/models/visual/visual-model";
import {
  visualErrorCode,
  visualNameError,
  visualSourceSupportsKind,
  visualSourceUsesPresetFile,
} from "@/models/visual/visual-model";
import type {
  VisualRegistrationProgress,
  VisualRegistrationReporter,
  VisualRegistrationResult,
} from "@/models/visual/visual-registration-model";
import { readVisualUploads } from "@/models/visual/visual-upload-model";
import { cn } from "@/shared/lib/cn";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";
import { Switch } from "@/shared/ui/switch";
import { Tabs, TabsContent } from "@/shared/ui/tabs";

export function VisualPresetLibrary({
  disabled = false,
  accountKey,
}: {
  disabled?: boolean;
  accountKey: string | null;
}) {
  const { t } = useTranslation("submission");
  const [activeKind, setActiveKind] = useState<VisualKind>("keyviewer");
  const [registrationKind, setRegistrationKind] = useState<VisualKind | null>(null);
  const [presetToDelete, setPresetToDelete] = useState<VisualPreset | null>(null);
  const library = useVisualLibrary(!disabled, registrationKind !== null, accountKey);
  const presets = library.presets.data?.presets ?? [];

  useEffect(() => {
    if (!disabled || library.importPreset.isPending || library.removePreset.isPending) return;
    setRegistrationKind(null);
    setPresetToDelete(null);
  }, [disabled, library.importPreset.isPending, library.removePreset.isPending]);

  const closeRegistration = () => {
    if (!library.importPreset.isPending) {
      library.importPreset.reset();
      setRegistrationKind(null);
    }
  };
  const closeDelete = () => {
    if (!library.removePreset.isPending) setPresetToDelete(null);
  };

  const deletePreset = async () => {
    if (!presetToDelete) return;
    try {
      await library.removePreset.mutateAsync(presetToDelete.id);
      setPresetToDelete(null);
    } catch {
      // The dialog renders the mutation error and keeps the destructive action available.
    }
  };

  return (
    <section aria-label={t("visual.libraryTitle")} className="space-y-5">
      <Tabs
        value={activeKind}
        onValueChange={(value) => setActiveKind(value as VisualKind)}
        className="gap-5"
      >
        <div className="flex flex-wrap items-center justify-between gap-3">
          <VisualKindTabs />
          <Button
            type="button"
            size="sm"
            disabled={disabled}
            onClick={() => setRegistrationKind(activeKind)}
          >
            <HugeiconsIcon aria-hidden="true" icon={PlusSignIcon} className="size-4" />
            {t(activeKind === "keyviewer" ? "visual.addKeyviewer" : "visual.addOverlay")}
          </Button>
        </div>
        <p className="text-sm leading-relaxed text-muted-foreground">
          {t("visual.libraryDescription")}
        </p>
        {library.presets.isPending ? (
          <p role="status" className="flex items-center gap-2 text-xs text-muted-foreground">
            <HugeiconsIcon
              aria-hidden="true"
              icon={Loading03Icon}
              className="size-4 animate-spin"
            />
            {t("visual.loading")}
          </p>
        ) : library.presets.isError ? (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-destructive/8 px-3 py-3 text-xs text-destructive ring-1 ring-destructive/20">
            <p role="alert">{visualErrorText(library.presets.error, t("visual.error"), t)}</p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => void library.presets.refetch()}
            >
              {t("visual.retryList")}
            </Button>
          </div>
        ) : (
          (["keyviewer", "overlay"] as const).map((kind) => (
            <TabsContent key={kind} value={kind}>
              <PresetKindSection
                kind={kind}
                presets={presets.filter((preset) => preset.kind === kind)}
                onDelete={setPresetToDelete}
              />
            </TabsContent>
          ))
        )}
      </Tabs>

      <VisualPresetRegistrationDialog
        kind={registrationKind}
        open={registrationKind !== null}
        sources={library.sources.data?.sources ?? []}
        sourcesPending={library.sources.isPending}
        sourcesError={library.sources.error}
        importPending={library.importPreset.isPending}
        importError={library.importPreset.error}
        onRetrySources={() => void library.sources.refetch()}
        onImport={async (input, onProgress) => {
          const result = await library.importPreset.mutateAsync({ input, onProgress });
          if (result.state === "completed") setRegistrationKind(null);
          return result;
        }}
        onOpenChange={(open) => !open && closeRegistration()}
      />
      <VisualPresetDeleteDialog
        preset={presetToDelete}
        pending={library.removePreset.isPending}
        error={library.removePreset.error}
        onOpenChange={(open) => !open && closeDelete()}
        onConfirm={() => void deletePreset()}
      />
    </section>
  );
}

function PresetKindSection({
  kind,
  presets,
  onDelete,
}: {
  kind: VisualKind;
  presets: VisualPreset[];
  onDelete: (preset: VisualPreset) => void;
}) {
  const { t } = useTranslation("submission");
  return (
    <div className="min-h-64">
      {presets.length === 0 ? (
        <div className="flex min-h-64 flex-col items-center justify-center gap-2 px-4 text-center">
          <p className="text-sm font-medium">
            {t("visual.empty", { kind: t(`visual.kind.${kind}`) })}
          </p>
          <p className="max-w-xs text-xs leading-relaxed text-muted-foreground">
            {t("visual.emptyHint")}
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          {presets.map((preset) => (
            <VisualPresetCard
              key={preset.id}
              preset={preset}
              action={
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  className="shrink-0 text-muted-foreground hover:text-destructive"
                  aria-label={t("visual.deleteTitle", { name: preset.name })}
                  onClick={() => onDelete(preset)}
                >
                  <HugeiconsIcon aria-hidden="true" icon={Delete02Icon} className="size-4" />
                </Button>
              }
            />
          ))}
        </div>
      )}
    </div>
  );
}

function VisualPresetRegistrationDialog({
  kind,
  open,
  sources,
  sourcesPending,
  sourcesError,
  importPending: mutationPending,
  importError,
  onRetrySources,
  onImport,
  onOpenChange,
}: {
  kind: VisualKind | null;
  open: boolean;
  sources: VisualSourceInfo[];
  sourcesPending: boolean;
  sourcesError: unknown;
  importPending: boolean;
  importError: unknown;
  onRetrySources: () => void;
  onImport: (
    input: VisualPresetImport,
    onProgress: VisualRegistrationReporter,
  ) => Promise<VisualRegistrationResult>;
  onOpenChange: (open: boolean) => void;
}) {
  const { t } = useTranslation("submission");
  const [name, setName] = useState("");
  const [source, setSource] = useState<VisualSource | null>(null);
  const [file, setFile] = useState<File | null>(null);
  const [cssFile, setCssFile] = useState<File | null>(null);
  const dmnote = useDmnoteRegistration(file, cssFile);
  const [localError, setLocalError] = useState<string | null>(null);
  const [checking, setChecking] = useState(false);
  const [progress, setProgress] = useState<VisualRegistrationProgress | null>(null);
  const [requirements, setRequirements] = useState<VisualInspection["missing_assets"]>([]);
  const [assetFiles, setAssetFiles] = useState<Record<string, File>>({});
  const importPending = mutationPending || checking;

  useEffect(() => {
    if (!open) return;
    setName("");
    setSource(null);
    setFile(null);
    setCssFile(null);
    setLocalError(null);
    setRequirements([]);
    setAssetFiles({});
    setProgress(null);
  }, [open]);

  const availableSources = useMemo(
    () => (kind ? sources.filter((candidate) => candidate.kinds.includes(kind)) : []),
    [kind, sources],
  );
  const selectedSource = source ? sources.find((candidate) => candidate.source === source) : null;
  useEffect(() => {
    if (!open || source || sourcesPending || sourcesError) return;
    const usable = availableSources.filter((candidate) => candidate.available);
    if (usable.length === 1) setSource(usable[0].source);
  }, [open, source, sourcesPending, sourcesError, availableSources]);

  const submit = async () => {
    if (!kind || importPending) return;
    const nameError = visualNameError(name);
    if (nameError) {
      setLocalError(nameError);
      return;
    }
    if (!source || !selectedSource || !visualSourceSupportsKind(selectedSource, kind)) {
      setLocalError("visual_source_required");
      return;
    }
    if (visualSourceUsesPresetFile(source) && !file) {
      setLocalError("visual_dmnote_file_required");
      return;
    }
    setLocalError(null);
    setChecking(true);
    setProgress({ stage: "preparing", completed_assets: 0 });
    setRequirements([]);
    try {
      if (file && file.size > 64 * 1024 * 1024) throw new Error("visual_payload_too_large");
      const presetJson =
        visualSourceUsesPresetFile(source) && file ? await dmnote.prepare() : undefined;
      const assets = await readVisualUploads(assetFiles, presetJson);
      const input: VisualPresetImport = {
        name: name.trim(),
        kind,
        source,
        presetJson,
        assets,
      };
      const result = await onImport(input, setProgress);
      if (result.state === "needs_assets") setRequirements(result.missing_assets);
    } catch (cause) {
      const code = visualErrorCode(cause) ?? (cause instanceof Error ? cause.message : null);
      setLocalError(code && code in visualErrorKeys ? code : "visual_request_failed");
    } finally {
      setChecking(false);
    }
  };

  const error =
    localError ??
    (visualSourceUsesPresetFile(source) ? dmnote.error : null) ??
    (importError ? (visualErrorCode(importError) ?? "visual_request_failed") : null);
  const dialogKind = kind ? t(`visual.kind.${kind}`) : "";
  return (
    <Dialog open={open} onOpenChange={(nextOpen) => !importPending && onOpenChange(nextOpen)}>
      <DialogContent
        className="flex h-[min(44rem,calc(100dvh-2rem))] w-[min(40rem,calc(100vw-2rem))] flex-col gap-6 overflow-hidden p-5 sm:p-7"
        onEscapeKeyDown={(event) => importPending && event.preventDefault()}
        onPointerDownOutside={(event) => importPending && event.preventDefault()}
      >
        <DialogHeader>
          <DialogTitle>{t("visual.registerTitle", { kind: dialogKind })}</DialogTitle>
          <DialogDescription className="leading-relaxed">
            {t("visual.registerDescription")}
          </DialogDescription>
        </DialogHeader>
        <form
          id="visual-registration-form"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
          className="min-h-0 flex-1 space-y-6 overflow-y-auto px-1 -mx-1"
          aria-busy={importPending}
        >
          <fieldset className="space-y-2">
            <legend className="text-sm font-medium">{t("visual.sourceStep")}</legend>
            <p className="text-xs text-muted-foreground">{t("visual.sourceHint")}</p>
            {sourcesPending ? (
              <p role="status" className="flex items-center gap-2 text-xs text-muted-foreground">
                <HugeiconsIcon
                  aria-hidden="true"
                  icon={Loading03Icon}
                  className="size-4 animate-spin"
                />
                {t("visual.loadingSources")}
              </p>
            ) : sourcesError ? (
              <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl bg-destructive/8 px-3 py-3 text-xs text-destructive ring-1 ring-destructive/20">
                <p role="alert">{visualErrorText(sourcesError, t("visual.error"), t)}</p>
                <Button type="button" variant="outline" size="sm" onClick={onRetrySources}>
                  {t("visual.retrySources")}
                </Button>
              </div>
            ) : availableSources.length === 0 ? (
              <p role="alert" className="text-xs text-destructive">
                {t("visual.error")}
              </p>
            ) : (
              <div className="grid gap-2 sm:grid-cols-3">
                {availableSources.map((candidate) => {
                  const selected = candidate.source === source;
                  return (
                    <button
                      key={candidate.source}
                      type="button"
                      className={cn(
                        "rounded-lg border px-3 py-3 text-left text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-40",
                        selected
                          ? "border-foreground/50 bg-muted/60 text-foreground"
                          : "border-border bg-transparent hover:bg-muted/30",
                      )}
                      aria-pressed={selected}
                      disabled={
                        importPending || !visualSourceSupportsKind(candidate, kind ?? "keyviewer")
                      }
                      onClick={() => {
                        setSource(candidate.source);
                        setLocalError(null);
                        setRequirements([]);
                        setAssetFiles({});
                      }}
                    >
                      <span className="flex items-center justify-between gap-2 font-medium">
                        {t(`visual.source.${candidate.source}`)}
                        <span
                          aria-hidden="true"
                          className={cn(
                            "grid size-4 shrink-0 place-items-center rounded-full border",
                            selected
                              ? "border-primary bg-primary text-primary-foreground"
                              : "border-muted-foreground/40",
                          )}
                        >
                          {selected ? <span className="size-1.5 rounded-full bg-current" /> : null}
                        </span>
                      </span>
                      <span className="mt-1 block text-xs text-muted-foreground">
                        {candidate.available
                          ? t("visual.sourceVersion", { version: candidate.version })
                          : t("visual.sourceUnavailable")}
                      </span>
                    </button>
                  );
                })}
              </div>
            )}
          </fieldset>
          {!sourcesPending &&
          !sourcesError &&
          availableSources.some((candidate) => !candidate.available) ? (
            <div className="space-y-2">
              <p className="text-xs leading-relaxed text-muted-foreground">
                {t("visual.detectionHint")}
              </p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                disabled={importPending}
                onClick={onRetrySources}
              >
                {t("visual.retrySources")}
              </Button>
            </div>
          ) : null}
          <label className="block space-y-2" htmlFor="visual-preset-name">
            <span className="text-sm font-medium">{t("visual.nameStep")}</span>
            <span id="visual-preset-name-hint" className="block text-xs text-muted-foreground">
              {t(source ? "visual.nameHint" : "visual.chooseSourceFirst")}
            </span>
            <input
              id="visual-preset-name"
              className="h-10 w-full rounded-xl border border-input bg-background/60 px-3 text-sm outline-none transition focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
              value={name}
              maxLength={80}
              minLength={1}
              required
              onChange={(event) => {
                setName(event.target.value);
                setLocalError(null);
              }}
              placeholder={t("visual.namePlaceholder")}
              autoComplete="off"
              aria-describedby="visual-preset-name-hint"
              aria-label={t("visual.nameLabel")}
              disabled={importPending || !source}
            />
          </label>
          {visualSourceUsesPresetFile(source) ? (
            <>
              <RegistrationFileField
                id="dmnote-preset-file"
                label={t("visual.dmnoteFileLabel", {
                  source: source ? t(`visual.source.${source}`) : "DMNote",
                })}
                hint={t("visual.dmnoteFileHint")}
                accept="application/json,.json"
                file={file}
                disabled={importPending}
                onChange={(next) => {
                  setFile(next);
                  setLocalError(null);
                  setRequirements([]);
                  setAssetFiles({});
                }}
              />
              {dmnote.pending ? (
                <p role="status" className="text-xs text-muted-foreground">
                  {t("visual.checkingAssets")}
                </p>
              ) : null}
              <DmnotePlacementEditor editor={dmnote} disabled={importPending} />
              <details className="group border-t border-border pt-4">
                <summary className="cursor-pointer text-sm font-medium focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                  {t("visual.advancedOptions")}
                </summary>
                <div className="space-y-5 pt-5">
                  <RegistrationFileField
                    id="dmnote-css-file"
                    label={t("visual.dmnoteCssLabel")}
                    hint={t("visual.dmnoteCssHint")}
                    accept="text/css,.css"
                    file={cssFile}
                    disabled={importPending}
                    onChange={(next) => {
                      setCssFile(next);
                      setRequirements([]);
                      setLocalError(null);
                    }}
                  />
                  <div className="flex items-center justify-between gap-4">
                    <label htmlFor="dmnote-key-counters" className="text-sm">
                      {t("visual.dmnoteKeyCounters")}
                    </label>
                    <Switch
                      id="dmnote-key-counters"
                      checked={dmnote.keyCounterEnabled}
                      disabled={importPending}
                      onCheckedChange={dmnote.setKeyCounterEnabled}
                    />
                  </div>
                </div>
              </details>
            </>
          ) : source ? (
            <p className="border-l-2 border-border pl-3 text-sm leading-relaxed text-muted-foreground">
              {t("visual.savedSetupHint", { source: t(`visual.source.${source}`) })}
            </p>
          ) : null}
          {requirements.length > 0 ? (
            <fieldset className="space-y-4 border-t border-border pt-4">
              <legend className="px-1 text-sm font-medium">{t("visual.assetsTitle")}</legend>
              <p role="status" className="text-xs leading-relaxed text-muted-foreground">
                {t("visual.assetsHint")}
              </p>
              {requirements.map((asset, index) => (
                <RegistrationFileField
                  key={asset.reference}
                  id={`visual-asset-${index}`}
                  label={asset.reference}
                  accept={
                    asset.kind === "font"
                      ? ".ttf,.otf,.woff,.woff2"
                      : asset.kind === "image"
                        ? "image/png,image/jpeg,image/webp,image/gif,image/bmp,image/svg+xml,image/x-icon,image/avif,.ico,.avif"
                        : ".ttf,.otf,.woff,.woff2,.png,.jpg,.jpeg,.webp,.gif,.bmp,.svg,.ico,.avif"
                  }
                  file={assetFiles[asset.reference] ?? null}
                  disabled={importPending}
                  onChange={(selected) => {
                    setAssetFiles((current) => {
                      const next = { ...current };
                      if (selected) next[asset.reference] = selected;
                      else delete next[asset.reference];
                      return next;
                    });
                    setLocalError(null);
                  }}
                />
              ))}
            </fieldset>
          ) : null}
        </form>
        <div className="shrink-0 space-y-3 border-t border-border pt-4">
          {progress && (importPending || error || requirements.length > 0) ? (
            <VisualRegistrationProgressView
              progress={progress}
              status={
                importPending ? "working" : requirements.length > 0 ? "needs_assets" : "failed"
              }
            />
          ) : null}
          {error ? (
            <p role="alert" className="text-sm leading-relaxed text-destructive">
              {localError === "visual_source_required"
                ? t("visual.sourceRequired")
                : localError === "visual_dmnote_file_required"
                  ? t("visual.dmnoteFileRequired")
                  : visualErrorText({ code: error }, t("visual.error"), t)}
            </p>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              disabled={importPending}
              onClick={() => onOpenChange(false)}
            >
              {t("close")}
            </Button>
            <Button
              type="submit"
              disabled={
                importPending ||
                sourcesPending ||
                Boolean(sourcesError) ||
                !source ||
                !selectedSource ||
                !visualSourceSupportsKind(selectedSource, kind ?? "keyviewer") ||
                !name.trim() ||
                (visualSourceUsesPresetFile(source) && (!file || Boolean(dmnote.error))) ||
                (visualSourceUsesPresetFile(source) && dmnote.pending)
              }
              form="visual-registration-form"
            >
              {importPending ? t("visual.registering") : t("visual.register")}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function RegistrationFileField({
  id,
  label,
  hint,
  accept,
  file,
  disabled,
  onChange,
}: {
  id: string;
  label: string;
  hint?: string;
  accept: string;
  file: File | null;
  disabled: boolean;
  onChange: (file: File | null) => void;
}) {
  const { t } = useTranslation("submission");
  const input = useRef<HTMLInputElement>(null);
  return (
    <div className="space-y-2">
      <label htmlFor={id} className="block break-words text-sm font-medium">
        {label}
      </label>
      {hint ? (
        <p id={`${id}-hint`} className="text-xs leading-relaxed text-muted-foreground">
          {hint}
        </p>
      ) : null}
      <input
        ref={input}
        id={id}
        type="file"
        accept={accept}
        disabled={disabled}
        className="sr-only"
        tabIndex={-1}
        aria-describedby={hint ? `${id}-hint` : undefined}
        onChange={(event) => onChange(event.target.files?.[0] ?? null)}
      />
      <div className="flex items-center gap-3 rounded-lg border border-dashed border-border bg-muted/15 p-3">
        <span className="min-w-0 flex-1 break-words text-xs text-muted-foreground">
          {file?.name ?? t("visual.noFile")}
        </span>
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={disabled}
          aria-label={`${file ? t("visual.replaceFile") : t("visual.chooseFile")}: ${label}`}
          onClick={() => input.current?.click()}
        >
          {file ? t("visual.replaceFile") : t("visual.chooseFile")}
        </Button>
        {file ? (
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            disabled={disabled}
            aria-label={t("visual.removeFile", { name: file.name })}
            onClick={() => {
              if (input.current) input.current.value = "";
              onChange(null);
            }}
          >
            <HugeiconsIcon icon={Delete02Icon} aria-hidden="true" className="size-4" />
          </Button>
        ) : null}
      </div>
    </div>
  );
}

function VisualPresetDeleteDialog({
  preset,
  pending,
  error,
  onOpenChange,
  onConfirm,
}: {
  preset: VisualPreset | null;
  pending: boolean;
  error: unknown;
  onOpenChange: (open: boolean) => void;
  onConfirm: () => void;
}) {
  const { t } = useTranslation("submission");
  const kind = preset ? t(`visual.kind.${preset.kind}`) : "";
  return (
    <Dialog open={preset !== null} onOpenChange={(open) => !pending && onOpenChange(open)}>
      <DialogContent
        className="min-h-0 w-[min(27rem,calc(100vw-2rem))]"
        onEscapeKeyDown={(event) => pending && event.preventDefault()}
        onPointerDownOutside={(event) => pending && event.preventDefault()}
      >
        <DialogTitle>{t("visual.deleteTitle", { name: preset?.name ?? "" })}</DialogTitle>
        <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
          {t("visual.deleteDescription", { kind })}
        </p>
        {error ? (
          <p role="alert" className="mt-3 text-sm leading-relaxed text-destructive">
            {visualErrorText(error, t("visual.error"), t)}
          </p>
        ) : null}
        <div className="mt-6 flex justify-end gap-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            disabled={pending}
            onClick={() => onOpenChange(false)}
          >
            {t("close")}
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            disabled={pending}
            onClick={onConfirm}
          >
            {pending ? t("visual.deleting") : t("visual.delete")}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function visualErrorText(error: unknown, fallback: string, t: TFunction<"submission">) {
  const code = visualErrorCode(error);
  const key = code ? visualErrorKeys[code] : undefined;
  return key ? t(key) : fallback;
}

const visualErrorKeys: Record<string, VisualErrorTranslationKey> = {
  visual_name_required: "visual.errors.visual_name_required",
  visual_name_too_long: "visual.errors.visual_name_too_long",
  visual_name_taken: "visual.errors.visual_name_taken",
  visual_source_unsupported: "visual.errors.visual_source_unsupported",
  visual_bundle_invalid: "visual.errors.visual_bundle_invalid",
  visual_asset_missing: "visual.errors.visual_asset_missing",
  visual_payload_too_large: "visual.errors.visual_payload_too_large",
  visual_multiple_tabs: "visual.errors.visual_multiple_tabs",
  visual_selected_tab_missing: "visual.errors.visual_selected_tab_missing",
  visual_preset_not_found: "visual.errors.visual_preset_not_found",
  visual_selection_conflict: "visual.errors.visual_selection_conflict",
  visual_request_failed: "visual.errors.visual_request_failed",
  visual_registration_busy: "visual.errors.visual_registration_busy",
  visual_registration_expired: "visual.errors.visual_registration_expired",
};

type VisualErrorTranslationKey =
  | "visual.errors.visual_name_required"
  | "visual.errors.visual_name_too_long"
  | "visual.errors.visual_name_taken"
  | "visual.errors.visual_source_unsupported"
  | "visual.errors.visual_bundle_invalid"
  | "visual.errors.visual_asset_missing"
  | "visual.errors.visual_payload_too_large"
  | "visual.errors.visual_multiple_tabs"
  | "visual.errors.visual_selected_tab_missing"
  | "visual.errors.visual_preset_not_found"
  | "visual.errors.visual_selection_conflict"
  | "visual.errors.visual_request_failed"
  | "visual.errors.visual_registration_busy"
  | "visual.errors.visual_registration_expired";
