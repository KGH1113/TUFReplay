import { expect, test } from "bun:test";
import { binaryFrame } from "../src/client/frames";
import { requireLocal } from "../src/config";
import { keyCount, trimCsv } from "../src/fixtures/convert";
import { requireSuccessfulClear } from "../src/fixtures/eligibility";
import { catalogFileId } from "../src/local-tuf/catalog";
import { localTufSubmissionEligible } from "../src/local-tuf/eligibility";
import { isAllowedOrigin } from "../src/origin";
import type { Fixture } from "../src/types";

test("successful fixtures require a full Strict clear without NoFail", () => {
	const clear = {
		result: "cleared",
		start_tile: 0,
		no_fail_mode: 0,
		judgment_difficulty: 2,
		judgment_overload: 0,
		judgment_too_early: 0,
		judgment_early: 0,
		judgment_late: 0,
		judgment_too_late: 0,
		judgment_miss: 0,
	};
	expect(() => requireSuccessfulClear(clear)).not.toThrow();
	expect(() => requireSuccessfulClear({ ...clear, no_fail_mode: 1 })).toThrow(
		"NoFail",
	);
	expect(() =>
		requireSuccessfulClear({
			...clear,
			judgment_too_early: 3,
			judgment_early: 28,
		}),
	).not.toThrow();
	expect(() =>
		requireSuccessfulClear({ ...clear, judgment_difficulty: 1 }),
	).toThrow("Strict");
	expect(() => requireSuccessfulClear({ ...clear, start_tile: 10 })).toThrow(
		"full clear",
	);
});

test("retains negative time and removes post-clear inputs without changing values", () => {
	const source = "-20,32,3,32,0\n10,32,2,32,0\n21,44,3,44,0\n";
	expect(trimCsv(source, 5, 0, 20)).toEqual(["-20,32,3,32,0", "10,32,2,32,0"]);
	expect(keyCount(trimCsv(source, 5, 0, 20))).toBe(1);
});
test("missing hit timestamps cannot silently become synthetic gameplay data", () => {
	expect(() => trimCsv("0,1,0,0,0,0,0,0,0,0,0,3,\n", 13, 12, 100)).toThrow();
	expect(() => trimCsv("0,1,0\n", 13, 12, 100)).toThrow();
});
test("binary frame follows TUFR big endian protocol and preserves payload", () => {
	const payload = new TextEncoder().encode("-1,32,3,32,0\n");
	const bytes = binaryFrame({ kind: 0, timeUs: -1, payload }, 257);
	expect(new TextDecoder().decode(bytes.slice(0, 4))).toBe("TUFR");
	const view = new DataView(bytes.buffer);
	expect(view.getUint16(6)).toBe(0);
	expect(view.getBigUint64(8)).toBe(257n);
	expect(view.getUint32(16)).toBe(payload.length);
	expect(bytes.slice(20)).toEqual(payload);
});
test("test infrastructure refuses external destinations", () => {
	expect(() => requireLocal("https://api.tuforums.com")).toThrow();
	expect(requireLocal("http://127.0.0.1:5152").hostname).toBe("127.0.0.1");
});

test("browser requests accept either loopback hostname on the configured UI port", () => {
	expect(
		isAllowedOrigin(`http://127.0.0.1:${process.env.E2E_UI_PORT ?? 5174}`),
	).toBe(true);
	expect(
		isAllowedOrigin(`http://localhost:${process.env.E2E_UI_PORT ?? 5174}`),
	).toBe(true);
	expect(isAllowedOrigin("https://tuforums.com")).toBe(false);
	expect(isAllowedOrigin("not-an-origin")).toBe(false);
});

test("local TUF validation uses the synchronized revision ID", () => {
	const fixture = {
		fileId: "e2e-fixture-file",
		actualFileId: "current-local-tuf-file",
	} as Fixture;
	expect(catalogFileId(fixture, false)).toBe("e2e-fixture-file");
	expect(catalogFileId(fixture, true)).toBe("current-local-tuf-file");
	expect(
		catalogFileId({ ...fixture, actualFileId: undefined }, true),
	).toBeUndefined();
});

test("local TUF registration only enables official P and G difficulties", () => {
	const fixture = {
		difficulty: { id: 1, type: "PGU", name: "P16" },
	} as Fixture;
	expect(localTufSubmissionEligible(fixture)).toBe(true);
	expect(
		localTufSubmissionEligible({
			...fixture,
			difficulty: { id: 2, type: "PGU", name: "G20" },
		}),
	).toBe(true);
	expect(
		localTufSubmissionEligible({
			...fixture,
			difficulty: { id: 46, type: "PGU", name: "U6" },
		}),
	).toBe(false);
});
