import type { HealthDto } from "@/schemas/health/health-schema";
import type { TufReplayBuildFlavor } from "@/shared/config/tufreplay-build-info";

export interface Health {
  ok: boolean;
  mod: string;
  modVersion: string;
  protocolVersion: number;
  serverVersion: number;
  replayEngineId: string;
  replayFormatVersion: number;
  buildFlavor: TufReplayBuildFlavor;
  autoSubmissionProtocolVersion: number;
}

export const SUPPORTED_AUTO_SUBMISSION_PROTOCOL_VERSION = 1;

export type AutoSubmissionCompatibilityReason =
  | "web_build"
  | "health_unavailable"
  | "mod_build"
  | "protocol_version";

export type AutoSubmissionCompatibility =
  | { available: true; reason: null }
  | { available: false; reason: AutoSubmissionCompatibilityReason };

export function getAutoSubmissionCompatibility(
  webFlavor: TufReplayBuildFlavor,
  health: Health | null | undefined,
): AutoSubmissionCompatibility {
  if (webFlavor !== "auto-submission") return { available: false, reason: "web_build" };
  if (!health?.ok) return { available: false, reason: "health_unavailable" };
  if (health.buildFlavor !== "auto-submission") return { available: false, reason: "mod_build" };
  if (health.autoSubmissionProtocolVersion !== SUPPORTED_AUTO_SUBMISSION_PROTOCOL_VERSION) {
    return { available: false, reason: "protocol_version" };
  }
  return { available: true, reason: null };
}

export function mapHealth(dto: HealthDto): Health {
  return {
    ok: dto.Ok,
    mod: dto.Mod,
    modVersion: dto.ModVersion,
    protocolVersion: dto.ProtocolVersion,
    serverVersion: dto.ServerVersion,
    replayEngineId: dto.ReplayEngineId,
    replayFormatVersion: dto.ReplayFormatVersion,
    buildFlavor: dto.BuildFlavor,
    autoSubmissionProtocolVersion: dto.AutoSubmissionProtocolVersion,
  };
}
