# 10단계: 실서버 배포와 단계적 출시

## 목적

검증된 ingest/persistence/submit pipeline을 `ssh kgh` 환경에 안전하게 배포하고, 게임 플레이와 기존 웹 기능을 방해하지 않으면서 단계적으로 활성화한다.

## 선행조건

- 1~9단계 완료 기준 통과
- 200-connection load/soak test report 통과
- production 인증과 rate limit 활성화
- rollback과 backup 복원 절차 검증

실제 ADOFAI semantic validator가 준비되지 않았다면 production에서는 수집과 evidence 보존까지만 shadow mode로 운영하고 실제 제출 승격은 비활성화한다.

## 인프라 준비

1. PostgreSQL에 자동 backup과 restore test를 구성한다.
2. Redis ingest는 queue/cache Redis와 분리하고 `noeviction`을 유지한다.
3. `TUF_ARTIFACT_ROOT`는 backup 가능한 영속 디스크에 둔다.
4. artifact temp와 final 경로가 같은 filesystem에서 atomic publish 가능한지 확인한다.
5. reverse proxy에서 WebSocket upgrade, idle timeout, body/frame 제한을 설정한다.
6. process와 proxy의 file descriptor limit을 200 connections와 headroom에 맞춘다.
7. TLS, secret injection과 log redaction을 확인한다.
8. migration은 app rollout과 호환되는 expand/contract 방식으로 수행한다.
9. worker와 web process의 CPU/memory 제한을 분리한다.
10. health/readiness는 PostgreSQL, Redis와 필수 storage 상태를 반영하되 일시적 TUF API 장애로 기존 stream까지 모두 내리지 않는다.

## 배포 순서

1. DB migration 적용
2. Redis/storage/backup 점검
3. server 배포, ingest route feature flag off
4. 내부 synthetic client로 smoke test
5. persistence/cleanup worker 소수 concurrency로 시작
6. 개발 계정 allowlist에만 ingest 활성화
7. TUFReplay opt-in canary 배포
8. 지표와 오류를 관찰하며 5% → 25% → 100%로 확대
9. web에는 server-side submit availability가 실제로 생길 때만 버튼 노출

## feature flag

최소한 다음 flag를 독립적으로 제어한다.

- run issuance
- WebSocket ingest
- evidence persistence
- automatic cleanup
- client capture fan-out
- client network sender
- web run list
- submit action

긴급 상황에서 client update 없이 server가 새 run 발급을 중단할 수 있어야 한다. 이미 시작한 run은 가능하면 TTL까지 drain한다.

## rollback

- 새 발급을 먼저 차단한다.
- 기존 WebSocket은 drain deadline까지 유지한다.
- worker를 멈춰도 sealed Redis TTL 안에 evidence가 남도록 한다.
- migration rollback보다 이전 app과 호환되는 schema 유지가 우선이다.
- artifact와 evidence row를 배포 rollback 과정에서 삭제하지 않는다.
- client는 server unavailable 시 자동 제출만 포기하고 로컬 replay를 정상 보존한다.

## 운영 알림

- active connection 또는 handshake failure 급증
- ACK p99 SLO 초과
- Redis memory 70/85/95% 단계 경보
- Redis command latency와 rejected connection
- PostgreSQL pool wait
- persistence queue age
- storage disk 70/85/95%
- evidence persistence failure
- catalog conflict/unstable 급증
- client recorder overflow 증가

## 출시 합격 기준

- canary 사용자에게 frame-time regression이 없다.
- 24시간 soak 동안 process memory와 file descriptor가 안정적이다.
- 정상 네트워크에서 recorder overflow가 없다.
- Redis/DB/storage 한 구성요소의 일시 장애가 게임을 멈추지 않는다.
- 새 발급 차단, drain, rollback과 restore를 실제로 연습했다.
- 200 connections와 clear burst에서 9단계 SLO를 유지한다.
- semantic validator가 없는 동안 잘못된 `submit_available` 또는 실제 제출이 발생하지 않는다.

