export const LEGACY_REPLAY_NOTICE_STORAGE_KEY = "tuf-replay.legacy-replay-notice-ack.v1";

type ReadableStorage = Pick<Storage, "getItem">;
type WritableStorage = Pick<Storage, "setItem">;

export function hasAcknowledgedLegacyReplayNotice(
  storage: ReadableStorage | null = readableLocalStorage(),
) {
  try {
    return storage?.getItem(LEGACY_REPLAY_NOTICE_STORAGE_KEY) === "true";
  } catch {
    return false;
  }
}

export function acknowledgeLegacyReplayNotice(
  storage: WritableStorage | null = writableLocalStorage(),
) {
  try {
    storage?.setItem(LEGACY_REPLAY_NOTICE_STORAGE_KEY, "true");
  } catch {
    // The notice still closes for this page when storage is unavailable.
  }
}

function readableLocalStorage() {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

function writableLocalStorage() {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}
