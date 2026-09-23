# 재사용 레벨 세션 v2

## 연결과 시도의 분리

`GET /api/v2/levels/{level_id}/runs/stream`은 OAuth bearer로 인증하는 양방향 WebSocket이다. 모드는 레벨을 열 때 연결을 준비하고 여러 시도에 재사용한다. 연결·heartbeat만으로 PostgreSQL run이나 Redis evidence를 만들지 않는다. Redis에는 만료 가능한 계정별 연결 lease만 유지한다.

각 시도의 UUID는 클라이언트가 첫 캡처 전에 생성하며 서버 run PID로 사용한다. `Begin`은 네트워크나 설치 파일 읽기 없이 고정 크기 capture buffer를 붙인다. 백그라운드 worker가 설치 정보 읽기, 계정 토큰 획득, 연결, 승인, 직렬화, ACK 및 terminal 처리를 맡는다. 시도 등록에는 최대 8개 항목의 짧은 admission lock이 있으며 그 안에서 I/O를 하지 않는다. 입력 capture callback은 기존 고정 ring buffer 경로를 사용한다.

레벨 변경 알림용 `/api/v1/levels/{id}/changes`는 별도의 읽기 전용 연결로 유지한다. 제출 권한이 필요한 v2 업로드 소켓과 합치지 않는다.

## Wire 계약

첫 프레임은 `{"type":"session_hello","protocol_version":2}`이며 서버는 `session_ready`와 `protocol_version:2`를 돌려준다. 미지원 버전은 `unsupported_protocol`로 거절한다. session control 및 run control은 JSON text, evidence는 binary다.

| 클라이언트 → 서버 | 추가 필드 | 서버 응답 |
| --- | --- | --- |
| `heartbeat` | 없음 | `heartbeat` |
| `run_start` | `run_id`, `client_game_version`, `client_mod_version`, `client_tuf_file_id`, `client_level_relative_path`, `last_acknowledged_sequence` | `ready` 또는 이미 완료된 `sealed` |
| `run_heartbeat` | `run_id` | `ack` |
| `run_fail` | `run_id` | `failed`, 이미 완료되었으면 `sealed` |
| `run_complete` | `run_id`, `final_sequence`, `input_count`, `hit_context_count` | `sealed` |

`ready`에는 `run_id`, `acknowledged_sequence`, `max_chunk_bytes`, `heartbeat_interval_ms`가 포함된다. `ack`/`sealed`는 `run_id`와 `acknowledged_sequence`, `nack`은 `run_id`와 `expected_sequence`를 포함한다. 클라이언트 ACK 초기값은 -1이고 실제 resume 위치는 서버 ACK를 따른다. 서버는 알 수 없는 control 필드와 메시지 형식을 거절한다.

Binary envelope는 다음과 같다.

| Offset | 길이 | 내용 |
| --- | --- | --- |
| 0 | 4 | ASCII `TUF2` |
| 4 | 16 | run UUID, RFC 4122/network byte order |
| 20 | 20 | 기존 `TUFR` v1 chunk header |
| 40 | 가변 | 기존 chunk payload |

내부 header의 version, kind, flags, big-endian sequence 및 길이와 evidence 바이트는 그대로다. C# `Guid.ToByteArray()`의 mixed-endian 순서를 그대로 전송하지 않는다. Rust/C# 테스트는 `00112233-4455-6677-8899-aabbccddeeff`의 동일한 wire 바이트를 확인한다. evidence manifest와 replay 파일 버전은 v1을 유지한다.

오류는 `{"type":"error","run_id":UUID또는null,"code":문자열,"terminal":true}`다. run 오류는 해당 시도의 전송을 중단하라는 뜻이며, 모든 오류가 소켓 자체를 닫는다는 뜻은 아니다. 권한 실패, 메시지 rate 초과, handshake 오류는 연결도 종료한다. 대표 code는 다음과 같다.

