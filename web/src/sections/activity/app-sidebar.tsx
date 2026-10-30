import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import {
  ConnectionRetryButton,
  ConnectionStatusText,
} from "@/components/activity/connection-status";
import type { DayDeleteOutcome } from "@/components/activity/day-delete-dialog";
import { LanguageMenu } from "@/components/activity/language-menu";
import { DownloadCenter } from "@/components/downloads/download-center";
import { MicrophoneControls } from "@/components/microphone/microphone-controls";
import { WebcamControls } from "@/components/webcam/webcam-controls";
import type {
  ActivityDay,
  ConnectionStatus,
  LevelCard,
  LevelMetadata,
} from "@/models/activity/activity-model";
import { cn } from "@/shared/lib/cn";
import { DayList } from "./day-list";

/**
 * Navigation on the left: where you are (days and their levels) and how the recorder is set up.
 * Below the large breakpoint it becomes a drawer over the content.
 */
export function AppSidebar({
  status,
  mockEnabled,
  days,
  selectedDate,
  selectedLevelGroupId,
  todayDate,
  timeZone,
  metadataFor,
  mobileOpen,
  onMobileOpenChange,
  onRetry,
  onSelectLevel,
  onDeleteDay,
}: {
  status: ConnectionStatus;
  mockEnabled: boolean;
  days: ActivityDay[];
  selectedDate: string | null;
  selectedLevelGroupId: string | null;
  todayDate: string;
  timeZone: string;
  metadataFor: (level: LevelCard) => LevelMetadata;
  mobileOpen: boolean;
  onMobileOpenChange: (open: boolean) => void;
  onRetry: () => void;
  onSelectLevel: (date: string, levelGroupId: string) => void;
  onDeleteDay: (day: ActivityDay, keepClears: boolean) => Promise<DayDeleteOutcome>;
}) {
  const { t } = useTranslation("common");

  useEffect(() => {
    if (!mobileOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onMobileOpenChange(false);
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [mobileOpen, onMobileOpenChange]);

  return (
    <>
      <div
        aria-hidden="true"
        onClick={() => onMobileOpenChange(false)}
        className={cn(
          "fixed inset-0 z-30 bg-background/70 backdrop-blur-[2px] transition-opacity duration-200 lg:hidden",
          mobileOpen ? "opacity-100" : "pointer-events-none opacity-0",
        )}
      />
      <aside
        aria-label={t("sidebar.label")}
        className={cn(
          "fixed inset-y-0 left-0 z-40 flex w-[min(22rem,85vw)] shrink-0 flex-col overflow-x-hidden border-r border-sidebar-border bg-sidebar text-sidebar-foreground transition-transform duration-300 ease-out-quint motion-reduce:transition-none lg:static lg:z-auto lg:translate-x-0",
          mobileOpen ? "translate-x-0 shadow-2xl" : "-translate-x-full",
        )}
      >
        <div className="flex shrink-0 items-center gap-2 px-4 pt-4 pb-2">
          <div className="min-w-0 flex-1">
            <p className="truncate text-[15px] leading-tight font-semibold tracking-tight">
              {t("appName")}
            </p>
            <ConnectionStatusText status={status} mockEnabled={mockEnabled} />
          </div>
          <ConnectionRetryButton status={status} onRetry={onRetry} />
        </div>
        <DayList
          days={days}
          selectedDate={selectedDate}
          selectedLevelGroupId={selectedLevelGroupId}
          todayDate={todayDate}
          timeZone={timeZone}
          readOnly={status !== "online"}
          metadataFor={metadataFor}
          onSelectLevel={(date, levelGroupId) => {
            onSelectLevel(date, levelGroupId);
            onMobileOpenChange(false);
          }}
          onDeleteDay={onDeleteDay}
        />
        <section
          aria-label={t("settings.label")}
          className="shrink-0 border-t border-sidebar-border px-2 pt-2 pb-3"
        >
          <div className="grid grid-cols-2 gap-1">
            <MicrophoneControls connectionStatus={status} mockEnabled={mockEnabled} />
            <WebcamControls connectionStatus={status} />
            <DownloadCenter connectionStatus={status} />
            <LanguageMenu />
          </div>
        </section>
      </aside>
    </>
  );
}
