import {
	apiBase,
	databaseUrl,
	incomingToken,
	localTufBackendDir,
	outgoingToken,
	redisUrl,
	root,
	serverDir,
	uiPort,
} from "./config";
import { log } from "./logs";

export function rustServer(options: { all?: boolean; redisUrl?: string } = {}) {
	const child = Bun.spawn(
		[
			"cargo",
			"run",
			"--features",
			"e2e",
			"--",
			"start",
			...(options.all === false ? [] : ["--all"]),
			"--environment",
			"e2e",
		],
		{
			cwd: serverDir,
			env: {
				...process.env,
				LOCO_ENV: "e2e",
				SCHEDULER_CONFIG: "config/scheduler.yaml",
				E2E_DATABASE_URL: databaseUrl,
				E2E_REDIS_URL: options.redisUrl ?? redisUrl,
				TUF_TO_AUTO_SUBMISSION_TOKEN: incomingToken,
				AUTO_SUBMISSION_TO_TUF_TOKEN: outgoingToken,
			},
			stdout: "pipe",
			stderr: "pipe",
		},
	);
	async function capture(stream: ReadableStream<Uint8Array>) {
		const decoder = new TextDecoder();
		let pending = "";
		for await (const chunk of stream) {
			pending += decoder.decode(chunk, { stream: true });
			const lines = pending.split("\n");
			pending = lines.pop() ?? "";
			for (const line of lines)
				if (line.trim()) {
					// biome-ignore lint/suspicious/noControlCharactersInRegex: strips ANSI color escapes from Cargo output.
					const clean = line.replace(/\u001b\[[0-9;]*m/g, "");
					if (!/^[\s▀▄█]+$/.test(clean)) log("server", clean);
					console.log(line);
				}
		}
	}
	void capture(child.stdout);
	void capture(child.stderr);
	return child;
}
export function viteServer() {
	return Bun.spawn(
		[
			"bun",
			"x",
			"--no-install",
			"vite",
			"--host",
			"127.0.0.1",
			"--port",
			String(uiPort),
			"--strictPort",
		],
		{ cwd: root, stdout: "inherit", stderr: "inherit" },
	);
}
export function localTufCdc() {
	return Bun.spawn(
		[
			"node",
			"--env-file=.env",
			"--import",
			"tsx",
			"src/externalServices/cdcService/app.ts",
		],
		{
			cwd: localTufBackendDir,
			env: process.env,
			stdout: "inherit",
			stderr: "inherit",
		},
	);
}
export async function ready(child: ReturnType<typeof rustServer>) {
	const deadline = Date.now() + 180000;
	while (Date.now() < deadline) {
		if (child.exitCode !== null)
			throw new Error(`Rust server exited (${child.exitCode})`);
		try {
			const response = await fetch(`${apiBase}/_health`, {
				signal: AbortSignal.timeout(1000),
			});
			if (response.ok) return;
		} catch {}
		await Bun.sleep(250);
	}
	throw new Error("Rust server startup timeout");
}

export async function readyLocalTufCdc(child: ReturnType<typeof localTufCdc>) {
	const deadline = Date.now() + 30_000;
	while (Date.now() < deadline) {
		if (child.exitCode !== null)
			throw new Error(`Local TUF CDC exited (${child.exitCode})`);
		try {
			const response = await fetch("http://127.0.0.1:3990/health", {
				signal: AbortSignal.timeout(1000),
			});
			if (response.ok) return;
		} catch {}
		await Bun.sleep(250);
	}
	throw new Error("Local TUF CDC startup timeout");
}
