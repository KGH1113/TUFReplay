# Auto-submission trusted tester 배포 준비 — 2026-09-15

## 적용할 동작

- 실제 TUF 프로덕션에 pass를 등록하는 테스터 경로다.
- 게임플레이 시뮬레이터는 생략한다. 기록 데이터의 구조, OAuth 권한, 소유권, 청크 무결성, 레벨 조건, 중복 등록 방지, TUF 점수 계산은 유지한다.
- TUF BE가 허용한 계정만 신규 캡처·제출·등록을 수행한다. TUFReplay 베타 업데이트 설정은 이 권한과 독립적이다.
- 기본 정책은 비활성이고 빈 목록·잘못된 설정은 모두 거절한다.

## 확정된 계정과 주소

| 항목 | 값 |
| --- | --- |
| 최초 테스터 | 표시 이름 `impl`, TUF 계정 `impl.dev` |
| TUF player ID | `7410` |
| 허용할 계정 UUID | `670cac2c-8175-46a6-87f7-b92741d4499f` |
| 공식 OAuth Client ID | `1dc9ff206f5301c9e7ef4ba9b209c7c7` |
| OAuth callback | `https://tufreplay.impl1113.dev/oauth/callback` |
| Rust API 및 companion web | `https://tufreplay.impl1113.dev` |
| TUF API / 웹 | `https://api.tuforums.com` / `https://tuforums.com` |
| web-adofai | `https://web-adofai.impl1113.dev` |

계정 UUID는 공개 player API에서 연결 관계를 확인했다. OAuth 앱은 사용자가 생성해 Client ID를 제공했다. 공개 Client ID는 비밀값이 아니다. 허용 목록은 숫자 player ID가 아닌 `users.id` UUID를 사용한다.

## 구현 계약

### 인증과 권한

`AUTO_SUBMISSION_ENABLED`와 `AUTO_SUBMISSION_TRUSTED_USER_IDS`는 TUF BE만 관리한다. 공식 OAuth client, submission scope, 유효한 grant, 활성 계정, 연결된 player 및 기존 제출 제한을 계속 검사한다.

Rust의 `GET /api/v1/account`는 직접 JSON 객체로 `owner_id`, `grant_id`, `client_id`, `username`, `nickname`, `can_submit`, `denial_reason`을 반환한다. 제출 권한 필드가 없으면 거절한다. 테스터에서 제외되어도 유효한 기본 계정 인증으로 본인의 저장 기록을 조회·삭제할 수 있다.

신규 발급·제출 접수·업로드 중 권한 재확인·worker 처리·TUF 등록 직전에 제출 권한을 요구한다. TUF 등록은 트랜잭션 안에서 권한을 다시 확인한다. 이미 커밋된 영수증의 응답 유실 복구는 신규 등록과 구분해 권한 해제 후에도 같은 pass를 복구한다.

모드는 계정 정보를 백그라운드에서 조회해 계정 이름과 제출 가능 상태를 IPC/웹에 표시한다. 토큰은 OS 보안 저장소에 남고 브라우저에 전달하지 않는다. 허용 상태가 없는 계정은 자동 제출용 준비와 캡처를 시작하지 않는다.

### 기록 결과와 검증 생략

모드 완료 metadata에 `submissionResult.version=1` 결과 snapshot을 추가한다. 판정 배열 순서는 `[Overload, TooEarly, Early, EarlyPerfect, Perfect, LatePerfect, Late, TooLate, Miss]`다. 경쟁 판정에서 Perfect 칸은 XPerfect이며 Perfect−/Perfect+는 별도 필드다. 클리어 결과 수집에 실패하면 제출 증거를 중단하며 누락된 판정을 0으로 만들지 않는다.

Rust의 `SUBMISSION_VALIDATION_MODE`는 `unavailable`(기본) 또는 `trusted_tester`다. 테스터 경로는 제한된 크기의 저장 metadata, 성공한 전체 클리어, Strict, NoFail OFF, recorder 오류 없음, 게임 버전·hold 설정·run ID·판정 계약을 확인한다. 현재 공식 차트를 획득해 replay 식별자를 구성하며 게임플레이 시뮬레이터는 호출하지 않는다. 로컬 E2E fixture 성공 값은 운영 테스터 경로에서 사용하지 않는다.

Rust → TUF 결과 계약 v2에는 다음 표시를 서버가 붙인다.

```json
{
  "validation_contract_version": 2,
  "validation_status": "skipped_trusted_tester",
  "result_provenance": "recorded_game_result"
}
```

판정·Perfect±·era(`1=v2`, `2=pre-3.4`, `3=3.4+`)·경쟁 모드를 함께 전달한다. TUF의 `preparePassJudgementsForPersist`가 현재 Level의 midspin 값으로 필요한 보정을 한 번 적용하고 공통 점수 함수를 사용한다. midspin 값이 없을 때는 기존 공통 함수의 차감 생략 규칙을 따른다. 기존 저장 결과 v1은 replay 조회와 영수증 복구를 위해 읽을 수 있지만 신규 v2 등록 결과로 간주하지 않는다.

