import type { Fixture } from "../types";

export function localTufSubmissionEligible(item: Fixture | undefined): boolean {
	const difficulty = item?.difficulty;
	if (difficulty?.type !== "PGU") return false;
	return /^[PG](?:[1-9]|1[0-9]|20)$/.test(difficulty.name);
}
