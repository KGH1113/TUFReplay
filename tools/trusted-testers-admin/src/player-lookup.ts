import { InputError, parseUserId } from "./testers.model";

export interface LinkedPlayer {
  playerId: number;
  playerName: string;
  username: string;
  userId: string;
}

type PlayerFetch = (input: RequestInfo | URL, init?: RequestInit) => Promise<Response>;

export class PlayerLookupError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
  }
}

export function parsePlayerId(value: unknown): number {
  if (typeof value !== "string" && typeof value !== "number") {
    throw new InputError("숫자 플레이어 ID를 입력해 주세요.");
  }
  const text = String(value);
  if (!/^[1-9][0-9]*$/.test(text)) {
    throw new InputError("플레이어 ID는 양의 정수여야 합니다.");
  }
  const id = Number(text);
  if (!Number.isSafeInteger(id)) {
    throw new InputError("플레이어 ID가 너무 큽니다.");
  }
  return id;
}

function record(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? value as Record<string, unknown>
    : null;
}

function matchesPlayerId(value: unknown, playerId: number): boolean {
  try {
    return parsePlayerId(value) === playerId;
  } catch {
    return false;
  }
}

export async function lookupLinkedPlayer(
  playerId: number,
  baseUrl = "https://api.tuforums.com",
  fetchImpl: PlayerFetch = fetch,
): Promise<LinkedPlayer> {
  const origin = new URL(baseUrl);
  const local = origin.protocol === "http:" && ["localhost", "127.0.0.1", "[::1]"].includes(origin.hostname);
  if ((origin.protocol !== "https:" && !local) || origin.username || origin.password || origin.pathname !== "/" || origin.search || origin.hash) {
    throw new Error("Invalid TUF API base URL");
  }
  const url = new URL(`/v2/database/players/${playerId}`, origin);
  let response: Response;
  try {
    response = await fetchImpl(url, {
      method: "GET",
      headers: { Accept: "application/json" },
      redirect: "error",
      signal: AbortSignal.timeout(10_000),
    });
  } catch {
    throw new PlayerLookupError("TUF 계정을 조회할 수 없습니다. 잠시 후 다시 시도해 주세요.", 502);
  }
  if (response.status === 404) throw new PlayerLookupError("해당 플레이어 ID를 찾지 못했습니다.", 404);
  if (!response.ok) throw new PlayerLookupError("TUF 계정을 조회할 수 없습니다. 잠시 후 다시 시도해 주세요.", 502);

  let body: unknown;
  try {
    body = await response.json();
  } catch {
    throw new PlayerLookupError("TUF 계정 조회 결과를 확인할 수 없습니다.", 502);
  }
  const root = record(body);
  const envelope = record(root?.data) ?? root;
  const player = record(envelope?.player) ?? envelope;
  const account = record(player?.user) ?? record(player?.account);
  if (!player || !matchesPlayerId(player.id ?? player.playerId ?? player.player_id, playerId)) {
    throw new PlayerLookupError("TUF 계정 조회 결과의 플레이어 ID가 일치하지 않습니다.", 502);
  }
  if (!account) throw new PlayerLookupError("이 플레이어에 연결된 TUF 계정이 없습니다.", 404);
  if (!matchesPlayerId(account.playerId ?? account.player_id, playerId)) {
    throw new PlayerLookupError("TUF 계정 조회 결과의 연결 정보가 일치하지 않습니다.", 502);
  }
  let userId: string;
  try {
    userId = parseUserId(account.id);
  } catch {
    throw new PlayerLookupError("TUF 계정 조회 결과의 UUID를 확인할 수 없습니다.", 502);
  }
  if (typeof account.username !== "string" || !account.username.trim()) {
    throw new PlayerLookupError("TUF 계정 조회 결과의 사용자 이름을 확인할 수 없습니다.", 502);
  }
  return {
    playerId,
    playerName: typeof player.name === "string" ? player.name : "",
    username: account.username,
    userId,
  };
}
