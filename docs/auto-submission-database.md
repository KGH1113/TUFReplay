# 자동 제출 DB 컬럼

`run_sessions`는 업로드 세션, `run_submission_records`는 증거 저장·검증·등록 작업이다. run당 1:1로 연결된다. 업로드가 끝난 `status=sealed`와 등록이 끝난 `state=submitted`는 함께 존재할 수 있다.

## run_sessions

| 컬럼 | 의미 |
| --- | --- |
| id | 내부 숫자 기본키. 다른 테이블의 참조 대상 |
| pid | API·WS·UI에서 사용하는 run UUID |
| status | 생성·전송·봉인·실패 등 업로드 상태 |
| protocol_version | 클라이언트 업로드 프로토콜 버전 |
| client_game_version, client_mod_version | 클라이언트가 보낸 게임·모드 버전 |
| tuf_level_id | 제출 대상 TUF 레벨 |
| client_tuf_file_id | 클라이언트가 주장한 설치 파일 ID. 공식 검증 결과와 별개 |
| client_level_relative_path | ZIP에서 선택한 차트의 상대 경로 |
| upload_token_hash | 업로드 토큰의 해시. 원문 토큰은 저장하지 않음 |
| lease_expires_at | 활동·재연결 유효 기한 |
| hard_expires_at | 활동 여부와 무관한 세션 최종 기한 |
| sealed_at | 완료 메시지를 받아 업로드를 봉인한 시각 |
| level_revision_id, level_revision_chart_id | 과거 차트 고정 구조의 참조. 현재 새 run에서는 NULL |
| created_at, updated_at | 생성·마지막 수정 시각 |

## run_submission_records

| 컬럼 | 의미 |
| --- | --- |
| id | 제출 레코드 내부 기본키 |
| run_session_id | run_sessions.id 참조. UNIQUE이므로 run당 한 레코드 |
| owner_id | 소유한 TUF 사용자 ID |
| oauth_grant_id | 작업 권한 재확인용 OAuth grant ID. access token이 아님 |
| state | uploading → evidence_ready → validation_pending → registering → submitted 등 처리 상태 |
| manifest | 증거 스트림의 Storage 경로·SHA-256·바이트·레코드 수·최종 sequence. CSV 원문은 포함하지 않음 |
| ingest_released_at | 영속 저장 후 Redis 청크 정리 완료 시각. manifest가 있는데 NULL이면 정리 재시도 대상 |
| validation | 판정·키 수·속도·검증기 버전·검증한 증거와 차트 식별값 등 검증 결과 JSON |
| external_pass_id | 등록된 TUF pass ID. E2E에서는 가짜 ID |
| reason | 거절·오류·정리 이유 코드 |
| requested_at | 사용자가 제출을 요청한 시각. 클리어 시각과 별개 |
| lease_owner, lease_until | Worker의 작업 독점 소유자와 만료 시각. 업로드 lease와 별개 |
| retry_count, next_attempt_at | 처리 재시도 횟수와 다음 예정 시각 |
| evidence_expires_at | 명시적 증거 만료 기한. 정상 저장된 증거는 NULL로 기한 없이 보관한다. 과거 정상 보관 기록의 기한은 마이그레이션으로 제거 |
| created_at, updated_at | 생성·마지막 수정 시각 |

Redis 청크 삭제가 실패해도 manifest와 evidence_ready를 되돌리지 않는다. 삭제 성공 뒤에만 ingest_released_at을 기록한다. 삭제 후 DB 기록 전에 종료되면 다음 복구에서 없는 키를 다시 삭제하고 완료를 기록한다. 봉인 메타데이터는 ACK 복구용 24시간 TTL로 남는다.

## 기타 테이블

| 테이블 | 역할 |
| --- | --- |
| level_revisions | 과거 공식 파일 ID, payload hash·버전, ZIP 경로, 원본 수정·확보 시각. 현재 런타임에서는 사용하지 않는 기존 구조 |
| level_revision_charts | revision별 차트 상대 경로, hash·버전, 저장 경로. 기존 참조 보존 |
| pg_loco_queue | Loco 큐. id=작업 ID, name=Worker, task_data=인자, status=처리 상태, run_at=실행 예정. interval·tags·priority는 반복·분류·우선순위 |
| seaql_migrations | version=적용한 마이그레이션 이름, applied_at=적용 시각 |

`ingest_released_at`은 `m20260908_063527_add_ingest_released_at_to_run_submission_records` 마이그레이션에서 추가했다. 이번 검증에서는 E2E·테스트 DB에 적용했으며 다른 환경도 새 서버를 실행하기 전에 마이그레이션을 적용해야 한다.
