# 테스터 관리 앱과 DB 권한 전환

테스터 명단은 Rust 서버의 PostgreSQL `trusted_testers`에 저장한다. 별도 Bun 관리자 앱은 추가·재활성화·비활성화를 수행하며 모든 변경 사유를 `trusted_tester_events`에 같은 트랜잭션으로 남긴다. 비활성화는 기록·증거·pass를 삭제하지 않는다.

## 권한 계약

- Rust는 TUF OAuth identity/authorization 확인 후 현재 `trusted_testers.active`를 조회한다. row가 없거나 비활성이면 `auto_submission_tester_required`로 제출을 거절한다. DB 조회 실패도 허용으로 처리하지 않는다.
- 조회 결과를 캐시하지 않는다. 새 연결, `run_start`, terminal 처리, 제출 처리의 기존 권한 확인에 적용된다. v2 연결의 5초 grant 재확인에서도 DB 변경이 반영된다.
- 테스터 자격이 없어져도 기존 본인 기록 조회·삭제는 가능하다. 이미 등록된 pass의 응답 유실 복구도 기존 영수증을 유지한다.
- TUF BE는 OAuth grant/client/scope, 계정 상태, 연결된 player, 제출 권한과 최종 등록 트랜잭션을 계속 검사한다. `AUTO_SUBMISSION_ENABLED=false`는 전환 후에도 비상 OFF 스위치다.

TUF BE의 `AUTO_SUBMISSION_TESTER_AUTHORITY`는 다음 두 값만 허용한다.

| 값 | 테스터 판정 |
| --- | --- |
| `environment` 또는 미설정 | 기존 `AUTO_SUBMISSION_TRUSTED_USER_IDS` 검사 유지. 새 Rust와 조합하면 DB 활성 row도 필요하다. |
| `replay` | TUF 내부 API는 기존 명단을 읽지 않고 OAuth·계정 조건을 검사한다. 실제 테스터 판정은 인증된 Rust 서버의 DB 조회가 담당한다. |

알 수 없는 authority 값은 거절한다. `replay`는 공개 요청으로 선택할 수 없고 서버 환경 설정으로만 적용된다. 단순히 이 값을 바꾸는 것만으로 Rust의 DB 등록을 건너뛸 수 없다.

## 적용 순서

GitHub Actions를 통한 기존 명단 전환은 호스트의 보호된 파일 `/home/kgh/tuf-replay-data/auto-submission/trusted-testers-bootstrap.txt`에 계정 UUID를 한 줄에 하나씩 준비한다(mode 600). 배포 스크립트는 migration 직후, 새 API 시작 전에 이를 한 트랜잭션으로 등록하고 감사 이력을 남긴다. 이미 존재하는 계정은 변경하지 않아 비활성화를 되돌리지 않는다. 성공하면 파일을 `.applied`로 이동하며, 잘못된 UUID나 DB 오류가 있으면 배포를 중단한다. 명단은 Git에 넣지 않는다.

운영 적용 전 코드와 설정을 함께 준비한다. 이 작업에서는 운영 서버를 재시작하거나 운영 DB를 변경하지 않았다.

1. Rust migration `m20260923_000001_create_trusted_testers`를 적용한다. 초기 명단은 비어 있으며 운영 테스터를 자동 승인하지 않는다. 기존 migration 명령으로 실행한다.
2. `deploy/auto-submission.env.example`의 관리자 설정에 서로 다른 임의의 DB 비밀번호와 관리자 비밀번호를 넣는다. DB 비밀번호는 URL-safe 32자 이상, 관리자 비밀번호는 24자 이상이다. 비밀 파일을 Git에 넣지 않는다.
3. 아래 SQL을 DB owner 권한으로 한 번 적용해 전용 `trusted_testers_admin` 역할을 만든다. SQL은 테스터 두 테이블과 이벤트 ID sequence만 허용하고 DELETE·DDL·run/evidence 접근 권한을 부여하지 않는다. 기존에 같은 이름의 역할을 다른 용도로 사용했다면 재사용하지 말고 먼저 권한을 확인한다.
4. `tester-admin` Compose profile로 관리자 앱을 시작하고 기존 테스터의 숫자 player ID로 연결된 TUF 계정을 조회·확인해 등록한다. 앱은 TUF user UUID만 DB에 저장한다. 연결된 TUF 계정이 없는 player는 등록할 수 없다. Rust DB 검사 활성화 전에 명단을 옮겨 놓아 전환 중 제출 중단을 피한다.
5. DB 판정을 포함한 Rust 서버를 적용하고 활성·비활성 계정의 `/api/v1/account` 결과와 새 run 접근을 확인한다. TUF는 아직 기존 `environment` 설정으로 둘 수 있다.
6. 새 TUF BE 코드를 적용하고 `AUTO_SUBMISSION_TESTER_AUTHORITY=replay`를 설정한다. 이 시점부터 기존 `AUTO_SUBMISSION_TRUSTED_USER_IDS`를 제거할 수 있다. 이후 명단 변경에는 서버 재시작이 필요 없다.

