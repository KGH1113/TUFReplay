import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useActivityData } from "@/hooks/activity/use-activity-data";
import { useLevelMetadata } from "@/hooks/activity/use-level-metadata";
import { useLevelSessionData } from "@/hooks/activity/use-level-session-data";
import { useRunActions } from "@/hooks/activity/use-run-actions";
import { useReplayControl } from "@/hooks/replay/use-replay-control";
import { aggregateRunMarkers, groupSessionsByDay } from "@/models/activity/activity-data";
import type { ActivityRun, RunMarker } from "@/models/activity/activity-model";

export function useActivityPageViewModel() {
  const { t } = useTranslation("activity");
  const activity = useActivityData();
  const runActions = useRunActions();
  const replay = useReplayControl(activity.status);
  const clearLevelFilePicker = replay.clearLevelFilePicker;
  const timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || "UTC";
  const [selectedDate, setSelectedDate] = useState<string | null>(null);
  const [selectedLevelGroupId, setSelectedLevelGroupId] = useState<string | null>(null);
  const [selectedMarkerId, setSelectedMarkerId] = useState<string | null>(null);
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const [firstMarkerLevelSessionId, setFirstMarkerLevelSessionId] = useState<string | null>(null);
  const [replayChoiceRun, setReplayChoiceRun] = useState<ActivityRun | null>(null);

  const days = useMemo(
    () => groupSessionsByDay(activity.sessions, timeZone),
    [activity.sessions, timeZone],
  );
  const selectedDay = days.find((day) => day.date === selectedDate) ?? days[0] ?? null;
  const selectedAppSessionIds = useMemo(
    () => selectedDay?.appSessions.map((session) => session.id) ?? [],
    [selectedDay],
  );
  const levelSessions = selectedDay?.levelSessions ?? [];
  const selectedLevel =
    levelSessions.find(
      (session) => session.levelGroupId === selectedLevelGroupId && session.canOpen,
    ) ??
    levelSessions.find((session) => session.canOpen) ??
    null;
  const levelData = useLevelSessionData(
    selectedLevel?.id ?? null,
    selectedAppSessionIds,
    selectedLevel?.chartAvailable ?? false,
    selectedLevel?.runCount ?? 0,
    selectedDay?.date ?? null,
  );
  const markers = useMemo(() => aggregateRunMarkers(levelData.runs), [levelData.runs]);
  const selectedMarker = markers.find((marker) => marker.id === selectedMarkerId) ?? null;
  const selectedRun = selectedMarker
    ? (levelData.runs.find(
        (run) => run.id === selectedRunId && run.startTile === selectedMarker.floorIndex,
      ) ?? null)
    : null;
  const metadataFor = useLevelMetadata(levelSessions);
  const selectionScope = `${selectedDay?.date ?? ""}:${selectedLevel?.id ?? ""}`;
  const previousSelectionScope = useRef(selectionScope);

  useEffect(() => {
    if (previousSelectionScope.current === selectionScope) return;
    previousSelectionScope.current = selectionScope;
    setSelectedMarkerId(null);
    setSelectedRunId(null);
    setFirstMarkerLevelSessionId(selectedLevel?.id ?? null);
    clearLevelFilePicker();
    setReplayChoiceRun(null);
  }, [clearLevelFilePicker, selectedLevel?.id, selectionScope]);

  useEffect(() => {
    if (!days.length) {
      setSelectedDate(null);
      return;
    }
    if (!days.some((day) => day.date === selectedDate)) setSelectedDate(days[0].date);
  }, [days, selectedDate]);

  useEffect(() => {
    if (!levelSessions.length) {
      setSelectedLevelGroupId(null);
      return;
    }
    if (
      !levelSessions.some(
        (session) => session.levelGroupId === selectedLevelGroupId && session.canOpen,
      )
    ) {
      const firstLevel = levelSessions.find((session) => session.canOpen) ?? null;
      setSelectedLevelGroupId(firstLevel?.levelGroupId ?? null);
      setFirstMarkerLevelSessionId(firstLevel?.id ?? null);
    }
  }, [levelSessions, selectedLevelGroupId]);

  useEffect(() => {
    if (!levelData.runsSettled) return;
    if (selectedMarkerId && !markers.some((marker) => marker.id === selectedMarkerId)) {
      setSelectedMarkerId(null);
      setSelectedRunId(null);
    }
  }, [levelData.runsSettled, markers, selectedMarkerId]);

  useEffect(() => {
    if (!firstMarkerLevelSessionId || selectedLevel?.id !== firstMarkerLevelSessionId) return;
    if (!levelData.runsSettled) return;
    if (levelData.overview?.id === firstMarkerLevelSessionId) {
      setSelectedMarkerId(markers[0]?.id ?? null);
      setSelectedRunId(null);
      setFirstMarkerLevelSessionId(null);
      return;
    }
    if (levelData.error) setFirstMarkerLevelSessionId(null);
  }, [
    firstMarkerLevelSessionId,
    levelData.error,
    levelData.runsSettled,
    levelData.overview?.id,
    markers,
    selectedLevel?.id,
  ]);

  useEffect(() => {
    if (!levelData.runsSettled) return;
    if (selectedRunId && !selectedRun) setSelectedRunId(null);
  }, [levelData.runsSettled, selectedRun, selectedRunId]);

  useEffect(() => {
    if (!levelData.runsSettled) return;
    if (replayChoiceRun && !levelData.runs.some((run) => run.id === replayChoiceRun.id)) {
      clearLevelFilePicker();
      setReplayChoiceRun(null);
    }
  }, [clearLevelFilePicker, levelData.runs, levelData.runsSettled, replayChoiceRun]);

  const selectMarker = (marker: RunMarker | null) => {
    setFirstMarkerLevelSessionId(null);
    setSelectedMarkerId(marker?.id ?? null);
    setSelectedRunId(null);
  };
  const selectRun = (run: ActivityRun) =>
    setSelectedRunId((current) => (current === run.id ? null : run.id));
  const selectDate = (date: string) => {
    const firstLevel =
      days.find((day) => day.date === date)?.levelSessions.find((level) => level.canOpen) ?? null;
    setSelectedDate(date);
    setSelectedLevelGroupId(firstLevel?.levelGroupId ?? null);
    setSelectedMarkerId(null);
    setSelectedRunId(null);
    setFirstMarkerLevelSessionId(firstLevel?.id ?? null);
  };
  const selectLevel = (levelGroupId: string) => {
    const level =
      levelSessions.find((item) => item.levelGroupId === levelGroupId && item.canOpen) ?? null;
    if (!level) return;
    setSelectedLevelGroupId(levelGroupId);
    setSelectedMarkerId(null);
    setSelectedRunId(null);
    setFirstMarkerLevelSessionId(level.id);
  };
  const deleteMicrophoneRecording = async (run: ActivityRun) => {
    await runActions.deleteMicrophoneRecording(run.id);
    await levelData.updateRun(run.id, {
      hasMicrophoneRecording: false,
      microphoneRecordingBytes: 0,
      microphoneDurationSeconds: null,
      microphoneSampleRate: null,
      microphoneChannels: null,
      microphoneRecordingPermanent: false,
      microphoneRecordingExpiresAtUtc: null,
    });
  };
  const keepMicrophoneRecording = async (run: ActivityRun) => {
    await runActions.keepMicrophoneRecording(run.id);
    await levelData.updateRun(run.id, {
      microphoneRecordingPermanent: true,
      microphoneRecordingExpiresAtUtc: null,
    });
  };
  const downloadMicrophoneRecording = async (run: ActivityRun) => {
    const url = await runActions.prepareMicrophoneRecordingDownload(run.id);
    const frame = document.createElement("iframe");
    frame.hidden = true;
    frame.tabIndex = -1;
    frame.referrerPolicy = "no-referrer";
    frame.src = url;
    document.body.append(frame);
    // Keep the navigation alive while the browser handles a slow native download.
    window.setTimeout(() => frame.remove(), 30 * 60 * 1000);
  };
  const deleteRun = async (run: ActivityRun) => {
    await runActions.deleteRun(run.id);
    await levelData.removeRun(run.id);
    setSelectedRunId((current) => (current === run.id ? null : current));
    if (replayChoiceRun?.id === run.id) {
      clearLevelFilePicker();
      setReplayChoiceRun(null);
    }
    await activity.retry();
  };

  return {
    activity,
    replay,
    replayChoiceRun,
    days,
    selectedDay,
    levelSessions,
    selectedLevel,
    levelData,
    markers,
    selectedMarker,
    selectedRun,
    metadataFor,
    timeZone,
    copy: {
      unavailableTitle: t("levels.unavailableTitle"),
      unavailableBody: t("levels.unavailableBody"),
      empty: t("empty"),
    },
    actions: {
      retry: activity.retry,
      selectDate,
      selectLevel,
      selectMarker,
      selectRun,
      openReplayChoice: setReplayChoiceRun,
      closeReplayChoice: () => setReplayChoiceRun(null),
      deleteRun,
      deleteMicrophoneRecording,
      keepMicrophoneRecording,
      downloadMicrophoneRecording,
    },
  };
}