### OAuth 화면

TUF 동의 화면은 submission scope를 표시한다. 일반 앱 생성 권한은 공개 프로필로 유지한다. 앱의 redirect 설정 저장은 `allowedScopes`를 덮어쓰지 않아 운영자가 설정한 공식 앱 권한을 보존한다.

## Git 준비와 원본 보존

| 저장소 | 브랜치 | 준비 기준 |
| --- | --- | --- |
| tuf-backend | `feat/auto-submission` | origin/dev `69ef042d8197eecaa4e8c6fa5d4a374ce34c3234` |
| t21c-web-frontend | `feat/auto-submission` | origin/dev `2a136a6aeea07147a6e9a655250b5391b4b45170` |
| tuf-replay | `feat/auto-submission` | origin/dev `f8238e70b969d6981f11cdcd0c1148c348238ffd`를 merge한 `b7857a0e7451e40d283065d70680bd200531238c` |

BE/FE Git 준비는 사용자가 지정한 GPT-5.6 Luna Medium이 수행했다. BE 기존 변경은 충돌 없이 복원했다. FE의 PassFlags/PassDetailPage 충돌은 기존 자동 제출 표시와 dev의 era/XPerfect 표시를 함께 보존했다. 당시 untracked 파일 BE18개/FE17개를 백업과 바이트 단위로 확인했다.

BE/FE 원본 백업은 `/private/tmp/auto-submission-git-backup.A8lBug/`에 있으며 보존 stash도 유지했다. replay merge 전 백업은 `/private/tmp/tuf-replay-merge-backup-20260915-6keidG`다. 사용자 AGENTS.md 변경과 .agents/.codex는 보존했다. merge 후 레거시 테스트·mock 인터페이스에 중복된 항목을 바로잡았다. 기능 구현은 작업 트리에 남기며 원격 push나 릴리스 발행은 하지 않는다.

## 배포 구성과 준비 상태

[배포 안내](../deploy/README.md)의 root Compose + production overlay로 web, Rust API/worker/scheduler, 별도 Postgres·Redis·artifact 볼륨을 실행한다. Rust는 non-root, 호스트 포트는 loopback `5150`이다. 운영 DB/Redis는 호스트 포트를 공개하지 않는다. Redis는 ACK된 증거의 내구성을 위해 `appendfsync=always`를 유지한다.

GitHub CI/deploy는 Rust fmt·Clippy·격리 DB/Redis 테스트·웹 검사와 Docker 빌드를 수행한다. 배포는 검사한 커밋이 현재 main과 일치할 때만 진행한다. 환경파일 확인 후 이미지를 빌드하고 DB migration을 명시적으로 실행한 다음 기존 `tuf-replay-web.service`를 시작한다. `start --all`로 worker와 scheduler를 함께 실행하며 `/_readiness`가 DB와 Postgres queue를 확인한다.

실제 애플리케이션 배포는 각 저장소의 GitHub Actions workflow로만 수행한다. replay는 `deploy.yml`의 main push 또는 main 대상 수동 dispatch를 사용한다. 배포 스크립트는 workflow 내부 helper이며 검증된 SHA 인자를 요구한다. SSH에서 직접 배포 스크립트·Compose 업데이트·운영 migration·애플리케이션 restart를 실행하지 않는다. TUF BE/FE는 각 저장소의 `promote-production.yml`을 사용한다.

홈 서버에서 확인한 현재 구성은 `/srv/TUFReplay`, loopback web `4174`, Nginx `127.0.0.1:8080`, Cloudflare Tunnel의 기존 정확한 hostname이다. 새로운 DNS/tunnel 경로는 필요 없다. `/api/v1/`, `/internal/`, `/_readiness`를 Rust로 보내고 `/api/tuf/*`, `/oauth/callback`, 웹은 기존 upstream을 사용한다.

- GitHub의 `TS_OAUTH_CLIENT_ID`, `TS_OAUTH_SECRET` 등록 여부를 확인했다. 값은 읽지 않았다.
- 독립된 새 DB 비밀번호와 양방향 토큰을 생성해 로컬 `.env.production`(Git 제외)과 홈 서버 `/srv/TUFReplay/.env.production`에 600 권한으로 준비했다.
- 같은 토큰과 초기 테스터 UUID를 넣은 TUF 설정 조각은 `/private/tmp/tuf-auto-submission-operator-wnwnroj_/tuf-backend.env.snippet`에 600 권한으로 준비했다. 전체 기존 TUF env를 이 조각으로 교체하면 안 된다. 초기 `AUTO_SUBMISSION_ENABLED=false`다.
- 홈 서버의 실제 Nginx 설정을 보존한 변경안은 같은 비공개 폴더의 `nginx-impl1113.dev.candidate`에 있다. 로컬 Nginx 문법 검사를 통과했다.
- 홈 서버 서비스 restart, Nginx 적용/reload, 운영 DB migration, TUF 서버 SSH 접근은 수행하지 않았다.

