# Auto submission 구현 상태 — 2026-09-15

[확정된 제품 계약](auto-submission-decisions.md)에 trusted tester 배포 조건을 추가했다. 최신 운영 준비와 검증 결과는 [테스터 배포 기록](auto-submission-trusted-testers-rollout-2026-09-15.md), 실행 절차는 [배포 안내](../deploy/README.md)를 참고한다.

## 현재 구현

- 기존 영상 제출을 유지하고 공개 PGU 난이도의 P1–P20/G1–G20을 지원한다. 최종 관리자 심사 단계는 없다.
- 웹은 UI와 OAuth 브라우저 이동만 담당한다. 로그인·목록·설정·제출·삭제는 IPC로 모드에 요청하며 OAuth 토큰을 받지 않는다.
- 모드는 TUF Authorization Code + PKCE로 로그인한다. 공식 OAuth 앱과 auto 환경 origin이 기본 설정에 포함된다. refresh token은 macOS Keychain 또는 Windows Credential Manager에 저장하고 다음 실행에서 복원한다.
- 일반 토큰 갱신은 현재 캡처를 끊지 않는다. 현재 기기 로그아웃은 해당 grant를 해제하고, TUF 앱 권한 철회는 해당 앱의 모든 기기 grant를 해제한다. 로그아웃 통신 실패 시 보안 저장소에 해제 의도를 남겨 다음 실행에서 재시도한다.
- 로그인 후 자동 캡처는 기본 ON이며 끌 수 있다. 저장된 기록과 진행 중 제출 작업은 유지된다. 계정당 동시에 업로드하는 플레이는 하나다.
- TUFHelperLite 설치 정보로 레벨을 식별한다. 발급 시 공식 파일을 다운로드하지 않는다. trusted tester 경로는 Strict, 전체 플레이, NoFail OFF, 실패 없는 클리어를 대상으로 한다. pause는 자동 제출을 중단한다.
- 플레이 중 native input, hit context, lifecycle, runtime settings, recorder health를 청크로 전송한다. 입력 경로는 고정 버퍼를 쓰고 직렬화·통신은 백그라운드에서 수행한다.
- keyCount는 클리어까지 실제로 눌린 서로 다른 키 수다. 게임 버전과 hold 설정 변화도 증거에 포함한다. 이 클라이언트 값은 검증을 대신하지 않는다.
- 클리어 후에는 증거만 저장한다. 제출 클릭 시 최신 공식 차트를 임시 다운로드하여 검증기에 전달하고, 임시 ZIP과 추출 원본은 정리한다. 과거 공식 원본과의 호환성을 유지하지 않는다.
- **시뮬레이터는 미구현이다.** `SUBMISSION_VALIDATION_MODE=unavailable`이 기본이며 `validator_unavailable`로 끝난다. 명시적으로 `trusted_tester`를 설정하면 TUF BE가 허용한 계정의 클리어 결과를 변환해 실제 pass 등록 경로로 전달한다. 결과 계약 v2에 `validation_status=skipped_trusted_tester`, `result_provenance=recorded_game_result`를 저장하며 게임플레이를 검증했다고 표시하지 않는다. TUF 공통 함수가 era/XPerfect/midspin을 반영해 정확도와 점수를 계산한다.
- TUF pass에는 `submissionSource`와 `autoSubmissionRunId`를 기록한다. 공개 pass의 replay manifest·증거 제공과 TUF의 iframe 재생 연결도 구현되어 있다.

## 빌드와 배포 채널

- `main`과 `dev` 웹은 표준 모드를 위한 독립 Compose 환경이다. `feat/auto-submission`은 `tufreplay-auto.impl1113.dev`에서 웹·Rust·데이터 저장소를 별도로 운영한다.
- 모드 health에 `BuildFlavor`와 `AutoSubmissionProtocolVersion`을 포함한다. 자동 제출 웹은 `auto-submission` flavor와 제출 프로토콜 1을 확인하고, 각 제출 IPC 호출 직전에 다시 확인한다. 표준 모드는 제출 기능과 IPC를 초기화하지 않는다.
- 표준 버전은 GitHub Releases를 사용한다. 자동 제출 패키지는 홈 서버의 `/updates/auto-submission/latest.json`과 버전별 ZIP으로 배포하며 GitHub Release로 공개하지 않는다.
- OAuth 앱에는 `https://tufreplay-auto.impl1113.dev/oauth/callback`이 등록되어 있다. OAuth scope 확대와 TUF 운영 allowlist 적용은 별도 운영 설정이다.

