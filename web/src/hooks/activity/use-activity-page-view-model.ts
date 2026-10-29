import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useActivityData } from "@/hooks/activity/use-activity-data";
import { useLevelMetadata } from "@/hooks/activity/use-level-metadata";
import { useLevelSessionData } from "@/hooks/activity/use-level-session-data";
import { useRunActions } from "@/hooks/activity/use-run-actions";
import { useReplayControl } from "@/hooks/replay/use-replay-control";
import {
  aggregateRunMarkers,
  dateKeyInTimeZone,
  groupSessionsByDay,
  isClearRun,
} from "@/models/activity/activity-data";
import type { ActivityDay, ActivityRun, RunMarker } from "@/models/activity/activity-model";

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
  // The day to select when a delete removes the selected day: the one that slides into its place.
  const deletedDayFallback = useRef<string | null>(null);

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
    if (days.some((day) => day.date === selectedDate)) return;
    const fallback = deletedDayFallback.current;
    deletedDayFallback.current = null;
    setSelectedDate(
      fallback && days.some((day) => day.date === fallback) ? fallback : days[0].date,
    );
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
    deletedDayFallback.current = null;
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
    const file = await runActions.prepareMicrophoneRecordingDownload(run.id);
    await file.download();
  };
  const deleteCameraRecording = async (run: ActivityRun) => {
    await runActions.deleteCameraRecording(run.id);
    await levelData.updateRun(run.id, {
      hasCameraRecording: false,
      cameraRecordingBytes: 0,
      cameraRecordingPermanent: false,
      cameraRecordingExpiresAtUtc: null,
    });
  };
  const keepCameraRecording = async (run: ActivityRun) => {
    await runActions.keepCameraRecording(run.id);
    await levelData.updateRun(run.id, {
      cameraRecordingPermanent: true,
      cameraRecordingExpiresAtUtc: null,
    });
  };
  const downloadCameraRecording = async (run: ActivityRun) => {
    const file = await runActions.prepareCameraRecordingDownload(run.id);
    await file.download();
  };
  const setRunLocked = async (run: ActivityRun, locked: boolean) => {
    if (run.locked === locked) return;
    // Patch the day's lock count before the request: the refresh that follows replaces recent
    // sessions with the recorder's counts, so patching after it would count the lock twice.
    const delta = locked ? 1 : -1;
    const clearDelta = isClearRun(run) ? delta : 0;
    activity.adjustLockedRuns(run.levelSessionId, delta, clearDelta);
    try {
      await runActions.setRunLocked(run.id, locked);
    } catch (cause) {
      activity.adjustLockedRuns(run.levelSessionId, -delta, -clearDelta);
      throw cause;
    }
    // Locking also keeps the run's recordings; mirror that until the refresh lands.
    await levelData.updateRun(
      run.id,
      locked
        ? {
            locked,
            ...(run.hasMicrophoneRecording
              ? { microphoneRecordingPermanent: true, microphoneRecordingExpiresAtUtc: null }
              : {}),
            ...(run.hasCameraRecording
              ? { cameraRecordingPermanent: true, cameraRecordingExpiresAtUtc: null }
              : {}),
          }
        : { locked },
    );
  };
  const deleteDay = async (day: ActivityDay, keepClears: boolean) => {
    const index = days.findIndex((candidate) => candidate.date === day.date);
    deletedDayFallback.current = (days[index + 1] ?? days[index - 1])?.date ?? null;
    let result: Awaited<ReturnType<typeof runActions.deleteAppSessionRuns>>;
    try {
      result = await runActions.deleteAppSessionRuns(
        day.appSessions.map((session) => session.id),
        keepClears,
      );
    } catch (cause) {
      deletedDayFallback.current = null;
      throw cause;
    }
    await activity.reloadAllSessions();
    return result;
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
    todayDate: dateKeyInTimeZone(new Date().toISOString(), timeZone),
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
      setRunLocked,
      deleteDay,
      deleteMicrophoneRecording,
      keepMicrophoneRecording,
      downloadMicrophoneRecording,
      deleteCameraRecording,
      keepCameraRecording,
      downloadCameraRecording,
    },
  };
}
