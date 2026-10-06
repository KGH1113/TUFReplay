import type { WebcamStateDto } from "@/schemas/webcam/webcam-schema";

export type { CameraCrop, WebcamSettingsPatch } from "@/schemas/webcam/webcam-schema";
export type WebcamState = ReturnType<typeof mapWebcamState>;

export function mapWebcamState(dto: WebcamStateDto) {
  return {
    supported: dto.Supported,
    backend: dto.Backend,
    enabled: dto.Enabled,
    captureLocked: dto.CaptureLocked,
    status: dto.Status,
    captureError: dto.Error,
    devices: dto.Devices.map((device) => ({ id: device.Id, name: device.Name })),
    deviceId: dto.SelectedDeviceId,
    quality: dto.Quality,
    offsetMs: dto.OffsetMs,
    storageLimitMb: dto.StorageLimitMb,
    retentionDays: dto.RetentionDays,
    playbackVisible: dto.PlaybackVisible,
    liveVisible: dto.LiveVisible,
    mirror: dto.Mirror,
    flipVertical: dto.FlipVertical,
    crop: dto.Crop,
  };
}
