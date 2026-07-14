import { useEffect, useMemo, useState } from "react";

import type { ActivityRun, RunMarker } from "./activity.model";
import { ActivityWorkspace } from "./components/activity-workspace.component";
import { DashboardHeader } from "./components/dashboard-header.component";
import { DayRail } from "./components/day-rail.component";
import { LevelStrip } from "./components/level-strip.component";
import { useActivityData } from "./hooks/use-activity-data.hook";
import { useLevelMetadata } from "./hooks/use-level-metadata.hook";
import { useLevelSessionData } from "./hooks/use-level-session-data.hook";
import { useReplayControl } from "./hooks/use-replay-control.hook";
import { aggregateRunMarkers, groupSessionsByDay } from "./lib/activity-data.utils";

export function ActivityDashboard() {
  const activity = useActivityData();
  const replay = useReplayControl(activity.gatewayRef, activity.status);
  const browserTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  const timeZone = browserTimeZone;
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [selectedLevelSessionId, setSelectedLevelSessionId] = useState<string | null>(null);
  const [selectedMarkerId, setSelectedMarkerId] = useState<string | null>(null);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const days = useMemo(() => groupSessionsByDay(activity.sessions, timeZone), [activity.sessions, timeZone]);
  const selectedDay = days.find((day) => day.date === selectedDate) ?? days[0] ?? null;
  const levelSessions = selectedDay?.levelSessions ?? [];
  const selectedLevel = levelSessions.find((session) => session.Id === selectedLevelSessionId) ?? levelSessions[0] ?? null;
  const levelData = useLevelSessionData(selectedLevel?.Id ?? null, selectedLevel?.ChartAvailable ?? false, selectedLevel?.RunCount ?? 0, activity.gatewayRef);
  const markers = useMemo(() => aggregateRunMarkers(levelData.runs), [levelData.runs]);
  const selectedMarker = markers.find((marker) => marker.id === selectedMarkerId) ?? null;
  const selectedRun = selectedMarker
    ? levelData.runs.find((run) => run.Id === selectedRunId && run.StartTile === selectedMarker.floorIndex) ?? null
    : null;
  const metadataFor = useLevelMetadata(levelSessions.map((session) => session.TufLevelId));
  useEffect(() => {
    if (!days.length) { setSelectedDate(null); return; }
    if (!days.some((day) => day.date === selectedDate)) setSelectedDate(days[0].date);
  }, [days, selectedDate]);
  useEffect(() => {
    if (!levelSessions.length) { setSelectedLevelSessionId(null); return; }
    if (!levelSessions.some((session) => session.Id === selectedLevelSessionId)) setSelectedLevelSessionId(levelSessions[0].Id);
  }, [levelSessions, selectedLevelSessionId]);
  useEffect(() => {
    if (selectedMarkerId && !markers.some((marker) => marker.id === selectedMarkerId)) {
      setSelectedMarkerId(null);
      setSelectedRunId(null);
    }
  }, [markers, selectedMarkerId]);
  useEffect(() => {
    if (selectedRunId && !selectedRun) setSelectedRunId(null);
  }, [selectedRun, selectedRunId]);

  const handleMarker = (marker: RunMarker | null) => {
    setSelectedMarkerId(marker?.id ?? null);
    setSelectedRunId(null);
  };
  const handleRun = (run: ActivityRun) => setSelectedRunId((current) => current === run.Id ? null : run.Id);
  const handleDate = (date: string) => {
    setSelectedDate(date);
    setSelectedMarkerId(null);
    setSelectedRunId(null);
  };
  const handleLevel = (id: string) => {
    setSelectedLevelSessionId(id);
    setSelectedMarkerId(null);
    setSelectedRunId(null);
  };
  return (
    <main className="h-screen overflow-hidden bg-background text-foreground">
      <div className="grid h-full grid-cols-[9rem_minmax(0,1fr)]">
        <DayRail days={days} selectedDate={selectedDay?.date ?? null} onSelectDate={handleDate} />
        <section className="flex min-h-0 min-w-0 flex-col border-l border-border">
          <DashboardHeader status={activity.status} onRetry={() => void activity.retry()} />
          {activity.error && !activity.sessions.length ? <div className="border-b border-destructive/40 bg-destructive/10 px-4 py-2 text-sm text-destructive">{activity.error}</div> : null}
          <LevelStrip levelSessions={levelSessions} selectedLevelSessionId={selectedLevel?.Id ?? null} timeZone={timeZone} metadataFor={metadataFor} onSelectLevelSession={handleLevel} />
          {!selectedLevel ? <div className="grid flex-1 place-items-center text-sm text-muted-foreground">{activity.status === "connecting" ? "Connecting to TUFReplay…" : "No recorded activity yet."}</div> : <ActivityWorkspace chartAvailable={selectedLevel.ChartAvailable} chart={levelData.chart} runs={levelData.runs} markers={markers} selectedMarker={selectedMarker} selectedRun={selectedRun} loading={levelData.loading} error={levelData.error} timeZone={timeZone} replayStatus={replay.status} replayPendingRunId={replay.pendingRunId} replayError={replay.error} replayErrorRunId={replay.errorRunId} onSelectMarker={handleMarker} onSelectRun={handleRun} onPlayReplay={(run) => void replay.play(run.Id)} />}
        </section>
      </div>
    </main>
  );
}
