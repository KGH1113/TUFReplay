# 5단계: TUFReplay capture fan-out과 hot path

## 목적

기존 로컬 replay 기록과 auto-submission upload가 동일한 capture source를 공유하게 한다. 플레이어가 입력 지연, 프레임 저하, 메모리 증가나 UI 방해를 느끼지 않아야 한다.

## 선행조건

- 1단계의 record schema와 client 성능 계약 확정
- 서버 발급 요청에 필요한 TUFHelperLite level context를 background에서 얻을 수 있음

## 핵심 구조

```text
ADOFAI hook / native input callback
             │
             ▼
       immutable record
          ├──────────────► 기존 local replay builder/SQLite
          └──────────────► bounded upload queue ─► background sender
```

새 raw schema를 별도로 만들지 않는다. 기존 serializer가 만드는 record를 한 번 캡처한 뒤 두 consumer로 fan-out한다.

## 구현 작업

1. 현재 `RecordingSession`, `RecordInputTracker`, `RecordingPatches`의 capture ownership을 명확히 한다.
2. `NativeInput`과 `HitContext`를 불변 record 또는 pool-backed value로 만든다.
3. capture callback은 bounded single-producer queue에 record reference/value를 넣고 즉시 반환한다.
4. local replay consumer와 upload consumer가 서로의 처리 속도에 의존하지 않게 한다.
5. queue에는 count뿐 아니라 byte budget을 둔다.
6. queue가 가득 차면 main thread를 block하지 않는다.
7. overflow 시 해당 run의 auto submission만 terminal recorder-health failure로 전환하고 로컬 replay 기록은 계속한다.
8. start eligibility gate는 0% 시작, No Fail 비활성, TUF-managed latest revision 준비 여부만 UX 최적화로 사용한다.
9. lifecycle/runtime-setting snapshot은 capture 시작 전에 background state 준비가 끝난 경우에만 enqueue한다.
10. clear/fail/retry/editor exit/scene unload에서 producer lifecycle을 정확히 닫는다.
11. mod unload와 게임 종료 시 callback이 disposed queue를 건드리지 않게 한다.
12. auto submission 내부 상태는 일반 플레이 HUD에 표시하지 않는다.

## 메모리와 스레드 규칙

- callback에서 `Task.Run`을 record마다 호출하지 않음
- record마다 closure 생성 금지
- 매 입력마다 CSV 문자열을 만들지 않음
- growing `MemoryStream` 전체 복사 금지
- SQLite BLOB 재조회 금지
- monitor/lock을 main thread에서 기다리지 않음
- queue budget은 최악의 reconnect 시간을 고려하되 고정 상한을 가짐
- pool에 반환하는 buffer는 모든 consumer가 사용을 끝낸 뒤에만 반환

## 성능 예산

- capture/fan-out main-thread p50: 0.02 ms 이하
- p99: 0.1 ms 이하
- 최악 단일 callback: 0.5 ms 미만
- 정상 플레이 중 Gen 0 allocation 증가가 측정 가능한 수준으로 지속되지 않음
- background upload 비활성/활성의 frame-time p99 차이: 0.2 ms 이하
- disconnect 상태에서도 메모리가 무한 증가하지 않음

수치는 Windows와 macOS, 60/144/240 Hz, 고밀도 입력 chart에서 각각 측정한다.

## 장애 UX

정상 상황에는 아무 표시도 하지 않는다. 다음 문제도 플레이를 멈추거나 modal을 띄우지 않는다.

- 서버 접속 실패
- queue overflow
- token/session 만료
- network disconnect
- recorder-health fatal

필요하면 플레이 종료 후에만 “이번 플레이는 자동 제출할 수 없음”을 비침투적으로 알려준다. 로컬 replay 결과는 영향받지 않아야 한다.

## 테스트

- 2,000 BPM burst와 큰 chord
- 두 입력 장치와 동일 key overlap
- countdown/첫 frame/retry
- clear 직전 마지막 입력
- fail과 callback 경쟁
- scene unload와 mod unload
- queue capacity 경계
- upload consumer를 의도적으로 멈춘 상태
- 6시간 synthetic run의 bounded memory
- auto submission off/on 비교 profiler capture

## 완료 기준

- upload sender가 완전히 멈춰도 게임 main thread와 로컬 replay 저장이 계속된다.
- main-thread 성능 예산이 지원 OS별 benchmark에서 통과한다.
- record 손실, overflow와 recorder failure가 조용히 valid run으로 처리되지 않는다.
- 사용자는 정상 플레이 중 auto submission 관련 UI나 지연을 경험하지 않는다.

