import { toolBase } from "./config";
import { fixtures, fixtureDir } from "./fixtures/store";
import { join } from "node:path";
import { logs, log, logSession } from "./logs";
import { executions, start, action } from "./executions";
import { tuf } from "./mock/tuf";
import { localTuf, localTufBase, localFrontendBase } from "./local-tuf/config";
import { isAllowedOrigin } from "./origin";

function forbidden(error: string) {
  return Response.json({ error }, { status: 403 });
}

export function serve() {
  return Bun.serve({
    hostname: "127.0.0.1",
    port: 5152,
    idleTimeout: 30,
    async fetch(request) {
      const url = new URL(request.url);
      if (url.host !== new URL(toolBase).host) return forbidden("Invalid host");
      const origin = request.headers.get("origin");
      if (origin && !isAllowedOrigin(origin)) return forbidden("Invalid origin");
      try {
        if (url.pathname.startsWith("/v2/") || url.pathname.startsWith("/archives/"))
          return await tuf(request);
        if (request.method === "GET" && url.pathname === "/harness/state")
          return Response.json({
            logSession,
            tufTarget: localTuf ? { mode: "local", api: localTufBase, frontend: localFrontendBase } : { mode: "mock" },
            fixtures: await fixtures(),
            executions: executions(),
            logs: logs(Number(url.searchParams.get("after") ?? 0)),
          });
        if (request.method === "GET" && url.pathname === "/harness/selection")
          return new Response(Bun.file(join(fixtureDir, "selection.json")));
        if (request.method === "POST" && url.pathname === "/harness/runs")
          return Response.json(await start(await request.json()), {
            status: 201,
          });
        const match = url.pathname.match(
          /^\/harness\/runs\/([a-f0-9-]+)\/(submit|disconnect|reconnect|fail)$/,
        );
        if (request.method === "POST" && match)
          return Response.json(await action(match[1], match[2]));
        return Response.json({ error: "not_found" }, { status: 404 });
      } catch (error) {
        log("tool", "Request failed", String(error));
        return Response.json({ error: String(error) }, { status: 400 });
      }
    },
  });
}
