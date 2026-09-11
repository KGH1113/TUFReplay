import type { HealthDto } from "@/schemas/health/health-schema";

export interface Health {
  ok: boolean;
  mod: string;
  modVersion: string;
  protocolVersion: number;
  serverVersion: number;
  replayEngineId: string;
  replayFormatVersion: number;
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
  };
}
