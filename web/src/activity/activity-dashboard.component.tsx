import { useEffect, useMemo, useState } from "react";

import type { RunMarker } from "./activity.model";
import { ActivityWorkspace } from "./components/activity-workspace.component";
import { DashboardHeader } from "./components/dashboard-header.component";
import { DayRail } from "./components/day-rail.component";
import { LevelStrip } from "./components/level-strip.component";
import { useActivityData } from "./hooks/use-activity-data.hook";
import { useLevelMetadata } from "./hooks/use-level-metadata.hook";
import { useLevelSessionData } from "./hooks/use-level-session-data.hook";
import { aggregateRunMarkers, groupSessionsByDay } from "./lib/activity-data.utils";

export function ActivityDashboard() {
  const activity = useActivityData();
  const browserTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  const [timeZone, setTimeZone] = useState(browserTimeZone);
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [selectedLevelSessionId, setSelectedLevelSessionId] = useState<string | null>(null);
  const [selectedMarkerId, setSelectedMarkerId] = useState<string | null>(null);
  const days = useMemo(() => groupSessionsByDay(activity.sessions, timeZone), [activity.sessions, timeZone]);
  const selectedDay = days.find((day) => day.date === selectedDate) ?? days[0] ?? null;
  const levelSessions = selectedDay?.levelSessions ?? [];
  const selectedLevel = levelSessions.find((session) => session.Id === selectedLevelSessionId) ?? levelSessions[0] ?? null;
  const levelData = useLevelSessionData(selectedLevel?.Id ?? null, selectedLevel?.ChartAvailable ?? false, selectedLevel?.RunCount ?? 0, activity.gatewayRef);
  const markers = useMemo(() => aggregateRunMarkers(levelData.runs), [levelData.runs]);
  const selectedMarker = markers.find((marker) => marker.id === selectedMarkerId) ?? null;
  const metadataFor = useLevelMetadata(levelSessions.map((session) => session.TufLevelId));
  const timeZones = useMemo(() => [...new Set([
    browserTimeZone,
    timeZone,
    ...activity.sessions.map((session) => session.RecorderTimeZoneId).filter((zone): zone is string => Boolean(zone)),
  ])], [activity.sessions, browserTimeZone, timeZone]);

  useEffect(() => {
    if (!days.length) { setSelectedDate(null); return; }
    if (!days.some((day) => day.date === selectedDate)) setSelectedDate(days[0].date);
  }, [days, selectedDate]);
  useEffect(() => {
    if (!levelSessions.length) { setSelectedLevelSessionId(null); return; }
    if (!levelSessions.some((session) => session.Id === selectedLevelSessionId)) setSelectedLevelSessionId(levelSessions[0].Id);
  }, [levelSessions, selectedLevelSessionId]);
  useEffect(() => {
    if (selectedMarkerId && !markers.some((marker) => marker.id === selectedMarkerId)) setSelectedMarkerId(null);
  }, [markers, selectedMarkerId]);

  const handleMarker = (marker: RunMarker | null) => setSelectedMarkerId(marker?.id ?? null);
  return (
    <main className="min-h-screen bg-background text-foreground">
      <div className="grid min-h-screen grid-cols-[9rem_minmax(0,1fr)]">
        <DayRail days={days} selectedDate={selectedDay?.date ?? null} onSelectDate={setSelectedDate} />
        <section className="flex min-h-screen min-w-0 flex-col border-l border-border">
          <DashboardHeader status={activity.status} timeZone={timeZone} timeZones={timeZones} onTimeZoneChange={setTimeZone} onRetry={() => void activity.retry()} />
          {activity.error && !activity.sessions.length ? <div className="border-b border-destructive/40 bg-destructive/10 px-4 py-2 text-sm text-destructive">{activity.error}</div> : null}
          <LevelStrip levelSessions={levelSessions} selectedLevelSessionId={selectedLevel?.Id ?? null} timeZone={timeZone} metadataFor={metadataFor} onSelectLevelSession={(id) => { setSelectedLevelSessionId(id); setSelectedMarkerId(null); }} />
          {!selectedLevel ? <div className="grid flex-1 place-items-center text-sm text-muted-foreground">{activity.status === "connecting" ? "Connecting to TUFReplay…" : "No recorded activity yet."}</div> : <ActivityWorkspace chartAvailable={selectedLevel.ChartAvailable} chart={levelData.chart} runs={levelData.runs} markers={markers} selectedMarker={selectedMarker} loading={levelData.loading} error={levelData.error} timeZone={timeZone} onSelectMarker={handleMarker} />}
        </section>
      </div>
    </main>
  );
}
