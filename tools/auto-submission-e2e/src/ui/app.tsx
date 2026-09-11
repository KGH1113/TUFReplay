import { useState } from "react";
import type { Mode, Scenario, Speed } from "../types";
import { useSnapshot, command } from "./api";
import { RunPanel } from "./run-panel";
import { LogPanel } from "./log-panel";

export function App() {
  const { data, error } = useSnapshot();
  const [selected, setSelected] = useState("");
  const [executionId, setExecutionId] = useState("");
  const [mode, setMode] = useState<Mode>("accepted"),
    [scenario, setScenario] = useState<Scenario>("normal"),
    [speed, setSpeed] = useState<Speed>(10);
  const [pending, setPending] = useState(false),
    [actionError, setActionError] = useState("");
  const fixture = data.fixtures.find((f) => f.id === selected) ?? data.fixtures[0];
  const run =
    data.executions.find((r) => r.id === executionId) ??
    data.executions.find((r) => r.fixtureId === fixture?.id);
  const busy = data.executions.some((r) =>
    ["issuing", "uploading", "disconnected", "saving", "sealed", "failing"].includes(r.phase),
  );
  async function act(path: string, body?: unknown) {
    setPending(true);
    setActionError("");
    try {
      const result = await command(path, body);
      setExecutionId(result.id);
    } catch (error) {
      setActionError(String(error));
    } finally {
      setPending(false);
    }
  }
  return (
    <div className="workspace">
      <header>
        <div className="brand">
          <span className="brand-mark">T</span>
          <strong>TUFReplay</strong>
          <span className="separator">/</span>
          <span>Submission Lab</span>
        </div>
        <span className="connection">
          <i />
          {error ? "실행기 연결 확인 필요" : "로컬 테스트 환경"}
        </span>
      </header>
      <div className="layout">
        <aside>
          <span className="eyebrow">RECORDED CLEARS</span>
          <h1>
            클리어 기록 <small>{data.fixtures.length}</small>
          </h1>
          <p className="muted">원본 리플레이로 서버 흐름 테스트</p>
          <nav aria-label="클리어 기록">
            {data.fixtures.map((item, index) => (
              <button
                key={item.id}
                className={`fixture ${fixture?.id === item.id ? "selected" : ""}`}
                disabled={busy}
                onClick={() => {
                  setSelected(item.id);
                  setExecutionId("");
                }}
              >
                <span className="track-number">0{index + 1}</span>
                <span>
                  <strong>{item.song}</strong>
                  <small>
                    #{item.levelId}
                    {item.difficulty ? ` · ${item.difficulty.name}` : ""} ·{" "}
                    {(item.durationUs / 1000000).toFixed(0)}초
                  </small>
                  {item.artist && <small>{item.artist}</small>}
                  <small>
                    {item.inputCount.toLocaleString()} inputs / {item.hitCount.toLocaleString()}{" "}
                    hits
                  </small>
                </span>
              </button>
            ))}
          </nav>
          {!data.fixtures.length && (
            <p className="empty">
              터미널에서 <code>bun run e2e:prepare</code>를 실행하세요.
            </p>
          )}
          <details className="history">
            <summary>이전 실행 ({data.executions.length})</summary>
            {data.executions.map((r) => (
              <button
                key={r.id}
                onClick={() => {
                  setSelected(r.fixtureId);
                  setExecutionId(r.id);
                }}
              >
                {r.startedAt.slice(11, 19)} · {r.phase}
              </button>
            ))}
          </details>
          <a className="selection-link" href="/harness/selection" target="_blank" rel="noreferrer">
            기록 선정·제외 내역 ↗
          </a>
          <div className="boundary">
            <span className="eyebrow">TEST BOUNDARY</span>
            <p>실제 Rust 서버 · 실제 저장소</p>
            <p>{data.tufTarget?.mode === "local" ? "로컬 TUF에 실제 pass 등록" : "TUF API는 테스트 대역"}</p>
            <p>차트·검증 결과는 테스트 대역</p>
          </div>
        </aside>
        <main>
          {(error || actionError) && (
            <p className="error" role="alert">
              {error || actionError}
            </p>
          )}
          <RunPanel
            localTuf={data.tufTarget?.mode === "local"}
            fixture={fixture}
            run={run}
            mode={mode}
            scenario={scenario}
            speed={speed}
            setMode={setMode}
            setScenario={setScenario}
            setSpeed={setSpeed}
            pending={pending}
            busy={busy}
            start={() =>
              void act("runs", {
                fixtureId: fixture?.id,
                mode,
                scenario,
                speed,
              })
            }
            action={(name) => run && void act(`runs/${run.id}/${name}`)}
          />
          <LogPanel logs={data.logs} />
          {data.tufTarget?.mode === "local" && (
            <a href={`${data.tufTarget.frontend}/passes${run?.record?.external_pass_id ? `/${run.record.external_pass_id}` : ""}`} target="_blank" rel="noreferrer">
              {run?.record?.external_pass_id ? `TUF pass #${run.record.external_pass_id} 보기 ↗` : "로컬 TUF 제출 기록 보기 ↗"}
            </a>
          )}
        </main>
      </div>
      <footer>
        <span>127.0.0.1:5151 · AUTO SUBMISSION</span>
        <span>외부 TUF 요청 없음</span>
        <span>{data.tufTarget?.mode === "local" ? "127.0.0.1:3002 · LOCAL TUF" : "127.0.0.1:5152 · MOCK TUF"}</span>
      </footer>
    </div>
  );
}
