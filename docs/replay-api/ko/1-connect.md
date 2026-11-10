# 1. TUFReplay에 연결하기

모드는 TUFReplay.dll을 참조하지 않고 TUFReplay와 통신합니다. `TUFReplay.ReplayApi`를 이름으로 찾고, 모드 자신의 타입으로 만든 객체(프로바이더)를 등록합니다. TUFReplay는 프로바이더의 public 메서드를 이름으로 찾습니다.

## 규칙

- **TUFReplay.dll을 참조하지 않습니다.** 로드된 어셈블리 중 이름이 `TUFReplay`인 것에서 `TUFReplay.ReplayApi`를 이름으로 찾습니다.
- **타입은 모드의 것입니다.** 프로바이더와 저장하는 클래스는 모드의 어셈블리에 둡니다. TUFReplay는 프로바이더의 메서드를 한 번 바인딩하고, 클래스의 필드는 리플렉션으로 읽습니다.
- **경계를 넘는 건 BCL과 Unity 타입뿐입니다**: `string`, 숫자, `bool`, `KeyCode`, `Canvas`, `RectInt`, `Action`. 메서드 이름과 id가 계약입니다.

어느 한쪽 모드가 없거나 꺼져 있어도 다른 쪽은 그대로 동작합니다.

## 어댑터

모드 안의 정적 클래스 하나가 API를 한 번 찾아서 등록합니다.

```csharp
// TUFReplay ReplayApi에 붙는 선택적 연결. TUFReplay를 컴파일 시점에 참조하지 않습니다.
internal static class TUFReplayAPI
{
    private const string Id = "MyOverlay";
    private const int RequiredApiVersion = 2;
    private static Func<string, object, bool> _register;
    private static Action<string> _unregister;
    private static Func<string, object, bool> _record;
    private static bool _registered;

    public static void TryRegister()
    {
        if (_registered || !Resolve()) return;
        _registered = _register(Id, new ReplayFollower());   // 2단계에서 만듭니다
    }

    public static void Shutdown()
    {
        _unregister?.Invoke(Id);
        _registered = false;
    }

    // 3단계에서 씁니다. 기록 중인 판이 없으면 false.
    public static bool Record(string id, object value) => _record != null && _record(id, value);

    private static bool Resolve()
    {
        if (_register != null) return true;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name != "TUFReplay") continue;
            Type api = assembly.GetType("TUFReplay.ReplayApi", false);
            if (api?.GetField("ApiVersion")?.GetValue(null) is not int version || version < RequiredApiVersion) continue;
            _register = (Func<string, object, bool>)Delegate.CreateDelegate(
                typeof(Func<string, object, bool>), api.GetMethod("Register"));
            _unregister = (Action<string>)Delegate.CreateDelegate(
                typeof(Action<string>), api.GetMethod("Unregister"));
            _record = (Func<string, object, bool>)Delegate.CreateDelegate(
                typeof(Func<string, object, bool>), api.GetMethod("RecordRunEvent"));
            return true;
        }
        return false;
    }
}
```

모드가 켜질 때 등록하고, 성공할 때까지 다른 모드가 로드를 마칠 때마다 다시 시도합니다.

```csharp
protected override void OnEnable() => TUFReplayAPI.TryRegister();
protected override void OnDisable() => TUFReplayAPI.Shutdown();

[JAPatch(typeof(UnityModManager.ModEntry), "Load", PatchType.Postfix, false)]
private static void OnModLoaded() => TUFReplayAPI.TryRegister();
```

## 등록

```csharp
public static bool Register(string id, object provider);
public static void Unregister(string id);
public static bool IsRegistered(string id);
```

- 유니티 메인 스레드에서 등록합니다.
- TUFReplay가 모드보다 늦게 로드될 수 있습니다. `TUFReplay.ReplayApi`가 없으면 다음 모드가 로드를 마쳤을 때 다시 시도합니다.
- 같은 id로 다시 등록하면 프로바이더를 바꿉니다.
- id나 프로바이더가 잘못되면 `Register`는 false를 반환하고 이유를 모드 로그에 남깁니다.
- 성공하면 프로바이더가 무엇에 참여하는지 로그에 남깁니다. 예: `Registered MyOverlay (replay begin/end, keys, clock).`
- 프로바이더 메서드는 모두 선택이지만, 하나 이상은 있어야 합니다. 다음 단계에서 추가합니다.
- 모드가 꺼질 때 `Unregister`를 호출합니다.

## id

id는 프로바이더의 이름이자, 그 id로 판마다 저장되는 값의 이름입니다(3단계).

- 바꾸지 마세요. 저장된 값을 id로 찾습니다.
- 영문자, 숫자, `.`, `_`, `-`로 64자까지 씁니다. 예: `"MyOverlay"`, `"MyOverlay.Layout"`.
- 모드 하나가 id를 여러 개 등록할 수 있습니다. 리플레이를 따라가는 id 하나와, 저장하는 값마다 id 하나씩입니다.

## 버전

`ApiVersion`은 2입니다. ApiVersion 2의 기능을 쓴다면 어댑터처럼 더 오래된 TUFReplay는 건너뜁니다. [바뀐 점](reference.md#apiversion-2-변경-사항)을 참고하세요.

다음: [2. 리플레이 따라가기](2-follow-a-replay.md)
