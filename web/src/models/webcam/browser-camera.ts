export type BrowserCameraDevice = { id: string; label: string };

export function browserCameraDevices(devices: MediaDeviceInfo[]): BrowserCameraDevice[] {
  return devices
    .filter((device) => device.kind === "videoinput" && device.deviceId !== "")
    .map((device) => ({ id: device.deviceId, label: device.label }));
}

export function matchBrowserCamera(devices: BrowserCameraDevice[], gameLabel: string | null) {
  if (!gameLabel) return null;
  const normalized = normalizeLabel(gameLabel);
  if (!normalized) return null;
  const matches = devices.filter((device) => normalizeLabel(device.label) === normalized);
  return matches.length === 1 ? matches[0] : null;
}

function normalizeLabel(label: string) {
  return label.normalize("NFKC").trim().toLowerCase().replace(/\s+/g, " ");
}

export function browserCameraErrorKey(error: unknown) {
  const name = error instanceof Error ? error.name : "";
  if (name === "NotAllowedError" || name === "SecurityError") return "permission";
  if (name === "NotFoundError" || name === "OverconstrainedError") return "notFound";
  if (name === "NotReadableError" || name === "AbortError") return "inUse";
  return "failed";
}
