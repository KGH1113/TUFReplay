# Redis 청크 정리 실측

2026-09-08 로컬 E2E 서버·PostgreSQL·Redis DB 14·Storage·Loco Worker/Scheduler로 검증했다. TUF는 로컬 mock이며 성공 판정은 fixture 기반 테스트 검증 결과다.

## 초기화와 fixture

E2E 앱·큐를 초기화해 run 29개가 0개가 됐다. Redis `e2e:*` 키 3개와 E2E Storage·fixture·가짜 영수증을 삭제했다. 기존 마이그레이션 이력 6개를 보존하고 새 마이그레이션을 적용했다. 원본 SQLite와 개발·운영 DB는 초기화하지 않았다.

NoFail 기록 ヤラララ와 Level 5를 제외했다. NoFail OFF·Strict·실패 판정 0개에 시간값과 로컬 차트까지 갖춘 `The Limit Does Not Exist`만 선정했다. 원본 run은 `092eb0faa4694a1a82019a58fe2c113f`, 전송 입력 2,762개·hit 1,360개다.

## 정리 실패와 재시작

서버 run: `43b32403-9c03-41fb-bc69-119aabc05de4`.

| 단계 | 업로드 / 제출 상태 | manifest 파일 | Redis 청크 | 청크 메모리 | 청크 TTL / 메타 TTL(초) |
| --- | --- | ---: | ---: | ---: | --- |
| 발급 | created / uploading | 0 | 0 | 키 없음 | -2 / 45 |
| 첫 ACK | streaming / uploading | 0 | 1 | 396 B | 45 / 45 |
| 봉인, Worker 시작 전 | sealed / uploading | 0 | 2,902 | 227,683 B | 86,400 / 86,400 |
| manifest 확정, UNLINK 거절 | sealed / evidence_ready | 7 | 2,902 | 227,683 B | 86,397 / 86,397 |
| 정상 권한으로 재시작·복구 | sealed / evidence_ready | 7 | 0 | 키 없음 | -2 / 86,393 |
| 재연결·complete 재전송 | sealed / evidence_ready | 7 | 0 | 키 없음 | -2 / 86,393 |
| 가상 제출 완료 | sealed / submitted | 7 | 0 | 키 없음 | -2 / 86,393 |

Redis TTL -2는 키가 없다는 뜻이다. 임시 Redis 읽기 전용 사용자로 실제 UNLINK 권한 오류를 유도했다. 파일 7개와 DB manifest는 저장되었지만 ingest_released_at은 NULL로 남았다. 정상 권한으로 서버를 재시작한 뒤 주기적 복구가 청크를 지우고 `2026-09-08T06:43:31.202Z`를 기록했다. 임시 ACL 사용자는 검사 종료 시 삭제했다.

청크 삭제 후 hello·complete를 재전송해 최종 ACK 2,901과 원래 입력·hit 개수를 복구했다. 제출 후에도 청크는 다시 생기지 않았다. 원시 관측 파일은 Git에서 제외한 `tools/auto-submission-e2e/.data/lifecycle-observations.json`에 있다. 재실행 시 파일은 새 관측으로 바뀐다.

## 회귀 검사

`bun run e2e:test`의 가상 성공·검증기 미준비·ACK 유실·등록 응답 유실 4개 HTTP/WS 시나리오가 통과했다. 저장 input/hit CSV와 fixture의 바이트 일치, 미준비 모드의 등록 없음, 응답 유실 복구 시 등록 요청 1회, 영속 저장 후 청크 키 없음도 검사한다. 실행별 UUID와 상태는 `.data/e2e-observations.json`에 남긴다.

Rust 테스트 26개와 도구 단위 테스트 5개가 통과했다. 이 검증은 게임 캡처나 실제 치팅 판정 정확성을 검증하지 않는다.

실제 NOPERM/UNLINK 로그 확인을 추가한 재검사도 run `ccf2e871-6d5c-475b-8db9-b9c3d71b6e85`에서 통과했다. 봉인 청크 2,902개(227,808 B)가 재시작 복구 후 0개가 됐고 정리 시각은 `2026-09-08T06:49:29.194Z`였다. 현재 원시 lifecycle 관측 파일에는 이 재검사가 저장되어 있다. Clippy(`-D warnings`), Rust 포맷 검사, 도구 타입 검사·Vite 빌드도 통과했다.

실행 명령과 환경 준비는 [E2E Lab README](../tools/auto-submission-e2e/README.md), 컬럼 설명은 [DB 문서](auto-submission-database.md)를 참고한다.
