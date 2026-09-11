import { ConnectionStatePanel } from "@/components/activity/connection-state-panel";
import { DashboardHeader } from "@/components/activity/dashboard-header";
import { LegacyReplayNoticeDialog } from "@/components/activity/legacy-replay-notice-dialog";
import { ReplayLevelChoiceDialog } from "@/components/replay/replay-level-choice-dialog";
import { useActivityPageViewModel } from "@/hooks/activity/use-activity-page-view-model";
import { useLegacyReplayNotice } from "@/hooks/activity/use-legacy-replay-notice";
import { ActivityWorkspace } from "@/sections/activity/activity-workspace";
import { DayRail } from "@/sections/activity/day-rail";
import { LevelStrip } from "@/sections/activity/level-strip";

export function ActivityPage() {
  const viewModel = useActivityPageViewModel();
  const { activity, replay, actions } = viewModel;
  const legacyReplayNotice = useLegacyReplayNotice(activity.status);

  return (
    <>
      <main className="h-screen overflow-hidden bg-muted/20 p-3 text-foreground">
        <div className="grid h-full grid-cols-[9rem_minmax(0,1fr)] gap-3">
          <DayRail
            days={viewModel.days}
            selectedDate={viewModel.selectedDay?.date ?? null}
            onSelectDate={actions.selectDate}
          />
          <section className="flex min-h-0 min-w-0 flex-col gap-3">
            <DashboardHeader
              status={activity.status}
              onRetry={() => void actions.retry()}
              mockEnabled={activity.mockEnabled}
            />
            <LevelStrip
              levelSessions={viewModel.levelSessions}
              selectedLevelGroupId={viewModel.selectedLevel?.levelGroupId ?? null}
              timeZone={viewModel.timeZone}
              metadataFor={viewModel.metadataFor}
              onSelectLevelGroup={actions.selectLevel}
            />
            {activity.status === "incompatible" ? (
              <div className="grid min-h-0 flex-1 place-items-center overflow-hidden rounded-2xl bg-black/30 shadow-2xl ring-1 ring-foreground/10">
                <ConnectionStatePanel
                  status={activity.status}
                  error={activity.error}
                  versionMismatch={activity.versionMismatch}
                  onRetry={() => void actions.retry()}
                />
              </div>
            ) : !viewModel.selectedLevel ? (
              activity.status !== "online" ? (
                <div className="grid min-h-0 flex-1 place-items-center overflow-hidden rounded-2xl bg-black/30 shadow-2xl ring-1 ring-foreground/10">
                  <ConnectionStatePanel
                    status={activity.status}
                    error={activity.error}
                    versionMismatch={activity.versionMismatch}
                    onRetry={() => void actions.retry()}
                  />
                </div>
              ) : viewModel.selectedDay &&
                !viewModel.selectedDay.hasOpenableLevels &&
                viewModel.levelSessions.length > 0 ? (
                <div className="grid flex-1 place-items-center overflow-hidden rounded-2xl bg-black/30 px-6 text-center shadow-2xl ring-1 ring-foreground/10">
                  <div className="max-w-md">
                    <h2 className="font-heading text-lg font-semibold">
                      {viewModel.copy.unavailableTitle}
                    </h2>
                    <p className="mt-2 text-sm text-muted-foreground">
                      {viewModel.copy.unavailableBody}
                    </p>
                  </div>
                </div>
              ) : (
                <div className="grid flex-1 place-items-center overflow-hidden rounded-2xl bg-black/30 text-sm text-muted-foreground shadow-2xl ring-1 ring-foreground/10">
                  {viewModel.copy.empty}
                </div>
              )
            ) : (
              <ActivityWorkspace
                chartAvailable={viewModel.selectedLevel.chartAvailable}
                chart={viewModel.levelData.chart}
                runs={viewModel.levelData.runs}
                markers={viewModel.markers}
                selectedMarker={viewModel.selectedMarker}
                selectedRun={viewModel.selectedRun}
                loading={viewModel.levelData.loading}
                error={viewModel.levelData.error}
                readOnly={activity.status !== "online"}
                timeZone={viewModel.timeZone}
                replayStatus={replay.status}
                replayPendingRunId={replay.pendingRunId}
                replayError={replay.error}
                replayErrorRunId={replay.errorRunId}
                onSelectMarker={actions.selectMarker}
                onSelectRun={actions.selectRun}
                onPlayReplay={actions.openReplayChoice}
                onDeleteRun={actions.deleteRun}
                onDeleteMicrophoneRecording={actions.deleteMicrophoneRecording}
                onKeepMicrophoneRecording={actions.keepMicrophoneRecording}
              />
            )}
          </section>
        </div>
      </main>
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
    </>
  );
}
