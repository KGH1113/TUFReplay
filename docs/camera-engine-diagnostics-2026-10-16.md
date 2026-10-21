# 카메라 FPS 비교용 엔진 계측

`Player-prev.log`의 2026-10-15 실행에서 카메라 ON 상태의 레벨 5 플레이는 약 107–122 FPS, OFF 뒤 같은 시작 타일의 플레이는 약 228–239 FPS였다. 당시 계측한 카메라 코드의 게임 스레드 비용만으로는 차이를 설명할 수 없었다. 이 변경은 원인을 조사할 로그를 추가하며 캡처, 녹화 품질, 프리뷰 해상도나 UI 동작을 바꾸지 않는다.

## 로그

기존 `[Camera/Diagnostics]`의 `render.performance`를 schema version 3으로 확장한다. 기존 `metrics` 필드는 유지한다. 약 5초마다, 상태 전환 시, 종료 시 완료된 게임 프레임을 기록한다.

- `windowEndUtc`, `windowEndFrameId`: 구간 끝 시각과 Unity 프레임 번호. 상태 전환 로그도 별도의 `utc`, `frameId`를 포함하므로 백그라운드 출력 지연과 구분할 수 있다.
- `context`: 카메라 활성화, 라이브 표시 요청, 게임 진입, 포커스, 확인 모달, 리플레이 여부. 변경 시 이전 구간을 `reason: context_changed`로 닫고 `render.context`를 출력한다.
- `capture`: 구간 끝의 캡처 준비 여부, 장치 ID, 녹화 품질 설정. 실제 녹화 시작·종료·writer 상태는 기존 캡처 로그와 native helper 로그로 대조한다.
- `engine.profilerMetrics`: 엔진이 제공하는 제한된 마커와 카운터만 수집한다. 시간 단위는 `ms`, 렌더링 호출은 `Count`, 할당량은 `Bytes`다. 각 항목의 `values`에는 `samples`, `total`, `mean`, `min`, `max`가 있다.
- `engine.frameTiming`: `enabled`, `unavailableReason`, `emptyPolls`, CPU 전체/메인 스레드/렌더 스레드/present 대기/GPU 프레임 시간. Frame Timing Stats가 게임 빌드에서 켜져 있을 때만 사용한다.
- `engine.samplingCpu`: 엔진 샘플을 읽고 집계한 게임 스레드 시간(ms). 5초마다 지원 여부를 다시 확인하는 비용은 포함하고, 초기 수집기 생성과 구간 스냅샷 생성은 포함하지 않는다.

프로파일러 수집 대상은 `Main Thread`, `Render Thread`, `Gfx.WaitForPresentOnGfxThread`, `Gfx.WaitForRenderThread`, `Gfx.WaitForCommands`, `Gfx.PresentFrame`, `WaitForTargetFPS`, `Canvas.SendWillRenderCanvases`, `Canvas.BuildBatch`, `Draw Calls Count`, `Batches Count`, `SetPass Calls Count`, `GC Allocated In Frame`이다. 특정 플랫폼·배포 빌드가 마커를 제공하지 않으면 `available: false`, `status: unavailable`과 이유를 기록한다. 마커가 유효해도 구간에 완료된 샘플이 없으면 `status: no_samples`다. 샘플이 없을 때 평균 등은 `null`이다. 실제 카운터의 0은 유효한 측정값으로 유지한다.

## 해석의 한계와 계측 비용

엔진 지표는 게임과 다른 모드의 작업을 모두 포함한다. Canvas 마커는 모든 Canvas를 합친 CPU 시간이며 GPU 실행 시간이 아니다. present·프레임 제한 대기와 메인/렌더 스레드 시간은 겹칠 수 있다. 대기 시간만으로 GPU 병목을 확정하거나 중첩 지표를 더하지 않는다. [Unity의 마커 설명](https://docs.unity3d.com/6000.3/Documentation/Manual/profiler-markers.html)을 참고한다.

엔진 샘플은 완료 후 전달되므로 현재 게임 상태보다 늦을 수 있다. 특히 GPU 시간은 ON/OFF 전환 바로 옆 구간에 이전 상태의 샘플이 포함될 수 있다. 설정 전환 뒤 안정된 여러 구간을 비교한다. 같은 `frameStartTimestamp`의 프레임 시간은 구간을 넘어 중복 집계하지 않는다. 타임스탬프가 없거나, 시간이 0 이하·NaN·무한대이면 해당 시간을 측정값으로 집계하지 않는다. 유효한 메인 스레드 시간에 수반되는 present 대기 0ms는 유지한다.

전역 Profiler나 deep profiling은 켜지 않는다. [ProfilerRecorder](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorder.html)의 기본 프레임 합산을 사용하며, 마커당 8개의 고정 native 샘플을 프레임마다 stack buffer로 비운다. 버퍼가 넘친 poll은 `overflowPolls`로 표시하며 이 경우 통계는 일부 샘플만 반영한다. 조건부 마커의 이전 `LastValue`를 매 프레임 반복해서 집계하지 않는다. 아직 생성되지 않은 마커는 상태 전환과 무관하게 약 5초마다 다시 시도하고, 예외가 난 수집기는 중단한다. 종료 시 native recorder를 해제한다. 프레임별 집계는 managed 메모리를 할당하지 않으며 JSON 스냅샷은 기존 백그라운드 진단 큐에 전달한다. native 수집 자체의 오버헤드는 실제 게임에서 별도로 확인해야 한다.

## 비교 순서

동일 레벨, 시작 타일, 속도, 해상도, FPS 제한, 포커스 상태를 유지한다.

1. 카메라 OFF로 플레이한다.
2. 카메라 ON, **Show live camera OFF**로 같은 구간을 플레이한다.
3. 카메라 ON, **Show live camera ON**으로 같은 구간을 플레이한다.

각 조건에서 설정 전환·로딩·확인 모달 직후를 제외한 여러 5초 구간을 확보한다. `metrics.gameFps`와 기존 복사/업로드 시간, 엔진 스레드·대기·Canvas·렌더 호출·할당량을 함께 비교하고 native helper의 `capture.health` 및 recording 이벤트와 시각을 맞춘다. 게임 빌드에서 GPU 계측을 제공하지 않으면 `disabled_in_player` 또는 샘플 없음으로 남으며 0ms로 해석하지 않는다.
