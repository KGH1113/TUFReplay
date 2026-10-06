import type { DomainMessage, LocalAppChannels, LocalMessagePeer } from "@/ports/local-message-peer";

const outcomes: Record<string, string> = {
  "health.read": "health.snapshot",
  "activity.sessions.read": "activity.sessions.snapshot",
  "activity.legacy-status.read": "activity.legacy-status.snapshot",
  "activity.level-session.read": "activity.level-session.snapshot",
  "activity.level-session.runs.read": "activity.level-session.runs.snapshot",
  "activity.level-session.chart.read": "activity.level-session.chart.snapshot",
  "activity.level.read": "activity.level.snapshot",
  "activity.runs.read": "activity.runs.snapshot",
  "activity.chart.read": "activity.chart.snapshot",
  "activity.run.remove": "activity.run.removed",
  "microphone.recording.remove": "microphone.recording.removed",
  "microphone.recording.retain": "microphone.recording.retained",
  "microphone.recording.download": "download.ready",
  "replay.start": "replay.state.changed",
  "replay.state.read": "replay.state.changed",
  "replay.level-file.choose": "replay.level-file.finished",
  "microphone.devices.refresh": "microphone.devices.changed",
  "microphone.access.change": "microphone.devices.changed",
  "microphone.device.choose": "microphone.devices.changed",
  "microphone.offset.change": "microphone.timing.changed",
  "microphone.volume.change": "microphone.timing.changed",
  "calibration.start": "calibration.state.changed",
  "calibration.state.read": "calibration.state.changed",
  "calibration.result.read": "calibration.result.ready",
  "calibration.preview.start": "calibration.state.changed",
  "calibration.preview.stop": "calibration.state.changed",
  "calibration.offset.change": "calibration.state.changed",
  "calibration.volume.change": "calibration.state.changed",
  "calibration.close": "calibration.state.changed",
};

export function scriptedChannels(
  handle: (command: string, payload: unknown) => unknown,
): LocalAppChannels {
  let id = 0;
  const listeners = new Map<string, Set<(payload: unknown, message: DomainMessage) => void>>();
  const publish = (name: string, correlationId: string, payload: unknown) => {
    for (const listener of listeners.get(name) ?? [])
      listener(payload, { id: "event", correlationId, payload });
  };
  const peer: LocalMessagePeer = {
    whenReady: async () => {},
    on(name, listener) {
      const set = listeners.get(name) ?? new Set();
      set.add(listener);
      listeners.set(name, set);
      return () => {
        set.delete(listener);
      };
    },
    send(command, payload = {}) {
      const correlationId = `command-${++id}`;
      queueMicrotask(async () => {
        try {
          const result = await handle(command, payload);
          if (result && typeof result === "object" && "error" in result)
            publish("command.rejected", correlationId, result.error);
          else publish(outcomes[command], correlationId, result);
        } catch (cause) {
          const error = cause as { code?: string; message?: string };
          publish("command.failed", correlationId, {
            code: error.code ?? "ipc_unavailable",
            message: error.message ?? "Connection failed.",
          });
        }
      });
      return correlationId;
    },
  };
  return { namespace: peer, pickerNamespace: peer };
}
