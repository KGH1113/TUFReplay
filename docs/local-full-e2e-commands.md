# 실제 게임을 사용하는 로컬 E2E

이 컴퓨터의 네 checkout(`tuf-replay`, `tuf-backend`, `t21c-web-frontend`, `adofai-web-editor`)과 기존 로컬 테스트 DB를 사용한다. 브라우저 로그인과 PKCE를 거쳐 모드가 토큰을 보관하며, companion은 실제 AdofaiIpc에 연결한다.

## 실행

`bun run e2e:live`는 `../adofai-web-editor`의 현재 소스에서 Vite를 실행한다.
플레이어의 버그 수정·최적화는 이 checkout에 들어 있으면 반영되며 `dist`를 사용하지 않는다.
리플레이 protocol 3의 API·CORS 환경변수는 실행 시 설정하므로 변경 후에는 스택을 다시 실행한다.
TUF frontend는 기록 ID만 전달하고, 다운로드·로딩·재시도·재생 컨트롤은 플레이어가 맡는다.

Docker Desktop을 켜고 인프라를 시작한다. 기존 볼륨을 삭제하거나 reset할 필요는 없다.

```sh
rtk proxy docker start tuf-web-test-mysql tuf-web-test-redis tuf-web-test-search
cd ~/dev/src/tuf-replay
rtk proxy docker compose up -d postgres redis-ingest
rtk proxy bun run e2e:live:prepare
```

`prepare`는 전용 로컬 계정 `local-game-tester`, OAuth client `tuf-replay-local-game`, PostgreSQL `tuf_replay_local_game`을 준비한다. 비밀번호는 `tools/live-e2e/.data/login.json`에만 저장한다. OAuth grant나 토큰을 미리 발급하지 않는다. Backend `.env`는 `NODE_ENV=development`, `DB_HOST=127.0.0.1`, `DB_PORT=3307`, `DB_DATABASE=tuf_web_test`여야 한다.

기본 차트는 설치된 TUFHelperLite 차트 #8068 Merry Christmas EX(P2), #3072 The Limit Does Not Exist(P16)다. 원본 폴더를 보존한 채 ZIP을 만들고, 로컬 카탈로그의 file ID 및 P/G 난이도와 일치하는지 확인한 후 해당 레벨의 다운로드 주소만 로컬 ZIP 서버로 바꾼다. 다른 차트는 `bun run e2e:live:prepare <level-id> ...`로 지정한다. 먼저 로컬 카탈로그와 게임에 동일한 차트가 있어야 한다.

로컬 ZIP 주소를 저장할 때 CDN 주소에서 file ID를 추출하는 backend 훅은 이 전용 DB의 준비 작업에서만 건너뛰고, 설치된 file ID를 주소와 함께 보존한다. 이전 준비 스크립트로 인해 **해당 레벨의 정확한 로컬 ZIP 주소 + null file ID**가 남은 경우 재실행으로 복구된다. 실제로 서로 다른 file ID는 자동 덮어쓰기하지 않는다. 차트들의 카탈로그 갱신은 하나의 트랜잭션으로 처리한다.

게임에서 저장한 뒤 완전히 종료하고 모드와 로컬 접속 설정을 적용한다.

```sh
cd ~/dev/src/tuf-replay
TUFREPLAY_BUILD_FLAVOR=auto-submission \
TUFREPLAY_BUILD_VERSION=0.2.0-auto-submission.1 \
  rtk proxy ./scripts/run.sh build
rtk proxy bun run e2e:live:settings install
```

설정 명령은 실행 중인 게임을 감지하면 중단한다. OAuth client ID, TUF API, 제출 API, OAuth callback 네 필드의 원래 값을 백업하고 나머지 설정을 보존한다. `ADOFAI_DIR`로 설치 경로를 바꿀 수 있다. 기본 경로는 macOS Steam 설치 폴더다.

서버는 터미널 하나에서 실행한다.

```sh
cd ~/dev/src/tuf-replay
rtk proxy bun run e2e:live
```

| 주소 | 역할 |
| --- | --- |
| http://127.0.0.1:5180 | 실제 게임 IPC에 연결하는 companion |
| http://127.0.0.1:5176 | 로컬 TUF frontend / 로그인 / 제출 기록 |
| http://127.0.0.1:3002 | TUF API / OAuth |
| http://127.0.0.1:5151 | 자동 제출 API / worker / scheduler |
| http://127.0.0.1:5152 | 준비된 공식 차트 ZIP만 제공하는 서버 |
| http://127.0.0.1:5190 | web-adofai iframe |
| http://127.0.0.1:3990 | TUF CDC |

준비 완료 메시지를 확인하고 ADOFAI를 실행한다. 모든 주소는 `127.0.0.1`로 통일한다. `?mock=1`은 사용하지 않는다. 로그는 `.data/logs/`에 저장된다. 사용 중인 포트가 있으면 실행기가 중단되며 다른 서버를 임의 종료하지 않는다.

## 실제 확인 순서

