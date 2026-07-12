import { useMemo, useState } from "react";

import {
  mockActivityDay,
  mockActivityDays,
  mockActivityLevelSession,
} from "./activity.mock";
import { DayRail } from "./components/day-rail.component";
import { DashboardHeader } from "./components/dashboard-header.component";
import { LevelStrip } from "./components/level-strip.component";
import { SegmentWorkspace } from "./chart/segment-workspace.component";
import { getSegmentGroups } from "./lib/activity-selectors";

export function ActivityDashboard() {
  const levelSessions = useMemo(
    () => mockActivityDay.AppSessions.flatMap((appSession) => appSession.LevelSessions),
    [],
  );
  const [selectedDate, setSelectedDate] = useState(mockActivityDay.Date);
  const [selectedLevelSessionId, setSelectedLevelSessionId] = useState(mockActivityLevelSession.Session.Id);
  const [selectedSegmentGroupIndex, setSelectedSegmentGroupIndex] = useState(0);

  const selectedDay = mockActivityDays.find((day) => day.Date === selectedDate) ?? mockActivityDays[0];
  const selectedLevelSession =
    levelSessions.find((levelSession) => levelSession.Id === selectedLevelSessionId) ?? mockActivityLevelSession.Session;
  const segmentGroups = getSegmentGroups(selectedLevelSession);

  return (
    <main className="min-h-screen bg-background text-foreground">
      <div className="grid min-h-screen grid-cols-[9rem_minmax(0,1fr)]">
        <DayRail selectedDate={selectedDate} selectedDay={selectedDay} onSelectDate={setSelectedDate} />
        <section className="flex min-h-screen min-w-0 flex-col border-l border-border">
          <DashboardHeader />
          <LevelStrip
            levelSessions={levelSessions}
            selectedLevelSessionId={selectedLevelSession.Id}
            onSelectLevelSession={(id) => {
              setSelectedLevelSessionId(id);
              setSelectedSegmentGroupIndex(0);
            }}
          />
          <SegmentWorkspace
            levelSession={selectedLevelSession}
            segmentGroups={segmentGroups}
            selectedSegmentGroupIndex={selectedSegmentGroupIndex}
            onSelectSegmentGroup={setSelectedSegmentGroupIndex}
          />
        </section>
      </div>
    </main>
  );
}

