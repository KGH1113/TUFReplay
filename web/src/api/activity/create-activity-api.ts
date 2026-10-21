import { z } from "zod";
import type { ActivityApi } from "@/api/activity/activity-api";
import { callRecorder, decodeRecorder } from "@/api/recorder-call";
import {
  toAppSessionDto,
  toLegacyReplayStatusDto,
  toLogicalLevelDto,
  toRunDto,
} from "@/api/recorder-dtos";
import {
  mapActivityChart,
  mapActivityRun,
  mapAppSession,
  mapLogicalLevel,
} from "@/models/activity/activity-model";
import type { LocalAppChannels } from "@/ports/local-message-peer";
import type { TextDownloads } from "@/ports/text-download";
import {
  activityRunDtoSchema,
  appSessionDtoSchema,
  legacyReplayStatusDtoSchema,
  logicalLevelDtoSchema,
} from "@/schemas/activity/activity-schema";

const PAGE_SIZE = 200;
export function createActivityApi(
  clients: LocalAppChannels,
  downloads: TextDownloads,
): ActivityApi {
  const listAppSessions = async (offset: number, limit: number) => {
    const items = await callRecorder(clients, (mod) => mod.listAppSessions({ offset, limit }));
    return decodeRecorder(z.array(appSessionDtoSchema), items.map(toAppSessionDto)).map(
      mapAppSession,
    );
  };
  return {
    async getLegacyReplayStatus() {
      const status = decodeRecorder(
        legacyReplayStatusDtoSchema,
        toLegacyReplayStatusDto(await callRecorder(clients, (mod) => mod.readLegacyReplayStatus())),
      );
      return { hasLegacyReplays: status.HasLegacyReplays };
    },
    listAppSessions,
    listAllAppSessions: (onPage) => loadAllPages(listAppSessions, onPage),
    async getLogicalLevel(id) {
      return mapLogicalLevel(
        decodeRecorder(
          logicalLevelDtoSchema,
          toLogicalLevelDto(await callRecorder(clients, (mod) => mod.readLogicalLevel({ id }))),
        ),
      );
    },
    listLogicalLevelRuns: (id, appSessionIds, onPage) =>
      loadAllPages(async (offset, limit) => {
        const items = await callRecorder(clients, (mod) =>
          mod.listLogicalLevelRuns({ id, appSessionIds, offset, limit }),
        );
        return decodeRecorder(z.array(activityRunDtoSchema), items.map(toRunDto)).map(
          mapActivityRun,
        );
      }, onPage),
    async getLogicalLevelChart(id, options) {
      const chart = await callRecorder(
        clients,
        (mod) => mod.readLogicalLevelChart({ id }),
        options,
      );
      const levelText = await downloads.readText(chart.file, options);
      return mapActivityChart({
        LevelSessionId: chart.levelSessionId,
        FloorCount: chart.floorCount,
        LevelText: levelText,
      });
    },
  };
}
async function loadAllPages<T>(
  load: (offset: number, limit: number) => Promise<T[]>,
  onPage?: (items: T[]) => void,
) {
  const all: T[] = [];
  for (let offset = 0; ; offset += PAGE_SIZE) {
    const page = await load(offset, PAGE_SIZE);
    all.push(...page);
    onPage?.([...all]);
    if (page.length < PAGE_SIZE) return all;
  }
}