| Code | 의미 |
| --- | --- |
| `submission_authorization_unavailable` | 현재 grant/제출 권한을 확인할 수 없거나 철회됨 |
| `level_not_eligible` / `catalog_unavailable` | P/G 대상 아님 / catalog 조회 실패 |
| `previous_run_active` | 현재 소켓의 이전 시도가 아직 active |
| `run_owner_mismatch` / `run_start_conflict` | 소유자·grant 불일치 / 같은 UUID의 불변 claim 불일치 |
| `run_evidence_expired` | 기존 run의 Redis 상태 소실; 새 evidence로 덮어쓰지 않음 |
| `run_not_active` / `stream_conflict` | 다른 시도의 frame / 새 연결이 fencing한 이전 연결 |
| `completion_conflict` / `run_failed` | 기존 terminal 결과와 충돌 |
| `run_start_rate_exceeded` / `message_rate_exceeded` | 시도/메시지 자원 한도 도달 |

## 상태와 복구

1. 레벨을 열면 idle 연결을 준비한다. 실패해도 로컬 기록 및 게임은 계속된다.
2. 시도가 시작되면 UUID와 capture buffer를 즉시 붙여 첫 입력부터 보존한다. 이전 시도 정리 중이어도 queue에 독립 시도로 등록한다.
3. worker가 `run_start`를 보낸다. 서버는 매번 현재 계정 grant, 제출 권한, 레벨 eligibility 및 기존 UUID의 소유자와 claims를 확인한다. 최초 요청만 run과 submission record를 생성한다.
4. `ready` 이후에만 evidence를 보낸다. 시작 응답 유실은 같은 UUID의 `run_start`로 복구하며, ACK 유실은 기존 journal에서 바이트가 동일한 frame을 재전송한다.
5. fail이면 worker가 `run_fail` 영수증을 받은 뒤 다음 시도를 전송한다. 게임 스레드는 이 통신을 기다리지 않는다. 승인 전에 이미 실패한 시도도 승인 가능하면 start/fail로 독립 처리한다.
6. clear 후 editor 복귀에서 capture를 마감한다. 모든 evidence를 drain/ACK한 뒤 `run_complete`를 보내고 `sealed`를 받아야 업로드 완료다. 완료 응답 유실은 같은 UUID로 terminal receipt를 다시 받는다.

서버의 기존 Redis connection fencing과 계정당 active upload 1개 제한을 유지한다. 새 연결이 같은 run을 재개하면 이전 연결의 frame은 `stream_conflict`다. 다른 UUID의 지연 frame은 현재 시도에 섞이지 않는다. 완료가 먼저 확정되면 늦은 fail은 seal을 취소하지 않고, fail이 먼저 확정되면 complete를 거절한다. 이전 run의 중복 fail은 새 active run을 해제하지 않는다.

연결 단절 자체는 fail이 아니다. 네트워크 변경, 절전, 서버 listener 재시작 뒤 Redis TTL 및 클라이언트 기한 내라면 같은 UUID와 ACK로 재개한다. Redis 상태가 사라졌거나 기한을 넘겼으면 해당 자동 제출만 포기한다. 로컬 replay를 나중에 새 run으로 소급 업로드하지 않는다.

레벨 변경 시 이전 session은 새 시도를 받지 않고 미완료 시도를 무효화한다. 이미 capture가 완료된 시도는 전송을 마친 뒤 닫는다. 로그아웃, 자동 캡처 OFF, 권한 상실은 session을 취소한다. 서버가 미완료 run을 정리하지 못한 경우 TTL 및 기존 reconciliation이 처리한다. idle 연결은 run hard duration과 무관하게 유지·재연결할 수 있다.

## 임시 방어 한도

