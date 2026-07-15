import type {
  ActivityAppSession,
  ActivityChart,
  ActivityLevelSessionOverview,
  ActivityRun,
  ReplayStatus,
} from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";

import level5Text from "./levels/tuf-5.adofai?raw";
import level303Text from "./levels/tuf-303.adofai?raw";
import level871Text from "./levels/tuf-871.adofai?raw";

interface MockLevel {
  session: ActivityLevelSessionOverview;
  chart: ActivityChart;
  runs: ActivityRun[];
}

const levels = [
  createLevel(
    "level-5",
    "app-2026-07-12",
    5,
    "2026-07-12T09:12:00.000Z",
    level5Text,
    [0, 0, 118, 245, 245, 402],
  ),
  createLevel(
    "level-303",
    "app-2026-07-12",
    303,
    "2026-07-12T10:03:00.000Z",
    level303Text,
    [0, 64, 64, 173, 288],
  ),
  createLevel(
    "level-871",
    "app-2026-07-13",
    871,
    "2026-07-13T01:20:00.000Z",
    level871Text,
    [0, 0, 92, 214, 356, 356, 480],
  ),
];

const appSessions: ActivityAppSession[] = [
  createAppSession("app-2026-07-13", "2026-07-13T01:18:00.000Z"),
  createAppSession("app-2026-07-12", "2026-07-12T09:08:00.000Z"),
];

export function createMockActivityGateway(): ActivityGateway {
  let replayStatus: ReplayStatus = {
    OperationId: null,
    RunId: null,
    State: "idle",
    ErrorCode: null,
    Message: null,
  };
  return {
    health: async () => ({ Status: "mock" }),
    listAllAppSessions: async (onPage) => {
      onPage?.(appSessions);
      return appSessions;
    },
    getLevelSession: async (id) => findLevel(id).session,
    listAllRuns: async (id, onPage) => {
      const runs = findLevel(id).runs;
      onPage?.(runs);
      return runs;
    },
    getChart: async (id) => findLevel(id).chart,
    playReplay: async (runId) => {
      replayStatus = {
        OperationId: `mock-${runId}`,
        RunId: runId,
        State: "playing",
        ErrorCode: null,
        Message: null,
      };
      return replayStatus;
    },
    getReplayStatus: async () => replayStatus,
  };
}

function createAppSession(id: string, startedAtUtc: string): ActivityAppSession {
  const levelSessions = levels
    .filter((level) => level.session.AppSessionId === id)
    .map((level) => level.session);
  return {
    Id: id,
    StartedAtUtc: startedAtUtc,
    EndedAtUtc: null,
    RecorderTimeZoneId: "Asia/Seoul",
    RecorderUtcOffsetMinutes: 540,
    LevelSessions: levelSessions,
  };
}

function createLevel(
  id: string,
  appSessionId: string,
  tufLevelId: number,
  openedAtUtc: string,
  levelText: string,
  starts: number[],
): MockLevel {
  const floorCount = readFloorCount(levelText);
  const runs = starts.map((startTile, index) =>
    createRun(id, tufLevelId, openedAtUtc, floorCount, startTile, index),
  );
  const clearRunCount = runs.filter((run) => run.Result === "Cleared").length;
  return {
    session: {
      Id: id,
      AppSessionId: appSessionId,
      TufLevelId: tufLevelId,
      OpenedAtUtc: openedAtUtc,
      ClosedAtUtc: null,
      RunCount: runs.length,
      ClearRunCount: clearRunCount,
      ChartAvailable: true,
    },
    chart: { LevelSessionId: id, LevelText: levelText, FloorCount: floorCount },
    runs,
  };
}

function createRun(
  levelSessionId: string,
  tufLevelId: number,
  openedAtUtc: string,
  floorCount: number,
  startTile: number,
  index: number,
): ActivityRun {
  const startedAt = new Date(new Date(openedAtUtc).getTime() + (index + 1) * 75_000);
  const cleared = index === 0 && startTile === 0;
  const lastTile = cleared ? floorCount - 1 : Math.min(floorCount - 1, startTile + 35 + index * 19);
  return {
    Id: `${levelSessionId}-run-${index + 1}`,
    LevelSessionId: levelSessionId,
    TufLevelId: tufLevelId,
    RunIndex: index + 1,
    StartedAtUtc: startedAt.toISOString(),
    EndedAtUtc: new Date(startedAt.getTime() + 42_000 + index * 6_000).toISOString(),
    StartTile: startTile,
    LastTile: lastTile,
    Result: cleared ? "Cleared" : "Failed",
    NoFailMode: tufLevelId === 5 && index === 1,
    GameplayStartSongPosition: null,
    LevelPitchPercent: 100,
    EffectivePitch: 1,
    XAccuracy: cleared ? 1 : Math.max(0, 0.985 - index * 0.011),
    JudgmentDifficulty: (["Strict", "Normal", "Lenient"] as const)[index % 3],
    JudgmentCounts: {
      Overload: cleared ? 0 : index % 2,
      TooEarly: index,
      Early: index + 1,
      EarlyPerfect: 3 + index,
      Perfect: Math.max(1, lastTile - startTile - 8),
      LatePerfect: 2 + index,
      Late: index,
      TooLate: cleared ? 0 : index % 3,
      Miss: cleared ? 0 : 1,
    },
    InputCount: Math.max(1, lastTile - startTile),
    HitContextCount: Math.max(1, lastTile - startTile),
    FloorCount: floorCount,
    InputBytes: 0,
    HitContextBytes: 0,
  };
}

function readFloorCount(levelText: string): number {
  const pathData = levelText.match(/"pathData"\s*:\s*"([^"]*)"/)?.[1];
  if (pathData) return pathData.length;
  const angleData = levelText.match(/"angleData"\s*:\s*\[([^\]]*)\]/)?.[1];
  return angleData ? angleData.split(",").length : 0;
}

function findLevel(id: string): MockLevel {
  const level = levels.find((candidate) => candidate.session.Id === id);
  if (!level) throw new Error(`Unknown mock level session: ${id}`);
  return level;
}
