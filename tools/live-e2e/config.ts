import { homedir } from "node:os";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

export const root = fileURLToPath(new URL("../../", import.meta.url));
export const data = join(root, "tools/live-e2e/.data");
export const backend = resolve(root, "../tuf-backend");
export const frontend = resolve(root, "../t21c-web-frontend");
export const editor = resolve(root, "../adofai-web-editor");
export const game =
	process.env.ADOFAI_DIR ||
	join(
		homedir(),
		"Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice",
	);
export const owner = "00000000-0000-4000-8000-000000000043";
export const clientId = "tuf-replay-local-game";
export const username = "local-game-tester";
export const callback = "http://127.0.0.1:5180/oauth/callback";
export const databaseUrl =
	"postgres://tuf_replay:tuf_replay_dev@127.0.0.1:5432/tuf_replay_local_game";
export const incomingToken = "local-game-tuf-to-submission-20260917";
export const outgoingToken = "local-game-submission-to-tuf-20260917";

/** This runner must never inherit production database or service destinations. */
export function assertBackendEnvironment(
	env: Record<string, string | undefined>,
) {
	if (
		env.DB_HOST !== "127.0.0.1" ||
		env.DB_PORT !== "3307" ||
		env.DB_DATABASE !== "tuf_web_test" ||
		env.NODE_ENV !== "development"
	)
		throw new Error(
			"Local game E2E requires development / 127.0.0.1:3307/tuf_web_test",
		);
}

export const backendOverrides = {
	NODE_ENV: "development",
	BIND_ADDRESS: "127.0.0.1",
	PORT: "3002",
	CLIENT_URL: "http://127.0.0.1:5176",
	DEV_URL: "http://127.0.0.1:3002",
	TUF_AUTO_SUBMISSION_OAUTH_CLIENT_ID: clientId,
	AUTO_SUBMISSION_ENABLED: "true",
	AUTO_SUBMISSION_TESTER_AUTHORITY: "replay",
	TUF_TO_AUTO_SUBMISSION_TOKEN: incomingToken,
	AUTO_SUBMISSION_TO_TUF_TOKEN: outgoingToken,
	AUTO_SUBMISSION_API_URL: "http://127.0.0.1:5151",
	DISCORD_ANNOUNCEMENT_DELIVERY_FORCE: "false",
};
