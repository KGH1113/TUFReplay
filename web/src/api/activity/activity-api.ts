import type {
  ActivityChart,
  ActivityRun,
  AppSession,
  LogicalLevel,
} from "@/models/activity/activity-model";

export interface ActivityApi {
  getLegacyReplayStatus(): Promise<{ hasLegacyReplays: boolean }>;
  listAppSessions(offset: number, limit: number): Promise<AppSession[]>;
  listAllAppSessions(onPage?: (items: AppSession[]) => void): Promise<AppSession[]>;
  getLogicalLevel(id: string): Promise<LogicalLevel>;
  listLogicalLevelRuns(
    id: string,
    appSessionIds: string[],
    onPage?: (items: ActivityRun[]) => void,
  ): Promise<ActivityRun[]>;
  getLogicalLevelChart(id: string): Promise<ActivityChart>;
}
