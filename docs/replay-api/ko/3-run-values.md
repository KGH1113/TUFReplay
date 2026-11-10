# 3. 판마다 값 저장하기

TUFReplay는 모드의 값을 기록된 판마다 저장하고, 그 판을 리플레이할 때 돌려줍니다. 시도 횟수 카운터는 그 판을 플레이했을 때의 시도 횟수를 보여 줍니다. 키뷰어는 플레이어가 판 중간에 레이아웃을 바꿨더라도 시점마다 그때의 레이아웃을 보여 줍니다. 심박수 오버레이는 그 판에서 잰 심박수를 보여 줍니다.

키와 판정은 따로 저장할 필요가 없습니다. 리플레이가 다시 재생합니다. 그 밖의 이유로 바뀌는 것을 저장하세요.

## 값마다 id 하나

id를 값이 저장되는 이름이라고 생각하면 됩니다. id마다 클래스 하나(상태 타입)가 있고, 한 번에 값 하나를 가집니다. 판이 시작할 때의 값이 먼저 있고, 판 중에 기록한 새 값이 차례로 이어집니다.

| id | 상태 타입 | 저장 시점 |
| --- | --- | --- |
| `MyOverlay.Attempts` | `AttemptState` | 판이 시작할 때 |
| `MyOverlay.Layout` | `LayoutSettings` | 판이 시작할 때, 그리고 플레이어가 바꿀 때마다 |
| `MyOverlay.HeartRate` | `HeartRate` | 판 중에 잴 때마다 |

종류가 다른 값은 다른 id에 저장해서 각자 자기 타입을 가집니다. 1단계의 어댑터에서 id마다 작은 프로바이더를 하나씩 등록하고, `Shutdown`에서 각각 등록을 해제합니다.

```csharp
public static void TryRegister()
{
    if (_registered || !Resolve()) return;
    _registered = _register("MyOverlay", new ReplayFollower());   // 2단계
    _register("MyOverlay.Attempts", new AttemptStore());
    _register("MyOverlay.Layout", new LayoutStore());
    _register("MyOverlay.HeartRate", new HeartRateStore());
}
```

## 판이 시작할 때의 값

`TState CaptureRunState()`는 판이 시작할 때 저장할 값을 반환합니다.

```csharp
internal sealed class AttemptState
{
    public int Attempt;
    public int FullAttempt;
}

internal sealed class AttemptStore
{
    public AttemptState CaptureRunState() =>
        new AttemptState { Attempt = Attempts.Current, FullAttempt = Attempts.Full };

    public void OnReplayEvent(AttemptState value, long ticks) => AttemptText.ShowRecorded(value); // null이면 "-"
    public void OnReplayEnd() => AttemptText.ShowLive();
}
```

**언제 불리나**
- 판의 입력 기록이 시작될 때, 즉 카운트다운이나 체크포인트에서 첫 입력이 기록되기 전에 불립니다.
- 그때 활동 데이터베이스가 바빴다면 대신 플레이어가 조작을 시작할 때 불립니다.
- 그 판에 시작 값을 저장하지 않으려면 null을 반환합니다.

**빠르게**
- 판이 시작하는 순간 메인 스레드에서 불립니다. 1ms 안에 끝내세요. 더 느리면 로그에 한 번 남깁니다.
- 느리거나 다른 프로세스를 거치는 작업은 `Task<TState>`를 반환합니다. 판이 저장되기 전(플레이어가 클리어하거나, 죽거나, 레벨을 나갈 때)에 끝나면 저장되고, 그보다 늦으면 로그를 남기고 버립니다.

값은 TUFReplay가 받는 즉시 JSON으로 저장됩니다. 그 뒤에 객체를 바꿔도 저장된 값은 바뀌지 않습니다.

## 판 중에 바뀌는 값

값이 바뀔 때 `ReplayApi.RecordRunEvent(id, value)`를 호출합니다.

```csharp
internal sealed class LayoutSettings
{
    public float Size;
    public List<KeyCode> Keys = new List<KeyCode>();
}

internal sealed class LayoutStore
{
    public LayoutSettings CaptureRunState() => Layout.Snapshot();

    public void OnReplayEvent(LayoutSettings value, long ticks) => Layout.ShowRecorded(value); // null이면 지금 레이아웃
    public void OnReplayEnd() => Layout.ShowLive();
}

// 플레이어가 설정 창을 닫을 때:
TUFReplayAPI.Record("MyOverlay.Layout", Layout.Snapshot());
```

