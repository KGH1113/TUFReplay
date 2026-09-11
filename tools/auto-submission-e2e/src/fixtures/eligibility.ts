/** A full game-reported clear without NoFail is authoritative for fixture selection. */
export function requireSuccessfulClear(row: Record<string, unknown>) {
  if (row.result !== "cleared" || row.start_tile !== 0) throw new Error("not a full clear");
  if (row.no_fail_mode !== 0) throw new Error("NoFail run excluded from successful-clear fixtures");
  if (row.judgment_difficulty !== 2) throw new Error("Strict judgment required");
}
