import type { LogEvent } from "./types";
export const logSession = crypto.randomUUID();
const events: LogEvent[] = [];
let sequence = 0;
function redact(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(redact);
  if (value && typeof value === "object")
    return Object.fromEntries(
      Object.entries(value).map(([key, item]) => [
        key,
        /token|authorization|secret/i.test(key) ? "[redacted]" : redact(item),
      ]),
    );
  return value;
}
export function log(source: LogEvent["source"], message: string, detail?: unknown, runId?: string) {
  events.push({
    id: ++sequence,
    time: new Date().toISOString(),
    source,
    message,
    detail: redact(detail),
    runId,
  });
  if (events.length > 2000) events.splice(0, events.length - 2000);
}
export function logs(after = 0) {
  return events.filter((event) => event.id > after);
}
