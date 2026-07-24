import { useEffect, useState } from "react";

import type { ActivityLogicalLevelOverview, LevelMetadata } from "../activity.model";
import { getFallbackMetadata, getTufMetadata } from "../data/tuf-metadata.service";

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
    for (const levelId of key ? key.split(",").map(Number) : []) {
      void getTufMetadata(levelId)
        .then((value) => {
          if (active) setMetadata((current) => new Map(current).set(levelId, value));
        })
        .catch(() => undefined);
    }
    return () => {
      active = false;
    };
  }, [key]);
  return (session: ActivityLogicalLevelOverview) =>
    session.TufLevelId === null
      ? getFallbackMetadata(null, session)
      : (metadata.get(session.TufLevelId) ?? getFallbackMetadata(session.TufLevelId, session));
}