## 운영 적용 순서

1. TUF 담당자가 기존 migration 적용 현황을 확인하고 BE의 `promote-production.yml`로 코드를 배포한다. 필요한 기존 migration은 `1788742800_auto_submission_receipts.cjs`, `1788800000_auto_submission_pass_source.cjs` 및 dev의 선행 migration이다.
2. TUF 환경에 위 설정 조각을 병합하고 공식 OAuth 앱의 허용 scope를 `65537`로 설정한다. 앱 callback은 위 URI와 정확히 같아야 한다. [TUF operator guide](/Users/kgh/dev/src/tuf-backend/docs/auto-submission-operator.md)의 helper는 계획과 입력값 확인용이며 운영 변경을 수행하지 않는다.
3. 초기 Nginx 경로 구성을 준비한 후 replay의 `deploy.yml`을 main에서 실행한다. 코드 적용·migration·서비스 restart·readiness는 workflow가 수행한다. Nginx 변경안은 아직 미적용이며 현행 파일과 재비교 및 문법 검증이 필요한 초기 호스트 설정이다.
4. TUF FE의 production build env에 `VITE_AUTO_SUBMISSION_API_URL=https://tufreplay.impl1113.dev`, `VITE_WEB_ADOFAI_URL=https://web-adofai.impl1113.dev`를 넣고 `promote-production.yml`로 빌드·배포한다. companion web과 새 모드 패키지를 사용한다.
5. 준비가 끝나면 TUF의 `AUTO_SUBMISSION_ENABLED=true`를 반영한다. 첫 테스터로 OAuth 연결·실게임 클리어·제출·pass 점수·replay를 확인한다.

TUF 운영 서버 접근 및 변경은 사용자의 별도 허락을 받은 뒤 수행한다. 현재 준비된 설정 조각은 운영 서버에 적용하지 않았다.

## 검증 기록

최종 로컬 검증을 완료했다. GitHub workflow 자체의 원격 실행과 운영 배포는 수행하지 않았다.

- 공식 모드 `./scripts/run.sh build`: C# 전체 테스트, updater7개, native helper/input, Unity/Mono 호환성 통과. 설치 출력은 `/private/tmp`로 분리했다. CSharpier242파일 검사 통과.
- 최종 companion web111개 테스트, 타입 검사, Biome, production build 통과.
- TUF FE 수정 파일 ESLint 및 production build 통과. 테스트 빌드는 Sentry 업로드를 끄고 `/private/tmp`에 출력했다.
- TUF BE 집중 테스트17개와 전체 build(lint/TypeScript 포함) 통과. 오래된 로컬 adofai-lib1.4.5를 lockfile의1.4.7로 복원했다.
- Rust 유닛23개·통합14개(총37개), fmt, Clippy `--locked --all-targets -- -D warnings` 통과. 테스터 권한 철회 후 기존 영수증 복구도 검증했다.
- Rust 운영 Docker image build 및 격리 production Compose lifecycle 통과: migration7개, readiness200, worker/scheduler 시작, non-root UID10001, artifact 쓰기, 비인증 account/WS401, CORS preflight200. 테스트 대상 TUF 주소는 외부에 연결되지 않는 로컬 주소로 제한했다.
- 웹 Docker image build 및 실제 Host 헤더를 사용한 `/`·`/oauth/callback` HTTP200 통과. 모든 workspace manifest를 복사하고 fixture 준비 스크립트를 명시적 명령으로 분리해 clean `bun install --frozen-lockfile`이 게임 DB 없이 성공한다. E2E 도구 유닛 테스트8개 통과.
- 기본/운영 Compose config, 비밀값을 출력하지 않는 env preflight, Nginx 후보 문법, shell syntax, Actions actionlint 통과. 인자 없는 수동 배포 helper 실행은 변경 전에 거절된다.
- 독립 Astra 리뷰에서 BE/FE OAuth/배포, Rust/모드, 최종 웹 권한 처리와 판정 snapshot 변경 모두 중대한 문제 없음.

## 모드 배포 산출물

기존 `0.2.0-beta.1`이 이미 발행되어 새 패키지는 `0.2.0-beta.2`로 준비했다. 공식 package 명령으로 생성했으며 릴리스는 발행하지 않았다.

- 패키지: `/Users/kgh/dev/src/tuf-replay/build/TUFReplay.zip`
- 업데이트 manifest: `/Users/kgh/dev/src/tuf-replay/build/TUFReplay.update.json`
- 크기: `3781214` bytes
- SHA-256: `8d2afc4f697650ce1078e76e5a0d7667f6540ffce7d18c77287efb5ed510a7fc`
- archive54개 항목과 manifest의 해시·크기·버전이 일치하며 `.env`와 SQLite 데이터 파일은 포함하지 않는다.

실제 TUF OAuth·운영 MySQL 등록·실게임 프레임 비용·프로덕션 replay E2E는 운영 적용 후 확인할 항목이다. 시뮬레이터 구현은 이번 배포의 범위 밖이다.
