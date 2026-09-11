import type { Execution, Fixture } from "../types";
import "./play-stage.css";

const clock = (us: number) =>
  `${Math.floor(us / 60000000)}:${String(Math.floor(us / 1000000) % 60).padStart(2, "0")}`;
const points = Array.from({ length: 15 }, (_, i) => ({
  x: 50 + i * 48,
  y: 140 + Math.sin(i * 0.85) * 42,
}));

/** A schematic replay stage driven by acknowledged recording time, not chart simulation. */
export function PlayStage({ fixture, run }: { fixture?: Fixture; run?: Execution }) {
  const time = run?.playTimeUs ?? 0;
  const progress = fixture ? Math.min(1, time / fixture.durationUs) : 0;
  const hits = run?.playedHits ?? 0;
  const failed = ["failing", "gameplay_failed"].includes(run?.phase ?? "");
  const cleared = progress >= 1 && !failed;
  const active = run?.phase === "uploading";
  const current = hits % points.length;
  const position = points[current];
  return (
    <div className={`play-stage ${failed ? "play-failed" : ""}`} aria-label="기록 재생 화면">
      <div className="play-hud">
        <span>{active ? "REPLAYING" : failed ? "FAILED" : cleared ? "CLEAR" : "READY"}</span>
        <span>
          {clock(time)} <small>/ {clock(fixture?.durationUs ?? 0)}</small>
        </span>
      </div>
      <svg
        viewBox="0 0 800 280"
        role="img"
        aria-label="입력 기록에 반응하는 도식 경로. 실제 차트 지형이 아닙니다."
      >
        <polyline
          points={points.map((p) => `${p.x},${p.y}`).join(" ")}
          fill="none"
          stroke="#35443d"
          strokeWidth="3"
        />
        {points.map((point, i) => (
          <rect
            key={i}
            x={point.x - 10}
            y={point.y - 10}
            width="20"
            height="20"
            rx="4"
            fill={i === current ? "#9aefcc" : "#24332d"}
            stroke="#577368"
          />
        ))}
        <g
          className="play-orbit"
          style={{ transform: `translate(${position.x}px, ${position.y}px)` }}
        >
          <circle r="23" fill="none" stroke="#9aefcc" opacity="0.25" />
          <circle r="7" fill={failed ? "#f69d90" : "#e8fff5"} />
          <circle
            className={active ? "satellite active" : "satellite"}
            cx="23"
            r="6"
            fill="#9aefcc"
          />
        </g>
      </svg>
      <div className="play-result" aria-live="polite">
        {failed ? (
          <>
            <strong>플레이 실패</strong>
            <span>테스트 실패 주입 · 제출하지 않는 기록</span>
          </>
        ) : cleared ? (
          <>
            <strong>클리어!</strong>
            <span>증거 저장 후 제출할 수 있어요</span>
          </>
        ) : (
          <>
            <strong>
              {hits.toLocaleString()} <small>HITS</small>
            </strong>
            <span>
              {active
                ? "원본 기록 재생 중"
                : run?.phase === "disconnected"
                  ? "연결 대기 · 재생 일시 중지"
                  : "준비되면 플레이를 시작하세요"}
            </span>
          </>
        )}
      </div>
      <div
        className="play-timeline"
        role="progressbar"
        aria-label="곡 진행률"
        aria-valuenow={Math.round(progress * 100)}
        aria-valuemin={0}
        aria-valuemax={100}
      >
        <i style={{ width: `${progress * 100}%` }} />
      </div>
      <div className="play-caption">
        <span>
          {(run?.playedInputs ?? 0).toLocaleString()} INPUTS · {(progress * 100).toFixed(1)}%
        </span>
        <span>기록 기반 도식 · 실제 게임 판정·차트 렌더링 아님</span>
      </div>
    </div>
  );
}
