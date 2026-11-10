# 레퍼런스

## TUFReplay 쪽 API

```csharp
namespace TUFReplay
{
    public static class ReplayApi
    {
        public const int ApiVersion = 2;
        public const int MaxStateBytes = 65536;     // 저장되는 값 하나의 최대 크기, UTF-8 JSON

        public static bool IsReplaying { get; }     // OnReplayBegin과 OnReplayEnd 사이
        public static bool IsRendering { get; }     // 리플레이를 영상으로 렌더하는 중
        public static string ReplayRunId { get; }   // 리플레이 중인 판, 없으면 null
        public static long ReplayTicks { get; }     // 리플레이 시계, 100 ns 단위
        public static int RenderWidth { get; }      // 렌더 중에는 영상 크기, 아니면 0
        public static int RenderHeight { get; }
        public static int RenderFps { get; }
        public static bool RenderTakesOverlayVideos { get; } // 렌더러가 모드의 영상을 겹쳐 줌
        public static string RenderFfmpegPath { get; }       // 렌더러와 같이 쓰는 FFmpeg, 없으면 null

        public static bool Register(string id, object provider);
        public static void Unregister(string id);
        public static bool IsRegistered(string id);

        public static bool RecordRunEvent(string id, object value);  // 판의 지금 이 순간의 값
    }
}
```

## 프로바이더 계약

TUFReplay는 아래 public 인스턴스 메서드를 이름으로 찾습니다. 모두 선택이지만, 프로바이더에 하나 이상은 있어야 합니다. `TState`는 그 id의 상태 타입입니다.

| 메서드 | 게임 안 리플레이 | 렌더 | 단계 |
| --- | --- | --- | --- |
| `TState CaptureRunState()` 또는 `Task<TState> CaptureRunState()` | 판의 기록이 시작될 때: 판의 시작 값 | | [3](3-run-values.md) |
| `TState UpgradeRunState(TOldState old)` | 예전 상태 타입으로 저장된 값을 읽을 때. 몇 개든 가능 | | [4](4-state-types.md) |
| `void OnReplayBegin(bool render)` 또는 `()` | 레벨이 시작하기 전 | 렌더러가 레벨을 열기 전 | [2](2-follow-a-replay.md) |
| `void OnReplayEvent(TState value, long ticks)` | `OnReplayBegin` 직후 시작 값, 그다음 기록된 값마다 그 시간에, 탐색 뒤 | 같음, 영상 시간으로 | [3](3-run-values.md) |
| `void OnReplayClock(long ticks)` | 매 프레임, 실제 시간 | 매 프레임, 영상 시간 | [2](2-follow-a-replay.md) |
| `void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)` | 기록된 키마다 | 기록된 키마다 | [2](2-follow-a-replay.md) |
| `void OnReplayEnd()` | 리플레이가 끝나거나, 실패하거나, 바뀔 때 | 렌더가 완료되거나, 실패하거나, 취소될 때 | [2](2-follow-a-replay.md) |
| `IEnumerable<Canvas> GetReplayCanvases()` 또는 `Canvas GetReplayCanvases()` | 불리지 않음 | 영상이 시작될 때 한 번: 영상에 넣을 캔버스 | [5](5-render.md) |
| `void OnBeforeFrameCapture()` / `void OnAfterFrameCapture()` | 불리지 않음 | 새 영상 프레임마다 앞뒤로 | [5](5-render.md) |
| `void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)` | 불리지 않음 | 렌더가 완료되면 마지막 프레임 뒤에 한 번: 완성된 영상 위에 겹칠 영상 | [5](5-render.md) |

## 저장 방식

값은 TUFReplay의 활동 데이터베이스에 저장됩니다.

**테이블**
- `run_states`: 판과 id마다 한 줄씩 시작 값을 담습니다. shape id, JSON 값, 저장 시각.
- `run_state_events`: 판 중에 기록한 값마다 한 줄씩 담습니다. 판, id, 순서, 판 타임라인 시간, shape id, JSON 값.
- `run_state_shapes`: 서로 다른 형태(shape)를 한 번씩만 담습니다. 키는 정규화한 JSON의 SHA-256입니다.

**저장과 삭제**
- 판의 시작 값과 기록 값은 판과 같은 트랜잭션에서 저장됩니다.
- 외래 키가 연쇄 삭제되므로 판을 지우면 함께 지워집니다.
- 잠긴 판은 값도 유지합니다.

**예전 TUFReplay와의 호환**
- 테이블은 그 자리에서 추가되고 데이터베이스 버전은 1 그대로입니다. 예전 TUFReplay도 파일을 열고 시작 값을 읽습니다.
- 외래 키가 `run_states`와 `run_state_events`에 있어서 예전 TUFReplay에서 판을 지워도 함께 지워집니다.

## ApiVersion 2 변경 사항

- `OnReplayBegin`이 더 이상 상태를 받지 않습니다. `void OnReplayBegin(bool render)`나 `void OnReplayBegin()`입니다. `OnReplayBegin`이 상태를 받는 프로바이더는 등록되지 않습니다. 시작 값은 그 직후에 불리는 `OnReplayEvent(TState value, long ticks)`에서 받으세요.
- `RecordRunEvent`로 판 중에 값을 기록하고, `OnReplayEvent`가 그 값을 각자의 시간에 돌려줍니다.
- `CaptureRunState`, `UpgradeRunState`와 나머지 메서드는 그대로입니다. `OnReplayEvent`가 필요한 모드는 `ApiVersion` 2를 요구합니다.

## 로그 메시지

모든 메시지는 `[ReplayApi]`로 시작하고 id를 밝힙니다. 메시지는 영어로 남습니다.

```text
[ReplayApi] Registered JipperResourcePack (replay begin/end, keys, clock, render canvases, frame capture).
[ReplayApi] MyMod was not registered: Provider.OnReplayClock must be void OnReplayClock(long ticks).
[ReplayApi] Element.Attempts was not registered: AttemptState.Extra is object, which can't be stored. TUFReplay needs the exact type to read a run back. Use a concrete class or struct.
[ReplayApi] Element.Attempts: 12 saved runs can't be read: AttemptState.Speed was string and is now double. Add UpgradeRunState(the old type) to AttemptStore, or those runs' values reach OnReplayEvent as null or are skipped.
[ReplayApi] MyMod was not registered: Provider.OnReplayBegin must be void OnReplayBegin() or void OnReplayBegin(bool render). The run's state no longer comes with it: take it in void OnReplayEvent(AttemptState value, long ticks), which runs right after it.
[ReplayApi] MyKeyViewer.Layout: RecordRunEvent got AttemptState, but the state type is LayoutSettings.
[ReplayApi] MyKeyViewer.Layout: this run already has 1000 recorded values, the most one run keeps (1000 values or 1024 KiB). Later values are not saved. Record a value when it changes, not every frame.
[ReplayApi] Element.KeyCounts: CaptureRunState finished after the run ended, so its value was not saved for that run.
[ReplayApi] Element.Attempts: AttemptStore.CaptureRunState took 3.2 ms on the main thread, right as a run starts. Return a Task for slow work so ADOFAI does not stutter.
[ReplayApi] Element.Render: overlay video /tmp/render/hand.mov was left out: the file does not exist.
```
