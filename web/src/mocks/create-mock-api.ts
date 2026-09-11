import type { AppApi } from "@/api/app-api";
import { createActivityWireFixture } from "@/mocks/activity/activity-api-fixture";
import { createReplayApiMock } from "@/mocks/replay/replay-api-mock";
import { createSubmissionApiMock } from "@/mocks/submission/submission-api-mock";
import {
  mapActivityChart,
  mapActivityRun,
  mapAppSession,
  mapLogicalLevel,
} from "@/models/activity/activity-model";
import { mapCalibrationResult, mapCalibrationStatus } from "@/models/calibration/calibration-model";
import { mapHealth } from "@/models/health/health-model";
import {
  mapMicrophoneDevicesState,
  mapMicrophoneTimingSettings,
} from "@/models/microphone/microphone-model";
import {
  activityChartDtoSchema,
  activityRunDtoSchema,
  appSessionDtoSchema,
  logicalLevelDtoSchema,
} from "@/schemas/activity/activity-schema";
import {
  calibrationResultDtoSchema,
  calibrationStatusDtoSchema,
} from "@/schemas/calibration/calibration-schema";
import { healthDtoSchema } from "@/schemas/health/health-schema";
import {
  microphoneDevicesStateDtoSchema,
  microphoneTimingSettingsDtoSchema,
} from "@/schemas/microphone/microphone-schema";

export function createMockApi(): AppApi {
  const fixture = createActivityWireFixture();
  return {
    submission: createSubmissionApiMock(),
    health: {
      async get() {
        return mapHealth(healthDtoSchema.parse(await fixture.health()));
      },
    },
    activity: {
      async getLegacyReplayStatus() {
        const status = await fixture.getLegacyReplayStatus();
        return { hasLegacyReplays: status.HasLegacyReplays };
      },
      async listAppSessions(offset, limit) {
        return (await fixture.listAppSessions(offset, limit)).map((item) =>
          mapAppSession(appSessionDtoSchema.parse(item)),
        );
      },
      async listAllAppSessions(onPage) {
        return (
          await fixture.listAllAppSessions((items) => {
            onPage?.(items.map((item) => mapAppSession(appSessionDtoSchema.parse(item))));
          })
        ).map((item) => mapAppSession(appSessionDtoSchema.parse(item)));
      },
      async getLogicalLevel(id) {
        return mapLogicalLevel(logicalLevelDtoSchema.parse(await fixture.getLogicalLevel(id)));
      },
      async listLogicalLevelRuns(id, appSessionIds, onPage) {
        return (
          await fixture.listAllLogicalLevelRuns(id, appSessionIds, (items) => {
            onPage?.(items.map((item) => mapActivityRun(activityRunDtoSchema.parse(item))));
          })
        ).map((item) => mapActivityRun(activityRunDtoSchema.parse(item)));
      },
      async getLogicalLevelChart(id) {
        return mapActivityChart(
          activityChartDtoSchema.parse(await fixture.getLogicalLevelChart(id)),
        );
      },
    },
    run: {
      async deleteRun(runId) {
        const result = await fixture.deleteRun(runId);
        return { runId: result.RunId, changed: result.Deleted };
      },
      async deleteMicrophoneRecording(runId) {
        const result = await fixture.deleteMicrophoneRecording(runId);
        return { runId: result.RunId, changed: result.Deleted };
      },
      async keepMicrophoneRecording(runId) {
        const result = await fixture.keepMicrophoneRecording(runId);
        return { runId: result.RunId, changed: result.Permanent };
      },
    },
    replay: createReplayApiMock(),
    microphone: {
      async getDevices() {
        return mapMicrophoneDevicesState(
          microphoneDevicesStateDtoSchema.parse(await fixture.getMicrophoneDevices()),
        );
      },
      async setEnabled(enabled) {
        return mapMicrophoneDevicesState(
          microphoneDevicesStateDtoSchema.parse(await fixture.setMicrophoneEnabled(enabled)),
        );
      },
      async selectDevice(deviceId) {
        return mapMicrophoneDevicesState(
          microphoneDevicesStateDtoSchema.parse(await fixture.selectMicrophoneDevice(deviceId)),
        );
      },
      async setOffset(offsetMs) {
        return mapMicrophoneTimingSettings(
          microphoneTimingSettingsDtoSchema.parse(await fixture.setMicrophoneOffset(offsetMs)),
        );
      },
      async setVolume(volumeDb) {
        return mapMicrophoneTimingSettings(
          microphoneTimingSettingsDtoSchema.parse(await fixture.setMicrophoneVolume(volumeDb)),
        );
      },
    },
    calibration: {
      async start() {
        return mapCalibrationStatus(
          calibrationStatusDtoSchema.parse(await fixture.startMicrophoneCalibration()),
        );
      },
      async getStatus(operationId) {
        return mapCalibrationStatus(
          calibrationStatusDtoSchema.parse(
            await fixture.getMicrophoneCalibrationStatus(operationId),
          ),
        );
      },
      async getResult(operationId, revision) {
        return mapCalibrationResult(
          calibrationResultDtoSchema.parse(
            await fixture.getMicrophoneCalibrationResult(operationId, revision),
          ),
        );
      },
      async playPreview(operationId) {
        return mapCalibrationStatus(
          calibrationStatusDtoSchema.parse(
            await fixture.playMicrophoneCalibrationPreview(operationId),
          ),
        );
      },
      async stopPreview(operationId) {
        return mapCalibrationStatus(
          calibrationStatusDtoSchema.parse(
            await fixture.stopMicrophoneCalibrationPreview(operationId),
          ),
        );
      },
      async setOffset(operationId, offsetMs) {
        return mapCalibrationStatus(
          calibrationStatusDtoSchema.parse(
            await fixture.setMicrophoneCalibrationOffset(operationId, offsetMs),
          ),
        );
      },
      async setVolume(operationId, volumeDb) {
        return mapCalibrationStatus(
          calibrationStatusDtoSchema.parse(
            await fixture.setMicrophoneCalibrationVolume(operationId, volumeDb),
          ),
        );
      },
      async close(operationId) {
        return mapCalibrationStatus(
          calibrationStatusDtoSchema.parse(await fixture.closeMicrophoneCalibration(operationId)),
        );
      },
    },
  };
}
