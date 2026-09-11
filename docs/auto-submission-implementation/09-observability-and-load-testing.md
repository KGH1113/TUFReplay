# 9단계: 관측성, 용량 모델과 200-connection 부하 검증

## 목적

WebSocket 200개가 지속되는 상황을 감으로 판단하지 않고 재현 가능한 부하 테스트와 SLO로 검증한다. ingest와 persistence 부하가 게임 중 ACK/reconnect 품질을 떨어뜨리지 않게 한다.

## 선행조건

- 서버 ingest, persistence와 auth 경로 구현 완료
- 실제 protocol을 말하는 synthetic client 준비
- production과 유사한 PostgreSQL, Redis, storage 구성

## SLO

200개 지속 연결에서 권장 초기 목표:

- WebSocket handshake 성공률: 99.9% 이상
- 정상 chunk ACK p95: 100 ms 이하
- ACK p99: 250 ms 이하
- heartbeat timeout 오탐: 0
- event-loop lag p99: 50 ms 이하
- sequence 손실/중복 저장: 0
- 정상 reconnect 성공률: 99.9% 이상
- server process RSS: 테스트 시간에 따라 계속 증가하지 않음
- PostgreSQL pool exhaustion: 0
- Redis rejected command/OOM: 0
- 게임 client queue overflow: 정상 네트워크에서 0

SLO는 `ssh kgh` 실제 사양에서 측정 후 조정하되 완화 이유를 수치로 남긴다.

## 용량 모델

측정 전 다음 변수를 fixture로 둔다.

- 연결 수: 200 steady, 300 short burst
- heartbeat: 10초마다 1회
- 평균 chunk 크기와 초당 chunk 수
- 장시간 저입력 run과 고밀도 입력 run 비율
- reconnect burst: 200개 동시
- clear burst: 50개 동시 seal/persistence
- 최대 run: 128 MiB
- Redis TTL: active 45초, sealed 24시간

Redis memory는 대략 다음 항목을 합산해 계산하고 실측한다.

```text
raw payload
+ Stream entry/field overhead
+ meta hash
+ allocator fragmentation
+ reconnect 동안 미정리 sealed payload
```

payload 크기만 합산하지 말고 Redis `MEMORY USAGE` 표본과 `used_memory_rss`를 함께 본다.

## 계측

### Server

- active/connecting WebSocket gauge
- received chunk/bytes counter
- ACK latency histogram
- WebSocket task duration과 close reason
- protocol/error code counter
- Redis command/Lua latency
- Redis used memory와 fragmentation
- PostgreSQL pool used/idle/wait time
- persistence queue depth/duration/retry
- storage throughput/error
- Tokio event-loop 또는 runtime scheduling delay
- process CPU/RSS/file descriptor

### Client

- main-thread enqueue latency histogram
- capture queue high-water mark
- resend window bytes
- ACK RTT
- reconnect count와 duration
- overflow/abandon reason
- serializer throughput와 allocation

raw key event나 upload token을 telemetry에 넣지 않는다.

## 부하 시나리오

1. 200개 idle/heartbeat 연결을 6시간 유지한다.
2. 200개가 현실적인 입력률로 지속 전송한다.
3. 20개는 최대 입력률, 나머지는 일반 입력률로 전송한다.
4. 200개가 동시에 reconnect한다.
5. 50개가 동시에 complete하여 persistence job이 몰린다.
6. Redis latency를 인위적으로 높인다.
7. PostgreSQL/storage를 잠시 중단했다 복구한다.
8. slow reader, invalid token, tiny-frame flood를 정상 연결과 섞는다.
9. process rolling restart 중 reconnect를 확인한다.
10. 12~24시간 soak test로 memory/file descriptor leak를 확인한다.

## 통과 조건

- 모든 시나리오에서 SLO를 자동 판정하는 report가 생성된다.
- 부하 테스트 실패 시 latency, CPU, Redis, DB pool 중 병목 위치를 지표로 식별할 수 있다.
- persistence concurrency를 높여도 WebSocket ACK SLO가 우선 보호된다.
- 200개에서 최소 50%의 headroom을 확인하거나 명확한 admission control을 둔다.
- client profiler 결과가 5단계 main-thread 성능 예산을 만족한다.

## 튜닝 순서

1. per-connection allocation과 task leak 제거
2. Redis round trip/Lua 호출 수 확인
3. frame batching과 flush 빈도 조정
4. persistence worker concurrency 제한
5. PostgreSQL pool과 transaction 점유 시간 조정
6. OS file descriptor, TCP keepalive와 reverse proxy timeout 조정
7. 마지막으로 scale-out 필요성 판단

단순히 DB pool이나 worker 수를 크게 늘리면 ingest latency가 악화될 수 있으므로 지표 없이 값을 올리지 않는다.

