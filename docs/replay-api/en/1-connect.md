# 1. Connect to TUFReplay

Your mod talks to TUFReplay without referencing TUFReplay.dll. It finds `TUFReplay.ReplayApi` by name and registers objects of its own types, called providers. TUFReplay finds a provider's public methods by name.

## The rules

- **No reference to TUFReplay.dll.** Find `TUFReplay.ReplayApi` by name in the loaded assembly named `TUFReplay`.
- **Your own types.** Providers and the classes you save stay in your assembly. TUFReplay binds a provider's methods once and reads your classes' fields through reflection.
- **Only BCL and Unity types cross the boundary**: `string`, numbers, `bool`, `KeyCode`, `Canvas`, `RectInt`, `Action`, and your event store, which you bind by name like the API. The method names and the ids are the contract.

Either mod keeps working when the other is missing or turned off.

## The adapter

One static class in your mod resolves the API once and registers:

```csharp
// Optional bridge to TUFReplay's ReplayApi. No compile-time reference to TUFReplay.
internal static class TUFReplayAPI
{
    private const string Id = "MyOverlay";
    private const int RequiredApiVersion = 2;
    private static Func<string, object, bool> _register;
    private static Action<string> _unregister;
    private static Func<object, bool> _addEvent;
    private static bool _registered;

    public static void TryRegister()
    {
        if (_registered || !Resolve()) return;
        _registered = _register(Id, new ReplayFollower());   // built in step 2
    }

    public static void Shutdown()
    {
        _unregister?.Invoke(Id);
        _registered = false;
    }

    // Used in step 3. False outside a recorded run.
    public static bool AddEvent(object e) => _addEvent != null && _addEvent(e);

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
            var getEventStore = (Func<string, object>)Delegate.CreateDelegate(
                typeof(Func<string, object>), api.GetMethod("GetEventStore"));
            object eventStore = getEventStore(Id);
            _addEvent = (Func<object, bool>)Delegate.CreateDelegate(
                typeof(Func<object, bool>), eventStore, eventStore.GetType().GetMethod("AddEvent"));
            return true;
        }
        return false;
    }
}
```

Register when your mod is enabled, and again whenever another mod finishes loading, until it succeeds:

```csharp
protected override void OnEnable() => TUFReplayAPI.TryRegister();
protected override void OnDisable() => TUFReplayAPI.Shutdown();

[JAPatch(typeof(UnityModManager.ModEntry), "Load", PatchType.Postfix, false)]
private static void OnModLoaded() => TUFReplayAPI.TryRegister();
```

## Registering

```csharp
public static bool Register(string id, object provider);
public static void Unregister(string id);
public static bool IsRegistered(string id);
```

- Register from Unity's main thread.
- TUFReplay may load after your mod. If `TUFReplay.ReplayApi` is missing, try again when the next mod finishes loading.
- Registering an id again replaces its provider.
- `Register` returns false when the id or the provider is invalid, and writes the reason to the mod log.
- On success the log says what the provider takes part in, such as `Registered MyOverlay (replay begin/end, keys, clock).`
- Every provider method is optional, but a provider needs at least one. The next steps add them.
- Call `Unregister` when your mod is disabled.

## Ids

An id names a provider and what it saves with each run (step 3).

- Keep it stable: saved values are found by it.
- Use letters, digits, `.`, `_` and `-`, up to 64 characters, such as `"MyOverlay"` or `"MyOverlay.Chat"`.
- One id is enough for most mods. Register more when parts of your mod come and go on their own.

## Version

`ApiVersion` is 2. Skip older TUFReplay builds, as the adapter does, when you use anything from ApiVersion 2. See [what changed](reference.md#changes-in-apiversion-2).

Next: [2. Follow a replay](2-follow-a-replay.md)
