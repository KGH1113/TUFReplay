import { ConnectionStatePanel } from "@/components/activity/connection-state-panel";
import { DashboardHeader } from "@/components/activity/dashboard-header";
import { ReplayLevelChoiceDialog } from "@/components/replay/replay-level-choice-dialog";
import { useActivityPageViewModel } from "@/hooks/activity/use-activity-page-view-model";
import { ActivityWorkspace } from "@/sections/activity/activity-workspace";
import { DayRail } from "@/sections/activity/day-rail";
import { LevelStrip } from "@/sections/activity/level-strip";

export function ActivityPage() {
  const viewModel = useActivityPageViewModel();
  const { activity, replay, actions } = viewModel;

  return (
    <>
      <main className="h-screen overflow-hidden bg-background text-foreground">
        <div className="grid h-full grid-cols-[9rem_minmax(0,1fr)]">
          <DayRail
            days={viewModel.days}
            selectedDate={viewModel.selectedDay?.date ?? null}
            onSelectDate={actions.selectDate}
          />
          <section className="flex min-h-0 min-w-0 flex-col border-l border-border">
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
              <ConnectionStatePanel
                status={activity.status}
                error={activity.error}
                versionMismatch={activity.versionMismatch}
                onRetry={() => void actions.retry()}
              />
            ) : !viewModel.selectedLevel ? (
              activity.status !== "online" ? (
                <ConnectionStatePanel
                  status={activity.status}
                  error={activity.error}
                  versionMismatch={activity.versionMismatch}
                  onRetry={() => void actions.retry()}
                />
              ) : viewModel.selectedDay &&
                !viewModel.selectedDay.hasOpenableLevels &&
                viewModel.levelSessions.length > 0 ? (
                <div className="grid flex-1 place-items-center px-6 text-center">
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
                <div className="grid flex-1 place-items-center text-sm text-muted-foreground">
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
    </>
  );
}
