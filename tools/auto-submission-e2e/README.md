# Auto-submission E2E Lab

실제 클리어 리플레이를 HTTP·WebSocket으로 Rust 서버에 보내는 독립 로컬 테스트 도구다. 게임이나 기존 웹의 IPC mock은 사용하지 않는다. PostgreSQL·Redis·Storage·Loco 큐는 실제 구현을 사용한다. 기본 모드는 TUF API를 로컬 가짜 서버로 대체하며, 아래 로컬 TUF 연동 모드는 실제 로컬 API에 pass를 등록한다.

## 로컬 TUF까지 제출하기

현재 준비된 환경에서는 테스트 UI `http://127.0.0.1:5175`, TUF 프론트엔드 `http://127.0.0.1:5176`, TUF API `http://127.0.0.1:3002`를 사용한다.

`E2E_TUF_TARGET=local`로 실행하면 identity·권한 확인·등록·영수증 요청을 **고정된 `127.0.0.1:3002`**로 전달한다. redirect는 거절하고 외부 서비스 fallback은 없다. 기존 Rust HTTP 클라이언트와 TUF의 OAuth·방향별 internal token 검사를 사용한다. TUF가 실제 점수를 계산하고 MySQL에 pass와 영수증을 저장한다. **차트 ZIP·P1 등급·검증 결과는 여전히 fixture 기반 테스트 값이며, OAuth 브라우저 로그인 흐름도 검증 범위에 포함하지 않는다.**

이 컴퓨터의 전용 로컬 TUF 환경은 다음처럼 다시 실행한다. 각 장기 실행 명령은 별도 터미널에서 실행한다.

```sh
docker start tuf-web-test-mysql tuf-web-test-redis tuf-web-test-search

cd ~/dev/src/tuf-backend
# 테스트 계정·grant·차트를 준비하고 60분 유효 OAuth 토큰을 갱신한다.
node --env-file=.env --import tsx cache/web-test-oauth.ts
node --env-file=.env --import tsx cache/web-test-icons.ts
node --env-file=.env --import tsx cache/web-test-levels.ts
node --env-file=.env --import tsx src/app.ts

# 선택 사항: 예전 임시 SVG 자산 서버 (현재 난이도 아이콘은 실제 TUF CDN 사용)
cd ~/dev/src/tuf-backend
bun cache/web-test-assets.ts

# 별도 터미널: TUF 프론트엔드
cd ~/dev/src/t21c-web-frontend
node node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5176 --strictPort

# 별도 터미널: 기존 mock 모드 실행기를 먼저 Ctrl+C로 종료한 뒤 실행
cd ~/dev/src/tuf-replay
E2E_TUF_TARGET=local E2E_UI_PORT=5175 bun run e2e:dev
```

로컬 TUF 모드의 `e2e:dev`는 MySQL 변경을 검색 목록에 반영하는 TUF CDC도 함께 시작하고 종료한다. 이미 별도로 실행한 CDC가 있으면 먼저 종료한다. TUF backend checkout이 기본 형제 경로에 없다면 `E2E_TUF_BACKEND_DIR`로 절대 경로를 지정한다.

`tuf-backend/.env`와 프론트엔드 `.env.development.local`은 로컬 전용이며 Git에서 제외된다. MySQL은 `127.0.0.1:3307/tuf_web_test`, TUF Redis는 `127.0.0.1:6380`, Elasticsearch는 `127.0.0.1:9201`이다. 기존 Rust E2E의 PostgreSQL·Redis DB 14와 분리된다. 이 DB는 모델에서 생성한 테스트 스키마이며 운영 마이그레이션 이력을 복제한 DB가 아니다. `3003`의 간단한 자산 서버는 P1·U20·Qq·UQ4 글자의 테스트 아이콘을 제공한다. 난이도 ID·순서·점수는 테스트 값이다. 전체 CDN·메일·운영 OAuth·백업 저장소는 구성하지 않았다. 아이콘 fixture를 직접 갱신했다면 TUF API를 재시작하고 브라우저를 새로고침해 난이도 캐시 해시를 갱신한다.