## 레이어

| 위치 | 책임 |
| --- | --- |
| mod Recording/Capture·Telemetry | 증거 값과 게임 상태 수집 |
| mod Submission/Auth | OAuth 흐름, 토큰 갱신, OS 보안 저장소 |
| mod Submission/Api·Transport | API 호출, 청크·ACK·재연결 |
| mod Submission/Sessions·Ipc | 세션 조정과 웹 요청 진입점 |
| server domain | 검증기·공식 차트 공급자·등록기 인터페이스 |
| server controllers·services·models | HTTP/WS, 작업 흐름, DB 상태 전이 |
| server workers·tasks | Loco 큐 실행, 주기적 복구·보관 정리 |
| web api·schemas·models·state·hooks·components | IPC 계약부터 화면까지 역할 분리 |
| TUF services/autoSubmission | 권한 재확인, pass 트랜잭션, 등록 영수증, 레벨 변경 전달 |

## 설정과 마이그레이션

1. replay 서버에 PostgreSQL, Redis, Loco Storage를 준비한다. `cargo loco db migrate`를 실행한다. 이번 개발에서는 로컬 replay 개발 DB와 별도 테스트 DB에만 적용했다.
2. TUF backend에는 `1788742800_auto_submission_receipts.cjs`와 `1788800000_auto_submission_pass_source.cjs` 마이그레이션이 필요하다. **이번 작업에서 MySQL 마이그레이션은 실행하지 않았다.**
3. 두 API에 `TUF_TO_AUTO_SUBMISSION_TOKEN`, `AUTO_SUBMISSION_TO_TUF_TOKEN`을 환경변수로 넣는다. 서로 다른 32–512바이트의 공백 없는 무작위 값이어야 한다. 이전 공용 secret은 사용하지 않는다. 일반 사이트/OAuth 토큰은 internal 라우트에 사용할 수 없다.
4. TUF backend의 `AUTO_SUBMISSION_API_URL`, replay 서버의 `TUF_API_BASE_URL`을 설정한다.
5. TUF OAuth 앱을 생성하고 그 ID를 backend의 `TUF_AUTO_SUBMISSION_OAUTH_CLIENT_ID`에 넣은 뒤 해당 앱의 허용 scope를 `65537` (User.Read.Public + User.Submission.Create)로 설정한다. 다른 앱은 제출 scope를 받을 수 없다.
6. 모드 설정 `AutoSubmissionOAuthClientId`, `AutoSubmissionServerUrl`에 배포 값을 주입한다. 공식 앱 Client ID는 `1dc9ff206f5301c9e7ef4ba9b209c7c7`, 서버 origin은 `https://tufreplay-auto.impl1113.dev`다. `AutoSubmissionTufApiUrl`, `AutoSubmissionOAuthRedirectUri`는 대상 환경과 OAuth 앱의 등록 redirect URI에 맞춘다.
7. TUF BE의 `AUTO_SUBMISSION_ENABLED`는 기본 false다. `AUTO_SUBMISSION_TRUSTED_USER_IDS`는 계정 UUID를 사용하며 빈 목록은 모두 거절한다. 초기 테스터 `impl.dev` (player `7410`)의 UUID는 `670cac2c-8175-46a6-87f7-b92741d4499f`다.
8. 웹 callback URL은 같은 웹 앱으로 연결되어야 한다. code/state는 IPC로 모드에 전달되며 주소창에서 제거된다. 토큰은 웹으로 전달하지 않는다.

TUF → replay 변경 통보 경로는 `/internal/tuf/levels/{id}/changed`다. replay → TUF 호출은 `/v2/internal/auto-submission/*` 아래에 있다. 변경 알림은 인증된 계정 WS를 거쳐 AssetBundle toast에 표시한다. 파일 ID 변경만으로 저장된 플레이를 즉시 거절하지 않고 제출 시 현재 차트로 검증한다.

## 복구와 보관