1. Companion의 **자동 제출 → TUF 계정 연결**을 누른다. 로컬 TUF 로그인 화면에서 `.data/login.json`의 계정으로 로그인하고 자동 제출 권한을 허용한다.
2. Callback에서 자동 제출 창이 열리고 계정 연결 및 신뢰 테스터 제출 권한이 표시되는지 확인한다.
3. 키뷰어와 오버레이를 각각 등록한다. 설치된 소스는 TUFReplay가 모드와 저장된 설정을 자동 감지한다. 소스를 선택하고 이름을 정해 등록하면 되며 설정 파일을 직접 선택하지 않는다. 감지되지 않으면 모드를 설치하고 설정을 한 번 저장한 뒤 게임을 재실행한다. DMNote는 내보낸 JSON의 현재 선택 탭을 가져온다. 필요한 이미지·폰트는 등록 중 첨부할 수 있고, DMNote는 custom CSS와 16:9 배치를 설정할 수 있다. 기존 사용자 파일은 수정하지 않는다.
4. **계정 연결 후** 준비된 P/G 차트를 처음부터 플레이하고 클리어한다. 클리어 이후 입력도 기록하려면 원하는 만큼 기다린 다음 에디터로 돌아와 기록을 종료한다. 기존 기록이나 연결 전 플레이는 새 증거로 소급 제출되지 않는다.
5. Companion의 새 기록 메뉴에서 증거 저장 완료를 기다린 뒤 **제출**을 누른다. 키뷰어 최대 하나와 오버레이 최대 하나를 각각 선택하거나 비워 둔다. 재시도에는 최초 선택이 고정된다.
6. 메뉴가 제출 처리 상태를 갱신한다. **등록된 기록 보기**가 나타나면 로컬 TUF pass를 열고 **Load replay**를 누른다.
7. 키뷰어·오버레이·판정선, 카운트다운과 기록 종료 시점, TUF FE의 볼륨 조절을 확인한다.

원본 DMNote JSON에 여러 탭이 있어도 파일에 저장된 현재 선택 탭만 등록된다. 첨부한 원본 `preset.json`은 `numpad`가 선택되어 있어 그대로 사용할 수 있다. 선택 정보가 없거나 삭제된 탭을 가리키면 DMNote에서 원하는 탭을 선택한 뒤 다시 내보낸다. `custom2.css`는 CSS 입력으로 함께 등록한다.

## 인증 자동 검증

서버 실행 중 별도 터미널에서:

```sh
cd ~/dev/src/tuf-replay
rtk proxy bun run e2e:live:verify-auth
```

실제 비밀번호 로그인, cookie/CSRF, 동의, PKCE, state 보존, 잘못된 verifier 및 코드 재사용 차단, 제출 API의 실제 계정 권한, refresh rotation과 검증용 grant 폐기를 확인한다. 토큰은 출력하거나 파일에 저장하지 않는다. 결과는 `.data/auth-verification.json`에 저장한다. 이 검사는 실제 플레이·클리어·iframe 렌더링 검증을 대신하지 않는다.

## 종료와 복원

- 서버 터미널에서 Ctrl+C: 실행기가 만든 자식 프로세스 그룹을 함께 종료한다. DB와 게임은 유지한다.
- 다시 시작할 때는 `bun run e2e:live`만 실행하면 된다. 토큰을 수동으로 발급할 필요는 없다.
- 원래 게임 접속 설정으로 돌아가려면 게임 종료 후 `rtk proxy bun run e2e:live:settings restore`를 실행한다. 네 endpoint 필드만 복원하고 다른 사용자 설정은 유지한다.
- Rust는 별도의 `tuf_replay_local_game` DB와 Redis DB 13 / `local-game:` prefix를 사용한다. 기존 Lab의 DB와 저장 기록을 섞지 않는다.

## 검증 범위

2026-09-17: 전체 서버 기동, 실제 비밀번호 로그인·OAuth·제출 권한·refresh/revoke HTTP 검증, companion의 실제 게임 기록 조회, C# 빌드/테스트와 companion 143개 테스트·타입 검사·production build를 통과했다. 게임 모드와 로컬 endpoint 적용은 게임 종료 후 진행해야 하며, 새 실게임 클리어에서 TUF pass까지의 최종 검증은 아직 완료되지 않았다.

레벨 자격 판정은 레벨 응답의 `diffId`를 `/v2/database/difficulties` 배열의 `id`와 연결한다. #8068의 `diffId: 2`는 `PGU/P2`이며 발급 가능하다. 난이도 ID 숫자 자체나 `rating.averageDifficultyId`로 자격을 추정하지 않는다. 정상 매핑된 제출 불가 난이도는 `level_not_eligible`, 목록 조회 실패·필수 필드 누락·잘못된 구조·매핑 실패는 `catalog_unavailable`로 구분한다. 모드는 발급 HTTP 오류 코드를 preflight/HUD까지 보존한다.

이 수정은 Rust 라이브러리 34개 테스트, run-session 통합 테스트 3개(난이도·오류 계약 32개 사례 포함), C# preflight 오류 전달 테스트와 전체 C# 테스트로 검증했다. 두 TUF API의 가용성과 응답 계약에 계속 의존하며, 업스트림을 조회하지 못하면 발급을 중단한다. 기존 Lab mock도 같은 두 endpoint 계약을 사용한다.

서버는 `trusted_tester` 정책으로 기록된 게임 결과를 등록한다. 게임 판정 시뮬레이터가 검증하는 구성은 아니다. 기존 `e2e:dev`/5189 fixture UI는 별도 테스트 도구이며, 이 실행기와 같은 포트로 동시에 켜지 않는다. 일부 기존 TUF 이미지/CDN 참조는 남아 있어 완전 오프라인 실행을 보장하지 않는다.
