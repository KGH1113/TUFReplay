import { tryConnect, type AdofaiIpcNamespaceClient } from "@adofai-ipc/client";

import type {
  ActivityAppSession,
  ActivityChart,
  ActivityLevelSessionDetail,
  ActivityRun,
} from "../activity.model";

const NAMESPACE = "tuf-replay";
const PAGE_SIZE = 200;

interface Page<T> {
  Items: T[];
}

export interface ActivityGateway {
  health(): Promise<unknown>;
  listAllAppSessions(onPage?: (items: ActivityAppSession[]) => void): Promise<ActivityAppSession[]>;
  getLevelSession(id: string): Promise<ActivityLevelSessionDetail>;
  listAllRuns(id: string, onPage?: (items: ActivityRun[]) => void): Promise<ActivityRun[]>;
  getChart(id: string): Promise<ActivityChart>;
}

export async function connectActivityGateway(): Promise<ActivityGateway> {
  const client = await tryConnect();
  return createActivityGateway(client.namespace(NAMESPACE));
}

export function createActivityGateway(namespace: Pick<AdofaiIpcNamespaceClient, "call">): ActivityGateway {
  return {
    health: () => namespace.call("health.get", {}),
    listAllAppSessions: (onPage) => loadAllPages<ActivityAppSession>(
      (offset, limit) => namespace.call<Page<ActivityAppSession>>("activity.app-sessions.list", { offset, limit }),
      onPage,
    ),
    getLevelSession: (id) => namespace.call("activity.level-session.get", { id }),
    listAllRuns: (id, onPage) => loadAllPages<ActivityRun>(
      (offset, limit) => namespace.call<Page<ActivityRun>>("activity.level-session.runs.list", { id, offset, limit }),
      onPage,
    ),
    getChart: (id) => namespace.call("activity.level-session.chart.get", { id }),
  };
}

export async function loadAllPages<T>(
  load: (offset: number, limit: number) => Promise<Page<T>>,
  onPage?: (items: T[]) => void,
): Promise<T[]> {
  const all: T[] = [];
  for (let offset = 0; ; offset += PAGE_SIZE) {
    const page = await load(offset, PAGE_SIZE);
    all.push(...page.Items);
    onPage?.([...all]);
    if (page.Items.length < PAGE_SIZE) return all;
  }
}
