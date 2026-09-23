import { execFile } from "node:child_process";
import { mkdir, readFile, unlink, writeFile } from "node:fs/promises";
import { promisify } from "node:util";
import { callback, clientId, data, game } from "./config";

export const localSettings = {
	AutoSubmissionOAuthClientId: clientId,
	AutoSubmissionTufApiUrl: "http://127.0.0.1:3002",
	AutoSubmissionServerUrl: "http://127.0.0.1:5151",
	AutoSubmissionOAuthRedirectUri: callback,
};
const path = `${game}/Mods/TUFReplay/Settings.json`;
const backup = `${data}/original-endpoints.json`;

export async function requireGameStopped() {
	// Only executable names, never process arguments (which may contain helper tokens).
	const { stdout } = await promisify(execFile)("ps", ["-axo", "comm="]);
	if (
		stdout
			.split("\n")
			.some((line) =>
				/(?:^|\/)ADanceOfFireAndIce(?:\.exe)?$/i.test(line.trim()),
			)
	)
		throw new Error(
			"Save and close ADOFAI before changing its settings or installed mod.",
		);
}
async function readSettings(): Promise<Record<string, unknown>> {
	try {
		return JSON.parse(await readFile(path, "utf8"));
	} catch (error) {
		if (error instanceof Error && "code" in error && error.code === "ENOENT")
			return {};
		throw error;
	}
}
function target(settings: Record<string, unknown>): Record<string, unknown> {
	if (
		settings.Setting &&
		typeof settings.Setting === "object" &&
		!Array.isArray(settings.Setting)
	)
		return settings.Setting as Record<string, unknown>;
	return settings;
}
export async function installSettings() {
	await requireGameStopped();
	await mkdir(data, { recursive: true, mode: 0o700 });
	const settings = await readSettings();
	const values = target(settings);
	// Save only fields we own, so restore preserves later microphone/UI/user changes.
	const previous = Object.fromEntries(
		Object.keys(localSettings).map((key) => [
			key,
			Object.hasOwn(values, key)
				? { present: true, value: values[key] }
				: { present: false },
		]),
	);
	try {
		await writeFile(backup, JSON.stringify(previous, null, 2), {
			flag: "wx",
			mode: 0o600,
		});
	} catch (error) {
		if (!(error instanceof Error && "code" in error && error.code === "EEXIST"))
			throw error;
	}
	Object.assign(values, localSettings);
	await writeFile(path, `${JSON.stringify(settings, null, 2)}\n`);
	console.log(
		"Local OAuth and submission endpoints applied. Start ADOFAI to load them.",
	);
}
export async function restoreSettings() {
	await requireGameStopped();
	const previous = JSON.parse(await readFile(backup, "utf8"));
	const settings = await readSettings();
	const values = target(settings);
	for (const key of Object.keys(localSettings)) {
		if (previous[key]?.present) values[key] = previous[key].value;
		else delete values[key];
	}
	await writeFile(path, `${JSON.stringify(settings, null, 2)}\n`);
	await unlink(backup);
	console.log("Original endpoints restored; other settings preserved.");
}
