import { useEffect, useState } from "react";

import type { ActivityLogicalLevelOverview, LevelMetadata } from "../activity.model";
import { getFallbackMetadata, getTufMetadata } from "../data/tuf-metadata.service";

const metadataBatchSize = 4;

export function useLevelMetadata(levelSessions: ActivityLogicalLevelOverview[]) {
  const [metadata, setMetadata] = useState<Map<number, LevelMetadata>>(new Map());
  const key = [
    ...new Set(
      levelSessions.map((session) => session.TufLevelId).filter((id): id is number => id !== null),
    ),
  ]
    .sort((a, b) => a - b)
    .join(",");
  useEffect(() => {
    let active = true;
    const levelIds = key ? key.split(",").map(Number) : [];
    void loadLevelMetadataBatches(
      levelIds,
      getTufMetadata,
      (batch) => setMetadata((current) => mergeLevelMetadata(current, batch)),
      metadataBatchSize,
      () => active,
    );
    return () => {
      active = false;
    };
  }, [key]);
  return (session: ActivityLogicalLevelOverview) =>
    session.TufLevelId === null
      ? getFallbackMetadata(null, session)
      : (metadata.get(session.TufLevelId) ?? getFallbackMetadata(session.TufLevelId, session));
}

export async function loadLevelMetadataBatches(
  levelIds: number[],
  load: (levelId: number) => Promise<LevelMetadata>,
  onBatch: (batch: Map<number, LevelMetadata>) => void,
  batchSize = metadataBatchSize,
  isActive: () => boolean = () => true,
): Promise<void> {
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
    for (const entry of loaded) {
      if (entry) batch.set(entry[0], entry[1]);
    }
    if (batch.size > 0) onBatch(batch);
  }
}

export function mergeLevelMetadata(
  current: Map<number, LevelMetadata>,
  batch: Map<number, LevelMetadata>,
): Map<number, LevelMetadata> {
  let next = current;
  for (const [levelId, value] of batch) {
    if (current.get(levelId) === value) continue;
    if (next === current) next = new Map(current);
    next.set(levelId, value);
  }
  return next;
}
