# TUFReplay auto-submission API

Loco 1.1 / SeaORM 2 기반 API다. OAuth로 인증된 run 발급, WebSocket 증거 수신, 영속 저장, 제출 시 최신 공식 차트의 임시 확보, 검증과 TUF 등록을 담당한다.

**게임플레이 시뮬레이터는 미구현이다.** 기본 검증기는 `validator_unavailable`을 반환하며 실제 pass를 승인하지 않는다. 기존 영상 제출을 대체하지 않는다.

설정, 프로토콜, 레이어 구성, 검증 결과와 남은 과제는 [auto-submission 상태 문서](../docs/auto-submission-status.md)를 참고한다.

게임과 실제 TUF API 없이 클리어 리플레이로 서버를 테스트하려면 [독립 E2E Lab 실행 안내](../tools/auto-submission-e2e/README.md)를 참고한다. 저장소 루트에서 `bun run e2e:prepare`, `bun run e2e:dev`로 시작하며 전용 DB·Redis namespace·Storage를 사용한다.

```sh
cargo loco db migrate
SCHEDULER_CONFIG=config/scheduler.yaml cargo loco start --all
```

PostgreSQL과 Redis가 필요하다. 환경별 연결과 저장소 설정은 `config/`에 있다. 기본 포트는 5150이다. 양쪽 서버에 서로 다른 고정 토큰 `TUF_TO_AUTO_SUBMISSION_TOKEN`과 `AUTO_SUBMISSION_TO_TUF_TOKEN`을 설정한다. 각각 32–512바이트의 공백 없는 무작위 값으로 생성하고 환경변수로만 주입한다. 누락되거나 두 값이 같으면 internal 인증을 거절한다. 이전 `TUF_SUBMISSION_SERVICE_SECRET`은 사용하지 않는다. TUF backend의 `AUTO_SUBMISSION_API_URL`, 필요한 경우 `TUF_API_BASE_URL`과 `SUBMISSION_WEB_ORIGIN`도 설정한다. 운영 환경의 공개 리플레이 조회에는 정확한 TUF 프론트엔드 origin을 `TUF_WEB_ORIGIN`으로 설정해야 한다.

제출 완료된 자동 제출 리플레이는 인증 없이 `GET /api/v1/replays/{run_id}`에서 manifest를 조회하고, 응답의 파일 URL에서 개별 증거를 스트리밍할 수 있다. 미제출·실패·삭제된 run은 공개하지 않으며 Storage key와 서버 수신 순서 증거도 노출하지 않는다. TUF 프론트엔드와 web-adofai의 전달 계약은 [리플레이 전달 계약](../docs/replay-delivery-contract.md)에 정의한다.

```sh
cargo test
```

통합 테스트는 `config/test.yaml`의 별도 테스트 DB를 재생성한다. 테스트 설정을 운영 DB로 연결하지 않는다.

## 실행과 폴더 구조

위 실행 명령은 HTTP 서버, Worker, Scheduler를 함께 시작한다. 프로세스를 분리하려면 `cargo loco start`, `cargo loco start --worker`, `cargo loco scheduler --config config/scheduler.yaml`을 각각 실행한다. HTTP 서버만 실행하면 큐에 들어간 작업은 Worker가 시작될 때까지 처리되지 않는다.

| 위치 | 책임 |
| --- | --- |
| `src/app.rs`, `src/initializers/` | Loco 부팅, 공유 설정·클라이언트 구성 |
| `src/controllers/` | HTTP/WS 입력·응답과 인증 경계 |
| `src/models/` | SeaORM 모델, DB 조회·트랜잭션·원자적 상태 전이 |
| `src/models/_entities/` | 자동 생성된 DB 엔티티 |
| `src/services/auth/`, `src/services/tuf/` | 계정 인증과 TUF 연동 |
| `src/services/ingest/` | Redis 업로드 세션, 청크·ACK·연결 수명 |
| `src/services/submission/`, `src/services/evidence/` | 발급·검증·등록 흐름과 증거 보관 |
| `src/services/replays.rs`, `src/controllers/replays/` | 제출된 리플레이의 공개 manifest와 파일 스트리밍 |
| `src/domain/`, `src/protocol/` | 검증·등록 인터페이스와 전송 계약 |
| `src/workers/`, `src/tasks/` | Loco 큐 작업과 주기적 복구·정리 진입점 |
| `config/scheduler.yaml` | 복구·정리 스케줄 |

작업 실행은 Loco의 Postgres 큐를 사용한다. 제출 상태의 재시도 시점·DB lease·TUF 등록 영수증은 중복 처리와 응답 유실 복구를 위해 모델과 서비스에 유지한다. Scheduler는 대기 작업을 큐에 넣고 Worker가 처리한다. 저장소는 `AppContext.storage`, DB는 `AppContext.db`를 사용한다. 과거 공식 차트의 다운로드·캐시·호환성 비교 경로는 제거했지만 기존 관련 DB 테이블과 데이터는 유지한다.

계정 권한은 모드가 보낸 TUF OAuth access token을 TUF의 internal identity API에서 확인한다. 개별 업로드에는 별도의 run token을 쓰고, 연결 중과 제출 처리 중에도 OAuth grant 권한을 재확인한다. Loco SaaS starter의 자체 사용자/JWT 가입 기능은 사용하지 않는다.
