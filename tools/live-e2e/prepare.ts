import { mkdir, readFile, writeFile, rename, rm } from "node:fs/promises";
import { createWriteStream } from "node:fs";
import { Readable, Transform } from "node:stream";
import { pipeline } from "node:stream/promises";
import { verifyArchiveSelection } from "./official-chart";
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
for (const value of levelIds) {
	console.log(`Preparing official chart #${value}...`);
	if (!/^[1-9]\d*$/.test(value)) throw new Error(`Invalid level ID: ${value}`);
	const folder = join(game, "Mods/TUFHelperLite/Downloads", `tuf-${value}`);
	const meta = JSON.parse(
		await readFile(join(folder, ".tufhelperlite-level.json"), "utf8"),
	);
	if (meta.Id !== Number(value) || !meta.DownloadedFileId)
		throw new Error(`Missing installed identity: ${value}`);
	const archive = `${data}/charts/${value}.zip`;
	// Official bytes and metadata are one fixture. Never promote an edited installed
	// chart to the server's reference, or flatten away the confirmed ZIP path.
	const base = "https://api.tuforums.com";
	const levelResponse = await fetch(
		`${base}/v2/database/levels/byId/${value}`,
		{ signal: AbortSignal.timeout(30000) },
	);
	if (!levelResponse.ok)
		throw new Error(`Official TUF #${value}: HTTP ${levelResponse.status}`);
	const official = await levelResponse.json();
	if (official.fileId !== meta.DownloadedFileId)
		throw new Error(
			`TUF #${value}: download the current official chart before preparing`,
		);
	const metadataResponse = await fetch(
		`${base}/cdn/${encodeURIComponent(official.fileId)}/metadata`,
		{ signal: AbortSignal.timeout(30000) },
	);
	if (!metadataResponse.ok)
		throw new Error(
			`Official metadata #${value}: HTTP ${metadataResponse.status}`,
		);
	const { metadata } = await metadataResponse.json();
	console.log(`Downloading official archive #${value}...`);
	const temp = `${data}/charts/${value}-${crypto.randomUUID()}.zip`;
	try {
		const response = await fetch(
			`${base}/cdn/${encodeURIComponent(official.fileId)}`,
			{ signal: AbortSignal.timeout(120000) },
		);
		if (!response.ok || !response.body)
			throw new Error(`Official ZIP #${value}: HTTP ${response.status}`);
		let bytes = 0;
		const limited = new Transform({
			transform(chunk, _encoding, callback) {
				bytes += chunk.byteLength;
				if (bytes > 1024 * 1024 * 1024)
					return callback(new Error("Official archive exceeds 1 GiB"));
				callback(null, chunk);
			},
		});
		await pipeline(
			Readable.fromWeb(response.body),
			limited,
			createWriteStream(temp, { flags: "wx", mode: 0o600 }),
		);
		console.log(`Checking official archive #${value} (${bytes} bytes)...`);
		const listing = Bun.spawn(["unzip", "-Z1", temp], {
			stdout: "pipe",
			stderr: "pipe",
		});
		const entries = (await new Response(listing.stdout).text()).split(/\r?\n/);
		if ((await listing.exited) !== 0)
			throw new Error(`Invalid official ZIP #${value}`);
		verifyArchiveSelection(metadata, entries);
		await rename(temp, archive);
	} finally {
		await rm(temp, { force: true });
	}
	manifest.push({
		levelId: Number(value),
		fileId: meta.DownloadedFileId,
		folder,
		archive,
		metadata,
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
console.log("Preparing local replay database...");
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
