import { useState } from "react";
import type { LogEvent } from "../types";

export function LogPanel({ logs }: { logs: LogEvent[] }) {
  const [source, setSource] = useState("all");
  const filtered = logs
    .filter((event) => source === "all" || event.source === source)
    .slice(-150)
    .reverse();
  return (
    <section className="log-panel">
      <div className="section-heading">
        <div>
          <span className="eyebrow">REQUEST INSPECTOR</span>
          <h2>요청과 응답</h2>
        </div>
        <label className="filter">
          소스{" "}
          <select
            aria-label="로그 소스"
            value={source}
            onChange={(event) => setSource(event.target.value)}
          >
            <option value="all">전체</option>
            <option value="client">모드 클라이언트</option>
            <option value="tuf">TUF 요청·응답</option>
            <option value="server">Rust 서버</option>
            <option value="tool">실행기</option>
          </select>
        </label>
      </div>
      <div className="log-list" role="log" aria-label="요청 로그">
        {!filtered.length && <p className="empty">실행하면 요청과 ACK가 여기에 표시됩니다.</p>}
        {filtered.map((event) => (
          <details key={event.id} className="log-entry">
            <summary>
              <time>{event.time.slice(11, 23)}</time>
              <span className={`source source-${event.source}`}>{event.source}</span>
              <span className="log-message">{event.message}</span>
            </summary>
            <pre>
              {JSON.stringify(
                event.detail ?? { message: event.message, runId: event.runId },
                null,
                2,
              )}
            </pre>
          </details>
        ))}
      </div>
      <div className="log-foot">최근 150개 표시 · 요청을 펼쳐 본문 확인 · 인증 토큰은 숨김</div>
    </section>
  );
}