현재 난이도 카탈로그는 사용자 요청에 따라 실제 TUF 공개 API의 91개 값으로 교체했다. `node --env-file=.env --import tsx cache/web-test-icons.ts`는 실제 ID·순서·점수·원본 아이콘 URL을 로컬 DB에 가져온다. 위 임시 SVG 서버는 현재 난이도 표시에는 사용되지 않으며, 브라우저는 원본 TUF CDN에서 아이콘을 읽는다. 이 공개 카탈로그/이미지 조회 외에 제출 요청은 계속 로컬 TUF로만 전달한다. 기존 pass 점수는 재계산하지 않는다.

`cache/web-test-levels.ts`는 fixture의 TUF level ID를 실제 공개 API에서 조회해 곡·아티스트·난이도·점수·file ID·다운로드 URL·제작자 크레딧을 로컬 DB와 Elasticsearch, 테스트 클라이언트 snapshot에 반영한다. `e2e:prepare`로 fixture를 다시 만들었다면 이 명령도 다시 실행한다. 화면에는 실제 메타데이터를 표시한다. 업로드 클라이언트의 설치 file ID claim과 catalog의 P1 적격성 응답, 검증할 차트 ZIP은 계속 명시적인 fixture 값이고, 검증 결과의 공식 file ID만 현재 로컬 TUF snapshot을 사용한다. ASGORE `6299`의 실제 난이도는 `U6`이므로 업로드 흐름은 시험할 수 있지만, 로컬 TUF의 P/G 자동 제출 정책에서는 등록이 거절되는 것이 정상이다.

실게임 모드로 fixture에 없는 로컬 TUF 레벨을 발급할 때는 `.data/catalog-levels/<level-id>/metadata.json`과 `chart.zip`을 사용한다. 이 snapshot은 로컬 TUF의 현재 file ID·P/G 난이도와 TUFHelperLite가 설치한 차트 사본만 담으며 테스트 UI의 리플레이 목록에는 추가되지 않는다. 따라서 Rust 서버는 외부 다운로드 주소에 접속하지 않고도 현재 로컬 차트를 검증할 수 있다.

로컬 TUF 연동에서는 fixture ZIP으로 검증하더라도 검증 결과의 감사용 `official_file_id`에는 위 snapshot의 현재 실제 file ID를 사용한다. 그래야 검증과 등록 사이에 실제로 revision이 바뀐 경우만 TUF가 `level_revision_changed`로 거절한다. snapshot이 없는 fixture는 오래된 mock file ID로 등록을 시도하지 않고 catalog 단계에서 `local_tuf_fixture_not_synced`로 중단한다.

토큰은 `.data/local-tuf-access-token`에 저장되고 로그에서는 가린다. 만료되면 위 준비 명령을 다시 실행하고 Lab을 재시작한다. 화면에서 **플레이 시작 → 증거 저장 완료 → 제출**을 누른다. 성공 시 `TUF pass #N 보기` 링크로 실제 상세 화면을 연다.

실행 중인 로컬 연동 Lab을 대상으로 등록 응답 유실·중복 등록 방지·검증기 미준비를 검사한다:

```sh
cd ~/dev/src/tuf-replay/tools/auto-submission-e2e
E2E_TUF_TARGET=local bun src/test-local-tuf.ts
```

결과는 `.data/local-tuf-verification.json`에 저장한다. 이 테스트는 로컬 TUF에 새 pass를 만든다. 종료는 각 앱 터미널에서 Ctrl+C, 인프라 종료는 `docker stop tuf-web-test-mysql tuf-web-test-redis tuf-web-test-search`다. 저장된 데이터는 삭제하지 않는다. 기본 mock 테스트는 `E2E_TUF_TARGET`을 해제하고 실행한다.

## 처음 실행하기

저장소 루트에서 실행한다. Bun, Rust/Cargo, `zip`, 실행 중인 로컬 PostgreSQL·Redis가 필요하다.

```sh
bun install
# PostgreSQL 역할 tuf_replay와 로컬 Redis가 준비되어 있을 때, 최초 한 번:
createdb -h 127.0.0.1 -U tuf_replay tuf_replay_e2e
bun run e2e:prepare
bun run e2e:dev
```

