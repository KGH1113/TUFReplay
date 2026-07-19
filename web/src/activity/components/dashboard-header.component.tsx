import { Button } from "@/ui/button.component";
import { cn } from "@/ui/ui-class.utils";
import type { ConnectionStatus, MicrophoneDevice } from "../activity.model";
import { MicrophoneDeviceMenu } from "./microphone-device-menu.component";

export function DashboardHeader({
  status,
  onRetry,
  microphoneDevices,
  selectedMicrophoneDeviceId,
  microphoneLoading,
  pendingMicrophoneDeviceId,
  microphoneError,
  showMicrophoneOffsetCalibration,
  onRefreshMicrophones,
  onSelectMicrophone,
  onAdjustMicrophoneOffset,
}: {
  status: ConnectionStatus;
  onRetry: () => void;
  microphoneDevices: MicrophoneDevice[];
  selectedMicrophoneDeviceId: string | null;
  microphoneLoading: boolean;
  pendingMicrophoneDeviceId: string | null | undefined;
  microphoneError: string;
  showMicrophoneOffsetCalibration: boolean;
  onRefreshMicrophones: () => void;
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
          selectedDeviceId={selectedMicrophoneDeviceId}
          loading={microphoneLoading}
          pendingDeviceId={pendingMicrophoneDeviceId}
          error={microphoneError}
          showOffsetCalibration={showMicrophoneOffsetCalibration}
          onRefresh={onRefreshMicrophones}
          onSelect={onSelectMicrophone}
          onAdjustOffset={onAdjustMicrophoneOffset}
        />
        <span
          role="status"
          aria-label={status}
          title={status}
          className={cn(
            "size-2 rounded-full bg-muted-foreground",
            status === "online" && "bg-[#7DCF00]",
            status === "error" && "bg-destructive",
          )}
        />
        {status === "error" ? (
          <Button size="sm" onClick={onRetry}>
            Retry
          </Button>
        ) : null}
      </div>
    </header>
  );
}
