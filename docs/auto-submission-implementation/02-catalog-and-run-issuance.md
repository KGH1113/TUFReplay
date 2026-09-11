# 2단계: TUF catalog와 run 발급 안정화

## 목적

run 발급 시점에 서버가 최신 공식 TUF revision과 정확한 chart를 결정하고 pin한다. 외부 TUF API나 artifact 다운로드가 불안정해도 잘못된 revision으로 세션을 발급하지 않는다.

## 선행조건

- 1단계 protocol version과 발급 error code 고정
- `level_revisions`, `level_revision_charts`, `run_sessions` migration 적용

## 구현 작업

1. TUF API client에 connect/read/overall timeout을 각각 설정한다.
2. retry는 timeout, 429와 일부 5xx에만 bounded exponential backoff와 jitter로 적용한다.
3. 매 발급 시 최신 metadata를 확인하고 upstream 장애 시 fail closed한다.
4. cache miss hydration은 `(tuf_level_id, tuf_file_id)` Redis lock과 process-local semaphore로 합친다.
5. lock owner token과 만료 시간을 사용하고 다른 owner의 lock을 해제하지 않는다.
6. ZIP은 임시 파일로 streaming download한다.
7. 다음 입력을 거부한다.
   - absolute path와 `..`
   - symlink
   - UTF-8로 안전하게 표현할 수 없는 경로
   - 대소문자만 다른 충돌 경로
   - 압축 512 MiB 초과
   - 해제 2 GiB 초과
   - 20,000개 초과 파일
   - 300초 초과 hydration
8. TUFHelperLite 호환 flattening과 payload hash를 golden fixture로 고정한다.
9. 모든 `.adofai`를 등록하고 정규화된 client chart path가 정확히 하나와 일치해야 발급한다.
10. hydration 뒤 metadata를 다시 읽어 revision 변경을 감지한다.
11. 동일 file ID의 canonical payload가 기존 immutable row와 다르면 덮어쓰지 않고 conflict로 기록한다.
12. artifact key는 content-addressed server-generated 문자열만 사용한다.
13. run row와 Redis meta 생성의 보상 transaction을 fault injection으로 검증한다.

## 발급 latency 정책

cache hit은 빠른 요청이어야 하지만 cache miss는 공식 artifact 다운로드 때문에 오래 걸릴 수 있다. 게임 시작을 막지 않기 위해 TUFReplay는 실제 countdown 직전에 동기적으로 큰 다운로드를 기다리면 안 된다.

권장 흐름:

1. TUFHelperLite에서 레벨을 열었을 때 background eligibility preflight를 시작한다.
2. run 시작 직전에는 preflight 결과가 최신인지 짧게 재확인한다.
3. 세션이 준비되지 않았으면 게임을 지연시키지 않고 해당 run의 자동 제출만 비활성화한다.
4. 게임 화면에는 정상 상황에서 아무 메시지도 표시하지 않는다. 제출이 불가능해진 경우에도 blocking modal을 띄우지 않는다.

## 관측 지표

- metadata lookup latency/error rate
- revision cache hit ratio
- hydration duration과 queue wait
- archive compressed/extracted bytes
- lock contention과 timeout
- revision conflict 수
- 발급 성공/거부 code별 count

## 테스트

- cache hit에서 artifact를 재다운로드하지 않음
- 동일 revision 동시 50회 요청 시 hydration 한 번
- lock owner crash 후 TTL recovery
- metadata가 다운로드 도중 한 번 변경됨
- metadata가 계속 변경되는 unstable catalog
- upstream timeout, 429, 5xx와 invalid JSON
- ZIP bomb, traversal, symlink, case collision
- 여러 chart와 Unicode 상대경로
- TUFHelperLite payload hash fixture 일치
- Redis 생성 실패 시 새 run만 삭제되고 revision artifact는 유지

## 완료 기준

- 잘못되거나 stale한 revision으로 run이 발급되는 경로가 없다.
- cache hit 발급 p95 목표를 측정하고 운영 dashboard에 노출한다.
- cache miss가 게임 main thread나 기존 WebSocket 처리량을 막지 않는다.
- artifact와 DB row는 같은 content identity를 가리킨다.

