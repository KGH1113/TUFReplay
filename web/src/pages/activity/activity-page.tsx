import { ActivityEmptyState, MainAreaState } from "@/components/activity/activity-empty-state";
import { AutoSubmissionBuildNotice } from "@/components/activity/auto-submission-build-notice";
import { ConnectionStatePanel } from "@/components/activity/connection-state-panel";
import { LegacyReplayNoticeDialog } from "@/components/activity/legacy-replay-notice-dialog";
import { OfflineBanner } from "@/components/activity/offline-banner";
import { RenderComingSoonDialog } from "@/components/render/render-coming-soon-dialog";
import { ReplayLevelChoiceDialog } from "@/components/replay/replay-level-choice-dialog";
import { useActivityPageViewModel } from "@/hooks/activity/use-activity-page-view-model";
import { useLegacyReplayNotice } from "@/hooks/activity/use-legacy-replay-notice";
import { useRenderComingSoon } from "@/hooks/render/use-render-coming-soon";
import { useSidebarDrawer } from "@/hooks/shared/use-sidebar-drawer";
import { ActivityWorkspace } from "@/sections/activity/activity-workspace";
import { AppSidebar } from "@/sections/activity/app-sidebar";
import { TUFREPLAY_WEB_BUILD } from "@/shared/config/tufreplay-build-info";
import { Toaster } from "@/shared/ui/sonner";
import { TooltipProvider } from "@/shared/ui/tooltip";

export function ActivityPage() {
  const viewModel = useActivityPageViewModel();
  const renderNotice = useRenderComingSoon();
  const drawer = useSidebarDrawer();
  const { activity, replay, actions } = viewModel;
  const legacyReplayNotice = useLegacyReplayNotice(activity.status);
  const retry = () => void actions.retry();
  const readOnly = activity.status !== "online";
  const selectedLevel = viewModel.selectedLevel;

  return (
    <TooltipProvider>
      <div
        data-tufreplay-environment={TUFREPLAY_WEB_BUILD.environment}
        data-tufreplay-build-flavor={TUFREPLAY_WEB_BUILD.flavor}
        data-tufreplay-build-origin={TUFREPLAY_WEB_BUILD.expectedOrigin}
        data-tufreplay-build-sha={TUFREPLAY_WEB_BUILD.buildSha ?? undefined}
        className="flex h-dvh overflow-hidden bg-background text-foreground"
      >
        <AppSidebar
          status={activity.status}
          mockEnabled={activity.mockEnabled}
          days={viewModel.days}
          selectedDate={viewModel.selectedDay?.date ?? null}
          selectedLevelGroupId={selectedLevel?.levelGroupId ?? null}
          todayDate={viewModel.todayDate}
          timeZone={viewModel.timeZone}
          metadataFor={viewModel.metadataFor}
          mobileOpen={drawer.open}
          onMobileOpenChange={drawer.setOpen}
          onRetry={retry}
          onSelectLevel={actions.selectLevel}
          onDeleteDay={actions.deleteDay}
        />
        <main className="flex min-w-0 flex-1 flex-col">
          {activity.status !== "incompatible" && viewModel.days.length > 0 ? (
            <OfflineBanner status={activity.status} onRetry={retry} />
          ) : null}
          <AutoSubmissionBuildNotice health={activity.health} />
          {activity.status === "incompatible" || (!selectedLevel && readOnly) ? (
            <MainAreaState onOpenSidebar={drawer.show}>
              <ConnectionStatePanel
                status={activity.status}
                error={activity.error}
                versionMismatch={activity.versionMismatch}
                onRetry={retry}
              />
            </MainAreaState>
          ) : !selectedLevel ? (
            <MainAreaState onOpenSidebar={drawer.show}>
              {viewModel.selectedDay &&
              !viewModel.selectedDay.hasOpenableLevels &&
              viewModel.levelSessions.length > 0 ? (
                <ActivityEmptyState
                  title={viewModel.copy.unavailableTitle}
                  body={viewModel.copy.unavailableBody}
                />
              ) : (
                <ActivityEmptyState title={viewModel.copy.empty} body={viewModel.copy.emptyBody} />
              )}
            </MainAreaState>
          ) : (
            <ActivityWorkspace
              level={selectedLevel}
              metadata={viewModel.metadataFor(selectedLevel)}
              summary={viewModel.levelSummary}
              chart={viewModel.levelData.chart}
              runs={viewModel.levelData.runs}
              markers={viewModel.markers}
              selectedMarker={viewModel.selectedMarker}
              selectedRun={viewModel.selectedRun}
              loading={viewModel.levelData.loading}
              error={viewModel.levelData.error}
              chartError={viewModel.levelData.chartError}
              readOnly={readOnly}
              timeZone={viewModel.timeZone}
              replayStatus={replay.status}
              replayPendingRunId={replay.pendingRunId}
              replayError={replay.error}
              replayErrorRunId={replay.errorRunId}
              recordingActions={viewModel.recordingActions}
              onRetry={viewModel.levelData.retry}
              onOpenSidebar={drawer.show}
              onSelectMarker={actions.selectMarker}
              onToggleRun={actions.toggleRun}
              onFocusRun={actions.focusRun}
              onPlayReplay={actions.openReplayChoice}
              onRender={renderNotice.show}
              onDeleteRun={actions.deleteRun}
              onSetRunLocked={actions.setRunLocked}
            />
          )}
        </main>
      </div>
      <ReplayLevelChoiceDialog
        run={viewModel.replayChoiceRun}
        pickerResult={replay.pickerResult}
        pickingRunId={replay.pickingRunId}
        replayStatus={replay.status}
        playError={replay.error}
        playErrorRunId={replay.errorRunId}
        onClose={actions.closeReplayChoice}
        onPlay={replay.play}
        onChooseAnother={replay.pickLevelFile}
        onResetPicker={replay.clearLevelFilePicker}
      />
      <LegacyReplayNoticeDialog
        open={legacyReplayNotice.open}
        onConfirm={legacyReplayNotice.confirm}
      />
      <RenderComingSoonDialog notice={renderNotice} />
      <Toaster position="bottom-center" />
    </TooltipProvider>
  );
}
