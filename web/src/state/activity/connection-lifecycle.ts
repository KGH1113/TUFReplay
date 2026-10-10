import type { AppEvents } from "@/api/app-events";
import type { NamespaceStatus, SessionState } from "@/ports/local-message-peer";

export interface ActivityConnectionSnapshot {
  session: SessionState | null;
  recorder: NamespaceStatus | null;
}

/** Transport reconnection precedes recorder startup; refresh only when the recorder is ready. */
export function observeActivityConnection(
  events: AppEvents,
  receive: (snapshot: ActivityConnectionSnapshot) => void,
  ready: () => void,
): () => void {
  let snapshot: ActivityConnectionSnapshot = { session: null, recorder: null };
  let previous: NamespaceStatus | null = null;
  const connection = events.on("connection.changed", (session) => {
    snapshot = { ...snapshot, session };
    if (session !== "connected") {
      snapshot.recorder = "unavailable";
      previous = null;
    }
    receive(snapshot);
  });
  const recorder = events.on("recorder.status.changed", (status) => {
    const becameReady = status === "ready" && previous !== "ready";
    previous = status;
    snapshot = { ...snapshot, recorder: status };
    receive(snapshot);
    if (becameReady) ready();
  });
  return () => {
    connection();
    recorder();
  };
}
