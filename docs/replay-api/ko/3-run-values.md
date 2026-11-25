# 3. 판마다 값 저장하기

TUFReplay는 모드의 값을 기록된 판마다 저장하고, 그 판을 리플레이할 때 돌려줍니다. 시도 횟수 카운터는 그 판을 플레이했을 때의 시도 횟수를 보여 줍니다. 키뷰어는 플레이어가 판 중간에 레이아웃을 바꿨더라도 시점마다 그때의 레이아웃을 보여 줍니다. 채팅 오버레이는 그 판 중에 메시지가 도착한 그대로 보여 줍니다.

키와 판정은 따로 저장할 필요가 없습니다. 리플레이가 다시 재생합니다. 그 밖의 이유로 바뀌는 것을 저장하세요.

## 시작 값과 이벤트

id마다 기록된 판 안에 자기 자리가 하나씩 있습니다. 거기에 두 가지를 담습니다.

- **시작 값**: 판이 시작할 때 모드가 보여 주는 것을 담은 객체 하나. 그 클래스가 상태 타입입니다.
- **이벤트**: 판 중에 일어난 일을, 일어난 순간과 함께 담습니다. 이벤트 종류마다 클래스가 따로 있습니다. 예: `LayoutChanged`, `ChatReceived`.

리플레이도 같은 방식으로 돌려줍니다. 시작 값을 먼저 주고, 그다음 이벤트를 각자의 시간에 줍니다. 모드는 실제로 플레이할 때처럼 이벤트를 하나씩 화면에 반영합니다.

| | 판을 기록할 때 | 리플레이할 때 |
| --- | --- | --- |
| 시작 값 | `TState CaptureRunState()` | `void OnReplayState(TState state, long ticks)` |
| 이벤트 | 이벤트 저장소의 `AddEvent(e)` | `void OnReplayEvent(TEvent e, long ticks)`, 이벤트 타입마다 하나 |

```csharp
internal sealed class OverlayState
{
    public int Attempt;
    public LayoutSettings Layout;
    public List<ChatMessage> Chat = new List<ChatMessage>();
}

internal sealed class LayoutChanged { public LayoutSettings Layout; }
internal sealed class ChatReceived { public ChatMessage Message; }
internal sealed class HeartRateChanged { public int Bpm; }

internal sealed class ReplayFollower
{
    // … 2단계의 메서드 …

    public OverlayState CaptureRunState() => new OverlayState
    {
        Attempt = Attempts.Current,
        Layout = Layout.Snapshot(),
        Chat = Chat.Recent(50),
    };

    public void OnReplayState(OverlayState state, long ticks) => Overlay.Show(state); // null이면 저장된 값 없음
    public void OnReplayEvent(LayoutChanged e, long ticks) => Overlay.SetLayout(e.Layout);
    public void OnReplayEvent(ChatReceived e, long ticks) => Overlay.AddChat(e.Message);
    public void OnReplayEvent(HeartRateChanged e, long ticks) => Overlay.SetHeartRate(e.Bpm);
}

// 플레이 중에, 1단계의 어댑터로:
if (TUFReplayAPI.IsRecording())   // 플레이어가 설정 창을 닫았을 때. 기록 중이 아니면 복사하지 않음
    TUFReplayAPI.AddEvent(new LayoutChanged { Layout = Layout.Snapshot() });
TUFReplayAPI.AddEvent(new ChatReceived { Message = message });   // 채팅 메시지가 왔을 때
```

## 시작 값

`TState CaptureRunState()`는 판이 시작할 때 저장할 값을 반환합니다.

**언제 불리나**
- 판의 입력 기록이 시작될 때, 즉 카운트다운이나 체크포인트에서 첫 입력이 기록되기 전에 불립니다.
- 그때 활동 데이터베이스가 바빴다면 대신 플레이어가 조작을 시작할 때 불립니다.
- 그 판에 시작 값을 저장하지 않으려면 null을 반환합니다.
- 판이 시작할 때 저장할 게 없는 프로바이더는 이 메서드를 뺍니다. 그러면 상태 타입은 `OnReplayState`에서 정해지고, `OnReplayState`는 null을 받습니다.

**빠르게**
- 판이 시작하는 순간 메인 스레드에서 불립니다. 1ms 안에 끝내세요. 더 느리면 로그에 한 번 남깁니다.
- 느리거나 다른 프로세스를 거치는 작업은 `Task<TState>`를 반환합니다. 판이 저장되기 전(플레이어가 클리어하거나, 죽거나, 레벨을 나갈 때)에 끝나면 저장되고, 그보다 늦으면 로그를 남기고 버립니다.

값은 TUFReplay가 받는 즉시 JSON으로 저장됩니다. 그 뒤에 객체를 바꿔도 저장된 값은 바뀌지 않습니다.

## 이벤트

`ReplayApi.GetEventStore(id)`는 그 id의 이벤트 저장소를 반환합니다. 이벤트 저장소의 `AddEvent(object e)`는 지금 기록 중인 판에 이벤트를 추가합니다. 1단계의 어댑터가 한 번 바인딩해 둡니다.

