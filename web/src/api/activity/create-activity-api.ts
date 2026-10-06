import { z } from "zod";
import type { ActivityApi } from "@/api/activity/activity-api";
import { type AdofaiIpcClients, sendDomainCommand } from "@/api/domain-messages";
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

const PAGE_SIZE = 200;

export function createActivityApi(clients: AdofaiIpcClients): ActivityApi {
  const listAppSessions = async (offset: number, limit: number) => {
    const items = await sendDomainCommand(
      clients.namespace,
      "activity.sessions.read",
      "activity.sessions.snapshot",
      { offset, limit },
      z.array(appSessionDtoSchema),
    );
    return items.map(mapAppSession);
  };

  return {
    async getLegacyReplayStatus() {
      const status = await sendDomainCommand(
        clients.namespace,
        "activity.legacy-status.read",
        "activity.legacy-status.snapshot",
        {},
        legacyReplayStatusDtoSchema,
      );
      return { hasLegacyReplays: status.HasLegacyReplays };
    },
    listAppSessions,
    listAllAppSessions: (onPage) => loadAllPages(listAppSessions, onPage),
    async getLogicalLevel(id) {
      return mapLogicalLevel(
        await sendDomainCommand(
          clients.namespace,
          "activity.level.read",
          "activity.level.snapshot",
          { id },
          logicalLevelDtoSchema,
        ),
      );
    },
    listLogicalLevelRuns: (id, appSessionIds, onPage) =>
      loadAllPages(async (offset, limit) => {
        const items = await sendDomainCommand(
          clients.namespace,
          "activity.runs.read",
          "activity.runs.snapshot",
          { id, appSessionIds, offset, limit },
          z.array(activityRunDtoSchema),
        );
        return items.map(mapActivityRun);
      }, onPage),
    async getLogicalLevelChart(id) {
      return mapActivityChart(
        await sendDomainCommand(
          clients.namespace,
          "activity.chart.read",
          "activity.chart.snapshot",
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
