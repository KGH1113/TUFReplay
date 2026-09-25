import { useEffect, useId, useState } from "react";
import { useTranslation } from "react-i18next";
import type { VisualPreset } from "@/models/visual/visual-model";
import { visualNameError } from "@/models/visual/visual-model";
import { Button } from "@/shared/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/shared/ui/dialog";

export function VisualPresetRenameDialog({
  preset,
  pending,
  error,
  onOpenChange,
  onSave,
  onEdit,
}: {
  preset: VisualPreset | null;
  pending: boolean;
  error: string;
  onOpenChange: (open: boolean) => void;
  onSave: (name: string) => void;
  onEdit: () => void;
}) {
  const { t } = useTranslation("submission");
  const id = useId();
  const [name, setName] = useState("");
  const [localError, setLocalError] = useState("");

  useEffect(() => {
    setName(preset?.name ?? "");
    setLocalError("");
  }, [preset]);

  const save = () => {
    if (!preset || pending) return;
    const problem = visualNameError(name);
    if (problem) {
      setLocalError(
        t(problem === "visual_name_required" ? "visual.nameRequired" : "visual.nameTooLong"),
      );
      return;
    }
    if (name.trim() !== preset.name) onSave(name.trim());
    else onOpenChange(false);
  };

  return (
    <Dialog open={preset !== null} onOpenChange={(open) => !pending && onOpenChange(open)}>
      <DialogContent className="min-h-0 w-[min(25rem,calc(100vw-2rem))]">
        <DialogHeader>
          <DialogTitle>{t("visual.renameTitle")}</DialogTitle>
          <DialogDescription>{t("visual.renameDescription")}</DialogDescription>
        </DialogHeader>
        <form
          className="mt-5 space-y-4"
          onSubmit={(event) => {
            event.preventDefault();
            save();
          }}
        >
          <label htmlFor={id} className="block text-sm font-medium">
            {t("visual.nameLabel")}
          </label>
          <input
            id={id}
            autoFocus
            value={name}
            onChange={(event) => {
              setName(event.target.value);
              setLocalError("");
              onEdit();
            }}
            disabled={pending}
            aria-invalid={Boolean(localError || error)}
            aria-describedby={localError || error ? `${id}-error` : undefined}
            className="h-10 w-full rounded-xl border border-input bg-background/60 px-3 text-sm outline-none transition focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50"
          />
          {localError || error ? (
            <p id={`${id}-error`} role="alert" className="text-sm text-destructive">
              {localError || error}
            </p>
          ) : null}
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              size="sm"
              variant="outline"
              disabled={pending}
              onClick={() => onOpenChange(false)}
            >
              {t("close")}
            </Button>
            <Button type="submit" size="sm" disabled={pending || !name.trim()}>
              {pending ? t("visual.renaming") : t("visual.rename")}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
