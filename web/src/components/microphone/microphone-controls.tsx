import { memo } from "react";
import { MicrophoneOffsetCalibrationDialog } from "@/components/calibration/microphone-offset-calibration-dialog";
import { MicrophoneDeviceMenu } from "@/components/microphone/microphone-device-menu";
import { useCalibrationGatewayAdapter } from "@/hooks/calibration/use-calibration-gateway-adapter";
import { useMicrophoneOffsetCalibration } from "@/hooks/calibration/use-microphone-offset-calibration";
import { useMicrophoneDevices } from "@/hooks/microphone/use-microphone-devices";
import type { ConnectionStatus } from "@/models/activity/activity-model";

export const MicrophoneControls = memo(function MicrophoneControls({
  connectionStatus,
  mockEnabled,
}: {
  connectionStatus: ConnectionStatus;
  mockEnabled: boolean;
}) {
  const calibrationGatewayRef = useCalibrationGatewayAdapter();
  const microphones = useMicrophoneDevices(connectionStatus);
  const microphoneOffset = useMicrophoneOffsetCalibration(
    calibrationGatewayRef,
    connectionStatus,
    mockEnabled,
    microphones.microphoneOffsetMs,
    microphones.microphoneVolumeDb,
    (settings) =>
      microphones.applyTimingSettings({
        microphoneOffsetMs: settings.MicrophoneOffsetMs,
        microphoneVolumeDb: settings.MicrophoneVolumeDb,
      }),
  );

  return (
    <>
      <MicrophoneDeviceMenu
        connectionStatus={connectionStatus}
        devices={microphones.devices}
        enabled={microphones.enabled}
        toggleLocked={microphones.toggleLocked}
        selectedDeviceId={microphones.selectedDeviceId}
        loading={microphones.loading}
        pendingDeviceId={microphones.pendingDeviceId}
        pendingEnabled={microphones.pendingEnabled}
        error={microphones.error}
        showOffsetCalibration={connectionStatus === "online"}
        offsetAdjustmentLocked={microphones.toggleLocked}
        onRefresh={microphones.refreshIfStale}
        onSetEnabled={(enabled) => void microphones.setEnabled(enabled)}
        onSelect={(deviceId) => void microphones.select(deviceId)}
        onAdjustOffset={microphoneOffset.openSettings}
      />
      <MicrophoneOffsetCalibrationDialog
        data={microphoneOffset.data}
        phase={microphoneOffset.phase}
        offsetMs={microphoneOffset.offsetMs}
        microphoneVolumeDb={microphoneOffset.microphoneVolumeDb}
        playing={microphoneOffset.playing}
        playbackPositionMs={microphoneOffset.playbackPositionMs}
        getPlaybackPositionMs={microphoneOffset.getPlaybackPositionMs}
        audioError={microphoneOffset.audioError}
        onClose={microphoneOffset.close}
        onStartCalibration={() => void microphoneOffset.start()}
        onCommitOffset={microphoneOffset.commitOffset}
        onCommitMicrophoneVolume={microphoneOffset.commitMicrophoneVolume}
        onResetOffset={microphoneOffset.resetOffset}
        onTogglePlayback={() => void microphoneOffset.togglePlayback()}
      />
    </>
  );
});
