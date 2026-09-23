import { type ChildProcess, spawn } from "node:child_process";
import { mkdir, readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { join } from "node:path";
import { requireFreePorts } from "../auto-submission-e2e/src/ports";
import { seedTrustedTester } from "../testing/trusted-tester-fixture";
import {
	assertBackendEnvironment,
	backend,
	backendOverrides,
	data,
	databaseUrl,
	owner,
	editor,
	frontend,
	incomingToken,
	outgoingToken,
	root,
} from "./config";

const requireBackend = createRequire(`${backend}/package.json`);
const backendEnv = requireBackend("dotenv").parse(
	await readFile(`${backend}/.env`),
);
assertBackendEnvironment(backendEnv);
const charts: Array<{ levelId: number; archive: string }> = JSON.parse(
	await readFile(`${data}/charts.json`, "utf8"),
);
if (!(await Bun.file(`${data}/login.json`).exists()))
	throw new Error("Run bun run e2e:live:prepare first");
await requireFreePorts([3002, 3990, 5151, 5152, 5176, 5180, 5190]);
await mkdir(`${data}/logs`, { recursive: true, mode: 0o700 });
let stopping = false;
const children: Array<{ process: ChildProcess; exited: Promise<void> }> = [];
const files: number[] = [];
const fs = await import("node:fs");
function start(
	name: string,
	cmd: [string, ...string[]],
	cwd: string,
	env: Record<string, string | undefined> = {},
) {
	const logfile = `${data}/logs/${name}.log`;
	const fd = fs.openSync(logfile, "w", 0o600);
	files.push(fd);
	const child = spawn(cmd[0], cmd.slice(1), {
		cwd,
		env: { ...process.env, ...env },
		stdio: ["ignore", fd, fd],
		detached: true,
	});
	const exited = new Promise<void>((resolve) => {
		child.once("error", (error) => {
			console.error(`${name}: ${error.message}`);
			resolve();
			if (!stopping) void stop(1);
		});
		child.once("exit", (code, signal) => {
			resolve();
			if (!stopping) {
				console.error(`${name} exited (${code ?? signal}); see ${logfile}`);
				void stop(1);
			}
		});
	});
	children.push({ process: child, exited });
	console.log(`${name}: started (log: ${logfile})`);
	return child;
}
async function waitReady(name: string, url: string, timeout = 180000) {
	const until = Date.now() + timeout;
	while (!stopping && Date.now() < until) {
		try {
			if ((await fetch(url, { signal: AbortSignal.timeout(1500) })).ok) {
				console.log(`${name}: ready`);
				return;
			}
		} catch {}
		await Bun.sleep(500);
	}
	throw new Error(`${name} failed to become ready; check ${data}/logs`);
}
const archives = Bun.serve({
	hostname: "127.0.0.1",
	port: 5152,
	fetch(request) {
		const url = new URL(request.url);
		if (url.host !== "127.0.0.1:5152")
			return new Response("Invalid host", { status: 403 });
		if (request.method !== "GET" && request.method !== "HEAD")
			return new Response(null, { status: 405 });
		if (url.pathname === "/health")
			return Response.json({
				mode: "local-game",
				charts: charts.map((x) => x.levelId),
			});
		const match = /^\/charts\/([1-9]\d*)\.zip$/.exec(url.pathname);
		const chart = match && charts.find((c) => c.levelId === Number(match[1]));
		return chart
			? new Response(Bun.file(chart.archive), {
					headers: { "Content-Type": "application/zip", "Access-Control-Allow-Origin": "http://127.0.0.1:5190" },
				})
			: new Response("Not found", { status: 404 });
	},
});
async function stop(code = 0) {
	if (stopping) return;
	stopping = true;
	await archives.stop(true);
	function signalGroups(signal: NodeJS.Signals) {
		for (const child of children) {
			if (!child.process.pid) continue;
			try {
				process.kill(-child.process.pid, signal);
			} catch (error) {
				if (
					!(error instanceof Error && "code" in error && error.code === "ESRCH")
				)
					console.error(error);
			}
		}
	}
	signalGroups("SIGTERM");
	await Promise.race([
		Promise.all(children.map((c) => c.exited)),
		Bun.sleep(5000),
	]);
	// Include grandchildren (cargo/bun/vite), even if their direct parent exited first.
	signalGroups("SIGKILL");
	for (const fd of files) fs.closeSync(fd);
	process.exit(code);
}
process.on("SIGINT", () => void stop());
process.on("SIGTERM", () => void stop());
try {
	const tufEnv = { ...backendEnv, ...backendOverrides };
	start(
		"tuf-backend",
		["node", "--import", "tsx", "src/app.ts"],
		backend,
		tufEnv,
	);
	await waitReady(
		"TUF API",
		"http://127.0.0.1:3002/.well-known/oauth-authorization-server",
	);
	start(
		"tuf-cdc",
		["node", "--import", "tsx", "src/externalServices/cdcService/app.ts"],
		backend,
		tufEnv,
	);
	start(
		"submission",
		["cargo", "run", "--", "start", "--all", "--environment", "local-game"],
		join(root, "server"),
		{
			TUF_TO_AUTO_SUBMISSION_TOKEN: incomingToken,
			AUTO_SUBMISSION_TO_TUF_TOKEN: outgoingToken,
			SCHEDULER_CONFIG: "config/scheduler.yaml",
		},
	);
	start(
		"web-adofai",
		[
			"bun",
			"run",
			"dev",
			"--host",
			"127.0.0.1",
			"--port",
			"5190",
			"--strictPort",
		],
		editor,
		{
			VITE_TUF_PARENT_ORIGINS: "http://127.0.0.1:5176,http://127.0.0.1:5180",
      VITE_AUTO_SUBMISSION_API_URL: "http://127.0.0.1:5151",
      VITE_TUF_API_URL: "http://127.0.0.1:3002",
      VITE_REPLAY_CDN_PROXY_URL: "http://127.0.0.1:5190/__tuf-replay-cdn",
		},
	);
	start(
		"tuf-frontend",
		[
			"node",
			"node_modules/vite/bin/vite.js",
			"--host",
			"127.0.0.1",
			"--port",
			"5176",
			"--strictPort",
		],
		frontend,
		{
			VITE_API_URL: "http://127.0.0.1:3002",
			VITE_OWN_URL: "http://127.0.0.1:5176",
			VITE_WEB_ADOFAI_URL: "http://127.0.0.1:5190",
		},
	);
	start(
		"companion",
		[
			"bun",
			"x",
			"--no-install",
			"vite",
			"--host",
			"127.0.0.1",
			"--port",
			"5180",
			"--strictPort",
		],
		join(root, "web"),
		{
			VITE_TUFREPLAY_BUILD_FLAVOR: "auto-submission",
			VITE_TUF_API_PROXY_URL: "http://127.0.0.1:3002",
			VITE_TUF_WEB_URL: "http://127.0.0.1:5176",
			VITE_WEB_ADOFAI_EMBED_URL: "http://127.0.0.1:5190/embed/chart",
			VITE_USE_MOCK_ACTIVITY: "false",
		},
	);
	await Promise.all([
		waitReady("Submission API", "http://127.0.0.1:5151/_health"),
		waitReady("CDC", "http://127.0.0.1:3990/health"),
		waitReady("TUF frontend", "http://127.0.0.1:5176"),
		waitReady("Companion", "http://127.0.0.1:5180"),
		waitReady("web-adofai", "http://127.0.0.1:5190"),
	]);
	await seedTrustedTester(databaseUrl, owner);
	console.log(
		`\nLocal game E2E ready\nCompanion: http://127.0.0.1:5180\nTUF: http://127.0.0.1:5176\nLogin credentials: ${data}/login.json\nValidation: trusted_tester (game simulation skipped; recorded results are used)\nStart ADOFAI with the prepared auto-submission mod, connect your account, and clear a prepared P/G chart.\nCtrl+C stops this stack; databases and game stay intact.`,
	);
} catch (error) {
	console.error(error instanceof Error ? error.message : error);
	await stop(1);
}