`createdb`는 PostgreSQL의 `tuf_replay` 역할에 DB 생성 권한이 있어야 한다. 권한이 없으면 로컬 DB 관리 계정으로 소유자가 `tuf_replay`인 `tuf_replay_e2e` DB를 생성한다. 프로젝트 기본 개발 계정의 암호는 `tuf_replay_dev`다. 이미 DB가 있으면 생성 명령은 생략한다.

연결 기본값은 다음과 같다. 다른 로컬 계정을 쓰면 환경변수로 변경한다. DB 이름과 Redis DB 번호는 격리를 위해 고정한다.

```sh
export E2E_DATABASE_URL='postgres://tuf_replay:tuf_replay_dev@127.0.0.1:5432/tuf_replay_e2e'
export E2E_REDIS_URL='redis://127.0.0.1:6379/14'
```

`e2e:dev`는 의존 서비스와 포트를 검사한 뒤 가짜 TUF·클라이언트 실행기, Rust 서버·Worker·Scheduler, Vite UI를 시작한다. E2E DB에는 마이그레이션만 적용하며 기존 데이터를 초기화하지 않는다.

| 주소 | 역할 |
| --- | --- |
| `http://127.0.0.1:5174` | 웹 UI |
| `http://127.0.0.1:5151` | 실제 Rust 자동 제출 서버 |
| `http://127.0.0.1:5152` | 로컬 클라이언트 실행기·가짜 TUF |

UI 포트가 사용 중이면 기존 프로세스를 종료할 필요 없이 다음과 같이 실행한다.

```sh
E2E_UI_PORT=5175 bun run e2e:dev
```

이 작업을 검증한 환경에서는 5174가 이미 사용 중이어서 **5175**로 실행했다. 브라우저는 `localhost` 대신 출력된 `127.0.0.1` 주소로 연다. 도구는 loopback에서만 동작하며 외부 TUF로 전환하는 옵션은 없다.

## 리플레이 준비

기본 원본 DB는 macOS Steam ADOFAI 설치 경로의 `Mods/TUFReplay/Data/tufreplay.sqlite`다. 다른 위치는 명시한다.

```sh
E2E_REPLAY_DB='/absolute/path/tufreplay.sqlite' bun run e2e:prepare
```

DB는 읽기 전용으로 연다. 게임이 기록한 `cleared`, 시작 타일 0, NoFail OFF, Strict를 검사한다. Too Early·Early 같은 무효 입력이 도중에 있어도 이후 정상 입력으로 완주할 수 있으므로 판정 카운터가 전부 0일 것을 요구하지 않는다. 입력·hit context 시간값·클리어 시각·로컬 차트를 가진 서로 다른 차트를 최대 3개 선택한다. 누락된 시간값이나 판정을 만들어 후보를 채우지 않는다.

현재 fixture에는 **The Limit Does Not Exist**와 **ASGORE (zsry Remix)**를 포함한 최근의 서로 다른 전체 클리어를 최대 3개 둔다. ASGORE는 level 6299, 원본 run `ee4ac47568c24960a1fd64877a9cfedc`인 NoFail OFF·Strict 클리어다. NoFail로 끝까지 진행한 기록은 계속 제외한다. 이 조건은 성공 fixture 선정 기준이며 제품의 NoFail 허용 정책을 변경하지 않는다.

원본은 수정하지 않고 `.data/fixtures/`에 CSV·메타데이터·차트 ZIP 사본을 저장한다. `selection.json`에 선정·제외 결과가 있다. 기록을 새로 추출할 때는 실행 중인 Lab을 먼저 종료한다.

클리어 이후 입력을 제외하고 음수 시간값과 CSV 값을 유지한다. 과거 lifecycle·설정·health 스트림은 재구성한 테스트 데이터다. P1 등급과 file ID도 mock 값이다. 당시 P/G 적격 여부나 과거 차트 호환성을 검증하지 않으며, 로컬 차트 사본을 테스트용 공식 ZIP으로 제공한다. UI의 보완 항목에서 이 차이를 확인할 수 있다.

## UI 사용하기

