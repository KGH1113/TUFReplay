# 1단계: 프로토콜 계약과 성능 기준 고정

## 목적

Rust 서버와 C# sender가 독립적으로 구현되어도 동일한 byte stream과 상태 전이를 해석하도록 현재 ingest protocol을 고정한다. 이후 단계에서 wire format 변경으로 client와 server를 동시에 다시 고치는 일을 막는다.

## 범위

- `POST /api/v1/runs` 요청/응답 계약
- `GET /api/v1/runs/{run_id}/stream` WebSocket 계약
- JSON control frame과 binary chunk header
- sequence, ACK, reconnect, heartbeat, fail, complete, seal 의미
- error code와 protocol-version 호환 정책
- client와 server의 성능 예산

게임 판정 규칙과 제출 승인 판단은 포함하지 않는다.

## 구현 작업

1. protocol version 1의 필드, 정수 크기, byte order와 최대값을 코드 상수로 모은다.
2. binary header의 magic, version, kind, flags, `u64 sequence`, `u32 payload_length`를 Rust와 C#에서 동일하게 정의한다.
3. chunk kind별 payload 규칙을 고정한다.
   - `NativeInputCsv`와 `HitContextCsv`는 완결된 record 경계에서만 자른다.
   - `MetaJson`은 하나의 논리 payload로 복원 가능해야 한다.
   - lifecycle, runtime-setting, recorder-health event는 version이 있는 schema를 사용한다.
4. `hello`, `ready`, `heartbeat`, `ack`, `nack`, `complete`, `fail`, `sealed`, `error`의 허용 상태를 표 기반 state machine으로 만든다.
5. stable error code를 enum으로 만들고 HTTP/WS가 같은 의미를 사용하게 한다.
6. unknown control message, unknown chunk kind, reserved flag와 future version 처리 방식을 정한다.
7. `complete`에 `final_sequence`, record count와 stream별 최종 digest를 포함시킬지 확정한다. digest는 전송 무결성 확인용이며 gameplay validation 결과가 아니다.
8. Rust가 생성한 golden frame을 C#이 읽고, C#이 생성한 frame을 Rust가 읽는 fixture를 저장한다.
9. protocol conformance test를 서버 unit test와 TUFReplay test 양쪽에서 실행한다.

## 고정 기본값

- chunk payload: 최대 64 KiB
- run payload: 최대 128 MiB
- heartbeat interval: 10초
- active Redis TTL: 45초
- hard duration: 6시간
- sealed Redis TTL: 24시간
- sequence: run 전체에서 0부터 시작하는 단일 전역 번호
- ACK: cumulative ACK
- disconnect: 실패가 아니며 TTL 안에서 reconnect 가능

값은 운영 config로 조정할 수 있어도 protocol 의미는 바뀌지 않아야 한다.

## 성능 계약

### Client main thread

- capture callback에서 네트워크 I/O 금지
- JSON/CSV block 직렬화 금지
- 파일 I/O와 SQLite query 금지
- lock 대기 금지
- enqueue p99 0.1 ms 이하
- callback당 정상 경로 allocation 0 또는 사전에 정한 고정 상한

### Client background sender

- memory queue와 resend window에 고정 상한
- socket write는 background thread/task에서만 수행
- ACK가 오기 전 원본 record의 ownership이 명확해야 함

### Server

- chunk 수신 handler는 O(payload size)를 넘는 전역 작업을 하지 않음
- PostgreSQL write는 chunk마다 수행하지 않음
- 압축 해제, hashing과 evidence assemble은 WebSocket task 밖에서 수행
- 200 connections steady state의 목표 ACK p95 100 ms 이하, p99 250 ms 이하
- event-loop lag p99 50 ms 이하

## 테스트

- 모든 control state transition table test
- malformed/truncated/oversized binary header
- payload length mismatch
- duplicate sequence와 gap
- sequence `u64` 경계와 JSON 정수 표현 문제
- disconnect 직전 ACK 유실 후 reconnect
- complete 중복 전송
- complete 후 chunk 전송
- fail과 complete 경쟁
- server restart 후 명시적인 복구/실패 동작
- protocol version mismatch

## 완료 기준

- Rust/C# cross-language golden fixture가 CI에서 통과한다.
- wire format에 미정 필드나 암묵적 byte order가 없다.
- 모든 terminal error는 stable code와 terminal 여부를 갖는다.
- 이후 단계가 protocol 내부 구현을 몰라도 fixture와 state table만으로 개발 가능하다.

