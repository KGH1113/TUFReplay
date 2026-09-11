import { useEffect, useRef, useState } from "react";
import type { Execution, Fixture, LogEvent } from "../types";
export interface Snapshot {
  tufTarget?: { mode: "local" | "mock"; api?: string; frontend?: string };
  logSession: string;
  fixtures: Fixture[];
  executions: Execution[];
  logs: LogEvent[];
}
export async function command(path: string, body?: unknown) {
  const response = await fetch(`/harness/${path}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const value = await response.json();
  if (!response.ok) throw new Error(value.error);
  return value;
}
export function useSnapshot() {
  const [data, setData] = useState<Snapshot>({
    logSession: "",
    fixtures: [],
    executions: [],
    logs: [],
  });
  const [error, setError] = useState("");
  const cursor = useRef(0);
  const session = useRef("");
  useEffect(() => {
    let stopped = false;
    let timer: ReturnType<typeof setTimeout>;
    async function update() {
      try {
        const response = await fetch(`/harness/state?after=${cursor.current}`);
        if (!response.ok) throw new Error("로컬 실행기에 연결할 수 없습니다");
        const next: Snapshot = await response.json();
        if (stopped) return;
        const restarted = session.current !== next.logSession;
        session.current = next.logSession;
        cursor.current = next.logs.at(-1)?.id ?? (restarted ? 0 : cursor.current);
        setData((old) => ({
          ...next,
          logs: [...(restarted ? [] : old.logs), ...next.logs].slice(-2000),
        }));
        setError("");
      } catch (error) {
        if (!stopped) setError(String(error));
      }
      if (!stopped) timer = setTimeout(update, 700);
    }
    void update();
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, []);
  return { data, error };
}
