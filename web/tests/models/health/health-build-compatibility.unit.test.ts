import { describe, expect, test } from "bun:test";
import { getAutoSubmissionCompatibility, mapHealth } from "@/models/health/health-model";
import { healthDtoSchema } from "@/schemas/health/health-schema";

const legacyHealthDto = {
  Ok: true,
  Mod: "TUFReplay",
  ModVersion: "0.2.0-beta.2",
  ProtocolVersion: 7,
  ServerVersion: 1,
  ReplayEngineId: "tufreplay.replay.v2",
  ReplayFormatVersion: 1,
};

function healthWith(
  values: Partial<
    Pick<
      ReturnType<typeof mapHealth>,
      "buildFlavor" | "autoSubmissionProtocolVersion" | "modVersion"
    >
  > = {},
) {
  return mapHealth(
    healthDtoSchema.parse({
      ...legacyHealthDto,
      BuildFlavor: "auto-submission",
      AutoSubmissionProtocolVersion: 2,
      ...(values.buildFlavor === undefined ? {} : { BuildFlavor: values.buildFlavor }),
      ...(values.autoSubmissionProtocolVersion === undefined
        ? {}
        : { AutoSubmissionProtocolVersion: values.autoSubmissionProtocolVersion }),
      ...(values.modVersion === undefined ? {} : { ModVersion: values.modVersion }),
    }),
  );
}

describe("auto-submission web/mod compatibility", () => {
  test("treats health from a legacy mod as standard with protocol 0", () => {
    const health = mapHealth(healthDtoSchema.parse(legacyHealthDto));

    expect(health.buildFlavor).toBe("standard");
    expect(health.autoSubmissionProtocolVersion).toBe(0);
    expect(getAutoSubmissionCompatibility("auto-submission", health)).toEqual({
      available: false,
      reason: "mod_build",
    });
  });

  test("fails closed when the web build is standard even if the mod supports auto-submission", () => {
    expect(getAutoSubmissionCompatibility("standard", healthWith())).toEqual({
      available: false,
      reason: "web_build",
    });
  });

  test("fails closed without a successful health response", () => {
    expect(getAutoSubmissionCompatibility("auto-submission", null)).toEqual({
      available: false,
      reason: "health_unavailable",
    });

    const unsuccessfulHealth = { ...healthWith(), ok: false };
    expect(getAutoSubmissionCompatibility("auto-submission", unsuccessfulHealth)).toEqual({
      available: false,
      reason: "health_unavailable",
    });
  });

  test("fails closed for the wrong mod flavor or protocol version", () => {
    expect(
      getAutoSubmissionCompatibility("auto-submission", healthWith({ buildFlavor: "standard" })),
    ).toEqual({ available: false, reason: "mod_build" });

    expect(
      getAutoSubmissionCompatibility(
        "auto-submission",
        healthWith({ autoSubmissionProtocolVersion: 0 }),
      ),
    ).toEqual({ available: false, reason: "protocol_version" });

    expect(
      getAutoSubmissionCompatibility(
        "auto-submission",
        healthWith({ autoSubmissionProtocolVersion: 1 }),
      ),
    ).toEqual({ available: false, reason: "protocol_version" });
  });

  test("accepts the auto flavor with protocol 2 regardless of mod patch version", () => {
    expect(
      getAutoSubmissionCompatibility(
        "auto-submission",
        healthWith({ modVersion: "0.2.0-beta.99" }),
      ),
    ).toEqual({ available: true, reason: null });
  });

  test("normalizes malformed compatibility fields to standard/protocol 0", () => {
    const health = mapHealth(
      healthDtoSchema.parse({
        ...legacyHealthDto,
        BuildFlavor: "future-flavor",
        AutoSubmissionProtocolVersion: -1,
      }),
    );

    expect(health.buildFlavor).toBe("standard");
    expect(health.autoSubmissionProtocolVersion).toBe(0);
    expect(getAutoSubmissionCompatibility("auto-submission", health).available).toBe(false);
  });
});
