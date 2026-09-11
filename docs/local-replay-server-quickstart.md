# 로컬 TUF API · 자동 제출 서버 실행

이미 준비된 이 컴퓨터의 로컬 테스트 환경을 다시 켜는 순서다. Docker Desktop을 먼저 실행한다. 기존 `e2e:dev`가 떠 있다면 그 터미널에서 Ctrl+C로 종료한 후 시작한다. DB 초기화는 필요 없다.

## 1. DB와 Redis 켜기

```sh
docker start tuf-web-test-mysql tuf-web-test-redis tuf-web-test-search

cd ~/dev/src/tuf-replay
docker compose up -d postgres redis-ingest
```

TUF는 MySQL `3307`, Redis `6380`, Elasticsearch `9201`을 사용한다. 자동 제출 서버는 PostgreSQL `5432`의 `tuf_replay_e2e` DB와 Redis `6379`의 DB 14를 사용한다. 기존 테스트 DB·fixture가 준비되어 있다는 전제다.

## 2. 터미널 A — TUF API

```sh
cd ~/dev/src/tuf-backend
node --env-file=.env --import tsx cache/web-test-oauth.ts
node --env-file=.env --import tsx cache/web-test-levels.ts
node --env-file=.env --import tsx src/app.ts
```

주소: **http://127.0.0.1:3002**

첫 명령은 로컬 테스트 계정·grant를 준비하고 테스트용 OAuth 토큰을 갱신한다. 토큰은 **60분** 동안 유효하다. 현재 checkout의 `.env`는 로컬 테스트 DB용 설정을 사용한다. 아이콘은 이미 채워져 있으므로 매번 다시 가져올 필요 없다.

두 번째 명령은 fixture가 가리키는 레벨의 실제 공개 TUF 메타데이터를 로컬 DB·검색 인덱스·테스트 클라이언트에 동기화한다. fixture를 다시 추출했거나 새로운 레벨을 추가했을 때 실행한다.

## 3. 터미널 B — 자동 제출 서버 + 테스트 클라이언트

TUF API가 시작된 다음 실행한다.

```sh
cd ~/dev/src/tuf-replay
E2E_TUF_TARGET=local E2E_UI_PORT=5175 bun run e2e:dev
```

이 명령 하나가 최신 Rust 서버와 테스트 실행기·웹 UI를 함께 시작한다.

| 서비스 | 주소 |
|---|---|
| 자동 제출 Rust API·Worker·Scheduler | http://127.0.0.1:5151 |
| 테스트 실행기·TUF 중계 | http://127.0.0.1:5152 |
| 테스트 클라이언트 웹 UI | http://127.0.0.1:5175 |

`E2E_TUF_TARGET=local`이 있어야 제출이 로컬 TUF API `3002`로 연결된다. 생략하면 가짜 TUF 모드다. 판정 검증은 여전히 테스트 검증기이며 실제 게임 시뮬레이터가 아니다. 웹에서 **가상 성공 → 플레이 시작 → 증거 저장 완료 → 제출** 순서로 테스트한다.

## 4. 터미널 C — 새 pass를 검색 목록에 반영

```sh
cd ~/dev/src/tuf-backend
node --env-file=.env --import tsx src/externalServices/cdcService/app.ts
```

로컬 DB의 변경을 검색 인덱스로 전달한다. 새 제출이 목록·검색에도 나타나는 것을 확인하려면 함께 실행한다.

## 리플레이 화면도 함께 켜기

각각 별도 터미널에서 실행한다.

```sh
cd ~/dev/src/adofai-web-editor
VITE_TUF_PARENT_ORIGINS=http://127.0.0.1:5176 bun run dev --host 127.0.0.1 --port 5177 --strictPort
```

```sh
cd ~/dev/src/t21c-web-frontend
VITE_AUTO_SUBMISSION_API_URL=http://127.0.0.1:5151 VITE_WEB_ADOFAI_URL=http://127.0.0.1:5177 bun run dev -- --host 127.0.0.1 --port 5176 --strictPort
```

TUF 화면은 **http://127.0.0.1:5176**, web-adofai는 **5177**이다. iframe은 TUF pass에서 Load를 눌렀을 때 생성된다. 페이지 접속만으로 자동 로드하지 않는다.

서버가 켜져 있어도 해당 pass의 공개 증거와 공식 차트 ZIP이 모두 있어야 재생된다. 현재 공식 차트의 의미 gameplay hash가 검증 당시 값과 다른 기록은 재생을 거절한다. file ID, 차트 파일명, VFX 또는 전체 파일 SHA만 달라진 경우에는 현재 archive로 재생한다. 자세한 현재 구현 범위는 [구현 보고](tuf-replay-embed-implementation-2026-09-10.md)를 참조한다.

## 종료·토큰 만료

- 각 서버 터미널에서 Ctrl+C. `e2e:dev`는 자식 Rust 서버·실행기·UI도 함께 종료한다.
- OAuth 토큰이 만료되면 터미널 B를 종료하고 터미널 A의 토큰 준비 명령만 다시 실행한 뒤 터미널 B를 재시작한다. TUF API 자체는 토큰 갱신 때문에 재시작할 필요 없다.
- 포트가 사용 중이면 같은 서버를 중복 실행하지 않는다. DB·Redis를 비우는 명령은 실행하지 않는다.
