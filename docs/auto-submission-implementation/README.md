# Auto Submission 안정화 구현 로드맵

## 목표

이 로드맵은 다음 두 가지 제품 요구사항을 만족시키기 위한 구현 순서를 정의한다.

1. 플레이어는 플레이 중 `NativeInput`과 `HitContext`가 기록되고 서버로 전송된다는 사실을 체감하지 않는다. 플레이가 끝난 뒤 웹에서 제출 가능한 run을 확인하고 제출 버튼을 누르는 것만 알면 된다.
2. 서버는 WebSocket 연결 약 200개가 지속되는 상황에서 ingest, reconnect, seal, 영속화와 조회 API를 안정적으로 제공한다.

실제 ADOFAI gameplay를 가상으로 재현하는 semantic validation은 이 로드맵의 범위 밖이다. 다만 evidence의 무결성 검사, 제출 가능 상태로의 전환 인터페이스, 나중에 validator를 연결할 job 경계는 포함한다.

## 변하지 않는 설계

- PostgreSQL 모델은 `run_sessions`를 유지한다.
- 발급 API는 `POST /api/v1/runs`를 유지한다.
- WebSocket은 `GET /api/v1/runs/{run_id}/stream`을 유지한다.
- 전송 상태의 authoritative store는 Redis다.
- PostgreSQL은 lifecycle과 내구성 있는 evidence index를 관리한다.
- `NativeInput`, `HitContext`, `MetaJson`은 기존 replay pipeline에서 다시 사용할 수 있는 바이트를 보존한다.
- 게임 main thread에서는 네트워크, JSON 직렬화, 압축, SQLite polling을 하지 않는다.
- client claim은 eligibility 안내에만 사용하고 신뢰 근거로 사용하지 않는다.
- headless ADOFAI를 실행하지 않는다.

## 단계

1. [프로토콜 계약과 성능 기준 고정](01-protocol-and-performance-contract.md)
2. [TUF catalog와 run 발급 안정화](02-catalog-and-run-issuance.md)
3. [sealed evidence 영속화와 정리](03-evidence-persistence-and-cleanup.md)
4. [인증, 소유권과 abuse 방어](04-auth-ownership-and-abuse.md)
5. [TUFReplay capture fan-out과 hot path](05-client-capture-fanout.md)
6. [TUFReplay 백그라운드 업로드 transport](06-client-upload-transport.md)
7. [run lifecycle, 제출 API와 상태 전환](07-run-lifecycle-and-submit-api.md)
8. [웹 submit UX](08-web-submit-ux.md)
9. [관측성, 용량 모델과 200-connection 부하 검증](09-observability-and-load-testing.md)
10. [실서버 배포와 단계적 출시](10-production-rollout.md)

각 단계는 이전 단계의 완료 기준을 통과한 후 시작한다. 단, 5단계의 capture microbenchmark와 9단계의 부하 테스트 도구 준비는 서버 작업과 병렬로 선행할 수 있다.

## 전체 완료 기준

- 플레이 중 auto submission 때문에 발생한 main-thread 시간 증가가 p99 기준 0.1 ms 이하이며 프레임 hitch를 만들지 않는다.
- 업로드 backlog에 명시적인 메모리 상한이 있고, 상한 도달 시 게임을 방해하지 않고 해당 run의 자동 제출만 포기한다.
- 200개 지속 WebSocket과 reconnect burst에서 ACK latency와 event-loop lag가 정의된 SLO를 만족한다.
- seal된 evidence는 Redis가 사라져도 내구성 저장소에서 byte-for-byte 복구할 수 있다.
- 중복 chunk, 중복 complete, worker 재시도와 submit 재시도가 모두 idempotent하다.
- 사용자는 게임 내에서 정상 플레이 흐름을 방해받지 않고 웹에서 `처리 중`, `제출 가능`, `제출 완료`, `제출 불가`를 이해할 수 있다.
- 장애가 발생해도 게임 플레이와 로컬 replay 기록은 계속되며, 자동 제출 실패가 사용자 데이터 손상으로 이어지지 않는다.

