import { useEffect, useState } from "react";

import { getFallbackMetadata, getTufMetadata } from "@/api/tuf/tuf-metadata-api";
import type { LevelMetadata, LogicalLevel } from "@/models/activity/activity-model";

const METADATA_BATCH_SIZE = 4;

export function useLevelMetadata(levels: LogicalLevel[]) {
  const [metadata, setMetadata] = useState<Map<number, LevelMetadata>>(new Map());
  const key = [
    ...new Set(levels.map((level) => level.tufLevelId).filter((id): id is number => id !== null)),
  ]
    .sort((left, right) => left - right)
    .join(",");

  useEffect(() => {
    let active = true;
    const levelIds = key ? key.split(",").map(Number) : [];
    void loadLevelMetadataBatches(
      levelIds,
      getTufMetadata,
      (batch) => setMetadata((current) => mergeLevelMetadata(current, batch)),
      METADATA_BATCH_SIZE,
      () => active,
    );
    return () => {
      active = false;
    };
  }, [key]);

  return (level: LogicalLevel): LevelMetadata => {
    const fallback = {
      Song: level.song,
      Author: level.author,
      Artist: level.artist,
    };
    return level.tufLevelId === null
      ? getFallbackMetadata(null, fallback)
      : (metadata.get(level.tufLevelId) ?? getFallbackMetadata(level.tufLevelId, fallback));
  };
}

export async function loadLevelMetadataBatches(
  levelIds: number[],
  load: (levelId: number) => Promise<LevelMetadata>,
  onBatch: (batch: Map<number, LevelMetadata>) => void,
  batchSize = METADATA_BATCH_SIZE,
  isActive: () => boolean = () => true,
) {
  const safeBatchSize = Math.max(1, Math.floor(batchSize));
  for (let offset = 0; offset < levelIds.length && isActive(); offset += safeBatchSize) {
    const ids = levelIds.slice(offset, offset + safeBatchSize);
    const loaded = await Promise.all(
      ids.map(async (levelId) => {
        try {
          return [levelId, await load(levelId)] as const;
        } catch {
          return null;
        }
      }),
    );
    if (!isActive()) return;
    const batch = new Map<number, LevelMetadata>();
    for (const entry of loaded) if (entry) batch.set(entry[0], entry[1]);
    if (batch.size > 0) onBatch(batch);
  }
}

export function mergeLevelMetadata(
  current: Map<number, LevelMetadata>,
  batch: Map<number, LevelMetadata>,
) {
  let next = current;
  for (const [levelId, value] of batch) {
    if (current.get(levelId) === value) continue;
    if (next === current) next = new Map(current);
    next.set(levelId, value);
  }
  return next;
}
