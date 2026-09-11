import type { Fixture } from "../types";

export function catalogFileId(
	item: Fixture,
	useLocalTuf: boolean,
): string | undefined {
	return useLocalTuf ? item.actualFileId : item.fileId;
}
