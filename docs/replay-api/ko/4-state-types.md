# 4. 상태 타입

id에 저장하는 클래스가 그 id의 상태 타입입니다. `CaptureRunState`가 반환하고, `RecordRunEvent`가 받고, `OnReplayEvent`와 `UpgradeRunState`가 씁니다. 프로바이더에 `CaptureRunState`와 `OnReplayEvent`가 둘 다 있으면 같은 타입이어야 합니다.

## 상태 타입에 넣을 수 있는 것

상태 타입은 매개변수 없는 생성자가 있는 클래스입니다. 생성자는 private이어도 됩니다.

**저장되는 멤버**
- public 인스턴스 필드.
- getter와 setter가 있는 public 프로퍼티. setter는 private이어도 됩니다.

**건너뛰는 멤버**
- 계산 프로퍼티처럼 getter만 있는 프로퍼티.
- `[NonSerialized]`가 붙은 필드.
- `readonly` 필드는 오류입니다. `readonly`를 빼거나 `[NonSerialized]`를 붙이세요.

멤버 타입은 이렇게 쓸 수 있습니다.

| 종류 | 타입 |
| --- | --- |
| 값 | `bool`, `int`, `long`, `float`, `double`, `string`, enum, `DateTime`, `byte[]` |
| nullable 값 | `int?`, `Hand?` 등. 참조 타입은 언제나 null일 수 있습니다. |
| 컬렉션 | `T[]`, `List<T>`, `Dictionary<string, T>` |
| 중첩 | 위 타입으로 이루어진 클래스나 구조체 |

**저장할 수 없는 것**
- `object`, 인터페이스, 추상 클래스. TUFReplay가 판을 다시 읽으려면 정확한 타입이 필요합니다.
- 그 밖의 컬렉션
- 그 밖의 숫자 타입(`short`, `uint`, `decimal`, …)
- `[Flags]` enum. 대신 enum 리스트를 저장하세요.
- Unity 객체
- 자기 자신을 포함하는 타입

**값**
- float은 유한한 값이어야 합니다. "값 없음"은 nullable 숫자로 나타내세요.
- enum은 멤버 이름으로 저장됩니다.
- `DateTime`은 UTC로 저장됩니다.
- 중첩 객체는 선언된 타입과 정확히 같아야 합니다.
- 저장되는 값 하나는 UTF-8 JSON으로 64 KiB(`ReplayApi.MaxStateBytes`)까지입니다.

**다시 읽을 때**
- 생성자에서 이미 만드는 리스트나 딕셔너리는 새로 바꾸지 않고 다시 채웁니다. 그래서 직접 지정한 키 비교자가 유지됩니다.

**TUFReplay가 확인하는 시점**
- 등록할 때 타입을 확인하고, 저장할 수 없는 첫 멤버와 고치는 방법을 알려 줍니다.
- 값은 저장할 때 확인합니다.

## 상태 타입 바꾸기

판마다 값을 저장했을 때의 형태(shape)가 그대로 남습니다. 타입이 바뀌면 TUFReplay는 예전 판을 이렇게 읽습니다.

**자동으로 읽는 경우**
- 추가된 멤버는 생성자가 준 값을 유지합니다.
- 제거된 멤버는 건너뜁니다.
- 숫자가 넓어지는 경우: `int`에서 `long`이나 `double`로, `float`에서 `double`로.
- 값이 nullable이 되는 경우.
- enum에 멤버가 추가된 경우.

**`UpgradeRunState`가 필요한 경우**
- 타입이 바뀐 경우. 예: `string`에서 `double`로, `long`에서 `int`로, `int?`에서 `int`로.
- enum 멤버가 제거된 경우.
- 예전 클래스를 그때의 멤버 그대로 남겨 두고, 프로바이더에 `TState UpgradeRunState(TOldState old)`를 추가합니다. TUFReplay는 예전 타입이 저장된 판과 맞는 업그레이드를 씁니다. 프로바이더에 몇 개든 둘 수 있습니다.

```csharp
// 예전 버전의 모드가 저장한 타입.
internal sealed class AttemptStateV1
{
    public int Attempt;
    public string Speed;   // "1.5x"
}

internal sealed class AttemptState
{
    public int Attempt;
    public double Speed = 1;
}

// 프로바이더에:
public AttemptState UpgradeRunState(AttemptStateV1 old) =>
    new AttemptState { Attempt = old.Attempt, Speed = double.Parse(old.Speed.TrimEnd('x'), CultureInfo.InvariantCulture) };
```

**멤버 이름 바꾸기**
- 이름을 바꾸면 멤버 하나가 제거되고 하나가 추가된 것으로 보입니다. 그래서 오류 없이 읽히지만 값을 잃습니다.
- 예전 타입이 저장된 판과 정확히 맞는 업그레이드는 자동 읽기보다 우선합니다. 값을 지키려면 예전 클래스에 대한 업그레이드를 추가하세요.

**등록할 때 확인**
- 등록할 때 TUFReplay는 그 id로 저장된 모든 형태를 확인합니다.
- 업그레이드 없이 읽을 수 없는 판과, 이름이 바뀐 것 같은 판이 있으면 로그에 남깁니다.

다음: [5. 영상 렌더](5-render.md)
