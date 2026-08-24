# Creplay v2.14 vs TUFReplay record/replay engine

최초 분석: 2026-08-22 (KST) · 첫 프레임 초기화 추가 조사: 2026-08-23 (KST)

## 결론

현재 TUFReplay가 전체 구조, 저할당 처리, 장애 복구, 초기 held-key 복원, replay 스케줄링에서는 Creplay보다 낫다. 그러나 **record 시각의 절대 정확도는 Creplay의 async 경로가 더 낫다.** 또한 손·발 키보드가 같은 virtual key를 겹쳐 누르는 경우 TUFReplay의 전역 key-state dedupe가 이벤트를 소실하므로, 지금 구현을 “두 키보드의 모든 입력을 정확히 기록한다”고 보증할 수 없다.

따라서 현재 판정은 다음과 같다.

- 게임 판정용 hit-context replay: TUFReplay 우세 또는 동급 이상.
- 키뷰어용 별도 input replay의 재생 정밀도와 성능: TUFReplay 우세.
- 키뷰어용 input의 녹화 절대 시각: Creplay 우세.
- 두 키보드에서 같은 VK가 겹치는 입력의 완전 보존: 둘 다 요구사항 미충족. TUFReplay는 dedupe 때문에 Creplay보다 더 많은 원본 정보를 버릴 수 있다.
- “32키 + 1250 BPM 이상에서 loss/duplicate/order/timing 오류 0” 보증: 아직 실기 검증이 없으므로 불가.

가장 먼저 가져올 Creplay의 아이디어는 **이벤트 시각을 실제 capture clock anchor에 연결하는 방식**이다. Creplay 코드를 그대로 복사할 필요는 없고, TUFReplay의 더 좋은 monotonic timeline 구조에 capture 시점 anchor를 추가하는 편이 맞다.

## 분석 대상과 방법

