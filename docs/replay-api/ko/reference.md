# 레퍼런스

## TUFReplay 쪽 API

```csharp
namespace TUFReplay
{
    public static class ReplayApi
    {
        public const int ApiVersion = 2;
        public const int MaxStateBytes = 65536;     // 저장되는 시작 값이나 이벤트 하나의 최대 크기, UTF-8 JSON

        public static bool IsRecording { get; }     // 판을 기록하는 중, AddEvent가 이벤트를 받음
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

        public static ReplayEventStore GetEventStore(string id);    // 기록된 판마다 있는 그 id의 자리
    }

    public sealed class ReplayEventStore
    {
        public string Id { get; }
        public bool AddEvent(object e);                              // 판의 지금 이 순간의 이벤트
    }
}
```

## 프로바이더 계약

TUFReplay는 아래 public 인스턴스 메서드를 이름으로 찾습니다. 모두 선택이지만, 프로바이더에 하나 이상은 있어야 합니다. `TState`는 그 id의 상태 타입이고, `TEvent`는 그 id의 이벤트 타입 중 하나입니다.

| 메서드 | 게임 안 리플레이 | 렌더 | 단계 |
| --- | --- | --- | --- |
| `TState CaptureRunState()` 또는 `Task<TState> CaptureRunState()` | 판의 기록이 시작될 때: 판의 시작 값 | | [3](3-run-values.md) |
| `TState UpgradeRunState(TOldState old)` | 예전 상태 타입으로 저장된 시작 값을 읽을 때. 몇 개든 가능 | | [4](4-state-types.md) |
| `TEvent UpgradeRunEvent(TOldEvent old)` | 예전 이벤트 타입으로 저장된 이벤트를 읽을 때. 몇 개든 가능 | | [4](4-state-types.md) |
| `void OnReplayBegin(bool render)` 또는 `()` | 레벨이 시작하기 전 | 렌더러가 레벨을 열기 전 | [2](2-follow-a-replay.md) |
| `void OnReplayState(TState state, long ticks)` | `OnReplayBegin` 직후 시작 값, 그리고 탐색할 때마다 다시 | `OnReplayBegin` 직후 시작 값 | [3](3-run-values.md) |
| `void OnReplayEvent(TEvent e, long ticks)`, 이벤트 타입마다 하나 | 이벤트마다 그 시간에. 탐색 뒤에는 그 시간까지의 이벤트를 한꺼번에 | 이벤트마다 그 영상 시간에 | [3](3-run-values.md) |
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
- `run_events`: 판 중에 추가한 이벤트마다 한 줄씩 담습니다. 판, id, 순서, 판 타임라인 시간, 이벤트 타입의 네임스페이스와 이름, shape id, JSON 값.
- `run_state_shapes`: 서로 다른 형태(shape)를 한 번씩만 담습니다. 키는 정규화한 JSON의 SHA-256입니다.

**저장과 삭제**
- 판의 시작 값과 이벤트는 판과 같은 트랜잭션에서 저장됩니다.
- 외래 키가 연쇄 삭제되므로 판을 지우면 함께 지워집니다.
- 잠긴 판은 값도 유지합니다.

**예전 TUFReplay와의 호환**
- 테이블은 그 자리에서 추가되고 데이터베이스 버전은 1 그대로입니다. 예전 TUFReplay도 파일을 열고 시작 값을 읽습니다.
- 외래 키가 `run_states`와 `run_events`에 있어서 예전 TUFReplay에서 판을 지워도 함께 지워집니다.

## ApiVersion 2 변경 사항

- `OnReplayBegin`이 더 이상 상태를 받지 않습니다. `void OnReplayBegin(bool render)`나 `void OnReplayBegin()`입니다. `OnReplayBegin`이 상태를 받는 프로바이더는 등록되지 않습니다. 시작 값은 그 직후에 불리는 `OnReplayState(TState state, long ticks)`에서 받으세요.
- 이벤트: `GetEventStore(id).AddEvent(e)`로 판 중에 추가하고, `OnReplayEvent(TEvent e, long ticks)` 오버로드가 각자의 시간에 돌려주고, `UpgradeRunEvent`가 예전 이벤트 타입을 읽습니다. `IsRecording`으로 판을 기록하는 중인지 압니다.
- `CaptureRunState`, `UpgradeRunState`와 나머지 메서드는 그대로입니다. `OnReplayState`나 이벤트가 필요한 모드는 `ApiVersion` 2를 요구합니다.

## 로그 메시지

모든 메시지는 `[ReplayApi]`로 시작하고 id를 밝힙니다. 메시지는 영어로 남습니다.

```text
[ReplayApi] Registered MyOverlay (state OverlayState, replay begin/end, replay state, events LayoutChanged/ChatReceived, keys, clock).
[ReplayApi] MyMod was not registered: Provider.OnReplayClock must be void OnReplayClock(long ticks).
[ReplayApi] Element.Attempts was not registered: AttemptState.Extra is object, which can't be stored. TUFReplay needs the exact type to read a run back. Use a concrete class or struct.
[ReplayApi] MyMod was not registered: Provider.OnReplayBegin must be void OnReplayBegin() or void OnReplayBegin(bool render). The run's state no longer comes with it: take it in void OnReplayState(AttemptState state, long ticks), which runs right after it.
[ReplayApi] MyMod was not registered: Provider.OnReplayEvent(AttemptState e, long ticks) no longer gets the run's start value. Take it in void OnReplayState(AttemptState state, long ticks), which runs right after OnReplayBegin and after each seek.
[ReplayApi] MyOverlay: 12 saved runs can't be read: OverlayState.Speed was string and is now double. Add UpgradeRunState(the old type) to ReplayFollower, or those runs' start values reach OnReplayState as null.
[ReplayApi] MyOverlay: LayoutChanged events in 3 saved runs can't be read: LayoutChanged.Size was string and is now float. Add UpgradeRunEvent(the old type) to ReplayFollower, or replays skip those events.
[ReplayApi] MyOverlay: saved runs have HeartRateChanged events, but ReplayFollower has no OnReplayEvent(HeartRateChanged e, long ticks), so replays skip them.
[ReplayApi] MyOverlay: AddEvent got LayoutSettings, but ReplayFollower has no OnReplayEvent(LayoutSettings e, long ticks). It takes LayoutChanged, ChatReceived.
[ReplayApi] MyOverlay: this run already has 1000 events, the most one run keeps (1000 events or 1024 KiB). Later events are not saved. Add an event when something changes, not every frame.
[ReplayApi] Element.KeyCounts: CaptureRunState finished after the run ended, so its value was not saved for that run.
[ReplayApi] Element.Attempts: AttemptStore.CaptureRunState took 3.2 ms on the main thread, right as a run starts. Return a Task for slow work so ADOFAI does not stutter.
[ReplayApi] Element.Render: overlay video /tmp/render/hand.mov was left out: the file does not exist.
```
