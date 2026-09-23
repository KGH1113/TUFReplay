import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { SubmissionGalleryDialog } from "@/components/submission/submission-gallery-dialog";
import type { VisualSelection } from "@/models/submission/submission-model";
import { Button } from "@/shared/ui/button";

type Execution = {
  id: string;
  phase: string;
  runId?: string;
  error?: string;
  record?: { external_pass_id?: number };
};
export function VisualE2ERun() {
  const [fixtureId, setFixtureId] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const [gallery, setGallery] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [frozen, setFrozen] = useState<VisualSelection | null>(null);
  const state = useQuery({
    queryKey: ["visual-e2e-runs"],
    queryFn: async () => (await fetch("/harness/state")).json(),
    refetchInterval: 1000,
  });
  const runs: Execution[] = state.data?.executions ?? [];
  const run = runs.find((item) => item.id === selected);
  async function post(path: string, body: unknown) {
    const response = await fetch(path, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const result = await response.json();
    if (!response.ok) throw new Error(result.error ?? "request_failed");
    return result;
  }
  async function start() {
    setBusy(true);
    setError("");
    setFrozen(null);
    try {
      const value = await post("/harness/runs", {
        fixtureId,
        mode: "accepted",
        scenario: "normal",
        speed: 0,
      });
      setSelected(value.id);
    } catch (error) {
      setError(String(error));
    } finally {
      setBusy(false);
    }
  }
  async function submit(selection: VisualSelection | undefined) {
    if (!run) return;
    setBusy(true);
    setError("");
    try {
      await post(`/harness/runs/${run.id}/submit`, selection ? { presentation: selection } : {});
      setFrozen(selection ?? frozen);
      setGallery(false);
    } catch (error) {
      setError(String(error));
    } finally {
      setBusy(false);
    }
  }
  return (
    <section className="mt-8 space-y-4 border-t p-4">
      <h2 className="text-lg font-semibold">실제 제출 · 재생</h2>
      <select
        aria-label="저장된 플레이"
        value={fixtureId}
        onChange={(event) => setFixtureId(event.target.value)}
        className="rounded border bg-background p-2"
      >
        <option value="">플레이 선택</option>
        {(state.data?.fixtures ?? []).map((item: { id: string; song: string }) => (
          <option key={item.id} value={item.id}>
            {item.song}
          </option>
        ))}
      </select>
      <div className="flex gap-3">
        <Button disabled={!fixtureId || busy} onClick={() => void start()}>
          기록 업로드
        </Button>
        <Button disabled={run?.phase !== "evidence_ready" || busy} onClick={() => setGallery(true)}>
          제출
        </Button>
      </div>
      <p role="status">{run?.phase ?? "플레이를 선택해 주세요"}</p>
      {run?.runId && <p className="font-mono text-xs">{run.runId}</p>}
      {(error || run?.error) && <p role="alert">{error || run?.error}</p>}
      {run?.record?.external_pass_id && (
        <a
          href={`http://127.0.0.1:5176/passes/${run.record.external_pass_id}`}
          target="_blank"
          rel="noreferrer"
          className="underline"
        >
          TUF에서 리플레이 열기 #{run.record.external_pass_id}
        </a>
      )}
      <SubmissionGalleryDialog
        open={gallery}
        run={undefined}
        locked={frozen !== null}
        initialSelection={frozen}
        accountKey="00000000-0000-4000-8000-000000000041"
        pending={busy}
        error={error}
        onOpenChange={setGallery}
        onSubmit={(selection) => void submit(selection)}
      />
    </section>
  );
}