- 동일 sequence 재전송은 동일 바이트만 허용한다. 새 연결이 이전 연결을 대체하며 ACK·완료 응답 유실을 복구한다. 재연결 허용 시간은 30초다.
- 모드의 30초 복구 제한은 정상 연결 시간이 아닌 오류 감지 시점부터 계산한다. 복구 시 대기 횟수도 초기화하고, 최대 8회 조기 종료 제한은 제거했다. 남은 기한이 연결·hello·응답 전체에 적용된다. 가상 시계로 장시간 정상 플레이 후 단절·반복 단절·기한 이후 handshake 거절을 검사한다.
- 영속 저장된 미제출 증거는 기한 없이 보관한다(`evidence_expires_at=NULL`). 나중에 사용자가 제출하면 당시 권한·차트·검증 조건으로 처리한다. 명시적 삭제와 미완료 업로드 정리는 별도로 유지한다.
- 제출 작업은 DB lease와 영속 재시도로 게임 종료 후에도 계속된다. 일시 오류는 제한된 재시도 후 수동 재시도로 전환하고, 의미 검증 거절은 자동 재시도하지 않는다.
- 작업 실행은 Loco Postgres 큐를 사용한다. `SCHEDULER_CONFIG=config/scheduler.yaml cargo loco start --all`로 서버·Worker·Scheduler를 함께 실행한다. 분리 실행 명령과 폴더별 책임은 [서버 README](../server/README.md)에 정리했다.
- 등록 재시도는 run UUID 영수증을 먼저 조회한다. 응답 유실로 등록 여부가 불명확한 증거는 보관 만료로 삭제하지 않는다.
- 검증 후 등록 사이에 공식 차트가 변경되면 수동 재시도에서 최신 차트를 다시 검증한다.
- 제출된 증거는 pass가 유지되는 동안 보관한다. pass 숨김·삭제를 이유로 증거를 공개하거나 자동 제거하지 않는다.
- 일일 바이트 제한과 분당 5회 발급 제한은 제거했다. 개별 run·청크·버퍼의 방어 상수는 아직 실게임 측정 전 임시 구현값이며 확정된 서비스 한도가 아니다.

## 기존 기반 검증 (2026-09-08)과 남은 실환경 검증

아래 횟수는 기존 파이프라인의 당시 검증 기록이다. 2026-09-15 변경의 최종 결과는 위 테스터 배포 기록에 별도로 정리한다.

- 독립 [서버 E2E Lab](../tools/auto-submission-e2e/README.md)은 NoFail OFF·Strict·실패 판정 0개와 시간값·로컬 차트를 확인한 클리어 기록 1개를 HTTP/WS로 재전송한다. 이전 NoFail 기록 2개는 제외했다. 실제 DB·Redis·Worker를 사용하고 TUF API는 로컬 가짜 서버이며 검증은 가상 성공·미준비 모드로 선택한다. 원본 리플레이 DB는 읽기만 한다.
- 증거 파일과 manifest 확정 후 Redis 청크를 삭제하고 완료 응답 메타데이터는 유지한다. `ingest_released_at`으로 정리 완료를 기록하며 주기적 복구가 미완료 정리를 재시도한다. [DB 컬럼](auto-submission-database.md), [Redis 실측 검증](auto-submission-redis-verification.md).

- 공식 빌드 스크립트 통과: C# 빌드, Unity/Mono 호환성, native helper, 캡처/ACK/완료 응답 복구, PKCE·동시 refresh·로그아웃 실패 복원 테스트. 로컬 모드 설치까지 수행했고 **게임은 실행하지 않았다.**
- Rust 25개 테스트 통과: 로컬 PostgreSQL/Redis에서 발급, 소유권, 단일 동시 플레이, 청크 중복과 재연결, 저장, 만료, 재시도, 영수증 복구, 단독 Task의 큐 등록과 실제 Worker 처리를 검사했다. 리팩터링 후 `cargo fmt --check`, Clippy의 전체 target 경고 검사도 통과했다.
- 웹 타입 검사, 96개 Bun 테스트, production build 통과. 단일 JS chunk 크기 경고는 남는다.
- mock 브라우저에서 계정 연결, 캡처 OFF 후 저장 기록 유지, 제출 후 검증기 미준비 상태를 확인했다. 390px 화면에서 버튼과 만료 날짜가 표시되며 브라우저 오류는 없었다.
- TUF backend 타입 검사와 내부 토큰·등록 입력 테스트 4개 통과.
- 실제 TUF OAuth 로그인, OS 보안 저장소 접근, MySQL 등록 트랜잭션, 두 서버를 잇는 CDC 알림은 실제 환경 통합 검증이 남아 있다.
- 시뮬레이터 구현, 실게임 프레임 비용·고밀도 차트 측정, 장시간 연결 부하와 운영 복구 검증도 남아 있다. 테스트용 검증기의 성공을 실제 치팅 검증 성공으로 해석하면 안 된다.