| 항목 | 현재 값 |
| --- | --- |
| 클라이언트 active + 대기 시도 | 최대 8개 |
| 시도당 capture ring | 최대 8,192 records |
| 최초 승인 기한 | 시도 생성부터 10초, queue 대기 포함 |
| 정상 승인 후 outage 복구 | 오류 감지부터 최대 30초 |
| socket connect/send/receive | 작업당 최대 8초 |
| 실패한 시도의 추가 cleanup | 최대 2초 |
| 계정당 v2 연결 | 최대 4개, 60초 lease, 5초 갱신 |
| 계정당 신규 run | 최대 120개/60초 window |
| 소켓당 메시지 | 최대 256개/초 window |
| 서버 idle 수신 기한 | 45초, 클라이언트 heartbeat는 약 1초 |
| 서버 권한 재확인 | 5초마다, run start 및 terminal에서도 확인 |
| chunk / run payload | 기존 64 KiB / 128 MiB 기본값 |
| active run TTL / hard duration | 기존 45초 / 6시간 기본값 |

overflow, 시작 거절, timeout은 해당 시도의 자동 제출만 중단한다. 실게임 latency 및 입력 밀도 측정 전의 자원 방어값이며 서비스 사용량 정책으로 확정한 값은 아니다. 8개 queue와 승인 기한 때문에 네트워크가 장시간 막힌 상태의 모든 반복 시도를 서버에 보존한다고 보장하지 않는다.

## 호환성과 적용

기존 `POST /api/v1/runs`, run별 v1 WS, 제출·목록·조회·리플레이 API는 유지한다. 현재 모드 production orchestration은 REST preflight/lease 경로를 사용하지 않는다. 모드 health의 `AutoSubmissionProtocolVersion`과 companion web의 호환 검사 값은 2다. 표준 빌드는 0을 유지한다. v2 서버·모드·companion web을 함께 적용해야 새 흐름이 사용된다.

## 검증

- `LevelSubmissionSessionSuite`: 같은 소켓의 즉시 반복 실패/재시작, 승인 전 첫 입력 보존, start/ACK/seal 응답 유실, 승인 timeout, capture/시도 queue 상한, terminal 승자, 레벨 변경 시 queued capture 정리와 완료 시도 drain, idle 재연결 및 승인 거절 후 새 시도 복구.
- Rust integration `reusable_level_socket_has_no_idle_runs_and_survives_immediate_restarts`: idle 중 DB run 미생성, archive 미다운로드, 반복 시도 독립성, Redis 실패 evidence 정리, ACK 복구, 이전 연결 fencing, 중복 control과 claims 충돌, UUID 격리, seal 뒤 fail, 레벨/소유자 범위, Redis 상태 소실, protocol mismatch 및 권한 철회.
- 기존 v1 API, 제출, persistence, auth 및 Redis 한도 테스트를 함께 유지한다.

검사 진입점:

```sh
env TUFREPLAY_BUILD_FLAVOR=auto-submission ./scripts/run.sh mod-check
./scripts/run.sh server-check
./scripts/run.sh server-check --integration
./scripts/run.sh web-check
```

실제 게임의 즉시 재시작, 수시간 idle 뒤 플레이, OS 절전·네트워크 변경, 실제 프로세스 재시작, 200개 장시간 연결 및 main-thread p99 측정은 실환경 확인 대상이다. 단기 기능 테스트를 장시간 부하/SLO 검증으로 취급하지 않는다. 사용자 개발 서버나 게임을 자동으로 재시작하지 않는다.

2026-09-23 로컬 검증 결과:

- `mod-check`: auto-submission 빌드, Unity/Mono 호환성 및 C# 전체 테스트 통과. 모드 설치는 생략했다.
- `server-check`: Rust format/check 및 library 테스트 42개 통과.
- `server-check --integration`: PostgreSQL/Redis를 사용하는 통합 테스트 22개 통과. 실제 visual source fixture가 필요한 기존 테스트 1개는 기본 ignore 상태로 실행하지 않았다.
- `web-check`: 테스트 158개, 타입 검사, Biome 및 production build 통과. 기존 bundle 크기 경고가 남는다.
- `git diff --check`: 통과. 배포와 개발 서버 재시작은 수행하지 않았다.
