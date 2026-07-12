import {
  mockActivityLevelSession,
  mockActivityRuns,
  mockLevelMetadataById,
} from "../activity.mock";
import type {
  ActivityLevelSession,
  ActivityRun,
  LevelMetadata,
} from "../activity.model";
import { getElapsedMinutes } from "./activity-chart.utils";

export function getLevelMetadata(levelId: number): LevelMetadata {
  return (
    mockLevelMetadataById[levelId as keyof typeof mockLevelMetadataById] ?? {
      Difficulty: "Unknown",
      DifficultyIconUrl: "",
      LevelId: levelId,
      Artist: "Unknown",
      Name: `Level ${levelId}`,
      Creator: "Unknown",
    }
  );
}

export function isFullClear(run: ActivityRun) {
  return run.StartTile === 0 && run.LastTile === run.LevelTileCount && run.Result === "cleared";
}

export function getSegmentGroups(levelSession: ActivityLevelSession) {
  if (levelSession.Id === mockActivityLevelSession.Session.Id) return mockActivityLevelSession.SegmentGroups;

  return [
    {
      SegmentGroupIndex: 0,
      StartTile: levelSession.FirstStartTile ?? 0,
      AttemptCount: levelSession.RunCount,
      BestLastTile: levelSession.LastStartTile ?? levelSession.LevelTileCount,
      FirstStartedAtUtc: levelSession.OpenedAtUtc,
      LastStartedAtUtc: levelSession.ClosedAtUtc ?? levelSession.OpenedAtUtc,
    },
  ];
}

export function getLevelSessionRuns(levelSession: ActivityLevelSession) {
  return mockActivityRuns.filter((run) => run.LevelSessionId === levelSession.Id);
}

export function getLevelSessionClearCount(levelSession: ActivityLevelSession) {
  return getLevelSessionRuns(levelSession).filter(isFullClear).length;
}

export function getTimeDomain(levelSession: ActivityLevelSession, runs: ActivityRun[]) {
  const lastElapsed = runs.reduce((maxElapsed, run) => {
    return Math.max(maxElapsed, getElapsedMinutes(run.EndedAtUtc, levelSession.OpenedAtUtc));
  }, 0);

  return {
    min: 0,
    max: Math.max(20, Math.ceil(lastElapsed / 2) * 2),
  };
}