1. 기록, 검증 모드, 전송 속도, 복구 시나리오를 선택한다. 이 설정은 해당 실행에 고정된다.
2. **플레이 시작**을 누른다. 발급된 run token으로 WS를 열고 `hello → TUFR 청크 → ACK → complete`를 실행한다. 화면의 곡 시간·입력 수·hit 수는 ACK된 기록을 따라가며, 경로와 회전하는 구체는 도식 연출이다. 실제 차트 지형·게임 판정을 재현하지 않는다.
3. **증거 저장 완료**가 되면 **제출**을 누른다. 업로드만으로는 검증·등록하지 않는다.
4. `가상 성공`은 `submitted`와 가짜 pass까지, `검증기 미준비`는 `validator_unavailable`까지 진행한다.
5. 로그 소스를 **TUF 요청·응답**으로 바꾸고 요청을 펼쳐 identity·권한 확인·메타데이터·ZIP·영수증 조회·등록 요청을 확인한다. 인증 토큰은 숨긴다.

1배속·10배속·최대 속도는 전송 간격만 바꾸며 기록 시간값은 바꾸지 않는다. 한 번에 하나의 플레이만 업로드한다. **연결 끊기** 후 30초 안에 **재연결**하면 서버 ACK부터 이어 보낸다. 창을 새로고침해도 실행기가 살아 있으면 진행 상태를 다시 볼 수 있다.

복구 시나리오:

- **플레이 40% 지점에서 실패:** 해당 지점 이전까지만 기록을 보내고 실제 모드처럼 WS `fail`을 보낸다. **지금 실패시키기** 버튼으로 재생 중 수동 실패도 가능하다. 완료·제출은 하지 않는다. 서버가 실패를 확인하면 재도전할 수 있으며 실패 run의 Redis 청크와 meta는 모두 삭제된다. 원본 성공 기록의 판정을 수정하지 않는 테스트 주입이다.

- **ACK 수신 전 연결 끊김:** 청크를 보낸 직후 연결을 끊고 마지막으로 확인한 ACK로 재연결한다. 서버가 받았는지는 서버 ACK로 결정한다.
- **등록 후 응답 유실:** 가짜 TUF가 영수증을 저장한 뒤 응답 스트림을 중단한다. Rust의 실제 재시도 스케줄과 영수증 조회로 복구하며 중복 등록하지 않는다. 이 시나리오의 `injected lost response` 터미널 오류는 의도된 주입이다.

`.data/mock-tuf.sqlite`의 가짜 영수증과 Rust DB·증거 파일은 재시작 후에도 유지된다. UI 실행 이력과 최근 로그는 현재 실행기 프로세스 메모리에만 남는다. Ctrl+C로 종료하면 자식 서버들도 종료한다. 진행 중 업로드는 도구 재시작 후 새 run으로 시작한다. 이미 제출한 작업은 서버 큐·DB 상태에서 복구한다.

## Rust 서버를 따로 실행하기

통합 실행 명령을 쓰는 것이 기본이다. Rust만 디버깅하려면 가짜 TUF·실행기와 UI를 먼저 별도 실행하고 다음 명령을 사용한다. `e2e:dev`와 중복 실행하지 않는다.

```sh
# 터미널 1, 저장소 루트: 가짜 TUF·실행기
bun run tools/auto-submission-e2e/src/standalone.ts

# 터미널 2, 저장소 루트: UI
cd tools/auto-submission-e2e
bun x --no-install vite --host 127.0.0.1 --port 5174 --strictPort

# 터미널 3, 저장소 루트: Rust HTTP·Worker·Scheduler
cd server
TUF_TO_AUTO_SUBMISSION_TOKEN=e2e-tuf-to-submission-local-only-0001 \
AUTO_SUBMISSION_TO_TUF_TOKEN=e2e-submission-to-tuf-local-only-0002 \
SCHEDULER_CONFIG=config/scheduler.yaml \
cargo run --features e2e -- start --all --environment e2e
```

`e2e` Cargo feature와 E2E 실행 환경이 모두 있어야 테스트 검증기를 사용할 수 있다. 일반 빌드는 E2E 환경을 거절하고, 일반 실행 환경에서는 feature가 있어도 기존 검증기를 사용한다. 프로덕션 빌드에 테스트 feature를 추가할 필요가 없다.

