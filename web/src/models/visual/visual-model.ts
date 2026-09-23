import type { z } from "zod";
import type {
  visualInspectionSchema,
  visualKindSchema,
  visualPresetPageSchema,
  visualPresetSchema,
  visualSourceInfoSchema,
  visualSourceSchema,
  visualSourcesSchema,
} from "@/schemas/visual/visual-schema";

export type VisualKind = z.infer<typeof visualKindSchema>;
export type VisualInspection = z.infer<typeof visualInspectionSchema>;
export type VisualSource = z.infer<typeof visualSourceSchema>;
export type VisualPreset = z.infer<typeof visualPresetSchema>;
export type VisualPresetPage = z.infer<typeof visualPresetPageSchema>;
export type VisualSourceInfo = z.infer<typeof visualSourceInfoSchema>;
export type VisualSources = z.infer<typeof visualSourcesSchema>;

export const VISUAL_PRESET_NAME_MAX_LENGTH = 80;

export function visualPresetAvatar(name: string) {
  const Segmenter = (
    Intl as typeof Intl & {
      Segmenter?: new (
        locales?: string | string[],
        options?: { granularity: "grapheme" },
      ) => { segment(value: string): Iterable<{ segment: string }> };
    }
  ).Segmenter;
  if (Segmenter) {
    const first = new Segmenter(undefined, { granularity: "grapheme" })
      .segment(name)
      [Symbol.iterator]()
      .next();
    if (!first.done && first.value.segment) return first.value.segment;
  }
  return Array.from(name)[0] ?? "?";
}

export function visualSourceSupportsKind(source: VisualSourceInfo, kind: VisualKind) {
  return source.available && source.kinds.includes(kind);
}

export function visualResponseBelongsToAccount(
  requestedAccountKey: string | null,
  currentAccountKey: string | null,
) {
  return requestedAccountKey !== null && requestedAccountKey === currentAccountKey;
}

export function visualErrorCode(error: unknown) {
  if (!error || typeof error !== "object") return null;
  const code = (error as { code?: unknown }).code;
  return typeof code === "string" ? code : null;
}

export function visualNameError(name: string) {
  const trimmed = name.trim();
  if (!trimmed) return "visual_name_required" as const;
  if (trimmed.length > VISUAL_PRESET_NAME_MAX_LENGTH) return "visual_name_too_long" as const;
  return null;
}

/** DMNote and Impl DMNote share the portable export and placement contract. */
export function visualSourceUsesPresetFile(source: VisualSource | null) {
  return source === "dmnote" || source === "impl-dmnote";
}
