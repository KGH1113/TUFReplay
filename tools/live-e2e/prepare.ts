import { lstat, mkdir, readdir, readFile, writeFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { join } from "node:path";
import { SQL } from "bun";
import {
	assertBackendEnvironment,
	backend,
	backendOverrides,
	data,
	game,
	root,
} from "./config";
import {
	installSettings,
	localSettings,
	requireGameStopped,
} from "./game-settings";

if (process.argv.includes("--install-settings")) await requireGameStopped();

const requireBackend = createRequire(`${backend}/package.json`);
const env = requireBackend("dotenv").parse(await readFile(`${backend}/.env`));
assertBackendEnvironment(env);
await mkdir(`${data}/charts`, { recursive: true, mode: 0o700 });
const ids = process.argv.slice(2).filter((x) => x !== "--install-settings");
const levelIds = ids.length ? ids : ["8068", "3072"];
const manifest = [];
async function rejectLinks(folder: string): Promise<void> {
	for (const name of await readdir(folder)) {
		const path = join(folder, name),
			stat = await lstat(path);
		if (stat.isSymbolicLink())
			throw new Error(`Chart symlinks are not supported: ${path}`);
		if (stat.isDirectory()) await rejectLinks(path);
	}
}
for (const value of levelIds) {
	if (!/^[1-9]\d*$/.test(value)) throw new Error(`Invalid level ID: ${value}`);
	const folder = join(game, "Mods/TUFHelperLite/Downloads", `tuf-${value}`);
	const meta = JSON.parse(
		await readFile(join(folder, ".tufhelperlite-level.json"), "utf8"),
	);
	if (meta.Id !== Number(value) || !meta.DownloadedFileId)
		throw new Error(`Missing installed identity: ${value}`);
	await rejectLinks(folder);
	const archive = `${data}/charts/${value}.zip`;
	// Write a fresh archive, never update an old ZIP and retain removed entries.
	const temp = `${data}/charts/${value}-${crypto.randomUUID()}.zip`;
	const zipped = Bun.spawn(
		[
			"zip",
			"-q",
			"-r",
			temp,
			".",
			"-x",
			".tufhelperlite-level.json",
			"*.DS_Store",
		],
		{ cwd: folder },
	);
	if ((await zipped.exited) !== 0)
		throw new Error(`Chart packaging failed: ${value}`);
	await Bun.write(archive, Bun.file(temp));
	await Bun.file(temp).delete();
	manifest.push({
		levelId: Number(value),
		fileId: meta.DownloadedFileId,
		folder,
		archive,
	});
}
await writeFile(
	`${data}/charts.json`,
	`${JSON.stringify(manifest, null, 2)}\n`,
);
const db = new SQL(
	"postgres://tuf_replay:tuf_replay_dev@127.0.0.1:5432/postgres",
	{ max: 1 },
);
try {
	if (
		!(
			await db`SELECT 1 FROM pg_database WHERE datname = 'tuf_replay_local_game'`
		).length
	)
		await db.unsafe("CREATE DATABASE tuf_replay_local_game");
} finally {
	await db.close();
}
const seed = Bun.spawn(
	["node", "--import", "tsx", join(root, "tools/live-e2e/seed-backend.ts")],
	{
		cwd: backend,
		env: { ...process.env, ...env, ...backendOverrides },
		stdout: "inherit",
		stderr: "inherit",
	},
);
if ((await seed.exited) !== 0)
	throw new Error("Local backend preparation failed");
await writeFile(
	`${data}/mod-settings.json`,
	`${JSON.stringify(localSettings, null, 2)}\n`,
);
if (process.argv.includes("--install-settings")) {
	await installSettings();
}
console.log(
	"Ready: bun run e2e:live. Charts and original settings are preserved.",
);
