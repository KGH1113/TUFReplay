import { describe, expect, test } from "bun:test";
import { QueryClient, QueryObserver } from "@tanstack/react-query";
import type { ActivityApi } from "@/api/activity/activity-api";
import type { AppApi } from "@/api/app-api";
import { aggregateRunMarkers } from "@/models/activity/activity-data";
import type { ActivityRun } from "@/models/activity/activity-model";
import {
  type LogicalLevelRunsSnapshot,
  logicalLevelRunsQuery,
} from "@/state/activity/logical-level-runs-query";

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (error: Error) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  return { promise, resolve, reject };
}

function run(id: string, startTile = 0) {
  return {
    id,
    startTile,
    lastTile: startTile + 10,
    result: "Failed",
    noFailMode: false,
  } as ActivityRun;
}

function apiWithRuns(load: ActivityApi["listLogicalLevelRuns"]) {
  return Promise.resolve({ activity: { listLogicalLevelRuns: load } } as AppApi);
}

function client() {
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
}

describe("logical level run refreshes", () => {
  test("retains an incomplete first-load snapshot when pagination is cancelled and restarted", async () => {
    const queryClient = client();
    const started = deferred<void>();
    const oldFinal = deferred<ActivityRun[]>();
    const selected = [run("selected", 200)];
    const first = logicalLevelRunsQuery(
      queryClient,
      apiWithRuns(async (_id, _sessions, onPage) => {
        onPage?.(selected);
        started.resolve();
        return oldFinal.promise;
      }),
      "level",
      "2026-10-01",
      ["app"],
    );
    const oldRequest = queryClient.fetchQuery(first).catch(() => null);
    await started.promise;
    await queryClient.cancelQueries({ queryKey: first.queryKey, exact: true }, { revert: false });
    const nextPage = deferred<void>();
    const nextFinal = deferred<ActivityRun[]>();
    const next = logicalLevelRunsQuery(
      queryClient,
      apiWithRuns(async (_id, _sessions, onPage) => {
        onPage?.([run("new")]);
        nextPage.resolve();
        return nextFinal.promise;
      }),
      "level",
      "2026-10-01",
      ["app"],
    );
    const nextRequest = queryClient.fetchQuery(next);
    await nextPage.promise;
    const snapshot = queryClient.getQueryData<LogicalLevelRunsSnapshot>(next.queryKey);
    expect(snapshot).toEqual({ items: selected, complete: false });
    nextFinal.resolve([run("new"), ...selected]);
    await nextRequest;
    oldFinal.resolve(selected);
    await oldRequest;
    expect(queryClient.getQueryData(next.queryKey)?.complete).toBe(true);
    queryClient.clear();
  });
  test("streams the first load but only marks the final list complete", async () => {
    const queryClient = client();
    const firstPage = deferred<void>();
    const finalPage = deferred<ActivityRun[]>();
    const first = [run("one")];
    const options = logicalLevelRunsQuery(
      queryClient,
      apiWithRuns(async (_id, _sessions, onPage) => {
        onPage?.(first);
        firstPage.resolve();
        return finalPage.promise;
      }),
      "level",
      "2026-10-01",
      ["app"],
    );
    const request = queryClient.fetchQuery(options);
    await firstPage.promise;
    expect(queryClient.getQueryData<LogicalLevelRunsSnapshot>(options.queryKey)).toEqual({
      items: first,
      complete: false,
    });
    const all = [...first, run("two", 200)];
    finalPage.resolve(all);
    await request;
    expect(queryClient.getQueryData<LogicalLevelRunsSnapshot>(options.queryKey)).toEqual({
      items: all,
      complete: true,
    });
    queryClient.clear();
  });

  test("retains selected-tile runs through every partial refresh page", async () => {
    const queryClient = client();
    const firstPage = deferred<void>();
    const finalPage = deferred<ActivityRun[]>();
    const original = [run("recent"), run("selected", 200)];
    const added = run("new");
    const options = logicalLevelRunsQuery(
      queryClient,
      apiWithRuns(async (_id, _sessions, onPage) => {
        onPage?.([added, original[0]]);
        firstPage.resolve();
        return finalPage.promise;
      }),
      "level",
      "2026-10-01",
      ["app"],
    );
    queryClient.setQueryData(options.queryKey, { items: original, complete: true });
    const request = queryClient.fetchQuery(options);
    await firstPage.promise;
    const cached = queryClient.getQueryData(options.queryKey);
    expect(cached?.items).toEqual(original);
    expect(
      aggregateRunMarkers(cached?.items ?? []).some((marker) => marker.id === "floor-200"),
    ).toBe(true);
    finalPage.resolve([added, ...original]);
    await request;
    expect(queryClient.getQueryData(options.queryKey)?.items).toEqual([added, ...original]);
    queryClient.clear();
  });

  test("keeps the completed list when a later page fails and can retry it", async () => {
    const queryClient = client();
    const original = [run("selected", 200)];
    let fail = true;
    const options = logicalLevelRunsQuery(
      queryClient,
      apiWithRuns(async (_id, _sessions, onPage) => {
        onPage?.([run("new")]);
        if (fail) throw new Error("page failed");
        return [run("new"), ...original];
      }),
      "level",
      "2026-10-01",
      ["app"],
    );
    queryClient.setQueryData(options.queryKey, { items: original, complete: true });
    await expect(queryClient.fetchQuery(options)).rejects.toThrow("page failed");
    expect(queryClient.getQueryData<LogicalLevelRunsSnapshot>(options.queryKey)).toEqual({
      items: original,
      complete: true,
    });
    fail = false;
    await queryClient.fetchQuery(options);
    expect(queryClient.getQueryData(options.queryKey)?.items).toHaveLength(2);
    queryClient.clear();
  });

  test("reuses the same cache when another app session opens on the same day", async () => {
    const queryClient = client();
    const api = apiWithRuns(async (_id, sessions) => sessions.map((id) => run(id)));
    const first = logicalLevelRunsQuery(queryClient, api, "level", "2026-10-01", ["app-1"]);
    await queryClient.fetchQuery(first);
    const next = logicalLevelRunsQuery(queryClient, api, "level", "2026-10-01", ["app-2", "app-1"]);
    expect(next.queryKey).toEqual(first.queryKey);
    expect(queryClient.getQueryData(next.queryKey)?.items.map((item) => item.id)).toEqual([
      "app-1",
    ]);
    await queryClient.fetchQuery(next);
    expect(queryClient.getQueryData(next.queryKey)?.items.map((item) => item.id)).toEqual([
      "app-2",
      "app-1",
    ]);
    queryClient.clear();
  });

  test("does not show the previous day or another level while switching scopes", async () => {
    const queryClient = client();
    const pending = deferred<ActivityRun[]>();
    const api = apiWithRuns(async () => pending.promise);
    const previous = logicalLevelRunsQuery(queryClient, api, "level", "2026-10-01", ["app"]);
    queryClient.setQueryData(previous.queryKey, { items: [run("old")], complete: true });
    const observer = new QueryObserver(queryClient, { ...previous, enabled: false });
    const unsubscribe = observer.subscribe(() => {});
    for (const [id, date] of [
      ["level", "2026-10-02"],
      ["other-level", "2026-10-01"],
    ]) {
      observer.setOptions(logicalLevelRunsQuery(queryClient, api, id, date, ["other-app"]));
      expect(observer.getCurrentResult().data).toBeUndefined();
    }
    pending.resolve([]);
    unsubscribe();
    queryClient.clear();
  });

  test("ignores late pages from a cancelled request after a newer snapshot arrives", async () => {
    const queryClient = client();
    const started = deferred<void>();
    const obsolete = deferred<ActivityRun[]>();
    let oldPage: ((items: ActivityRun[]) => void) | undefined;
    const first = logicalLevelRunsQuery(
      queryClient,
      apiWithRuns(async (_id, _sessions, onPage) => {
        oldPage = onPage;
        started.resolve();
        return obsolete.promise;
      }),
      "level",
      "2026-10-01",
      ["app"],
    );
    const oldRequest = queryClient.fetchQuery(first).catch(() => null);
    await started.promise;
    await queryClient.cancelQueries({ queryKey: first.queryKey, exact: true });
    const latest = [run("latest", 200)];
    const next = logicalLevelRunsQuery(
      queryClient,
      apiWithRuns(async () => latest),
      "level",
      "2026-10-01",
      ["app", "new-app"],
    );
    await queryClient.fetchQuery(next);
    expect(() => oldPage?.([run("obsolete")])).toThrow();
    obsolete.resolve([run("obsolete")]);
    await oldRequest;
    expect(queryClient.getQueryData<LogicalLevelRunsSnapshot>(next.queryKey)).toEqual({
      items: latest,
      complete: true,
    });
    queryClient.clear();
  });
});
