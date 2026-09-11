import db from "/Users/kgh/dev/src/tuf-backend/src/models/index.ts";
import { getPoolManagerInstance } from "/Users/kgh/dev/src/tuf-backend/src/config/db.ts";
import { redis } from "/Users/kgh/dev/src/tuf-backend/src/server/services/core/RedisService.ts";

if (
  process.env.DB_DATABASE !== "tuf_web_test" ||
  process.env.DB_HOST !== "127.0.0.1" ||
  process.env.DB_PORT !== "3307"
)
  throw new Error("Demo reset is restricted to the dedicated local TUF database");
if (
  process.env.REDIS_URL !== "redis://127.0.0.1:6380" ||
  (process.env.ELASTICSEARCH_NODE ?? process.env.ELASTICSEARCH_URL) !==
    "http://127.0.0.1:9201" ||
  process.env.ELASTICSEARCH_INDEX_PREFIX !== "tuf_web_test_"
)
  throw new Error("Demo reset requires the dedicated local TUF Redis and Elasticsearch");

const query = async (sql: string) => (await db.sequelize.query(sql))[0] as Array<Record<string, unknown>>;
try {
  const passRows = await query("SELECT id,levelId,playerId FROM passes ORDER BY id");
  const before = {
    passes: passRows.length,
    judgements: Number((await query("SELECT COUNT(*) count FROM judgements"))[0]?.count ?? 0),
    receipts: Number(
      (await query("SELECT COUNT(*) count FROM auto_submission_receipts"))[0]?.count ?? 0,
    ),
    summaries: Number(
      (await query("SELECT COUNT(*) count FROM player_pass_summary"))[0]?.count ?? 0,
    ),
  };

  await db.sequelize.transaction(async (transaction) => {
    await db.sequelize.query("DELETE FROM auto_submission_receipts", { transaction });
    await db.sequelize.query("DELETE FROM passes", { transaction });
    await db.sequelize.query("UPDATE levels SET clears = 0 WHERE clears <> 0", { transaction });
  });
  await db.sequelize.query("ALTER TABLE passes AUTO_INCREMENT = 1");
  await db.sequelize.query("ALTER TABLE auto_submission_receipts AUTO_INCREMENT = 1");

  await redis.connect();
  const cacheKeysRemoved = await redis.delPattern("cache:*");

  const elasticsearch = process.env.ELASTICSEARCH_NODE ?? process.env.ELASTICSEARCH_URL;
  const passesIndex = `${process.env.ELASTICSEARCH_INDEX_PREFIX}passes_v1`;
  const response = await fetch(
    `${elasticsearch}/${passesIndex}/_delete_by_query?refresh=true&conflicts=proceed`,
    {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ query: { match_all: {} } }),
      signal: AbortSignal.timeout(30_000),
    },
  );
  if (!response.ok) throw new Error(`Elasticsearch cleanup failed: HTTP ${response.status}`);
  const esResult = (await response.json()) as { deleted?: number };
  const derivedDocuments = [
    {
      index: `${process.env.ELASTICSEARCH_INDEX_PREFIX}levels_v1`,
      source:
        "ctx._source.clears=0; ctx._source.uniqueClears=0; ctx._source.firstPass=null; ctx._source.firstPPPass=null; ctx._source.highestAccuracy=null",
    },
    {
      index: `${process.env.ELASTICSEARCH_INDEX_PREFIX}players_v1`,
      source:
        "ctx._source.rankedScore=0; ctx._source.generalScore=0; ctx._source.totalScoreV2=0; ctx._source.ppScore=0; ctx._source.wfScore=0; ctx._source.wfPPScore=0; ctx._source.score12K=0; ctx._source.averageXacc=null; ctx._source.universalPassCount=0; ctx._source.worldsFirstCount=0; ctx._source.worldsFirstPPCount=0; ctx._source.totalPasses=0; ctx._source.topDiffId=null; ctx._source.top12kDiffId=null; ctx._source.topDiffSortOrder=null; ctx._source.top12kDiffSortOrder=null; ctx._source.topDiff=null; ctx._source.top12kDiff=null; ctx._source.statsUpdatedAt=params.now",
    },
  ];
  for (const document of derivedDocuments) {
    const update = await fetch(
      `${elasticsearch}/${document.index}/_update_by_query?refresh=true&conflicts=proceed`,
      {
        method: "POST",
        headers: { "content-type": "application/json" },
        body: JSON.stringify({
          script: { lang: "painless", source: document.source, params: { now: new Date().toISOString() } },
          query: { match_all: {} },
        }),
        signal: AbortSignal.timeout(30_000),
      },
    );
    if (!update.ok) throw new Error(`Elasticsearch derived cleanup failed: HTTP ${update.status}`);
  }

  const after = {
    passes: Number((await query("SELECT COUNT(*) count FROM passes"))[0]?.count ?? 0),
    judgements: Number((await query("SELECT COUNT(*) count FROM judgements"))[0]?.count ?? 0),
    receipts: Number(
      (await query("SELECT COUNT(*) count FROM auto_submission_receipts"))[0]?.count ?? 0,
    ),
    summaries: Number(
      (await query("SELECT COUNT(*) count FROM player_pass_summary"))[0]?.count ?? 0,
    ),
  };
  console.log(
    JSON.stringify(
      {
        before,
        after,
        searchDocumentsRemoved: esResult.deleted ?? 0,
        cacheKeysRemoved,
        levelsPreserved: true,
        difficultiesPreserved: true,
        accountsAndOAuthPreserved: true,
      },
      null,
      2,
    ),
  );
} finally {
  await redis.disconnect();
  await getPoolManagerInstance().closeAllPools();
}
process.exit(0);
