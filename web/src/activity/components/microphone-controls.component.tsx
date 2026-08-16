import { memo, type RefObject } from "react";

import type { ConnectionStatus } from "../activity.model";
import type { ActivityGateway } from "../data/activity.gateway";
import { useMicrophoneDevices } from "../hooks/use-microphone-devices.hook";
import { useMicrophoneOffsetCalibration } from "../hooks/use-microphone-offset-calibration.hook";
import { MicrophoneDeviceMenu } from "./microphone-device-menu.component";
import { MicrophoneOffsetCalibrationDialog } from "./microphone-offset-calibration-dialog.component";

export const MicrophoneControls = memo(function MicrophoneControls({
  gatewayRef,
  connectionStatus,
  mockEnabled,
}: {
  gatewayRef: RefObject<ActivityGateway | null>;
  connectionStatus: ConnectionStatus;
  mockEnabled: boolean;
}) {
  const microphones = useMicrophoneDevices(gatewayRef, connectionStatus);
  const microphoneOffset = useMicrophoneOffsetCalibration(
    gatewayRef,
    connectionStatus,
    mockEnabled,
    microphones.microphoneOffsetMs,
    microphones.microphoneVolumeDb,
    microphones.applyTimingSettings,
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
