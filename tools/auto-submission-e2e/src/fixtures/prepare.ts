import { Database } from "bun:sqlite";
import { homedir } from "node:os";
import { join } from "node:path";
import { mkdir } from "node:fs/promises";
import { createHash } from "node:crypto";
import { fixtureDir } from "./store";
import { trimCsv, keyCount } from "./convert";
import { requireSuccessfulClear } from "./eligibility";
import type { Fixture } from "../types";

const dbPath =
  process.env.E2E_REPLAY_DB ??
  join(
    homedir(),
    "Library/Application Support/Steam/steamapps/common/A Dance of Fire and Ice/Mods/TUFReplay/Data/tufreplay.sqlite",
  );
interface Row {
  id: string;
  song: string;
  tuf_level_id: number | null;
  adofai_path: string;
  started_at_utc: string;
  input_csv: Uint8Array;
  hit_context_csv: Uint8Array;
  meta_json: string;
  [key: string]: unknown;
}

export async function prepare() {
  const db = new Database(dbPath, { readonly: true });
  const rows = db
    .query<Row, []>(`SELECT r.*, l.song, l.tuf_level_id, l.adofai_path
    FROM runs r JOIN level_sessions s ON s.id=r.level_session_id JOIN levels l ON l.id=s.level_id
    WHERE r.result='cleared' AND r.start_tile=0 AND r.input_count>0 AND r.hit_context_count>0
    ORDER BY r.started_at_utc DESC`)
    .all();
  db.close();
  const selected: Fixture[] = [],
    skipped: { id: string; reason: string }[] = [];
  const seen = new Set<string>();
  for (const row of rows) {
    if (selected.length === 3) break;
    try {
      requireSuccessfulClear(row);
      if (seen.has(row.adofai_path)) throw new Error("same chart already selected");
      const meta = JSON.parse(row.meta_json);
      if (meta.noFailMode === true) throw new Error("NoFail metadata contradicts successful clear");
      const won = meta.wonTimeUs;
      if (!Number.isSafeInteger(won) || won <= 0) throw new Error("missing clear time");
      const inputs = trimCsv(row.input_csv, 5, 0, won);
      const hits = trimCsv(row.hit_context_csv, 13, 12, won);
      if (!inputs.length || !hits.length) throw new Error("no records before clear");
      const chart = Bun.file(row.adofai_path);
      if (!(await chart.exists())) throw new Error("local chart file missing");
      const bytes = new Uint8Array(await chart.arrayBuffer());
      const sha = createHash("sha256").update(bytes).digest("hex");
      const keys = keyCount(inputs);
      if (keys < 1 || keys > 64) throw new Error("fixture key count outside server contract");
      const target = join(fixtureDir, row.id);
      await mkdir(target, { recursive: true });
      await Bun.write(join(target, "main.adofai"), bytes);
      await Bun.write(join(target, "inputs.csv"), inputs.join("\n") + "\n");
      await Bun.write(join(target, "hits.csv"), hits.join("\n") + "\n");
      const zip = Bun.spawn(
        ["zip", "-q", "-j", join(target, "chart.zip"), join(target, "main.adofai")],
        { stdout: "pipe", stderr: "pipe" },
      );
      if ((await zip.exited) !== 0) throw new Error(await new Response(zip.stderr).text());
      const judgments = [
        "overload",
        "too_early",
        "early",
        "early_perfect",
        "perfect",
        "late_perfect",
        "late",
        "too_late",
        "miss",
      ].map((key) => Number(row[`judgment_${key}`] ?? 0));
      const supplements = [
        "P1 등급·file ID는 mock 값",
        "lifecycle·runtime settings·health 스트림 재구성",
        "검증 결과는 저장 기록 기반 테스트 값",
        "로컬 차트를 가짜 공식 ZIP으로 제공 (과거 호환성 검증 없음)",
      ];
      if (!meta.gameVersion) supplements.push("게임 버전 미기록 → e2e-recorded");
      if (meta.submissionHoldBehavior === undefined)
        supplements.push("hold 설정 미기록 → 테스트 기본값");
      if (!row.tuf_level_id) supplements.push("TUF level ID 미기록 → 테스트 ID");
      selected.push({
        id: row.id,
        song: row.song || `Level ${row.tuf_level_id ?? "local"}`,
        levelId: row.tuf_level_id ?? 900000 + selected.length,
        startedAt: row.started_at_utc,
        durationUs: won,
        inputCount: inputs.length,
        hitCount: hits.length,
        chartSha: sha,
        fileId: `e2e-${sha.slice(0, 16)}`,
        supplements,
        judgments,
        keyCount: keys,
        speed: Number(meta.effectivePitch ?? 1),
        meta,
      });
      seen.add(row.adofai_path);
    } catch (error) {
      skipped.push({ id: row.id, reason: String(error) });
    }
  }
  await mkdir(fixtureDir, { recursive: true });
  await Bun.write(join(fixtureDir, "index.json"), JSON.stringify(selected, null, 2));
  await Bun.write(
    join(fixtureDir, "selection.json"),
    JSON.stringify({ selected: selected.map((f) => f.id), skipped }, null, 2),
  );
  console.log(
    JSON.stringify(
      {
        selected: selected.map((f) => ({
          id: f.id,
          song: f.song,
          inputs: f.inputCount,
          hits: f.hitCount,
        })),
        skipped: skipped.length,
      },
      null,
      2,
    ),
  );
  if (!selected.length)
    throw new Error(
      "No usable clear recordings found. Set E2E_REPLAY_DB to a compatible database.",
    );
}
if (import.meta.main) await prepare();
