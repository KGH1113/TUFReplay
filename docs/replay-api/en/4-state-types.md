# 4. State types

The class you save under an id is its state type. `CaptureRunState` returns it, `RecordRunEvent` takes it, and `OnReplayEvent` and `UpgradeRunState` use it. If a provider has both `CaptureRunState` and `OnReplayEvent`, they must use the same type.

## What a state type can hold

A state type is a class with a parameterless constructor. The constructor can be private.

**Saved members**
- Public instance fields.
- Public properties with a getter and any setter.

**Skipped members**
- Get-only properties, such as computed ones.
- Fields marked `[NonSerialized]`.
- A `readonly` field is an error. Remove `readonly`, or mark the field `[NonSerialized]`.

Member types can be:

| Kind | Types |
| --- | --- |
| Values | `bool`, `int`, `long`, `float`, `double`, `string`, an enum, `DateTime`, `byte[]` |
| Nullable values | `int?`, `Hand?` and so on. Reference types can always be null. |
| Collections | `T[]`, `List<T>`, `Dictionary<string, T>` |
| Nested | A class or struct made of the above |

**Not storable**
- `object`, interfaces and abstract classes. TUFReplay needs the exact type to read a run back.
- Other collections
- Other number types (`short`, `uint`, `decimal`, …)
- `[Flags]` enums. Store a list of the enum instead.
- Unity objects
- Types that contain themselves

**Values**
- Floats must be finite. Use a nullable number for "no value".
- Enums are saved by member name.
- A `DateTime` is saved as UTC.
- A nested object must be exactly its declared type.
- Each saved value can be up to 64 KiB (`ReplayApi.MaxStateBytes`) as UTF-8 JSON.

**Reading back**
- A list or dictionary your constructor already creates is refilled rather than replaced, so a custom key comparer is kept.

**When TUFReplay checks**
- TUFReplay checks the type when you register and reports the first member it can't store, with how to fix it.
- It checks values when they are saved.

## Changing a state type

Every run keeps the exact shape its values were saved with. When your type changes, TUFReplay reads older runs this way.

**Read automatically**
- An added member keeps the value your constructor gives it.
- A removed member is skipped.
- A number widens: `int` to `long` or `double`, `float` to `double`.
- A value becomes nullable.
- An enum gains members.

**Needs `UpgradeRunState`**
- A changed type, such as `string` to `double`, `long` to `int`, or `int?` to `int`.
- A removed enum member.
- Keep the old class with the members it had, and add `TState UpgradeRunState(TOldState old)` to the provider. TUFReplay uses the upgrade whose old type matches the saved run. A provider can have any number of them.

```csharp
// Saved by earlier versions of the mod.
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

// On the provider:
public AttemptState UpgradeRunState(AttemptStateV1 old) =>
    new AttemptState { Attempt = old.Attempt, Speed = double.Parse(old.Speed.TrimEnd('x'), CultureInfo.InvariantCulture) };
```

**Renaming a member**
- A rename looks like one removed member and one added member, so it would read without error and lose the value.
- An upgrade whose old type matches the saved run exactly is preferred over the automatic reading. Add one for the old class to keep the value.

**Checks at registration**
- When you register, TUFReplay checks every shape your id was saved with.
- It writes a log line for runs it can't read without an upgrade, and for runs that look renamed.

Next: [5. Render to video](5-render.md)