**이벤트 타입**
- 이벤트 타입은 클래스이고, 프로바이더에 그 타입을 받는 `OnReplayEvent(TEvent e, long ticks)` 오버로드가 있습니다. TUFReplay는 이 오버로드로 그 id가 받는 타입을 압니다.
- `AddEvent`는 정확히 그 타입만 받습니다. 다른 타입이면 false를 반환하고 로그에 남깁니다.
- 각 타입은 상태 타입과 같은 규칙을 따릅니다. [4단계](4-state-types.md)를 보세요.

**저장되는 것**
- TUFReplay는 이벤트에 이 순간의 판 타임라인 시간을 붙이고 바로 JSON으로 저장합니다. 판의 키, 판정과 같은 시계입니다. 그 뒤에 객체를 바꿔도 저장된 값은 바뀌지 않습니다.
- 이벤트는 모두 남습니다. 같은 순간에 여러 개를 추가해도 그렇습니다. 예: 게임이 일시정지된 동안 온 채팅 메시지.
- 한 판에 id마다 이벤트 1,000개, 1 MiB까지 저장합니다. 매 프레임이 아니라 무언가 바뀔 때 이벤트를 추가하세요.

**언제 기록되나**
- `CaptureRunState`가 불리는 시점부터 판이 저장될 때까지입니다. 클리어한 뒤까지, 죽은 뒤에는 플레이어가 다시 시작하거나 에디터로 돌아갈 때까지라서 실패 화면에서 추가한 이벤트도 들어갑니다.
- 유니티 메인 스레드에서 호출합니다. 기록 중인 판이 없으면 false를 반환하는데, 이건 정상입니다(레벨 밖, 보정 판, 리플레이, 렌더). 프로바이더가 받지 않는 타입의 이벤트 같은 다른 실패도 false를 반환하고 로그에 한 번 남깁니다.
- `ReplayApi.IsRecording`은 이 구간 동안만 true입니다. 레이아웃 복사본처럼 만드는 데 일이 드는 이벤트는 만들기 전에 확인하세요.

## 돌려받기

- 리플레이가 시작될 때, 프로바이더에 `OnReplayBegin`이 있으면 그 직후에, `OnReplayState`가 `ticks` 0으로 판의 시작 값이나 null을 받습니다.
- 그다음 이벤트마다 그 타입의 `OnReplayEvent` 오버로드가 받습니다. 이벤트를 추가한 시간에, 그 프레임의 키보다 먼저 옵니다. 키처럼 `ticks`가 현재 시계보다 조금 이전일 수 있습니다.
- 호출마다 새 객체를 넘기므로 그대로 보관해도 됩니다.

**타임라인을 탐색한 뒤**
- 리플레이가 판의 처음부터 다시 시작합니다. `OnReplayState`가 시작 값을 다시 받고, 이어서 새 시간까지의 이벤트가 모두 순서대로 한꺼번에 옵니다.
- 이 이벤트들은 모두 그 `OnReplayState`와 같은 `ticks`를 받습니다. 받은 것으로 화면을 다시 만들고, 이벤트의 `ticks`가 마지막 `OnReplayState`의 것과 같으면 새 메시지 알림음 같은 일회성 효과는 건너뛰세요.
- 그래서 `OnReplayEvent`가 있는 프로바이더에는 `OnReplayState`가 필요합니다. 모드는 이 호출로 처음부터 다시 시작해야 한다는 걸 압니다.

**`OnReplayState`가 null을 받는 경우**
- 모드가 이 id로 시작 값을 저장하기 전에 기록된 판.
- 모드가 그 판에 아무것도 저장하지 않은 경우.
- 지금은 읽을 수 없는 타입으로 저장된 판. [4단계](4-state-types.md#타입-바꾸기)를 보세요.

읽을 수 없는 이벤트는 건너뛰고 로그에 한 번 남깁니다. 프로바이더가 더 이상 받지 않는 타입의 이벤트도 마찬가지입니다.

**id가 여러 개일 때**
- 대부분의 모드는 id 하나면 충분합니다. id 하나에 상태 타입 하나와 이벤트 타입을 몇 개든 둘 수 있습니다.
- 프로바이더는 등록한 순서대로 하나씩 시작합니다. 각 프로바이더가 `OnReplayBegin`과 시작 값을 받은 뒤에 다음 프로바이더가 시작합니다.
- 어떤 프로바이더가 시작할 때 다른 프로바이더의 값이 필요하면, 그 프로바이더를 먼저 등록하세요.

## 어떤 판이 기록되나

- 시도 하나가 판 하나입니다.
- 보정 판, 리플레이, 오프라인 렌더는 기록하지 않습니다.
- TUFReplay가 저장하지 않는 판(앞으로 진행하지 않은 판, 네이티브 입력이 없는 판)은 값도 함께 버립니다.

다음: [4. 상태 타입과 이벤트 타입](4-state-types.md)