Compose 예시에서는 비밀 설정 파일 경로를 `ENV_FILE`로 지정한다. 비밀번호가 로그에 출력되는 shell tracing(`set -x`)을 사용하지 않는다. role provision 명령은 PostgreSQL 17 컨테이너의 psql에서 실행한다. `TRUSTED_TESTERS_DB_PASSWORD`는 같은 값으로 현재 shell에도 export되어 있어야 하며, SQL은 [psql의 환경변수 읽기](https://www.postgresql.org/docs/17/app-psql.html) 기능으로 전달받는다.

```sh
docker compose --env-file "$ENV_FILE" -f deploy/docker-compose.auto-submission.yml \
  build tuf-replay-server

docker compose --env-file "$ENV_FILE" -f deploy/docker-compose.auto-submission.yml \
  run --rm --no-deps tuf-replay-server db migrate

docker compose --env-file "$ENV_FILE" -f deploy/docker-compose.auto-submission.yml \
  exec -T -e TRUSTED_TESTERS_DB_PASSWORD postgres-production \
  sh -c 'exec psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB"' \
  < deploy/scripts/provision-trusted-testers-admin.sql

docker compose --env-file "$ENV_FILE" -f deploy/docker-compose.auto-submission.yml \
  --profile tester-admin up -d --build trusted-testers-admin
```

관리 앱은 호스트의 `127.0.0.1:4177`에만 공개한다. SSH 포트 포워딩으로 접속하고 공개 gateway/Nginx route는 추가하지 않는다. 컨테이너 내부 바인딩만 `0.0.0.0`이며 호스트 loopback 제한, 관리자 Basic 인증, POST Origin 검사, CSP 및 no-store를 함께 적용한다.

되돌릴 때는 TUF authority를 `environment`로 바꾸고 기존 명단을 복원하면 제한을 다시 추가할 수 있다. Rust DB 검사는 계속 적용된다. 테스터·감사 이력 테이블을 삭제하는 migration rollback은 제공하지 않는다.

## 로컬 개발과 확인

`bun run e2e:live`는 다음 실행 때 migration 후 로컬 테스트 UUID를 DB에 처음 한 번 등록한다. `ON CONFLICT DO NOTHING`으로 관리자에게 비활성화된 테스터를 다시 켜지 않는다. 이 fixture는 loopback의 `tuf_replay_local_game`/`tuf_replay_e2e`만 허용하며 운영 계정을 추가하지 않는다. 현재 실행 중인 사용자 개발 서버는 변경하지 않는다.

검사 명령:

```sh
./scripts/run.sh server-check --integration
./scripts/run.sh tester-admin-check
# 별도 테스트 DB의 migration이 적용된 경우 PostgreSQL 저장소 검사도 실행:
TESTER_ADMIN_TEST_DATABASE_URL='postgres://...@127.0.0.1:5432/tuf_replay_test' \
  ./scripts/run.sh tester-admin-check
```

2026-09-23 검증: Rust 단위 테스트 42개 및 통합 테스트 24개 통과(기존 real visual fixture 테스트 1개 ignore), 관리자 HTTP 테스트 3개 및 TypeScript 검사 통과, 실제 PostgreSQL 저장소 테스트 1개 통과. PostgreSQL 테스트는 감사 이벤트 insert가 실패하면 membership 변경도 rollback되는 것을 확인한다. TUF 정책·최종 OAuth/account gate·운영 도구 테스트 13개 및 TUF 전체 TypeScript 검사도 통과했다. Compose config와 shell syntax 검사는 통과했으며 shellcheck는 설치되어 있지 않아 실행하지 못했다. 컨테이너 운영 배포, 운영 role provision, 수동 UI 확인은 실행하지 않았다.
