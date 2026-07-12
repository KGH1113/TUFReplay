import { useEffect, useState } from "react";

import type { LevelMetadata } from "../activity.model";
import { getFallbackMetadata, getTufMetadata } from "../data/tuf-metadata.service";

export function useLevelMetadata(levelIds: (number | null)[]) {
  const [metadata, setMetadata] = useState<Map<number, LevelMetadata>>(new Map());
  const key = [...new Set(levelIds.filter((id): id is number => id !== null))].sort((a, b) => a - b).join(",");
  useEffect(() => {
    let active = true;
    for (const levelId of key ? key.split(",").map(Number) : []) {
      void getTufMetadata(levelId).then((value) => {
        if (active) setMetadata((current) => new Map(current).set(levelId, value));
      });
    }
    return () => { active = false; };
  }, [key]);
  return (levelId: number | null) => levelId === null ? getFallbackMetadata(null) : metadata.get(levelId) ?? getFallbackMetadata(levelId);
}
