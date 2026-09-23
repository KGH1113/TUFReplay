import { createAdminHandler } from "./http.controller";
import { createTesterRepository } from "./testers.repository";

const databaseUrl = process.env.DATABASE_URL;
const username = process.env.ADMIN_USERNAME || "operator";
const password = process.env.ADMIN_PASSWORD;
const hostname = process.env.HOST || "127.0.0.1";
const port = Number(process.env.PORT || "4177");

if (!databaseUrl || !password || password.length < 24) {
  throw new Error("DATABASE_URL and ADMIN_PASSWORD (at least 24 characters) are required");
}
if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error("Invalid PORT");
if (!["127.0.0.1", "::1", "localhost"].includes(hostname) && process.env.ALLOW_NON_LOOPBACK !== "true") {
  throw new Error("Non-loopback binding requires ALLOW_NON_LOOPBACK=true");
}

const repository = createTesterRepository(databaseUrl);
// Fail startup if the migration has not been installed or the database is unavailable.
await repository.list();
const assets = {
  html: await Bun.file(new URL("../public/index.html", import.meta.url)).text(),
  css: await Bun.file(new URL("../public/app.css", import.meta.url)).text(),
  javascript: await Bun.file(new URL("../public/app.js", import.meta.url)).text(),
};
const server = Bun.serve({
  hostname,
  port,
  development: false,
  maxRequestBodySize: 4096,
  fetch: createAdminHandler(repository, { username, password }, assets),
  error: () => new Response("Internal error", { status: 500 }),
});
console.info(`Trusted tester administration listening on ${server.url.origin}`);

async function stop() {
  server.stop();
  await repository.close();
}
process.once("SIGINT", () => void stop());
process.once("SIGTERM", () => void stop());
