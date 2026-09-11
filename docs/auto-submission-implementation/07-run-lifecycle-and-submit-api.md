# 7단계: run lifecycle, 제출 API와 상태 전환

## 목적

evidence가 안전하게 보존된 run을 웹에서 조회하고 한 번만 제출할 수 있게 한다. 실제 ADOFAI gameplay validation은 연결하지 않지만, 나중에 validator가 결과를 기록할 수 있는 경계를 마련한다.

## 선행조건

- 3단계 evidence persistence 완료
- 4단계 사용자 ownership 적용
- gameplay validator가 아직 없다는 사실을 production에서 우회하지 않음

## 상태 모델

권장 상태:

```text
created → streaming → sealed → persisting → evidence_ready
   └───────────────→ failed
created/streaming ─→ expired
persisting ────────→ persistence_failed | evidence_invalid

후속 validator:
evidence_ready → validating → submit_available | validation_rejected | validation_error

제출:
submit_available → submitted
```

현재 단계에서는 test-only 또는 명시적인 development stub 없이 `evidence_ready`를 `submit_available`로 자동 승격하지 않는다. validator 부재 상태에서 실제 기록 제출을 허용하면 안 된다.

## API

기존 명칭을 유지해 다음 형태를 권장한다.

```text
GET    /api/v1/runs?status=submit_available
GET    /api/v1/runs/{run_id}
POST   /api/v1/runs/{run_id}/submit
DELETE /api/v1/runs/{run_id}
```

목록은 cursor pagination을 사용하고 현재 사용자 소유 run만 반환한다.

## 구현 작업

1. status를 raw 문자열로 바꾸지 못하게 model transition method를 추가한다.
2. transition은 현재 상태, owner와 관련 timestamp를 transaction 안에서 검사한다.
3. GET 응답 DTO는 내부 storage key, upload token hash와 상세 anti-cheat 정보를 노출하지 않는다.
4. 목록에는 UI에 필요한 최소 필드와 chart/revision 표시 정보를 join한다.
5. submit API에 idempotency를 보장한다.
6. 같은 run에 동시 submit 요청이 들어와도 최종 결과가 하나만 생기게 unique constraint와 row lock을 사용한다.
7. submit 완료 뒤 같은 요청은 기존 결과를 반환한다.
8. 삭제는 active upload를 함부로 제거하지 않고 상태별 정책을 적용한다.
9. `validation_error`와 `validation_rejected`를 사용자 메시지에서 구분할 수 있는 공개 reason category로 변환한다.
10. validator용 내부 command는 evidence digest, pinned chart ID와 validator version을 요구하도록 정의한다.

## submit 결과 저장

최종 공개 run이 별도 row가 필요하면 `submitted_runs` 같은 immutable 모델을 둘 수 있다. 이름 정책상 `run_sessions`를 넓은 lifecycle row로 계속 사용할 경우에도 제출 당시 다음 snapshot은 변경 불가능하게 보존해야 한다.

- owner
- pinned TUF level/revision/chart
- evidence digest
- validation attempt/version
- 제출 시각
- score/judgment 결과가 추가될 경우 그 source version

## 테스트

- owner별 목록 격리
- cursor pagination 중 새 run이 추가되는 경우
- 모든 금지된 상태 전이
- concurrent submit 20회에 결과 하나
- submit 응답 유실 후 재시도
- validation 결과 없는 run submit 거부
- `validation_error` run submit 거부와 재검증 가능성
- active/sealed/submitted 상태별 delete
- pinned revision은 새 revision 공개 후에도 변하지 않음

## 완료 기준

- controller가 `ActiveModel`이나 status 문자열을 직접 변경하지 않는다.
- 제출은 정확히 한 번의 immutable 결과를 만들며 재시도에 안전하다.
- validator가 없는 상태에서는 production `submit_available`이 만들어지지 않는다.
- UI가 내부 구현을 추측하지 않고 공개 상태만으로 모든 화면을 구성할 수 있다.

