# 3단계: sealed evidence 영속화와 정리

## 목적

Redis에 seal된 chunk를 순서대로 조립해 내구성 저장소에 immutable evidence로 저장한다. Redis TTL 만료나 서버 재시작 뒤에도 기존 replay payload를 byte-for-byte 복구할 수 있어야 한다.

## 선행조건

- 1단계 protocol과 complete metadata 고정
- 2단계 canonical revision/chart pinning 안정화

## 저장 구조

대형 원본을 PostgreSQL `BYTEA` 한 row에 저장하면 WAL, backup과 replication 비용이 커진다. 권장 구조는 다음과 같다.

### PostgreSQL

`run_session_evidence`:

- `run_session_id` unique FK
- stream별 storage key
- stream별 SHA-256와 byte size
- record count
- protocol version
- final sequence
- persisted timestamp

### Loco Storage

- 원본 `InputCsv`
- 원본 `HitContextCsv`
- 원본 `MetaJson`
- lifecycle/runtime-setting/recorder-health 원본
- content-addressed key 사용

PostgreSQL에 원본을 반드시 넣어야 한다면 payload별 분리 테이블과 streaming read를 사용하고, 최대 row 크기와 WAL 비용을 별도로 부하 테스트한다.

## 구현 작업

1. Loco generator로 persistence worker 골격을 만든다.
2. seal 성공 뒤 job enqueue를 idempotent하게 수행한다.
3. worker는 DB advisory lock 또는 distributed lock으로 run 하나당 하나만 assemble한다.
4. Redis Stream을 page 단위로 읽고 sequence 연속성을 다시 검사한다.
5. chunk kind별 임시 파일에 append하면서 SHA-256, bytes와 record count를 계산한다.
6. 전체 payload를 한 번에 RAM에 올리지 않는다.
7. CSV record boundary, UTF-8 정책, MetaJson 개수와 complete count를 구조적으로 검사한다.
8. temp object에 업로드한 뒤 DB transaction commit과 함께 최종 content key를 publish한다.
9. 재시도 시 동일 digest의 이미 저장된 object를 재사용한다.
10. DB commit 이후에만 Redis payload 삭제 또는 TTL 단축을 허용한다.
11. 실패 원인을 retryable과 terminal structural error로 구분한다.
12. cleanup worker로 expired active session, orphan temp object와 오래된 failed evidence를 정리한다.

## 상태 전이

```text
sealed → persisting → evidence_ready
                  ├→ persistence_failed
                  └→ evidence_invalid
```

여기서 `evidence_invalid`는 전송 데이터가 구조적으로 복원 불가능하다는 뜻이며 gameplay 부정 판정이 아니다.

## 자원 제한

- worker 동시 실행 수를 config로 제한
- run당 disk/temp 사용량 128 MiB 상한
- assemble streaming buffer는 수 MiB 이하로 유지
- PostgreSQL connection을 storage upload 전체 시간 동안 잡고 있지 않음
- Redis Stream page 크기 고정
- shutdown 시 새 job 수신 중단 후 in-flight job에 graceful deadline 제공

## 테스트

- 여러 kind가 섞인 sequence를 원본별로 정확히 복원
- 128 MiB 경계에서 bounded memory 확인
- 중간 sequence 누락
- duplicate Redis entry 또는 malformed entry
- worker가 object upload 직후 죽음
- DB commit 직후 ACK 전 worker가 죽음
- 같은 job 동시 실행
- storage timeout과 PostgreSQL timeout
- Redis가 persistence 중 사라짐
- cleanup과 active worker 경쟁
- 최종 evidence가 기존 TUFReplay parser에서 byte-for-byte 재생 가능

## 완료 기준

- worker 재실행 횟수와 무관하게 최종 evidence row가 하나다.
- seal된 run은 Redis 삭제 후에도 완전히 복구 가능하다.
- persistence가 WebSocket ACK latency에 유의미한 영향을 주지 않는다.
- orphan/temp/expired 데이터의 보존 기간과 삭제 정책이 자동화되어 있다.

