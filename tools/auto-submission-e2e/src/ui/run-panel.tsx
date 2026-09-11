import { localTufSubmissionEligible } from "../local-tuf/eligibility";
import type { Execution, Fixture, Mode, Scenario, Speed } from "../types";
import { Button } from "./button";
import { PlayStage } from "./play-stage";

const names: Record<string, string> = {
	issuing: "세션 발급",
	uploading: "플레이 중 · 기록 전송",
	failing: "실패 처리 중",
	gameplay_failed: "플레이 실패 · 제출 불가",
	disconnected: "연결 끊김",
	saving: "증거 저장 중",
	sealed: "증거 저장 중",
	evidence_ready: "증거 저장 완료",
	submitting: "제출 요청",
	validation_pending: "검증 대기",
	registering: "가상 TUF 등록",
	submitted: "가상 제출 완료",
	validator_unavailable: "검증기 미준비",
	error: "실행 오류",
};
export function RunPanel({
	fixture,
	run,
	mode,
	scenario,
	speed,
	setMode,
	setScenario,
	setSpeed,
	start,
	action,
	pending,
	busy,
	localTuf = false,
}: {
	fixture?: Fixture;
	run?: Execution;
	mode: Mode;
	scenario: Scenario;
	speed: Speed;
	setMode: (value: Mode) => void;
	setScenario: (value: Scenario) => void;
	setSpeed: (value: Speed) => void;
	start: () => void;
	action: (name: string) => void;
	pending: boolean;
	busy: boolean;
	localTuf?: boolean;
}) {
	const progress = run?.totalChunks
		? Math.min(100, ((run.ack + 1) / run.totalChunks) * 100)
		: 0;
	const canRegister = !localTuf || localTufSubmissionEligible(fixture);
	return (
		<section className="run-panel">
			<div className="section-heading">
				<div>
					<span className="eyebrow">CLIENT EMULATOR</span>
					<h2>{fixture?.song ?? "기록을 준비하세요"}</h2>
					{fixture?.artist && (
						<p className="muted">
							{fixture.artist}
							{fixture.difficulty ? ` · ${fixture.difficulty.name}` : ""}
							{fixture.creator ? ` · ${fixture.creator}` : ""}
						</p>
					)}
				</div>
				<span className="local-tag">LOCAL ONLY</span>
			</div>
			<PlayStage fixture={fixture} run={run} />
			<div className="settings">
				<label>
					검증 모드
					<select
						aria-label="검증 모드"
						value={mode}
						disabled={busy}
						onChange={(e) => setMode(e.target.value as Mode)}
					>
						<option value="accepted">가상 성공</option>
						<option value="unavailable">검증기 미준비</option>
					</select>
				</label>
				<label>
					전송 속도
					<select
						aria-label="전송 속도"
						value={speed}
						disabled={busy}
						onChange={(e) => setSpeed(Number(e.target.value) as Speed)}
					>
						<option value={1}>1배속</option>
						<option value={10}>10배속</option>
						<option value={0}>최대 속도</option>
					</select>
				</label>
				<label>
					복구 시나리오
					<select
						aria-label="복구 시나리오"
						value={scenario}
						disabled={busy}
						onChange={(e) => setScenario(e.target.value as Scenario)}
					>
						<option value="normal">정상 처리</option>
						<option value="gameplay-fail">플레이 40% 지점에서 실패</option>
						<option value="ack-loss">ACK 수신 전 연결 끊김</option>
						<option value="receipt-loss">등록 후 응답 유실</option>
					</select>
				</label>
			</div>
			<div className="transport">
				<div className="status-line">
					<span className="status-dot" />
					<strong>
						{run
							? localTuf && run.phase === "submitted"
								? "로컬 TUF 제출 완료"
								: localTuf && run.phase === "registering"
									? "로컬 TUF 등록"
									: (names[run.phase] ?? run.phase)
							: "실행 준비"}
					</strong>
				</div>
				<details className="transport-detail">
					<summary>전송 상세 · ACK / 청크 ({progress.toFixed(0)}%)</summary>
					<div
						className="progress"
						role="progressbar"
						aria-label="청크 ACK 진행률"
						aria-valuenow={Math.round(progress)}
						aria-valuemin={0}
						aria-valuemax={100}
					>
						<div style={{ width: `${progress}%` }} />
					</div>
					<div className="metrics">
						<div>
							<span>마지막 ACK</span>
							<b>{run?.ack ?? "—"}</b>
						</div>
						<div>
							<span>전송 청크 / 전체</span>
							<b>{run ? `${run.sentChunks} / ${run.totalChunks}` : "—"}</b>
						</div>
						<div>
							<span>전송 데이터</span>
							<b>{run ? `${(run.sentBytes / 1024).toFixed(1)} KB` : "—"}</b>
						</div>
					</div>
				</details>
				<div className="actions">
					<Button onClick={start} disabled={!fixture || pending || busy}>
						{run ? "다시 플레이" : "플레이 시작"}
					</Button>
					<Button
						variant="ghost"
						onClick={() => action("fail")}
						disabled={pending || run?.phase !== "uploading"}
					>
						지금 실패시키기
					</Button>
					<Button
						variant="secondary"
						onClick={() => action("submit")}
						disabled={
							pending || run?.phase !== "evidence_ready" || !canRegister
						}
					>
						제출
					</Button>
					<Button
						variant="ghost"
						onClick={() =>
							action(run?.phase === "disconnected" ? "reconnect" : "disconnect")
						}
						disabled={
							pending ||
							!["uploading", "disconnected"].includes(run?.phase ?? "")
						}
					>
						{run?.phase === "disconnected" ? "재연결" : "연결 끊기"}
					</Button>
				</div>
			</div>
			<dl className="run-details">
				<dt>서버 run</dt>
				<dd>{run?.runId ?? "아직 발급하지 않음"}</dd>
				<dt>현재 실행</dt>
				<dd>
					{run
						? `${run.mode === "accepted" ? "가상 성공" : "검증기 미준비"} · ${run.speed || "최대"}배속 · ${run.scenario}`
						: "설정은 실행 시작 시 고정됩니다"}
				</dd>
				<dt>{localTuf ? "TUF pass" : "가짜 pass"}</dt>
				<dd>{String(run?.record?.external_pass_id ?? "—")}</dd>
			</dl>
			{run?.error && (
				<p role="alert" className="error">
					{run.error}
				</p>
			)}
			{!canRegister && fixture?.difficulty && (
				<p role="status" className="hint">
					{fixture.difficulty.name} 난이도는 실제 TUF의 P/G 자동 제출 대상이
					아닙니다. 업로드 흐름은 테스트할 수 있지만 pass로 등록할 수 없습니다.
				</p>
			)}
			<p className="hint">
				업로드 완료 후 제출을 눌러 검증을 시작합니다. 가상 성공은 게임플레이의
				유효성을 판정하지 않습니다.
			</p>
			{fixture && (
				<details className="fixture-detail">
					<summary>원본 기록과 테스트 보완 항목</summary>
					<p className="mono">{fixture.id}</p>
					<ul>
						{fixture.supplements.map((item) => (
							<li key={item}>{item}</li>
						))}
					</ul>
					<p>원본 입력·hit context는 클리어 시간까지만 전송합니다.</p>
				</details>
			)}
		</section>
	);
}
