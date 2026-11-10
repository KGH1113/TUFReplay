# 다른 API

이 API들도 TUFReplay.dll을 참조하지 않고 이름으로 찾습니다. 찾은 타입과 멤버는 캐시해 두세요. 어셈블리, 타입, 멤버가 없으면 "리플레이 아님", "일시정지 아님"으로 보면 됩니다.

## ReplayRuntime

`TUFReplay.ReplayRuntime.IsPlaybackActive`(static `bool`)는 TUFReplay 리플레이 전체 동안 true입니다. 준비, 레벨 로딩, 재생, 에디터로 돌아가기까지 포함합니다. 매 프레임이 아니라 필요한 시점에만 읽으세요. `ReplayRuntime.ApiVersion`은 1입니다.

## NativeInputPause

모드의 UI가 게임에서 키보드를 가져가는 동안, 예를 들어 설정 창이 열려 있는 동안 TUFReplay의 네이티브 입력을 멈춥니다.

```csharp
namespace TUFReplay
{
    public static class NativeInputPause
    {
        public const int ApiVersion = 1;
        public static bool IsPaused { get; }          // UMM 창이나 다른 요청자가 입력을 멈춘 상태
        public static void Request(string owner);     // 모드의 UI가 키보드를 가져갈 때
        public static void Release(string owner);     // 돌려줄 때, 그리고 모드가 꺼질 때
    }
}
```

- 요청이 하나라도 남아 있는 동안에는 UnityModManager 창이 열려 있을 때처럼, 기록은 네이티브 키보드와 마우스 버튼 입력을 무시하고 리플레이는 입력을 보내지 않습니다. 마지막 요청이 해제되고 0.2초 뒤에 입력이 다시 시작됩니다.
- 요청은 owner별로 관리됩니다. 같은 요청을 반복해도 괜찮고, 해제는 한 번이면 됩니다. `MyMod.Settings`처럼 바뀌지 않는 id를 쓰세요. owner가 null이거나 비어 있으면 `ArgumentException`이 납니다.
- 두 메서드 모두 어느 스레드에서나 부를 수 있습니다.
- 얼불춤 자체가 그 키를 받지 않을 때만 멈추세요. 게임이 판정하는 입력을 TUFReplay가 기록하지 않으면 리플레이가 판과 맞지 않게 됩니다.

```csharp
Type pause = AppDomain.CurrentDomain.GetAssemblies()
  .FirstOrDefault(assembly => assembly.GetName().Name == "TUFReplay")
  ?.GetType("TUFReplay.NativeInputPause");
var request = (Action<string>)Delegate.CreateDelegate(
  typeof(Action<string>),
  pause.GetMethod("Request", new[] { typeof(string) })
);
request("MyMod.Settings"); // "Release"도 같은 방식으로 찾습니다
```
