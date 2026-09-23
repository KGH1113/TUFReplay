# 6단계: TUFReplay 백그라운드 업로드 transport

## 목적

capture queue의 record를 background에서 incremental chunk로 직렬화하고 서버에 전송한다. 네트워크 단절과 ACK 유실에도 중복 없이 복구하며 게임 main thread에는 영향을 주지 않는다.

## 선행조건

- 1단계 cross-language protocol fixture 통과
- 5단계 bounded capture fan-out 통과
- 4단계 device/account 인증 방식 확정

## 상태 머신

```text
레벨 연결: Connecting ↔ Ready/Idle → Retiring → Closed
시도별: AwaitingStart → Streaming ↔ Reconnecting → Completing → Sealed
각 시도 상태 → Unavailable (해당 시도의 자동 제출만 종료)
```

소켓 I/O와 시도 전송 순서는 `LevelSubmissionSession`의 한 background worker가 소유한다. `Begin`은 최대 8개 시도 queue에 capture buffer를 등록하고 반환한다. 승인 전에 첫 입력을 보존하고, 이전 실패의 서버 응답을 기다리는 동안에도 다음 시도를 받을 수 있다. 자세한 상태/한도는 [11단계](11-reusable-level-session.md)를 따른다.

## 구현 작업

1. reusable sender thread/task와 bounded channel을 만든다.
2. 레벨을 열면 인증된 idle 소켓을 준비한다. 실제 run은 시도마다 UUID가 포함된 `run_start`로 생성하며 REST 사전 발급은 하지 않는다.
3. record serializer는 기존 replay byte format을 사용해 완결 record 경계로 chunk를 만든다.
4. 단일 sequence allocator가 모든 chunk kind에 전역 번호를 부여한다.
5. ACK 전 chunk를 bounded resend window에 유지한다.
6. cumulative ACK가 오면 해당 buffer를 즉시 release한다.
7. reconnect hello에는 client가 마지막으로 관측한 ACK를 보내되 server ACK를 authoritative하게 따른다.
8. duplicate ACK, ACK 역행, impossible ACK를 구분한다.
9. 현재 reconnect는 200 ms씩 증가하는 최대 2초 backoff와 오류 감지 후 30초 복구 기한을 사용한다. 최초 승인에는 시도 생성부터 10초의 별도 기한이 적용된다.
10. heartbeat는 gameplay thread와 무관한 monotonic timer로 보낸다.
11. clear 후 editor 복귀에서 producer를 닫고 queue를 drain한 뒤 `run_complete`를 보낸다.
12. `sealed` 응답 전까지 서버 업로드 완료로 판단하지 않는다.
13. fail 시 background에서 `run_fail`과 응답 처리를 마친 후 다음 시도를 전송한다. 게임은 기다리지 않는다. 응답 유실 시 같은 run UUID로 복구하고, 포기한 시도는 최대 2초의 별도 cleanup으로 종료를 시도한다.
14. token은 memory에만 두고 log, SQLite와 crash report에서 redact한다.
15. process 종료 시 짧은 graceful deadline 뒤 취소한다.

## backpressure 정책

- socket write가 늦어지면 resend window가 먼저 증가한다.
- resend window 상한 이후에는 capture queue 상한이 적용된다.
- 두 상한 중 하나라도 넘으면 run upload를 포기한다.
- 포기 시 로컬 replay consumer에는 영향을 주지 않는다.
- record를 임의로 drop하고 upload를 계속하지 않는다.
- 장기 offline spool을 도입한다면 background-only, 암호화/보존기한/용량 상한을 별도 설계한다. 초기 구현에는 넣지 않는다.

## 네트워크 정책

- TLS 인증서 검증 필수
- production endpoint allowlist
- proxy 환경 지원 여부 명시
- connect, handshake, write와 idle timeout 분리
- WebSocket library의 자동 message 압축은 초기 비활성화
- binary frame을 chunk 단위로 보내고 여러 record마다 flush하지 않음
- OS socket buffer와 application queue를 별도로 계측

## 테스트

- Rust/C# golden frame 왕복
- 모든 chunk 이후 연결 강제 종료 지점 순회
- ACK frame 유실, 중복과 지연
- server gap NACK
- network offline 10초/40초/TTL 초과
- clear와 reconnect 동시 발생
- fail과 complete 경쟁
- 서버가 frame을 천천히 읽음
- malformed server control message
- 128 MiB 상한
- 메모리와 buffer pool leak 검사
- 게임 종료/모드 unload 중 reconnect 취소

## 완료 기준

- 짧은 disconnect에서는 server authoritative ACK 이후 정확히 이어진다.
- TTL 초과나 overflow에서는 run을 명확히 포기하고 게임을 방해하지 않는다.
- sender의 CPU, allocation과 memory가 정의된 상한 안에 있다.
- 플레이어는 정상 실행 중 connection/reconnect 상태를 볼 필요가 없다.
