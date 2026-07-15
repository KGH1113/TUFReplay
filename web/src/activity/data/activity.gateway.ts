import { type AdofaiIpcNamespaceClient, tryConnect } from "@adofai-ipc/client";

import type {
  ActivityAppSession,
  ActivityChart,
  ActivityLevelSessionOverview,
  ActivityRun,
  ReplayStatus,
} from "../activity.model";
import { adofaiIpcFetch } from "./adofai-ipc.fetch";

const NAMESPACE = "tuf-replay";
const PAGE_SIZE = 200;

interface DomainErrorPayload {
  error: {
    code: string;
    message: string;
  };
}

export class ActivityDomainError extends Error {
  constructor(
    readonly code: string,
    message: string,
  ) {
    super(message);
    this.name = "ActivityDomainError";
  }
}

export interface ActivityGateway {
  health(): Promise<unknown>;
  listAllAppSessions(onPage?: (items: ActivityAppSession[]) => void): Promise<ActivityAppSession[]>;
  getLevelSession(id: string): Promise<ActivityLevelSessionOverview>;
  listAllRuns(id: string, onPage?: (items: ActivityRun[]) => void): Promise<ActivityRun[]>;
  getChart(id: string): Promise<ActivityChart>;
  playReplay(runId: string): Promise<ReplayStatus>;
  getReplayStatus(): Promise<ReplayStatus>;
}

export async function connectActivityGateway(): Promise<ActivityGateway> {
  const client = await tryConnect({
    fetch: adofaiIpcFetch,
  });
  return createActivityGateway(client.namespace(NAMESPACE));
}

export function createActivityGateway(
  namespace: Pick<AdofaiIpcNamespaceClient, "call">,
): ActivityGateway {
  return {
    health: () => callDomain(namespace, "health.get", {}),
    listAllAppSessions: (onPage) =>
      loadAllPages<ActivityAppSession>(
        (offset, limit) =>
          callDomain<ActivityAppSession[]>(namespace, "activity.app-sessions.list", {
            offset,
            limit,
          }),
        onPage,
      ),
    getLevelSession: (id) => callDomain(namespace, "activity.level-session.get", { id }),
    listAllRuns: (id, onPage) =>
      loadAllPages<ActivityRun>(
        (offset, limit) =>
          callDomain<ActivityRun[]>(namespace, "activity.level-session.runs.list", {
            id,
            offset,
            limit,
          }),
        onPage,
      ),
    getChart: (id) => callDomain(namespace, "activity.level-session.chart.get", { id }),
    playReplay: (runId) => callDomain(namespace, "replay.play", { runId }),
    getReplayStatus: () => callDomain(namespace, "replay.status.get", {}),
  };
}

export async function loadAllPages<T>(
  load: (offset: number, limit: number) => Promise<T[]>,
  onPage?: (items: T[]) => void,
): Promise<T[]> {
  const all: T[] = [];
  for (let offset = 0; ; offset += PAGE_SIZE) {
    const page = await load(offset, PAGE_SIZE);
    all.push(...page);
    onPage?.([...all]);
    if (page.length < PAGE_SIZE) return all;
  }
}

async function callDomain<TResult>(
  namespace: Pick<AdofaiIpcNamespaceClient, "call">,
  method: string,
  params: object,
): Promise<TResult> {
  const result: unknown = await namespace.call(method, params);
  if (isDomainError(result)) throw new ActivityDomainError(result.error.code, result.error.message);
  return result as TResult;
}

function isDomainError(value: unknown): value is DomainErrorPayload {
  if (!value || typeof value !== "object") return false;
  const error = (value as { error?: unknown }).error;
  return Boolean(
    error &&
      typeof error === "object" &&
      typeof (error as { code?: unknown }).code === "string" &&
      typeof (error as { message?: unknown }).message === "string",
  );
}
