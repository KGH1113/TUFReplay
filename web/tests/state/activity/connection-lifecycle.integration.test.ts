import { expect, test } from "bun:test";
import { QueryClient, QueryObserver } from "@tanstack/react-query";
import { createRecorderSnapshots } from "@/adapters/adofai-ipc/connection";
import { createAppEvents } from "@/api/create-app-events";
import type { NamespaceStatus, RecorderClient, SessionState } from "@/ports/local-message-peer";
import {
  type ActivityConnectionSnapshot,
  observeActivityConnection,
} from "@/state/activity/connection-lifecycle";
import { scriptedChannels } from "../../fixtures/local-message-peer";

test("late recorder readiness recovers a failed active query and a second game session", async () => {
  const recorderSnapshots = createRecorderSnapshots();
  let namespaceStatus: NamespaceStatus = "unavailable";
  const statusListeners = new Set<(value: NamespaceStatus) => void>();
  const peer: RecorderClient = {
    mod: scriptedChannels(() => ({})).recorder.mod,
    on: recorderSnapshots.on,
    async whenReady() {
      if (namespaceStatus !== "ready") throw new Error("Recorder is starting");
    },
    onStatus(listener) {
      statusListeners.add(listener);
      listener(namespaceStatus);
      return () => {
        statusListeners.delete(listener);
      };
    },
  };
  const status = (value: NamespaceStatus) => {
    if (namespaceStatus === value) return;
    namespaceStatus = value;
    if (value !== "ready") recorderSnapshots.clear();
    for (const listener of statusListeners) listener(value);
  };
  let session: SessionState = "connected";
  const listeners = new Set<(value: SessionState) => void>();
  const events = createAppEvents({
    recorder: peer,
    session: {
      get state() {
        return session;
      },
      onState(listener) {
        listeners.add(listener);
        listener(session);
        return () => listeners.delete(listener);
      },
    },
  });
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: Infinity } },
  });
  let reads = 0;
  const observer = new QueryObserver(client, {
    queryKey: ["activity", "sessions"],
    queryFn: async () => {
      reads++;
      await peer.whenReady({ timeoutMs: 5 });
      return { session: reads };
    },
  });
  const stopQuery = observer.subscribe(() => {});
  const snapshots: ActivityConnectionSnapshot[] = [];
  const stopConnection = observeActivityConnection(
    events,
    (snapshot) => snapshots.push(snapshot),
    () => {
      void client.invalidateQueries({}, { cancelRefetch: false });
    },
  );
  try {
    await observer.refetch();
    expect(observer.getCurrentResult().status).toBe("error");
    status("ready");
    await observer.refetch({ cancelRefetch: false });
    expect(observer.getCurrentResult().status).toBe("success");
    expect(reads).toBe(2);
    status("ready");
    await Promise.resolve();
    expect(reads).toBe(2);
    status("unavailable");
    session = "reconnecting";
    for (const listener of listeners) listener(session);
    expect(snapshots.at(-1)).toEqual({ session: "reconnecting", recorder: "unavailable" });
    session = "connected";
    for (const listener of listeners) listener(session);
    status("initializing");
    expect(reads).toBe(2);
    status("ready");
    await observer.refetch({ cancelRefetch: false });
    expect(observer.getCurrentResult().status).toBe("success");
    expect(reads).toBe(3);
    expect(snapshots.at(-1)).toEqual({ session: "connected", recorder: "ready" });
  } finally {
    stopConnection();
    stopQuery();
    client.clear();
    recorderSnapshots.clear();
  }
  expect(listeners.size).toBe(0);
});
