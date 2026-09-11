import { Database } from "bun:sqlite";
import { mkdirSync } from "node:fs";
import { join } from "node:path";
import { dataDir } from "../config";
import type { Scenario } from "../types";

mkdirSync(dataDir, { recursive: true });
const db = new Database(join(dataDir, "mock-tuf.sqlite"), { create: true });
db.exec(`CREATE TABLE IF NOT EXISTS receipts (pass_id INTEGER PRIMARY KEY AUTOINCREMENT, run_id TEXT UNIQUE NOT NULL, owner_id TEXT NOT NULL, digest TEXT NOT NULL, body TEXT NOT NULL);
CREATE TABLE IF NOT EXISTS scenarios (run_id TEXT PRIMARY KEY, scenario TEXT NOT NULL, lost INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS register_calls (run_id TEXT PRIMARY KEY, attempts INTEGER NOT NULL);`);

export function registerAttempts(runId: string): number {
  const row = db
    .query<{ attempts: number }, [string]>("SELECT attempts FROM register_calls WHERE run_id=?")
    .get(runId);
  return row?.attempts ?? 0;
}
export function scenario(runId: string, value: Scenario) {
  db.query("INSERT OR IGNORE INTO scenarios(run_id,scenario) VALUES (?,?)").run(runId, value);
}
export function lookup(
  runId: string,
): { pass_id: number; owner_id: string; digest: string } | null {
  return db.query("SELECT pass_id,owner_id,digest FROM receipts WHERE run_id=?").get(runId) as any;
}
export function register(body: any) {
  db.query(
    "INSERT INTO register_calls(run_id,attempts) VALUES (?,1) ON CONFLICT(run_id) DO UPDATE SET attempts=attempts+1",
  ).run(body.run_id);
  const previous = lookup(body.run_id);
  if (
    previous &&
    (previous.owner_id !== body.owner_id || previous.digest !== body.validation.evidence_digest)
  )
    throw new Error("receipt identity conflict");
  db.query("INSERT OR IGNORE INTO receipts(run_id,owner_id,digest,body) VALUES (?,?,?,?)").run(
    body.run_id,
    body.owner_id,
    body.validation.evidence_digest,
    JSON.stringify(body),
  );
  return { pass_id: lookup(body.run_id)!.pass_id, lose: loseRegistrationResponse(body.run_id) };
}
export function loseRegistrationResponse(runId: string): boolean {
  return (
    db
      .query("UPDATE scenarios SET lost=1 WHERE run_id=? AND scenario='receipt-loss' AND lost=0")
      .run(runId).changes > 0
  );
}