- Creplay 공개 저장소 commit: `5f7658cdbf9ecefa1e37d89ed6dc64126354a9a9`
- Creplay 릴리스: [v2.14](https://github.com/potatoonadofai/Creplay-mod/releases/tag/v2.14)
- Creplay DLL SHA-256: `3c83a7f4d668a98efeef8a0d710b930792f6ae6f2440419bacf48c5bc18076d9`
- TUFReplay 기준 commit: `c2ed8e61036c3fd0dbbc62768bcb2274a0db55b5`
- TUFReplay는 위 commit에 2026-08-22 현재 working-tree 리팩터링을 포함한 실제 파일을 분석했다.
- Creplay 저장소에는 소스가 없으므로 v2.14 DLL을 ILSpy 10.0.1로 decompile하여 확인했다.
- 코드는 수정하지 않았다. 이 문서만 추가했다.

Creplay README는 v2.13에서 async 입력을 재작성했고 v2.14에서 다시 experimental로 분류했다고 명시한다. 저프레임 환경에서 더 부드러운 키 표시가 목적이지만, 정식 녹화 전 환경별 테스트를 권고한다. [Creplay README](https://github.com/potatoonadofai/Creplay-mod)

## 두 스트림의 분리 여부

이 요구사항에서 가장 중요한 구조는 TUFReplay에 이미 있다.

- 판정 재현 스트림: `HitContexts`
- 키뷰어/OS 입력 표시 스트림: `Inputs`
- 저장 payload도 `HitContextCsv`와 `InputCsv`로 분리된다.

근거:

- [RecordedRunPayload.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Replay/Models/RecordedRunPayload.cs:30)
- [RunRecord.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Activity/Models/RunRecord.cs:33)
- [RecordingPayloadBuilder.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Sessions/RecordingPayloadBuilder.cs:19)

Creplay도 같은 개념으로 `HitContext_list`와 `key_event_list`를 별도로 보관한다. 게임 진행은 hit context를 angle/floor 기준으로 재현하고, keyviewer 표시는 별도 key event를 OS에 주입한다. 이 핵심 방향은 양쪽이 같다.

## Creplay가 더 나은 점

### 1. Async record의 sub-frame 절대 시각 보간

Creplay는 전용 Windows `WH_KEYBOARD_LL` hook thread에서 각 transition 발생 시 `Stopwatch.GetTimestamp()`를 저장한다. Unity `Update()`에서 큐를 비울 때 이벤트 시각을 이전 frame tick과 현재 frame tick 사이의 비율로 계산하고, 그 비율로 이전/현재 `songposition_minusi`를 보간한다.

즉, 60 FPS에서 프레임 사이에 키가 한 번만 들어와도 그 키를 무조건 프레임 끝에 붙이지 않고 프레임 내부 위치를 복원한다.

TUFReplay는 SkyHook의 nanosecond timestamp와 batch 내부 상대 간격을 보존하지만, `AddInputBatch`가 batch의 가장 최신 이벤트를 `CurrentTimelineTimeUsLocked()`에 anchor한다. drain 호출 시각과 최신 이벤트 발생 시각 사이의 지연을 빼지 않는다.

근거:

- [RecordingSession.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Sessions/RecordingSession.cs:188)
- 특히 newest event를 현재 timeline에 붙이는 부분: [RecordingSession.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Sessions/RecordingSession.cs:227)
- Unity state update마다 drain하는 부분: [RecordingPatches.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Patches/RecordingPatches.cs:61)

영향:

- 한 frame에 event가 하나면 그 event는 사실상 sample 시각으로 이동한다.
- 한 frame에 event가 여러 개면 event 사이 간격은 보존되지만 batch 전체가 늦게 이동한다.
- 서로 다른 frame에 걸친 interval은 frame cadence/stall 영향을 받는다.
- 1250 BPM은 beat 간격이 약 48 ms다. 60 FPS 한 frame은 약 16.67 ms이므로, 최악의 frame-end bias는 한 beat 간격의 약 35%다.
- replay pump가 microsecond 정밀도로 잘 재생해도 이미 record 단계에서 이동한 시각을 정확히 재생할 뿐이다.

이 항목은 우선순위 P0이다.

### 2. 원본 scan code와 low-level flags 보존

Creplay의 key event는 다음을 저장한다.

- virtual key
- scan code
- low-level hook flags
- down/up
- song position

키 remap을 하지 않는 경로에서는 저장한 scan code와 extended flag를 `keybd_event`에 다시 사용한다. TUFReplay는 Windows에서 VK와 `ExtendedKey`만 저장하고 replay 시 `MapVirtualKeyW`로 scan code를 재계산한다.

TUFReplay의 방식도 일반키, 좌우 modifier, keypad Enter에는 상당히 잘 대응한다. 하지만 다음 경우 Creplay의 원본 보존 방식이 더 충실할 수 있다.

- 비표준 keyboard layout
- OEM/vendor key
- VK는 같지만 scan code를 구분하는 keyviewer
- record와 replay 사이의 layout 변경

두 키보드의 device ID까지 저장하는 것은 Creplay도 하지 않으므로, 이 장점이 multi-keyboard 문제를 해결하지는 않는다.

### 3. 같은 VK의 raw duplicate event를 버리지 않음

Creplay async queue는 hook이 전달한 keydown/keyup을 그대로 저장한다. TUFReplay는 `(VK, ExtendedKey)`별 하나의 bool 상태만 두고 같은 상태의 event를 duplicate로 제거한다.

근거:

- 전역 state index 및 duplicate 제거: [RecordInputTracker.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Input/RecordInputTracker.cs:227)
- state index는 device가 아니라 key + extended bit뿐이다: [RecordInputTracker.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Input/RecordInputTracker.cs:454)

예를 들어 두 키보드에서 같은 `A`를 아래 순서로 입력하면:

1. 발 A down
2. 손 A down
3. 발 A up
4. 손 A up

TUFReplay는 2를 duplicate down으로 제거하고 4를 duplicate up으로 제거할 수 있다. 저장 결과는 `down, up` 두 개뿐이다. 어느 장치가 여전히 누르고 있는지 복원할 수 없다.

Creplay는 네 event를 보존하지만 device ID가 없고 OS replay도 device별 주입이 아니므로, 최종 key state를 완전히 재현한다고 볼 수는 없다. 그래도 원본 event를 덜 버린다는 점에서는 Creplay가 낫다.

이 항목도 두 키보드가 같은 16개 key mapping을 공유한다면 P0이다. 서로 완전히 다른 VK set을 쓰는 경우에는 직접 문제가 되지 않는다.

## TUFReplay가 더 나은 점

### 1. 녹화 시작 전에 이미 눌린 16키 복원

TUFReplay는 capture window가 활성화될 때 `RefreshPhysicalState()`로 현재 상태를 동기화하고, false→true 차이를 같은 timestamp의 down transition으로 enqueue한다.

근거:

- capture window 활성화 시 sync: [RecordInputTracker.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Input/RecordInputTracker.cs:134)
- held-state transition 생성: [RecordInputTracker.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Input/RecordInputTracker.cs:304)

따라서 countdown 시작 전에 발로 16키를 이미 누르고 있다가 필요한 순간에 떼고 다시 누르는 패턴에서 초기 held state를 replay/keyviewer에 만들 수 있다.

Creplay async hook은 hook 설치 이후의 transition만 받는다. 시작 시 물리 상태 snapshot이 없다. 첫 Unity callback에서는 오히려 queued event를 처리하지 않고 key list를 clear하는 초기화 경로도 있다. 이미 눌린 key는 release만 기록될 수 있다.

이 요구사항에서는 TUFReplay의 중요한 우위다.

### 2. capture hot path의 할당과 복구 설계

TUFReplay:

- 8192개 고정 ring buffer
- 고정 drain buffer
- key event를 struct로 유지
- duplicate, drop, max queue depth, resync, read failure 계측
- SkyHook unexpected stop 시 한 번 restart
- 실패하면 저해상도 state polling fallback
- overflow 시 현재 물리 상태로 resync

근거:

- [NativeInputTransitionRingBuffer.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Input/NativeInputTransitionRingBuffer.cs:6)
- [RecordInputTracker.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Input/RecordInputTracker.cs:188)
- [RecordInputTracker.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Input/RecordInputTracker.cs:343)

Creplay는 `ConcurrentQueue<KeyEvent>`를 frame마다 새 `List<KeyEvent>`로 drain하고, 저장 event도 class instance로 하나씩 만든다. hook 설치 성공 여부는 확인하지만 실행 중 hook 정지, overflow, drop count, resync 경로가 없다.

장시간 고밀도 입력과 ADOFAI frame 안정성에는 TUFReplay가 더 적합하다.

### 3. replay timing과 chord batch

TUFReplay는 전용 replay thread에서 `Stopwatch` deadline을 계산한다. 500 µs 밖에서는 wait하고, 가까워지면 yield하면서 deadline을 맞춘다. 동일 timestamp event는 한 group으로 묶어 Windows `SendInput` 한 번으로 전달한다.

근거:

- deadline pump: [ReplayNativeInputPump.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Replay/NativeInput/ReplayNativeInputPump.cs:178)
- 동일 timestamp grouping: [ReplayInputScheduler.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Replay/NativeInput/ReplayInputScheduler.cs:86)
- batch emission: [ReplayNativeInputPump.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Replay/NativeInput/ReplayNativeInputPump.cs:255)
- Win32 `SendInput` batch: [WindowsNativeInputEmitter.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Replay/NativeInput/Platform/WindowsNativeInputEmitter.cs:71)

Creplay는 Unity `PlayerControl_Update`/`Won_Update`에서 현재 song position까지 지난 event를 loop로 처리하고 `keybd_event`를 event마다 호출한다. 녹화 시각이 sub-frame이어도 재생은 frame-bound다.

따라서 32-key chord, 저프레임, frame stall, 키뷰어 animation smoothness에서는 TUFReplay replay가 명확히 낫다.

### 4. hit-context에 실제 resolved judgment를 추가 저장

양쪽 모두 floor, angle, overload, auto/no-fail, cached angle 등 핵심 hit context를 기록하고 floor/angle 기준으로 gameplay를 재현한다. TUFReplay는 원본 Creplay 계열 필드 외에 실제 `ResolvedHitMargin`과 `TimeUs`를 추가한다.

근거:

- [ReplayHitContext.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Replay/Playback/ReplayHitContext.cs:3)
- hit 후 실제 margin capture: [RecordingPatches.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Patches/RecordingPatches.cs:130)

이 때문에 후속 버전에서 판정 공식을 재계산하는 것보다 원래 판정을 안정적으로 표시할 수 있다. legacy payload에는 기존 계산 fallback도 있다.

### 5. post-clear input과 focus/window state 처리

TUFReplay는 `Won_Update`에서도 별도 input stream을 계속 sample하며 editor 복귀까지 기록하는 구조다. UMM window/focus 전환 때는 key를 강제로 누른 채 남기지 않도록 capture/emission state를 resync한다.

키뷰어용 입력을 판정 스트림과 분리해 끝까지 남겨야 한다는 요구에는 TUFReplay가 더 잘 맞는다.

## 두 키보드 요구사항의 정확한 판정

### 서로 다른 32개 VK를 사용하는 경우

예: 발 16키와 손 16키가 서로 다른 virtual key set.

- TUFReplay event capacity와 replay batching은 충분하다.
- 초기 발 16키 held-state도 기록 가능하다.
- 가장 큰 문제는 record absolute timestamp의 frame-end bias다.
- 실제 loss 여부는 실기 stress test가 필요하다.

### 같은 16개 VK를 두 키보드가 공유하는 경우

현재 구현은 엄격한 정확성 요구를 충족하지 않는다.

- SkyHook event에는 keyboard device ID가 없다.
- TUFReplay state/dedupe도 device별이 아니다.
- Creplay의 `WH_KEYBOARD_LL` event에도 device handle이 없다.
- Windows에 다시 주입할 때도 두 실제 keyboard identity를 재현하지 않는다.

정확한 해결에는 Windows Raw Input의 `RAWINPUTHEADER.hDevice`를 기록하고 다음 중 하나를 명시적으로 선택해야 한다.

1. keyviewer가 raw event log를 직접 소비하도록 device별 transition stream을 제공한다.
2. keyviewer용 aggregate state에 device별 press refcount를 두고, 첫 device down에서만 aggregate down, 마지막 device up에서만 aggregate up을 발생시킨다.
3. raw per-device stream과 aggregate keyviewer stream을 둘 다 저장한다.

요구사항상 3번이 가장 안전하다. 판정용 hit context, raw per-device input, keyviewer aggregate input의 3개 stream으로 나누면 원본 보존과 화면 재현을 동시에 만족시킬 수 있다.

## 권장 우선순위

### P0 — record clock anchor 수정

`newestTimestampNs → CurrentTimelineTimeUsLocked()`가 아니라 capture clock과 replay timeline 사이의 anchor pair를 유지해야 한다.

권장 방향:

- Unity sample 시점에도 같은 monotonic clock을 읽는다.
- `sampleTimelineUs - (sampleCaptureNs - eventCaptureNs) * timelineRate`로 각 event를 변환한다.
- frame 전후 song position 보간을 쓰더라도 capture timestamp가 frame bracket 안에 있는지 검증한다.
- clock discontinuity, pause, pitch change, Won 이후 unscaled timeline마다 anchor를 재설정한다.
- 결과 timestamp는 안정 정렬하되, 잘못된 anchor 때문에 이전 event와 강제로 같은 시각이 되는 횟수를 계측한다.

Creplay의 장점은 “이벤트 timestamp를 frame 안에서 사용한다”는 발상이다. TUFReplay에서는 기존 hybrid timeline과 replay pump에 맞춰 더 엄밀한 anchor 변환으로 구현하는 편이 좋다.

### P0 — device-aware raw input 또는 명시적 제한

- 손/발 두 키보드가 같은 VK를 쓸 가능성이 있으면 Raw Input device identity가 필수다.
- 구현 전까지 UI/metadata에 `device-agnostic` capture임을 표시해야 한다.
- `duplicates` 수가 0이 아니면 단순 autorepeat인지 multi-device overlap인지 현재는 구별할 수 없으므로, 완전 정확 녹화로 표시하면 안 된다.

### P1 — scan code 보존

- Windows record model에 make code/scan code와 E0/E1 정보를 추가한다.
- VK는 호환용으로 함께 저장한다.
- replay에서는 원본 scan code 우선, 안전하지 않거나 remap된 경우 VK mapping fallback을 사용한다.

### P1 — replay pump priority 실측

현재 pump thread가 `BelowNormal`이다. ADOFAI frame 보호에는 유리하지만 CPU 부하 시 keyviewer event가 늦어질 수 있다. 추측으로 priority를 올리지 말고 `MaxLatenessUs`의 p50/p95/p99/max를 실제 30/60/144/240 FPS 및 CPU 부하 조건에서 측정해야 한다.

### P1 — capture 계측을 run metadata로 영구 저장

현재 debug snapshot에는 received/duplicates/dropped/maxQueueDepth/resyncs/readFailures가 있다. 이것을 run metadata에 저장하면 해당 replay가 lossless였는지 사후 판정할 수 있다.

## 필수 실기 검증 시나리오

화면 녹화만 비교하면 안 된다. 기준 장치에서 생성한 input ground-truth log, TUFReplay 저장 stream, replay 중 별도 observer가 본 stream을 event 단위로 비교해야 한다.

### A. 초기 held-foot 16키

1. 녹화 시작 전에 발 키보드 16키를 모두 누른다.
2. countdown 진입 후 한 키씩 release/repress한다.
3. 손 키보드 16키를 동시에 입력한다.
4. 저장 stream 첫 상태가 16 held keys인지 확인한다.

### B. 같은 VK의 두 장치 overlap

각 key에 대해 `foot down → hand down → foot up → hand up`과 역순을 반복한다. device별 4 event와 aggregate state의 마지막-up 규칙을 각각 검증한다.

### C. 32-key chord

32 down을 가능한 한 동시에, 이후 32 up을 동시에 발생시킨다. 1000회 반복하고 loss, duplicate, order, replay batch 크기를 확인한다.

### D. 1250–2000 BPM burst

- 1250 BPM: 48 ms 간격
- 1500 BPM: 40 ms 간격
- 2000 BPM: 30 ms 간격

손 16키 round-robin과 발 release/repress를 섞고 10분 이상 실행한다.

### E. frame-rate와 stall

30/60/144/240 FPS에서 실행하고, 50/100/250 ms main-thread stall을 주입한다. record event 시각이 frame boundary에 몰리는지 histogram으로 확인한다.

### F. lifecycle

countdown, checkpoint, pause/resume, focus loss/regain, UMM window open/close, fail, Won, post-clear, editor return에서 stuck key와 event loss를 확인한다.

## 합격 기준 제안

- capture loss: 0
- capture duplicate/missing transition: 0 (정의된 autorepeat policy 제외)
- event order mismatch: 0
- stuck key after replay/focus/pause/stop: 0
- same-timestamp 32-key chord: 한 `SendInput` batch 또는 의미상 동일한 aggregate transition
- record timing error: p99 ≤ 1 ms, max ≤ 2 ms를 목표로 측정
- replay observer timing error: p99 ≤ 2 ms, max ≤ 5 ms를 60 FPS/정상 부하에서 목표로 측정
- frame stall 발생 시 loss 0; timing error는 stall과 분리하여 capture와 replay 각각 보고
- run마다 capture mode, dropped, duplicates, resyncs, maxQueueDepth, max replay lateness를 저장

## 추가 조사: 원작자가 경고한 첫 프레임 초기화 문제

원작자의 설명:

> Sure, but I also can't guarantee the reliability of it completely. For instance, in the first frame when this module is activated, many things haven't been fully initialized, such as ADOBase.conductor.songposition_minusi, which might cause some problems... Anyway, further testing is necessary!

이 경고는 실제 코드상 타당하다. 단순히 첫 프레임이 몇 ms 늦는 문제가 아니라, **native event clock을 잘못된 song timeline segment에 붙일 수 있는 초기화 순서 문제**다.

### ADOFAI와 Creplay의 실제 시작 순서

ADOFAI v3.x의 현재 설치된 `Assembly-CSharp.dll`을 ILSpy로 확인한 결과, retry/start 경로는 대략 다음 순서다.

1. `scrController.Start_Rewind()`가 `scrConductor.Rewind()`를 호출한다.
2. `scrConductor.Rewind()`는 `crotchetAtStart = 0`, `hasSongStarted = false`, `dspTimeSong = 0` 등을 설정한다.
3. 하지만 `_songposition_minusi` 자체는 여기서 초기화하지 않는다. 이전 run의 마지막 값이 잠시 남을 수 있다.
4. `scrController.Start_Rewind()`가 `scrConductor.StartMusic()` coroutine을 시작한다.
5. coroutine은 우선 임시 `dspTimeSong`을 설정한 뒤 약 0.1초 동안 yield하고, 이후 최종 `dspTimeSong`을 다시 설정해 audio를 schedule한다.
6. `OnMusicScheduled()`는 그 다음 frame에 호출되고 `Countdown` 또는 `Checkpoint` state로 전환한다.
7. 별도 `ToggleHasSongStarted()` coroutine이 실제 DSP 시작 시각까지 기다린 후 `hasSongStarted = true`로 만든다.
8. `scrConductor.Update()`는 `hasSongStarted && isGameWorld`일 때만 다음 식으로 `songposition_minusi`를 갱신한다.

```text
((dspTime - dspTimeSong - calibration_i) * song.pitch) - addoffset
```

Creplay의 `Start_Rewind` Harmony postfix는 원래 `Start_Rewind()`가 반환하자마자 `play_things.start_recording()`을 호출한다. 따라서 final `dspTimeSong`, scheduled state, `hasSongStarted`, 새 run의 `songposition_minusi`가 준비되기 전에 keyboard hook이 시작된다.

### Creplay 첫 callback의 동작

Creplay의 첫 `OnKeyboardFrame`은 다음과 같이 동작한다.

- `curr_frame_tick == 0`이면 현재 `Stopwatch` tick과 `ADOBase.conductor.songposition_minusi`를 첫 anchor로 저장한다.
- 그동안 들어온 `data.key_event_list`를 전부 clear한다.
- queued event를 변환하지 않고 return한다.

따라서 첫 callback 이전의 native transition은 의도적으로 소실된다. 더 큰 문제는 첫 anchor의 song position이 다음 중 하나일 수 있다는 점이다.

- 새 scene의 기본값 0
- 이전 실패/완주 run에서 남은 stale position
- 아직 final schedule이 정해지기 전의 값

`hasSongStarted == false`인 동안 conductor는 해당 값을 갱신하지 않는다. 이 구간에 들어온 event는 여러 frame 동안 같은 stale song position으로 기록될 수 있다.

실제 음악 시작 후 song position이 뒤로 점프하면 Creplay는 다음 조건으로 discontinuity를 감지한다.

```text
if (last_frame_songposition > current_songposition)
    last_frame_songposition = current_songposition
```

이 처리는 이전 run과 새 run을 가로질러 보간하는 최악의 오류는 막는다. 하지만 해당 frame의 모든 event가 동일한 current song position으로 뭉칠 수 있고, 첫 callback에서 버린 event는 복구하지 못한다. 또한 값이 잘못됐지만 앞으로 증가하는 경우에는 초기화 오류인지 정상 timeline인지 구분하지 못한다.

### Unity `Update` 실행 순서 문제

Creplay의 anchor는 새 `KeyboardListener : MonoBehaviour`의 일반 `Update()`에서 찍힌다. `scrConductor.Update()`와 둘 다 명시적 execution order가 없다.

따라서 listener가 conductor보다 먼저 실행되는 frame과 이후 실행되는 frame에서 다음 pairing이 달라질 수 있다.

- 현재 `Stopwatch` tick + 이전 frame song position
- 현재 `Stopwatch` tick + 현재 frame song position

동일한 실행 순서가 계속 유지되면 주로 고정 phase offset으로 나타나지만, scene/object lifecycle이나 다른 mod의 execution order 영향까지 고려하면 안정적인 clock anchor 지점으로 보기 어렵다.

### 현재 TUFReplay도 완전히 안전하지 않음

TUFReplay는 Creplay보다 늦은 `Countdown`/`Checkpoint` state 진입 시 capture를 시작한다. 이 시점에는 final `dspTimeSong`이 schedule된 뒤라 Creplay의 `Start_Rewind` postfix보다 안전하다.

그러나 `hasSongStarted`가 아직 false일 수 있고, 현재 `RecordingClock.CurrentSongPosition()`은 단순히 `ADOBase.conductor.songposition_minusi`를 반환한다. gameplay start 전 `AddInputBatch()`도 이 값을 anchor로 pending input의 song position을 계산한다.

근거:

- [RecordingClock.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Sessions/RecordingClock.cs:9)
- [RecordingSession.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Sessions/RecordingSession.cs:188)
- pending input의 current-song-position anchor: [RecordingSession.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Sessions/RecordingSession.cs:210)
- capture 시작 state: [RecordingPatches.cs](/Users/kgh/dev/src/tuf-replay/TUFReplay/Recording/Patches/RecordingPatches.cs:159)

따라서 현재 TUFReplay도 다음 입력이 잘못 배치될 가능성이 있다.

- countdown 시작 전에 이미 눌린 발 키 16개의 initial down
- `hasSongStarted` 전 countdown 입력
- retry 직후 이전 run의 stale song position이 남은 구간의 입력

fresh run에서는 stale 값이 0이라 문제가 덜 눈에 띌 수 있다. retry/checkpoint에서는 큰 timestamp 이동으로 나타날 수 있다.

## 권장 capture architecture

결론적으로 **SkyHook/native callback에서 Unity input polling으로 되돌아가면 안 된다.** 권장 구조는 native event capture와 Unity conductor anchor를 분리한 hybrid다.

```text
native callback
  -> raw event (sequence, callback Stopwatch tick, native timestamp,
                VK, scan code, hook flags, down/up, device identity if available)
  -> allocation-free queue

scrConductor.Update Harmony prefix/postfix
  -> capture-clock bracket + songposition anchor + conductor state

timeline mapper
  -> 같은 유효 segment의 두 anchor 사이 event만 보간
  -> 첫 유효 anchor 전 event는 삭제하지 않고 pending
  -> rewind/pause/pitch/focus/checkpoint/Won에서 segment 재설정

stored streams
  -> raw per-device input
  -> keyviewer aggregate input
  -> gameplay hit context
```

### 왜 `scrConductor.Update`를 직접 anchor 지점으로 써야 하는가

- 일반 `MonoBehaviour.Update` 사이의 미정 실행 순서를 피할 수 있다.
- conductor가 song position을 계산한 직후의 값을 읽을 수 있다.
- Harmony prefix/postfix에서 `Stopwatch` tick을 둘 다 저장하면 conductor update 실행 시간을 측정할 수 있다.
- 짧은 frame에서는 prefix tick, postfix tick 또는 둘의 midpoint 중 실제 오차가 작은 정책을 실측해 선택할 수 있다.
- update가 비정상적으로 오래 걸린 frame은 anchor quality를 낮춰 표시할 수 있다.

### 첫 anchor readiness 조건

값 하나만 검사하면 부족하다. 최소한 다음 조건을 함께 봐야 한다.

- conductor와 song이 null이 아님
- song position, `dspTime`, `dspTimeSong`, pitch가 finite
- pitch가 정상 범위이며 0이 아님
- final music schedule이 끝난 lifecycle state임
- 이전 anchor와 같은 recording generation/run id임
- song position 변화량이 capture clock 변화량 × effective pitch와 허용 오차 안에서 일치함
- rewind/checkpoint/pause/resume/pitch change 직후가 아님

`hasSongStarted`는 강한 readiness signal이지만 이것만 기다리면 실제 DSP start 전 countdown input을 시간축에 놓을 수 없다. 선택지는 두 가지다.

1. 첫 valid post-start anchor가 생길 때까지 native event를 보류하고 callback `Stopwatch` tick으로 과거 countdown event를 back-project한다.
2. `OnMusicScheduled` 이후 확정된 `dspTimeSong`과 conductor의 공식 식으로 pre-start song position을 계산한다.

1번은 게임 내부 식 의존도가 낮고, 2번은 countdown 중 즉시 timestamp를 만들 수 있다. 안정성을 우선하면 raw event를 항상 보존한 상태에서 1번을 기본으로 하고, 2번 결과를 검증용 secondary estimate로 두는 편이 안전하다.

### 보간 segment 규칙

다음 사건을 만나면 이전/다음 anchor 사이를 절대 보간하지 않아야 한다.

- `Start_Rewind`
- checkpoint scrub
- song position 역행
- pause/resume
- focus loss 또는 UMM input block
- pitch/playback speed 변경
- audio device change/desync correction
- `Won` 이후 conductor timeline에서 unscaled timeline으로 전환
- scene 또는 recording generation 변경

segment 시작 전에 들어온 event는 버리지 말고 `pending-unmapped` 상태로 둔다. 이후 충분한 anchor가 생기면 변환하고, 끝까지 변환할 수 없다면 payload에 raw event와 degraded reason을 남겨야 한다.

### scan code와 flags에 대한 주의

Creplay가 보존하는 것은 좋은 아이디어지만, `KBDLLHOOKSTRUCT.flags`를 `SendInput` flags로 그대로 재사용하면 안 된다. 두 flag enum은 의미와 bit 배치가 다르다.

- low-level hook의 extended bit는 replay의 `KEYEVENTF_EXTENDEDKEY`로 명시적으로 변환한다.
- down/up은 hook flags에 의존하지 말고 event type과 `KEYEVENTF_KEYUP`으로 변환한다.
- injected/lower-integrity-injected/Alt-context flags는 provenance metadata로 저장한다.
- scan code가 유효하면 `KEYEVENTF_SCANCODE`를 사용한다.
- VK는 호환, remap, keyviewer fallback용으로 함께 저장한다.
- E0/E1 및 Pause/PrintScreen 같은 특수 sequence는 별도 검증한다.

Creplay도 저장한 flags 전체를 replay하지 않는다. 실제 replay 경로는 `flags & 1`로 extended 여부만 가져오고, up/down flag를 별도로 조립한다. 따라서 “flags 보존으로 모든 VK 하드코딩이 사라진다”기보다는 **scan code + extended metadata로 특수키 하드코딩 범위를 크게 줄일 수 있다**가 정확하다.

## 추가 필수 테스트

기존 실기 시나리오에 다음을 추가해야 한다.

### G. fresh start와 retry 첫 1초 비교

- scene 최초 진입
- fail 직후 retry
- 완주 후 restart
- checkpoint restart

각 경우 `Start_Rewind` 전 마지막 song position, final schedule 시각, `hasSongStarted`, 첫 10개 anchor, 첫 100개 raw event와 mapped event를 기록한다. retry 결과가 fresh start와 동일한 상대 timeline을 가져야 한다.

### H. schedule 전 held/release

발 16키를 `Start_Rewind` 전에 누른 상태로 두고, final schedule 전과 실제 DSP start 전에 일부를 release/repress한다. 첫 callback에서 event가 사라지거나 0/stale timestamp에 몰리면 실패다.

### I. execution-order 역전

test mod로 conductor보다 앞/뒤에서 실행되는 `Update`를 각각 만들고 같은 input trace를 기록한다. mapper 결과가 script execution order와 무관해야 한다.

### J. timeline discontinuity

rewind, checkpoint scrub, pause, pitch 변경, audio device change를 event 두 개 사이에 발생시킨다. 두 segment를 가로지른 interpolation이 한 건도 없어야 한다.

### K. anchor 품질 계측

run metadata에 다음을 저장한다.

- first raw event tick
- first valid anchor tick/song position
- pending event count와 최대 pending duration
- invalid/stale anchor count
- discontinuity count와 reason
- conductor prefix→postfix duration p50/p95/p99/max
- mapping residual p50/p95/p99/max
- unmapped/degraded event count

## 최종 답

“Creplay의 record/replay 엔진에 우리보다 나은 점이 있는가?”에 대한 답은 **있다**이다.

가장 중요한 것은 async record timestamp의 frame-interpolation이며, 이는 현재 TUFReplay의 키뷰어 입력 시각보다 정확하다. 두 키보드가 같은 VK를 공유할 때 Creplay가 raw duplicate transition을 더 많이 보존하는 점과 scan code를 저장하는 점도 참고할 가치가 있다.

하지만 전체적으로 갈아탈 정도는 아니다. TUFReplay의 초기 held-state sync, 저할당 ring buffer, overflow/resync, 별도 microsecond replay pump, chord batch, post-clear capture, resolved judgment 저장은 Creplay보다 강하다. **Creplay의 capture-clock 아이디어와 scan/raw-event fidelity를 TUFReplay 구조에 흡수하되, 첫 anchor 전 event 보류, conductor lifecycle 검증, segment discontinuity 처리, multi-device identity를 함께 해결하는 것이 최선**이다.

원작자의 첫 프레임 우려는 실제로 존재하며 Creplay에만 국한되지 않는다. 현재 TUFReplay의 countdown pending input도 stale `songposition_minusi`를 anchor로 사용할 수 있다. 구현 시 가장 중요한 원칙은 “첫 frame을 버린다”가 아니라 **raw native event는 절대 버리지 않고, 유효한 conductor anchor가 생길 때까지 mapping만 미룬다**는 것이다.
