import { Button } from "@/ui/button.component";
import { cn } from "@/ui/ui-class.utils";
import type { ConnectionStatus, MicrophoneDevice } from "../activity.model";
import { MicrophoneDeviceMenu } from "./microphone-device-menu.component";

export function DashboardHeader({
  status,
  onRetry,
  microphoneDevices,
  microphoneEnabled,
  microphoneToggleLocked,
  selectedMicrophoneDeviceId,
  microphoneLoading,
  pendingMicrophoneDeviceId,
  pendingMicrophoneEnabled,
  microphoneError,
  showMicrophoneOffsetCalibration,
  onRefreshMicrophones,
  onSetMicrophoneEnabled,
  onSelectMicrophone,
  onAdjustMicrophoneOffset,
}: {
  status: ConnectionStatus;
  onRetry: () => void;
  microphoneDevices: MicrophoneDevice[];
  microphoneEnabled: boolean;
  microphoneToggleLocked: boolean;
  selectedMicrophoneDeviceId: string | null;
  microphoneLoading: boolean;
  pendingMicrophoneDeviceId: string | null | undefined;
  pendingMicrophoneEnabled: boolean | undefined;
  microphoneError: string;
  showMicrophoneOffsetCalibration: boolean;
  onRefreshMicrophones: () => void;
  onSetMicrophoneEnabled: (enabled: boolean) => void;
  onSelectMicrophone: (deviceId: string | null) => void;
  onAdjustMicrophoneOffset: () => void;
}) {
  return (
    <header className="flex min-h-14 items-center justify-between gap-3 border-b border-border bg-background px-4 py-2">
      <h1 className="font-heading text-2xl font-semibold tracking-tight">TUFReplay</h1>
      <div className="flex items-center gap-2">
        <MicrophoneDeviceMenu
          connectionStatus={status}
          devices={microphoneDevices}
          enabled={microphoneEnabled}
          toggleLocked={microphoneToggleLocked}
          selectedDeviceId={selectedMicrophoneDeviceId}
          loading={microphoneLoading}
          pendingDeviceId={pendingMicrophoneDeviceId}
          pendingEnabled={pendingMicrophoneEnabled}
          error={microphoneError}
          showOffsetCalibration={showMicrophoneOffsetCalibration}
          onRefresh={onRefreshMicrophones}
          onSetEnabled={onSetMicrophoneEnabled}
          onSelect={onSelectMicrophone}
          onAdjustOffset={onAdjustMicrophoneOffset}
        />
        <span
          role="status"
          className={cn(
            "inline-flex h-7 items-center gap-1.5 rounded-full border px-2.5 text-[11px] font-medium",
            status === "online" && "border-primary/25 bg-primary/8 text-primary",
            status === "connecting" && "border-border bg-muted/40 text-muted-foreground",
            status === "error" && "border-amber-400/25 bg-amber-400/8 text-amber-300",
          )}
        >
          <span
            className={cn(
              "size-1.5 rounded-full bg-current",
              status === "connecting" && "animate-pulse",
            )}
          />
          {status === "online" ? "Online" : status === "connecting" ? "Connecting" : "Offline"}
        </span>
        {status === "error" ? (
          <Button size="sm" variant="ghost" onClick={onRetry}>
            Retry connection
          </Button>
        ) : null}
      </div>
    </header>
  );
}
