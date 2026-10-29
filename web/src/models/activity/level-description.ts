import type { TFunction } from "i18next";
import type { LevelMetadata } from "@/models/activity/activity-model";

/** Display text for a level, with fallbacks for local files and missing TUF metadata. */
export function describeLevel(metadata: LevelMetadata, t: TFunction<"activity">) {
  const local = metadata.levelId === null;
  return {
    name:
      metadata.name ||
      (local
        ? t("metadata.customLevel")
        : t("metadata.levelNumber", { levelId: metadata.levelId })),
    artist: metadata.artist || (local ? t("metadata.localLevel") : t("metadata.tufDatabase")),
    creator: metadata.creator || t("metadata.unknownCreator"),
    difficulty:
      metadata.difficulty || (local ? t("metadata.local") : t("metadata.unknownDifficulty")),
    /** Source line for local levels, which have no TUF link. */
    source: metadata.artist
      ? t("metadata.localArtist", { artist: metadata.artist })
      : t("metadata.localUnknown"),
  };
}
