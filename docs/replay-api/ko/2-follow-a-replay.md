# 2. 리플레이 따라가기

판을 리플레이하는 동안 모드는 실제 게임 대신 리플레이를 보여 줘야 합니다. 기록된 키를, 리플레이의 시간에 맞춰서요. 프로바이더는 이 메서드들로 둘 다 받습니다.

```csharp
internal sealed class ReplayFollower
{
    public void OnReplayBegin(bool render) => KeyViewer.Instance.BeginReplay();     // 실제 입력을 멈추고 카운트를 복사
    public void OnReplayClock(long ticks) => KeyViewer.Instance.ReplayTicks = ticks;
    public void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks) =>
        KeyViewer.Instance.ApplyReplayKey(key, nativeKey, down, ticks);
    public void OnReplayEnd() => KeyViewer.Instance.EndReplay();                  // 다시 실제 입력으로
}
```

| 메서드 | 호출 시점 |
| --- | --- |
| `void OnReplayBegin(bool render)` 또는 `void OnReplayBegin()` | 레벨이 시작하기 전. 영상으로 렌더할 때 `render`가 true입니다. |
| `void OnReplayClock(long ticks)` | 매 프레임 |
| `void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)` | 기록된 키마다, 그 키의 시간에 |
| `void OnReplayEnd()` | 리플레이가 끝나거나, 실패하거나, 다른 리플레이로 바뀔 때 |

## 리플레이 중에는

- 리플레이의 키는 `OnReplayKey`에서 받고 실제 입력은 무시합니다. 지금은 게임 안 리플레이에서 TUFReplay가 OS를 통해 키를 누르기도 해서, 실제 입력도 듣는 모드는 키를 두 번 셉니다.
- 시간은 스톱워치가 아니라 `OnReplayClock`이나 `ReplayApi.ReplayTicks`에서 가져옵니다. 렌더 중에는 게임이 영상보다 느리게 돌아서 실제 시간이 맞지 않습니다.
- 리플레이 중에 진행 상황을 저장하거나, 시도 횟수를 세거나, 누적 카운터를 늘리는 건 대개 잘못입니다. `ReplayApi.IsReplaying`을 확인하세요.

```csharp
public static bool IsReplaying { get; }     // OnReplayBegin과 OnReplayEnd 사이
public static bool IsRendering { get; }     // 리플레이를 영상으로 렌더하는 중
public static string ReplayRunId { get; }   // 리플레이 중인 판, 없으면 null
public static long ReplayTicks { get; }     // 리플레이 시계, 100 ns 단위
```

`IsReplaying`은 `OnReplayBegin` 직전부터 `OnReplayEnd` 직전까지 true라서, `OnReplayEnd` 안에서는 이미 false입니다. 리플레이나 렌더는 한 번에 하나만 돌아갑니다.

## `ticks`

- 100 ns 단위(`TimeSpan.TicksPerSecond`)이고, 리플레이가 시작될 때 0입니다. 리플레이 중에 뒤로 가지 않습니다.
- 게임 안에서는 리플레이가 시작된 뒤 흐른 실제 시간이고, 렌더 중에는 영상의 시간입니다.
- 키의 `ticks`는 그 키를 누른 시간이라 현재 시계보다 조금 이전일 수 있습니다.

## `nativeKey`

- Windows에서는 키의 가상 키 코드입니다. SkyHook이 Windows에서 보고하는 코드와 같아서, 네이티브 코드로 키를 지정하는 모드도 그대로 맞습니다.
- macOS에서는 null입니다. 새 게임 버전의 SkyHook은 USB HID usage를 보고하지만, 기록은 macOS 키 코드를 그대로 쓰기 때문입니다. 지금은 렌더 중에도 null입니다. 그럴 때는 `key`로 맞추세요.

## 게임 안 리플레이 순서

- `OnReplayBegin(render: false)`는 레벨이 시작하기 전, 얼불춤의 `scnGame.Play` 전에 불립니다. 그래서 시도 횟수 카운터가 이번 플레이가 리플레이인지 미리 알 수 있습니다.
- `OnReplayClock`은 매 프레임 불립니다. `OnReplayKey`는 게임 창에 포커스가 있든 없든 기록된 키를 그 시간에 하나씩 전달합니다.
- 타임라인을 탐색하면 눌린 키를 떼고 새 시간부터 이어 갑니다. 그 사이의 키는 보내지 않습니다.
- `OnReplayEnd`는 리플레이가 끝나거나, 실패하거나, 다른 리플레이로 바뀔 때 불립니다. 눌린 키를 먼저 뗍니다.

렌더도 같은 호출을 영상 시간으로 합니다. 렌더에서 더해지는 건 [5단계](5-render.md)에서 다룹니다.

## 호출과 실패

- 모든 메서드는 유니티 메인 스레드에서 불립니다. 매 프레임 호출은 TUFReplay 쪽에서 메모리를 할당하지 않습니다.
- 메서드에서 예외가 나면 로그에 남기고 리플레이나 렌더는 계속됩니다. 매 프레임 호출은 메서드마다 최대 세 번까지만 남깁니다.

## ReplayApi를 쓰지 않는 모드

등록하지 않은 모드는 TUFReplay에서 아무것도 받지 않습니다.
- 렌더 중에는 리플레이의 키를 보지 못합니다. TUFReplay는 SkyHook이나 Unity Input에 키를 넣지 않습니다.
- 게임 안 리플레이에서는 지금은 TUFReplay가 OS를 통해 누르는 키를 봅니다. TUFReplay가 OS로 키를 누르지 않게 되면 이것도 멈추니, `ReplayApi`를 쓰세요.

다음: [3. 판마다 값 저장하기](3-run-values.md)
