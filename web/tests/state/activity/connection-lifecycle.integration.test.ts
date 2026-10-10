import { expect, test } from "bun:test";
import { QueryClient, QueryObserver } from "@tanstack/react-query";
import { adaptChannel } from "@/adapters/adofai-ipc/connection";
import { createAppEvents } from "@/api/create-app-events";
import type { SessionState } from "@/ports/local-message-peer";
import {
  type ActivityConnectionSnapshot,
  observeActivityConnection,
} from "@/state/activity/connection-lifecycle";
import { IpcConnection } from "../../../vendor/adofai-ipc/src";

test("late recorder readiness recovers a failed active query and a second game session", async () => {
  const sdk = new IpcConnection();
  const recorder = sdk.namespace("tuf-replay");
  const peer = adaptChannel(recorder);
  let session: SessionState = "connected";
  const listeners = new Set<(value: SessionState) => void>();
  const events = createAppEvents({
    namespace: peer,
    pickerNamespace: peer,
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
    const status = (value: "ready" | "initializing") =>
      recorder.receive({
        kind: "namespace",
        name: "namespace.changed",
        id: "status",
        payload: { status: value },
      });
    status("ready");
    await observer.refetch({ cancelRefetch: false });
    expect(observer.getCurrentResult().status).toBe("success");
    expect(reads).toBe(2);
    status("ready");
    await Promise.resolve();
    expect(reads).toBe(2);
    recorder.disconnected();
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
    sdk.close();
  }
  expect(listeners.size).toBe(0);
});
