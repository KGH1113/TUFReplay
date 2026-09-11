import { z } from "zod";
import type { ActivityApi } from "@/api/activity/activity-api";
import {
  mapActivityChart,
  mapActivityRun,
  mapAppSession,
  mapLogicalLevel,
} from "@/models/activity/activity-model";
import {
  activityChartDtoSchema,
  activityRunDtoSchema,
  appSessionDtoSchema,
  legacyReplayStatusDtoSchema,
  logicalLevelDtoSchema,
} from "@/schemas/activity/activity-schema";
import { type AdofaiIpcClients, callAdofaiIpc } from "@/shared/clients/adofai-ipc-client";

const PAGE_SIZE = 200;

export function createActivityApi(clients: AdofaiIpcClients): ActivityApi {
  const listAppSessions = async (offset: number, limit: number) => {
    const items = await callAdofaiIpc(
      clients.namespace,
      "activity.app-sessions.list",
      { offset, limit },
      z.array(appSessionDtoSchema),
    );
    return items.map(mapAppSession);
  };

  return {
    async getLegacyReplayStatus() {
      const status = await callAdofaiIpc(
        clients.namespace,
        "activity.legacy-replay-status.get",
        {},
        legacyReplayStatusDtoSchema,
      );
      return { hasLegacyReplays: status.HasLegacyReplays };
    },
    listAppSessions,
    listAllAppSessions: (onPage) => loadAllPages(listAppSessions, onPage),
    async getLogicalLevel(id) {
      return mapLogicalLevel(
        await callAdofaiIpc(
          clients.namespace,
          "activity.logical-level.get",
          { id },
          logicalLevelDtoSchema,
        ),
      );
    },
    listLogicalLevelRuns: (id, appSessionIds, onPage) =>
      loadAllPages(async (offset, limit) => {
        const items = await callAdofaiIpc(
          clients.namespace,
          "activity.logical-level.runs.list",
          { id, appSessionIds, offset, limit },
          z.array(activityRunDtoSchema),
        );
        return items.map(mapActivityRun);
      }, onPage),
    async getLogicalLevelChart(id) {
      return mapActivityChart(
        await callAdofaiIpc(
          clients.namespace,
          "activity.logical-level.chart.get",
          { id },
          activityChartDtoSchema,
        ),
      );
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
