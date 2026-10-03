export interface BrowserCameraMedia {
  getUserMedia(constraints: MediaStreamConstraints): Promise<MediaStream>;
  enumerateDevices(): Promise<MediaDeviceInfo[]>;
  addEventListener?(type: "devicechange", listener: EventListener): void;
  removeEventListener?(type: "devicechange", listener: EventListener): void;
}

export function browserCameraMedia(): BrowserCameraMedia | null {
  if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) return null;
  return navigator.mediaDevices;
}
