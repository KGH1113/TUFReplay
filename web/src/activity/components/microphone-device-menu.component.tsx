import {
  ArrowDown01Icon,
  Loading03Icon,
  Mic01Icon,
  Tick02Icon,
  WaveSquareIcon,
} from "@hugeicons/core-free-icons";
import { HugeiconsIcon } from "@hugeicons/react";
import { DropdownMenu as DropdownMenuPrimitive } from "radix-ui";

import { Button } from "@/ui/button.component";
import type { ConnectionStatus, MicrophoneDevice } from "../activity.model";

const SYSTEM_DEFAULT_VALUE = "__tufreplay_system_default__";

export function MicrophoneDeviceMenu({
  connectionStatus,
  devices,
  selectedDeviceId,
  loading,
  pendingDeviceId,
  error,
  showOffsetCalibration,
  onRefresh,
  onSelect,
  onAdjustOffset,
}: {
  connectionStatus: ConnectionStatus;
  devices: MicrophoneDevice[];
  selectedDeviceId: string | null;
  loading: boolean;
  pendingDeviceId: string | null | undefined;
  error: string;
  showOffsetCalibration: boolean;
  onRefresh: () => void;
  onSelect: (deviceId: string | null) => void;
  onAdjustOffset: () => void;
}) {
  const connected = connectionStatus === "online";
  const selectedName =
    selectedDeviceId === null
      ? "System default"
      : (devices.find((device) => device.Id === selectedDeviceId)?.Name ?? selectedDeviceId);
  const selectedValue = selectedDeviceId ?? SYSTEM_DEFAULT_VALUE;
  const saving = pendingDeviceId !== undefined;

  return (
    <DropdownMenuPrimitive.Root onOpenChange={(open) => open && onRefresh()}>
      <DropdownMenuPrimitive.Trigger asChild>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          disabled={!connected}
          aria-label={`Select microphone. Current input: ${selectedName}`}
          title={
            connected
              ? `Microphone: ${selectedName}`
              : "Connect to TUFReplay to select a microphone"
          }
          className="rounded-full text-muted-foreground hover:text-foreground data-[state=open]:bg-muted/60 data-[state=open]:text-foreground"
        >
          <HugeiconsIcon aria-hidden="true" icon={Mic01Icon} size={17} strokeWidth={2} />
        </Button>
      </DropdownMenuPrimitive.Trigger>
      <DropdownMenuPrimitive.Portal>
        <DropdownMenuPrimitive.Content
          align="end"
          sideOffset={8}
          collisionPadding={12}
          className="z-50 w-[min(20rem,calc(100vw-1.5rem))] origin-[var(--radix-dropdown-menu-content-transform-origin)] rounded-xl border border-border bg-popover p-1.5 text-popover-foreground shadow-xl outline-none data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
        >
          <div className="flex items-center justify-between gap-3 px-2.5 py-2">
            <div className="min-w-0">
              <p className="text-sm font-semibold">Microphone input</p>
              <p className="truncate text-xs text-muted-foreground">{selectedName}</p>
            </div>
            {loading || saving ? (
              <HugeiconsIcon
                aria-label={saving ? "Saving microphone" : "Refreshing microphones"}
                icon={Loading03Icon}
                size={15}
                strokeWidth={2}
                className="animate-spin text-muted-foreground"
              />
            ) : (
              <HugeiconsIcon
                aria-hidden="true"
                icon={ArrowDown01Icon}
                size={14}
                strokeWidth={2}
                className="text-muted-foreground"
              />
            )}
          </div>
          <DropdownMenuPrimitive.Separator className="my-1 h-px bg-border" />
          <DropdownMenuPrimitive.RadioGroup
            value={selectedValue}
            onValueChange={(value) => onSelect(value === SYSTEM_DEFAULT_VALUE ? null : value)}
            className="max-h-72 overflow-y-auto"
          >
            <MicrophoneItem
              value={SYSTEM_DEFAULT_VALUE}
              name="System default"
              description="Use the operating system input device"
              disabled={saving}
            />
            {devices.map((device) => (
              <MicrophoneItem
                key={device.Id}
                value={device.Id}
                name={device.Name}
                description={frequencyDescription(device)}
                disabled={saving}
              />
            ))}
          </DropdownMenuPrimitive.RadioGroup>
          {!loading && devices.length === 0 ? (
            <p className="px-2.5 py-2 text-xs text-muted-foreground">
              No microphone devices were detected.
            </p>
          ) : null}
          {error ? (
            <p
              role="alert"
              className="mx-1 mt-1 rounded-md bg-destructive/10 px-2 py-1.5 text-xs text-destructive"
            >
              {error}
            </p>
          ) : null}
          {showOffsetCalibration ? (
            <>
              <DropdownMenuPrimitive.Separator className="my-1 h-px bg-border" />
              <DropdownMenuPrimitive.Item
                onSelect={onAdjustOffset}
                className="flex cursor-default select-none items-center gap-3 rounded-lg px-2.5 py-2 outline-none transition-colors data-[highlighted]:bg-muted/70"
              >
                <span className="grid size-8 shrink-0 place-items-center rounded-full bg-primary/15 text-primary">
                  <HugeiconsIcon
                    aria-hidden="true"
                    icon={WaveSquareIcon}
                    size={16}
                    strokeWidth={2}
                  />
                </span>
                <span className="min-w-0">
                  <span className="block text-sm font-medium">Adjust timing offset</span>
                  <span className="block truncate text-xs text-muted-foreground">
                    Align microphone and game audio
                  </span>
                </span>
              </DropdownMenuPrimitive.Item>
            </>
          ) : null}
        </DropdownMenuPrimitive.Content>
      </DropdownMenuPrimitive.Portal>
    </DropdownMenuPrimitive.Root>
  );
}

function MicrophoneItem({
  value,
  name,
  description,
  disabled,
}: {
  value: string;
  name: string;
  description: string;
  disabled: boolean;
}) {
  return (
    <DropdownMenuPrimitive.RadioItem
      value={value}
      disabled={disabled}
      onSelect={(event) => event.preventDefault()}
      className="relative flex cursor-default select-none items-center rounded-lg py-2 pl-2.5 pr-9 outline-none transition-colors data-[disabled]:pointer-events-none data-[disabled]:opacity-50 data-[highlighted]:bg-muted/70"
    >
      <div className="min-w-0">
        <p className="truncate text-sm font-medium">{name}</p>
        <p className="truncate text-xs text-muted-foreground">{description}</p>
      </div>
      <DropdownMenuPrimitive.ItemIndicator className="absolute right-2.5 grid size-5 place-items-center rounded-full bg-primary/15 text-primary">
        <HugeiconsIcon aria-hidden="true" icon={Tick02Icon} size={13} strokeWidth={2.4} />
      </DropdownMenuPrimitive.ItemIndicator>
    </DropdownMenuPrimitive.RadioItem>
  );
}

function frequencyDescription(device: MicrophoneDevice) {
  if (device.MinFrequency <= 0 || device.MaxFrequency <= 0) return "Available input device";
  if (device.MinFrequency === device.MaxFrequency)
    return `${device.MaxFrequency.toLocaleString()} Hz`;
  return `${device.MinFrequency.toLocaleString()}–${device.MaxFrequency.toLocaleString()} Hz`;
}