## 자동 검증

통합 Lab을 Ctrl+C로 종료한 뒤 실행한다. 자동 E2E는 5151·5152를 사용한다.

```sh
bun run e2e:test
bun run --cwd tools/auto-submission-e2e test
bun run --cwd tools/auto-submission-e2e typecheck
bun run --cwd tools/auto-submission-e2e build
```

자동 E2E는 실제 서버 프로세스를 띄워 가상 성공, 검증기 미준비, ACK 유실, 등록 응답 유실, 자동·수동 플레이 실패의 6개 시나리오를 실행한다. 저장된 input/hit 파일을 fixture와 바이트 단위로 비교하고 제출 전 등록 없음, 재시도 시 등록 요청 1회, 영속 저장 후 Redis 청크 삭제도 검사한다. 실패는 DB failed·manifest 없음·Redis 청크와 meta 없음·등록 요청 없음·제출 차단을 검사한다. 실행 후 서버는 종료하고 `.data/e2e-observations.json`과 테스트 데이터를 보존한다.

## 초기화와 Redis 수명 검증

Lab을 종료한 뒤 저장소 루트에서 실행한다.

```sh
bun run e2e:reset   # E2E 앱 데이터·큐·e2e:* Redis 키·Storage·fixture·가짜 영수증 삭제
bun run e2e:prepare
bun run e2e:inspect # 단계별 DB·파일·Redis 관측, 정리 실패와 재시작 복구 검사
```

초기화는 `tuf_replay_e2e` DB와 Redis DB 14의 `e2e:*`만 대상으로 삼고 마이그레이션 이력을 유지한다. 원본 리플레이 SQLite는 수정하지 않는다. 초기화 결과는 `.data/reset.json`, 단계별 관측은 `.data/lifecycle-observations.json`에 남는다.

`e2e:inspect`는 로컬 Redis의 `ACL SETUSER` 권한이 필요하다. 임시 읽기 전용 사용자를 만들어 실제 `UNLINK` 실패를 유도하고 종료 시 삭제한다. 기본 Redis 사용자 권한은 변경하지 않는다. HTTP 서버만 실행해 봉인 직후를 관측한 다음 Worker를 시작하고, 삭제 실패 후 정상 권한으로 재시작해 주기적 복구를 검증한다.

증거 파일 저장과 DB manifest 확정 뒤에만 청크를 지운다. 완료 ACK용 메타데이터는 24시간 TTL을 유지한다. `ingest_released_at`이 비어 있으면 정리를 재시도한다. [DB 컬럼 설명](../../docs/auto-submission-database.md)과 [실측 결과](../../docs/auto-submission-redis-verification.md)를 참고한다.

성공 run의 meta가 남아 있는 것은 이 복구 정책에 따른다. 현재 meta에는 청크별 digest도 들어 있어 최소 크기의 영수증만 있는 것은 아니다. 실패 run은 복구할 완료 응답이 없으므로 `fail` 처리 때 두 키를 모두 삭제한다.

영속 저장된 미제출 증거는 기한 없이 보관한다. `evidence_expires_at=NULL`이며 Redis meta의 24시간 TTL과 무관하게 나중에 제출할 수 있다. CSV는 Storage, 상태·manifest는 PostgreSQL에 남는다. 제출할 때 현재 권한과 공식 차트 기준으로 검증하며, 기한 없는 보관이 승인이나 옛 차트 호환성을 보장하지는 않는다.

## 구조

`src/fixtures`는 SQLite 읽기·변환, `src/client`는 HTTP·바이너리·WS 전송, `src/mock`은 가짜 TUF와 영수증, `src/ui`는 화면을 담당한다. 실행 조정과 프로세스 관리는 각각 별도 모듈이다. 모든 개인 리플레이·차트·영수증·증거는 Git에서 제외한 `.data/`에 둔다.

실제 게임 캡처, OAuth 서비스, TUF 점수 계산, 치팅 판정 정확성은 이 도구의 검증 범위가 아니다.
