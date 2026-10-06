import type { DomainMessage, LocalAppChannels, LocalMessagePeer } from "@/ports/local-message-peer";

const outcomes: Record<string, string> = {
  "downloads.state.read": "downloads.state.changed",
  "downloads.renderer.request": "downloads.state.changed",
  "downloads.renderer.confirm": "downloads.state.changed",
  "downloads.renderer.cancel": "downloads.state.changed",
  "downloads.ffmpeg.request": "downloads.state.changed",
  "downloads.ffmpeg.confirm": "downloads.state.changed",
  "downloads.ffmpeg.cancel": "downloads.state.changed",
  "media.ffmpeg.release": "media.ffmpeg.state.changed",
  "replay.render-bundle.prepare": "render-bundle.state.changed",
  "replay.render-bundle.state.read": "render-bundle.state.changed",
  "replay.render-bundle.cancel": "render-bundle.state.changed",
  "renderer.settings.read": "renderer.settings.changed",
  "renderer.settings.change": "renderer.settings.changed",
  "renderer.folder.choose": "renderer.folder-selection.changed",
  "renderer.folder.cancel": "renderer.folder-selection.cancelled",
  "renderer.folder.open": "renderer.folder.opened",
  "render.start": "renderer.job.changed",
  "render.state.read": "renderer.job.changed",
  "render.cancel": "renderer.job.changed",
  "render.download": "download.ready",
  "webcam.state.refresh": "webcam.state.changed",
  "webcam.settings.change": "webcam.state.changed",
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
  overrideOutcomes: Record<string, string> = {},
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
          else publish(overrideOutcomes[command] ?? outcomes[command], correlationId, result);
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
