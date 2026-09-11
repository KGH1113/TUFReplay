import { uiBase, uiPort } from "./config";
import { localTuf } from "./local-tuf/config";
import { requireFreePorts } from "./ports";
import { preflight } from "./preflight";
import {
	localTufCdc,
	ready,
	readyLocalTufCdc,
	rustServer,
	viteServer,
} from "./processes";
import { serve } from "./service";

await preflight();
await requireFreePorts([5151, 5152, uiPort, ...(localTuf ? [3990] : [])]);
const service = serve();
const rust = rustServer();
const cdc = localTuf ? localTufCdc() : undefined;
let web: ReturnType<typeof viteServer> | undefined;
let stopping = false;
async function stop() {
	if (stopping) return;
	stopping = true;
	web?.kill();
	rust.kill("SIGTERM");
	cdc?.kill("SIGTERM");
	await service.stop(true);
}
process.on("SIGINT", () => void stop());
process.on("SIGTERM", () => void stop());
try {
	await Promise.all([ready(rust), ...(cdc ? [readyLocalTufCdc(cdc)] : [])]);
	web = viteServer();
	console.log(
		`\nAuto submission E2E: ${uiBase}\n${localTuf ? "로컬 TUF API에 실제 등록" : "실제 TUF 호출 없음"} · Ctrl+C로 종료\n`,
	);
	const code = await Promise.race([
		rust.exited,
		web.exited,
		...(cdc ? [cdc.exited] : []),
	]);
	if (!stopping && code !== 0) process.exitCode = 1;
} finally {
	await stop();
}
