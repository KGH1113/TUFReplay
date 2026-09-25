/** The metadata map key is the original archive path, not the flattened client path. */
export function confirmedArchivePath(metadata: unknown): string | null {
	if (!metadata || typeof metadata !== "object")
		throw new Error("Official chart metadata is missing");
	const value = metadata as Record<string, unknown>;
	if (value.pathConfirmed !== true) return null;
	if (
		typeof value.targetLevel !== "string" ||
		!value.levelFiles ||
		typeof value.levelFiles !== "object"
	)
		throw new Error("Official chart selection is incomplete");
	const entries = Object.entries(value.levelFiles).filter(
		([, entry]) =>
			entry &&
			typeof entry === "object" &&
			(entry as Record<string, unknown>).path === value.targetLevel,
	);
	if (entries.length !== 1)
		throw new Error("Official chart selection is ambiguous");
	return entries[0][0];
}

export function verifyArchiveSelection(metadata: unknown, entries: string[]) {
	const target = confirmedArchivePath(metadata);
	if (target !== null && !entries.includes(target))
		throw new Error(
			`Official ZIP does not contain its confirmed chart: ${target}`,
		);
}
