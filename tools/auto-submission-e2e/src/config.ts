import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { localAccessToken, localTuf } from "./local-tuf/config";

export const root = fileURLToPath(new URL("../", import.meta.url));
export const dataDir = join(root, ".data");
export const serverDir = fileURLToPath(
	new URL("../../../server/", import.meta.url),
);
export const localTufBackendDir =
	process.env.E2E_TUF_BACKEND_DIR ??
	fileURLToPath(new URL("../../../../tuf-backend/", import.meta.url));
export const apiBase = "http://127.0.0.1:5151";
export const toolBase = "http://127.0.0.1:5152";
export const uiPort = Number(process.env.E2E_UI_PORT ?? 5174);
if (!Number.isInteger(uiPort) || uiPort < 1024 || uiPort > 65535)
	throw new Error("Invalid E2E_UI_PORT");
export const uiBase = `http://127.0.0.1:${uiPort}`;
export const owner = localTuf
	? "00000000-0000-4000-8000-000000000041"
	: "e2e-local-player";
export const grant = "00000000-0000-4000-8000-000000000042";
export const accessToken = localTuf
	? localAccessToken()
	: "e2e-local-oauth-access-token";
export const incomingToken = "e2e-tuf-to-submission-local-only-0001";
export const outgoingToken = "e2e-submission-to-tuf-local-only-0002";
export const databaseUrl =
	process.env.E2E_DATABASE_URL ??
	"postgres://tuf_replay:tuf_replay_dev@127.0.0.1:5432/tuf_replay_e2e";
export const redisUrl =
	process.env.E2E_REDIS_URL ?? "redis://127.0.0.1:6379/14";

export function requireLocal(url: string) {
	const parsed = new URL(url);
	if (!["127.0.0.1", "localhost", "[::1]"].includes(parsed.hostname))
		throw new Error("E2E only permits loopback destinations");
	return parsed;
}
