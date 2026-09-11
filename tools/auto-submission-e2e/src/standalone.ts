import { preflight } from "./preflight";
import { requireFreePorts } from "./ports";
import { serve } from "./service";
await preflight();
await requireFreePorts([5152]);
const server = serve();
console.log("Local client runner and mock TUF: http://127.0.0.1:5152");
process.on("SIGINT", () => void server.stop(true));
process.on("SIGTERM", () => void server.stop(true));