판이 시작할 때 저장할 게 없는 값은 `CaptureRunState`를 빼도 됩니다. 그러면 상태 타입은 `OnReplayEvent`에서 정해집니다.

```csharp
internal sealed class HeartRate
{
    public int Bpm;
}

internal sealed class HeartRateStore
{
    public void OnReplayEvent(HeartRate value, long ticks) => HeartRateText.Show(value?.Bpm); // null이면 아직 잰 값 없음
    public void OnReplayEnd() => HeartRateText.ShowLive();
}

// 센서가 값을 보낼 때:
TUFReplayAPI.Record("MyOverlay.HeartRate", new HeartRate { Bpm = bpm });
```

**저장되는 것**
- `value`는 정확히 그 id의 상태 타입이어야 합니다.
- TUFReplay는 이 순간의 판 타임라인 시간을 붙이고 바로 JSON으로 저장합니다. 판의 키, 판정과 같은 시계입니다. 그 뒤에 객체를 바꿔도 저장된 값은 바뀌지 않습니다.
- 같은 순간에 기록한 값은 마지막 것만 남습니다. 한 프레임에 여러 번 기록했거나, 게임이 일시정지된 동안 기록한 경우입니다. 일시정지 중에는 곡이 멈춰 있기 때문입니다.
- 한 판에 id마다 값 1,000개, 1 MiB까지 저장합니다. 매 프레임이 아니라 값이 바뀔 때 기록하세요.

**언제 기록되나**
- `CaptureRunState`가 불리는 시점부터 판이 저장될 때까지입니다. 클리어한 뒤까지, 죽은 뒤에는 플레이어가 다시 시작하거나 에디터로 돌아갈 때까지라서 실패 화면에서 기록한 값도 들어갑니다.
- 유니티 메인 스레드에서 호출합니다. 기록 중인 판이 없으면 false를 반환하는데, 이건 정상입니다(레벨 밖, 보정 판, 리플레이, 렌더). 타입이 틀린 값 같은 다른 실패도 false를 반환하고 로그에 한 번 남깁니다.

**하나하나가 다 중요한 값**
- id는 한 번에 값 하나만 가집니다. 그래서 채팅 메시지를 하나씩 기록하면 같은 순간에 온 메시지는 마지막 것만 남고, 탐색하면 마지막 하나만 돌아옵니다.
- 대신 모드가 화면에 보여 주는 것을 기록하세요. 예: 최근 메시지 10개.

## 돌려받기

`void OnReplayEvent(TState value, long ticks)`가 리플레이 중에 값을 돌려줍니다.

- 리플레이가 시작될 때, 프로바이더에 `OnReplayBegin`이 있으면 그 직후에, `ticks` 0으로 판의 시작 값이나 null을 받습니다. 그 판에 이 id로 기록한 값이 없으면 이 호출 하나뿐입니다.
- 그다음 `RecordRunEvent`로 기록한 값을 기록된 시간에, 그 프레임의 키보다 먼저 하나씩 받습니다. 키처럼 `ticks`가 현재 시계보다 조금 이전일 수 있습니다.
- 타임라인을 탐색하면, 새 시간에 유효한 값이 마지막으로 받은 값과 다를 때 그 값을 받습니다. 그 시간 전에 마지막으로 기록한 값이고, 없으면 시작 값입니다.
- 호출마다 새 객체를 넘기므로 그대로 보관해도 됩니다.

**null을 받는 경우**
- 모드가 이 id로 값을 저장하기 전에 기록된 판.
- 모드가 그 판에 아무것도 저장하지 않은 경우.
- 지금은 읽을 수 없는 타입으로 저장된 판. [4단계](4-state-types.md#상태-타입-바꾸기)를 보세요.

읽을 수 없는 기록 값은 건너뛰고 로그에 한 번 남깁니다.

**id가 여러 개일 때**
- 프로바이더는 등록한 순서대로 하나씩 시작합니다. 각 프로바이더가 `OnReplayBegin`과 시작 값을 받은 뒤에 다음 프로바이더가 시작합니다.
- 어떤 프로바이더가 시작할 때 다른 프로바이더의 값이 필요하면, 그 프로바이더를 먼저 등록하세요.

## 어떤 판이 기록되나

- 시도 하나가 판 하나입니다.
- 보정 판, 리플레이, 오프라인 렌더는 기록하지 않습니다.
- TUFReplay가 저장하지 않는 판(앞으로 진행하지 않은 판, 네이티브 입력이 없는 판)은 값도 함께 버립니다.

다음: [4. 상태 타입](4-state-types.md)
