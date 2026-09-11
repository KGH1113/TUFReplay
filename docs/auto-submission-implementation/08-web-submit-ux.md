# 8단계: 웹 submit UX

## 목적

플레이어는 게임 중 업로드 과정을 관리하지 않고, 나중에 웹사이트에서 제출 가능한 플레이를 확인하고 제출 버튼만 누른다. 내부 recording, chunk, Redis, validation 같은 용어를 사용자에게 노출하지 않는다.

## 선행조건

- 7단계 상태 조회와 idempotent submit API
- 사용자 로그인과 ownership
- production에서 실제 `submit_available`은 validator가 만든다는 경계 유지

## 사용자 흐름

1. 사용자는 평소처럼 TUFHelperLite에서 레벨을 열고 플레이한다.
2. 게임 중에는 정상 auto submission 상태를 표시하지 않는다.
3. clear 후 서버가 처리하는 동안 웹에는 `처리 중` 상태가 보일 수 있다.
4. 준비가 끝나면 run이 `제출 가능` 목록에 나타난다.
5. 사용자가 제출 버튼을 누른다.
6. 중복 클릭이나 네트워크 재시도에도 한 번만 제출된다.

## 표시 상태

| 서버 상태 | 사용자 표현 | 주요 행동 |
|---|---|---|
| `sealed`, `persisting`, `evidence_ready`, `validating` | 처리 중 | 기다리기 |
| `submit_available` | 제출 가능 | 제출, 삭제 |
| `submitted` | 제출 완료 | 결과 보기 |
| `failed`, `expired` | 이번 플레이는 제출할 수 없음 | 닫기 |
| `evidence_invalid`, `validation_rejected` | 제출 요건을 확인할 수 없음 | 간단한 사유 보기 |
| `persistence_failed`, `validation_error` | 서버 처리 지연/오류 | 자동 재시도 안내 |

`validation_rejected`를 곧바로 “치팅”이라고 표현하지 않는다.

## 구현 작업

1. 인증된 run 목록 query를 별도 data hook으로 만든다.
2. cursor pagination과 상태 filter를 지원한다.
3. 초기에는 5~10초 polling을 사용하고 필요할 때만 SSE를 도입한다.
4. tab이 background면 polling 주기를 늘리거나 visibility API로 중단한다.
5. submit mutation 동안 해당 버튼만 disable한다.
6. idempotency key 또는 server idempotency를 이용해 double click을 처리한다.
7. optimistic하게 `submitted`로 만들지 않고 server 결과를 기다린다.
8. 오류 toast는 retryable/terminal을 구분한다.
9. 접근성 있는 상태 label, focus 처리와 keyboard submit을 제공한다.
10. 긴 레벨명, 여러 chart, 새 revision 공개 후 pinned old revision 표시를 확인한다.
11. 내부 upload token, raw evidence와 세부 anti-cheat signal은 브라우저에 보내지 않는다.
12. 처리 중 run의 개수가 많아도 activity page의 기존 기능과 렌더링 성능을 해치지 않게 한다.

## 게임 내 UX 원칙

- 정상 업로드 indicator 없음
- 네트워크 reconnect indicator 없음
- upload progress 없음
- 플레이 시작을 기다리게 하는 modal 없음
- 자동 제출 불가가 확정돼도 플레이 도중 방해하지 않음
- 필요 시 clear/fail 이후의 기존 결과 화면에만 비침투적 안내

개발자 진단 정보는 별도 debug setting/log에만 둔다.

## 테스트

- 처리 중 → 제출 가능 → 제출 완료
- submit 중 응답 유실과 재시도
- 같은 계정의 여러 browser tab 동시 submit
- 다른 계정 run 접근 차단
- polling 중 run 삭제/만료
- 서버 429/500/offline
- mobile/desktop, keyboard와 screen reader
- 100개 이상의 과거 run pagination
- 내부 transport 용어가 사용자 문구에 노출되지 않음

## 완료 기준

- 정상 사용자는 게임에서 auto submission 기술 상태를 보거나 조작하지 않는다.
- 웹에서는 제출 가능한 run과 처리 중 run을 혼동하지 않는다.
- 제출 버튼은 중복 결과를 만들지 않는다.
- 서버 장애를 사용자 부정행위나 플레이 실패처럼 표현하지 않는다.

