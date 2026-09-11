# 4단계: 인증, 소유권과 abuse 방어

## 목적

production에서 run 발급, stream 연결, 상태 조회와 제출을 실제 사용자에게 안전하게 연결한다. upload token 유출이나 대량 연결 시도가 다른 사용자 데이터와 서비스 안정성에 영향을 주지 않게 한다.

## 선행조건

- 3단계까지의 lifecycle과 evidence ownership 경계 확정
- Loco production에서 익명 ingest route가 등록되지 않는 기존 원칙 유지

## 인증 역할 분리

- 계정 access token: run 생성, 조회, 제출과 삭제 권한
- upload token: 특정 `run_id`의 WebSocket stream에만 사용할 일회성 capability
- worker identity: 내부 persistence/cleanup job 권한

upload token으로 다른 API를 호출할 수 없어야 하며 DB에는 원문을 저장하지 않는다.

## 구현 작업

1. Loco auth generator를 사용해 사용자 모델과 인증 controller를 다시 도입한다.
2. `run_sessions.user_id` FK migration을 추가한다.
3. 계정 로그인과 TUFReplay device/session 인증 흐름을 정의한다.
4. run 생성 시 account identity를 owner로 고정한다.
5. run 조회, 삭제와 submit에서 model-level ownership method를 사용한다.
6. upload token은 256-bit random 값으로 만들고 SHA-256 digest만 DB/Redis에 둔다.
7. token 비교는 constant-time 함수를 사용한다.
8. token은 URL/query/log에 넣지 않고 Authorization header로만 전달한다.
9. run별 동시 WebSocket은 기본 1개 active connection으로 제한하되 reconnect takeover 규칙을 둔다.
10. 사용자별 active run, 분당 발급 횟수, 일일 ingest bytes 제한을 둔다.
11. IP 기반 제한은 account 제한의 보조 수단으로 사용한다.
12. 로그와 tracing field에서 token, raw input과 개인정보를 redact한다.
13. 계정 삭제와 evidence retention 정책을 구현한다.

## 권장 초기 제한

- 사용자당 active run: 2개
- 사용자당 run 발급: 분당 5회 burst, 지속률 분당 1회
- IP당 unauthenticated handshake: 짧은 sliding-window 제한
- run당 payload: 128 MiB
- invalid token 반복 시 connection을 빨리 종료하고 상세 정보 미노출

실제 수치는 9단계 부하 및 정상 사용자 telemetry로 조정한다.

## 위협 시나리오

- 임의 UUID enumeration
- 다른 사용자의 run 상태 조회/submit
- upload token replay
- 같은 token으로 다중 연결해 sequence 경쟁
- 연결만 열고 heartbeat/chunk를 보내지 않는 slow client
- oversized frame과 작은 frame 폭탄
- run 발급으로 TUF artifact hydration 강제
- 특정 큰 revision을 반복 요청해 disk/CPU 소모
- error response 차이로 catalog 또는 사용자 정보 추측

## 테스트

- 모든 owner/non-owner 조합
- 만료/revoke된 account와 upload token
- 같은 upload token 동시 연결
- rate limit 경계와 proxy IP 처리
- token이 application log에 나타나지 않음
- production route가 인증 middleware 없이 등록될 수 없음
- hydration abuse가 semaphore와 rate limit을 우회하지 못함

## 완료 기준

- production ingest API의 모든 경로에 명시적인 인증 또는 upload capability 검사가 있다.
- 사용자 A가 사용자 B의 run 존재, 상태, evidence나 submit 결과를 알 수 없다.
- 공격성 연결이 200개 정상 연결의 SLO를 무너뜨리지 않는다.
- secret이 DB, Redis snapshot, URL, access log와 error payload에 원문으로 남지 않는다.

