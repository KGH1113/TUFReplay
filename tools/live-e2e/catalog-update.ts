interface InstalledChart {
	levelId: number;
	fileId: string;
}

interface LocalLevel {
	id: number;
	fileId: string | null;
	dlLink: string | null;
}

/** Only the dedicated local seed may override the CDN-derived fileId hook. */
export function localCatalogPatch(
	level: LocalLevel | null,
	chart: InstalledChart,
) {
	if (!level || level.id !== chart.levelId)
		throw new Error(
			`TUF #${chart.levelId}: level is missing from the local catalog`,
		);
	if (typeof chart.fileId !== "string" || !chart.fileId.trim())
		throw new Error(`TUF #${chart.levelId}: installed fileId is missing`);
	const dlLink = `http://127.0.0.1:5152/charts/${chart.levelId}.zip`;
	// Earlier versions changed dlLink via beforeSave, which erased fileId for
	// non-CDN URLs. Repair only that exact state produced by this local runner.
	const erasedByLocalSeed = level.fileId === null && level.dlLink === dlLink;
	if (level.fileId !== chart.fileId && !erasedByLocalSeed)
		throw new Error(
			`TUF #${chart.levelId}: installed fileId ${chart.fileId} differs from local catalog ${level.fileId ?? "(missing)"}. Synchronize the local catalog and installed chart before preparing.`,
		);
	return { dlLink, fileId: chart.fileId };
}
