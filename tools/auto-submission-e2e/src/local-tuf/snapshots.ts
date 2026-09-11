import { join } from "node:path";
import { dataDir, toolBase } from "../config";

export interface LocalCatalogSnapshot {
	levelId: number;
	fileId: string;
	difficulty: { type: string; name: string };
}

const snapshotsDir = join(dataDir, "catalog-levels");

export async function localCatalogSnapshot(
	levelId: number,
): Promise<LocalCatalogSnapshot | undefined> {
	const metadata = Bun.file(join(snapshotsDir, String(levelId), "metadata.json"));
	if (!(await metadata.exists())) return undefined;
	const value = (await metadata.json()) as Partial<LocalCatalogSnapshot>;
	if (
		value.levelId !== levelId ||
		typeof value.fileId !== "string" ||
		!value.fileId.trim() ||
		typeof value.difficulty?.type !== "string" ||
		typeof value.difficulty?.name !== "string"
	)
		throw new Error(`Invalid local catalog snapshot for TUF #${levelId}`);
	const archive = Bun.file(localCatalogArchivePath(levelId));
	if (!(await archive.exists()))
		throw new Error(`Missing local catalog archive for TUF #${levelId}`);
	return value as LocalCatalogSnapshot;
}

export function localCatalogArchivePath(levelId: number): string {
	return join(snapshotsDir, String(levelId), "chart.zip");
}

export function localCatalogArchiveUrl(levelId: number): string {
	return `${toolBase}/catalog-archives/${levelId}.zip`;
}
