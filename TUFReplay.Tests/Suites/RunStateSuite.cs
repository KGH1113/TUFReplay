#pragma warning disable CS0649 // State types are filled by reflection or never at all.

using System.Reflection;
using Microsoft.Data.Sqlite;
using TUFReplay;
using TUFReplay.Activity.Migrations;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;
using TUFReplay.RunState.Diagnostics;
using TUFReplay.RunState.Mapping;
using TUFReplay.RunState.Models;
using TUFReplay.RunState.Playback;
using TUFReplay.RunState.Providers;
using TUFReplay.RunState.Recording;
using TUFReplay.RunState.Repositories;
using TUFReplay.Shared.Database;
using static TestFixture;

// Mod states saved with each run (RunStateApi).
internal static class RunStateSuite
{
  private static readonly List<string> Messages = new List<string>();

  internal static void RunAll(string root)
  {
    RunStateLog.Sink = message =>
    {
      lock (Messages)
        Messages.Add(message);
    };
    try
    {
      MapsEveryStorableType();
      ExplainsTypesThatCannotBeStored();
      RoundTripsValues();
      RejectsValuesThatDoNotFitTheShape();
      ReadsOlderShapesAutomatically();
      RefusesIncompatibleShapesWithoutAnUpgrade();
      UpgradesOldShapesThroughTheProvider();
      ChecksProviderContracts();
      CapturesWhenARunStarts();
      CapturesThatFinishLateAreDropped();
      SavesAndCascadesWithTheRun(root);
      ExistingDatabaseGainsRunStateTables(root);
      HandsStatesBackDuringReplays(root);
      ReportsUnreadableRunsAtRegistration(root);
      PublicApiMatchesTheContract();
    }
    finally
    {
      RunStateLog.Sink = null;
      Reset();
    }
    Console.WriteLine("TUFReplay run state tests passed.");
  }

  private static void Reset()
  {
    RunStateProviders.ResetForTests();
    RunStateReplays.ResetForTests();
    lock (Messages)
      Messages.Clear();
  }

  private static bool Logged(string fragment)
  {
    lock (Messages)
      return Messages.Any(message => message.Contains(fragment, StringComparison.Ordinal));
  }

  private static void MapsEveryStorableType()
  {
    RunStateModel model = RunStateModel.For(typeof(EveryKind));
    string fields = string.Join(",", model.Shape.Fields.Select(field => field.Name));
    Assert(
      fields
        == "At,Blob,ByTab,Child,Children,Count,Flag,Hand,Label,MaybeCount,MaybeHand,Names,Numbers,Origin,Property,Ratio,Speed,Total",
      "The stored members were wrong: " + fields
    );
    Assert(model.Shape.FindField("MaybeCount").Shape.Nullable, "int? was not stored as nullable.");
    Assert(!model.Shape.FindField("Count").Shape.Nullable, "int was stored as nullable.");
    Assert(!model.Shape.FindField("Origin").Shape.Nullable, "A struct was stored as nullable.");
    Assert(RunStateModel.For(typeof(EveryKind)).ShapeId == model.ShapeId, "The same type produced two shape ids.");
    Assert(
      RunStateShapeJson.Write(RunStateShapeJson.Parse(model.ShapeJson)) == model.ShapeJson,
      "A shape did not survive a JSON round trip."
    );
    Console.WriteLine("PASS run states map public fields and settable properties, leaving out the rest");
  }

  private static void ExplainsTypesThatCannotBeStored()
  {
    ExpectModelProblem(typeof(WithObject), "WithObject.Anything is object, which can't be stored");
    ExpectModelProblem(typeof(WithInterface), "WithInterface.Items is IList<int>, which can't be stored");
    ExpectModelProblem(typeof(WithHashSet), "WithHashSet.Keys is HashSet<string>, which can't be stored");
    ExpectModelProblem(typeof(WithFlags), "[Flags] enum");
    ExpectModelProblem(typeof(WithNumberKeys), "Dictionary keys must be strings");
    ExpectModelProblem(typeof(WithDecimal), "WithDecimal.Money is decimal");
    ExpectModelProblem(typeof(Looping), "Looping.Next is Looping, which can't be stored. It contains itself");
    ExpectModelProblem(typeof(WithReadonly), "WithReadonly.Fixed is readonly");
    ExpectModelProblem(typeof(WithoutConstructor), "has no parameterless constructor");
    ExpectModelProblem(typeof(Point), "Use a class with fields");
    Console.WriteLine("PASS run states explain which member can't be stored and how to fix it");
  }

  private static void ExpectModelProblem(Type type, string fragment)
  {
    try
    {
      RunStateModel.For(type);
    }
    catch (RunStateModelException exception)
    {
      Assert(
        exception.Message.Contains(fragment, StringComparison.Ordinal),
        "The message for " + type.Name + " did not say \"" + fragment + "\": " + exception.Message
      );
      return;
    }
    throw new InvalidOperationException(type.Name + " was accepted as a run state.");
  }

  private static void RoundTripsValues()
  {
    RunStateModel model = RunStateModel.For(typeof(EveryKind));
    EveryKind value = EveryKind.Sample();
    string json = RunStateValueWriter.Write(model.Type, value);
    var read = (EveryKind)RunStateValueReader.Read(model.Type, json);

    Assert(read.Flag && read.Count == 7 && read.Total == 1L << 40, "Integers or booleans changed.");
    Assert(read.Ratio == 0.25f && read.Speed == 1.5, "Floating-point values changed.");
    Assert(read.Label == "Attempt" && read.Hand == Hand.Right, "Strings or enums changed.");
    Assert(read.At == value.At && read.At.Kind == DateTimeKind.Utc, "DateTime did not come back as the same UTC time.");
    Assert(read.Blob.SequenceEqual(new byte[] { 1, 2, 3 }), "Bytes changed.");
    Assert(read.MaybeCount == null && read.MaybeHand == Hand.Left, "Nullable values changed.");
    Assert(
      read.Numbers.SequenceEqual(new[] { 1, 2, 3 }) && read.Names.SequenceEqual(new[] { "a", "b" }),
      "Lists changed."
    );
    Assert(read.ByTab["HAND-DEFAULT"]["A"] == 12, "A dictionary lost its values or its case-insensitive comparer.");
    Assert(read.Origin.X == 3 && read.Origin.Y == 4, "A struct changed.");
    Assert(read.Child.Name == "child" && read.Children.Single().Value == 9, "Nested objects changed.");
    Assert(read.Property == 5, "A settable property was not stored.");
    Assert(read.Cache == null, "A [NonSerialized] field was stored.");
    Console.WriteLine("PASS run states round-trip every storable type");
  }

  private static void RejectsValuesThatDoNotFitTheShape()
  {
    RunStateModel model = RunStateModel.For(typeof(EveryKind));
    EveryKind notFinite = EveryKind.Sample();
    notFinite.Speed = double.NaN;
    ExpectValueProblem(model, notFinite, "EveryKind.Speed is NaN");

    EveryKind unnamed = EveryKind.Sample();
    unnamed.Hand = (Hand)5;
    ExpectValueProblem(model, unnamed, "EveryKind.Hand holds 5, which is not a named member");

    ExpectValueProblem(
      RunStateModel.For(typeof(WithBase)),
      new WithBase { Item = new DerivedItem() },
      "WithBase.Item holds a DerivedItem but is declared as BaseItem"
    );

    var huge = new WithText { Lines = Enumerable.Repeat(new string('x', 1024), 80).ToList() };
    ExpectValueProblem(RunStateModel.For(typeof(WithText)), huge, "a run state can hold up to 64 KiB");
    Console.WriteLine("PASS run states refuse values outside their declared shape");
  }

  private static void ExpectValueProblem(RunStateModel model, object value, string fragment)
  {
    try
    {
      RunStateValueWriter.Write(model.Type, value);
    }
    catch (RunStateValueException exception)
    {
      Assert(
        exception.Message.Contains(fragment, StringComparison.Ordinal),
        "The message did not say \"" + fragment + "\": " + exception.Message
      );
      return;
    }
    throw new InvalidOperationException("A value that should be refused was saved: " + fragment);
  }

  private static void ReadsOlderShapesAutomatically()
  {
    RunStateModel saved = RunStateModel.For(typeof(ScoreV1));
    RunStateModel current = RunStateModel.For(typeof(ScoreV2));
    RunStateCompatibility check = RunStateCompatibility.Check(saved.Shape, current.Shape, "ScoreV2");
    Assert(check.IsReadable, "Widening a number, adding and removing members needed an upgrade: " + check.Problem);
    Assert(check.Dropped.SequenceEqual(new[] { "ScoreV2.Note" }), "The removed member was not reported.");
    Assert(check.Defaulted.SequenceEqual(new[] { "ScoreV2.Speed" }), "The added member was not reported.");

    string json = RunStateValueWriter.Write(
      saved.Type,
      new ScoreV1
      {
        Attempt = 41,
        Note = "old",
        Hand = Hand.Left,
      }
    );
    var read = (ScoreV2)RunStateValueReader.Read(current.Type, json);
    Assert(read.Attempt == 41 && read.Speed == 1.5 && read.Hand == ExtendedHand.Left, "An older value was read wrong.");
    Console.WriteLine("PASS run states read older shapes that only added, removed or widened members");
  }

  private static void RefusesIncompatibleShapesWithoutAnUpgrade()
  {
    string changedType = RunStateCompatibility
      .Check(RunStateModel.For(typeof(SpeedText)).Shape, RunStateModel.For(typeof(SpeedNumber)).Shape, "SpeedNumber")
      .Problem;
    Assert(
      changedType == "SpeedNumber.Speed was string and is now double.",
      "A changed member type was not explained: " + changedType
    );

    string lostNull = RunStateCompatibility
      .Check(
        RunStateModel.For(typeof(MaybeCountState)).Shape,
        RunStateModel.For(typeof(CountState)).Shape,
        "CountState"
      )
      .Problem;
    Assert(lostNull?.Contains("a saved null would have nowhere to go") == true, "Narrowing int? to int was allowed.");

    string narrowed = RunStateCompatibility
      .Check(RunStateModel.For(typeof(ScoreV2)).Shape, RunStateModel.For(typeof(ScoreV1)).Shape, "ScoreV1")
      .Problem;
    Assert(
      narrowed != null && narrowed.Contains("ScoreV1.Attempt was long and is now int", StringComparison.Ordinal),
      "Narrowing long to int was allowed: " + narrowed
    );

    string removedEnum = RunStateCompatibility
      .Check(RunStateModel.For(typeof(WithExtendedHand)).Shape, RunStateModel.For(typeof(WithHand)).Shape, "WithHand")
      .Problem;
    Assert(
      removedEnum == "WithHand.Hand was saved with enum members that no longer exist: Both.",
      "A removed enum member was not explained: " + removedEnum
    );
    Console.WriteLine("PASS run states explain shape changes that need an upgrade");
  }

  private static void UpgradesOldShapesThroughTheProvider()
  {
    Reset();
    RunStateProvider provider = RunStateProvider.Bind("Test.Speed", new SpeedProvider());
    RunStateModel old = RunStateModel.For(typeof(SpeedText));
    RunStateReadPlan plan = provider.PlanFor(old.ShapeId, old.ShapeJson);
    Assert(plan.IsReadable && plan.Upgrade != null, "The matching UpgradeRunState was not chosen.");
    var upgraded = (SpeedNumber)
      provider.Read(plan, RunStateValueWriter.Write(old.Type, new SpeedText { Speed = "1.25x" }));
    Assert(upgraded.Speed == 1.25, "UpgradeRunState did not convert the old value.");

    // A rename reads automatically (one member dropped, one defaulted), but an upgrade written
    // for exactly that old shape wins, so the value is not lost.
    RunStateProvider renamed = RunStateProvider.Bind("Test.Renamed", new RenamedProvider());
    RunStateModel before = RunStateModel.For(typeof(AttemptsBeforeRename));
    RunStateReadPlan renamePlan = renamed.PlanFor(before.ShapeId, before.ShapeJson);
    Assert(renamePlan.Upgrade != null, "An exact old shape did not use its upgrade method.");
    var moved = (AttemptsAfterRename)
      renamed.Read(renamePlan, RunStateValueWriter.Write(before.Type, new AttemptsBeforeRename { FullAttempt = 30 }));
    Assert(moved.MapAttempt == 30, "The renamed member lost its value.");

    RunStateProvider noUpgrade = RunStateProvider.Bind("Test.NoUpgrade", new AfterRenameOnlyProvider());
    RunStateReadPlan silent = noUpgrade.PlanFor(before.ShapeId, before.ShapeJson);
    string hint = RunStateDiagnostics.Describe(
      noUpgrade,
      new StoredRunStateShape { ShapeId = before.ShapeId, RunCount = 2 },
      silent
    );
    Assert(
      hint != null
        && hint.Contains("2 saved runs are read without AttemptsAfterRename.FullAttempt", StringComparison.Ordinal),
      "A likely rename was not reported: " + hint
    );
    Console.WriteLine("PASS run states use typed upgrade methods for older shapes");
  }

  private static void ChecksProviderContracts()
  {
    Reset();
    Assert(!RunStateApi.Register("Test.Empty", new EmptyProvider()), "A provider without methods was registered.");
    Assert(
      Logged("EmptyProvider has neither CaptureRunState nor OnReplayBegin") && Logged("Public methods found: Capture"),
      "A provider without contract methods was not explained."
    );
    Assert(!RunStateApi.Register("Test.Mismatch", new MismatchProvider()), "Mismatched state types were registered.");
    Assert(
      Logged("MismatchProvider.CaptureRunState returns CountState but OnReplayBegin takes MaybeCountState"),
      "Mismatched state types were not explained."
    );
    Assert(
      !RunStateApi.Register("Test.Args", new CaptureWithArgumentProvider()),
      "A capture with parameters was registered."
    );
    Assert(Logged("CaptureRunState must take no parameters"), "A capture with parameters was not explained.");
    Assert(!RunStateApi.Register("Test.Void", new VoidCaptureProvider()), "A void capture was registered.");
    Assert(Logged("must return the state, or a Task<state>"), "A void capture was not explained.");
    Assert(!RunStateApi.Register("Test.Struct", new StructProvider()), "A struct state was registered.");
    Assert(Logged("Use a class with fields"), "A struct state was not explained.");
    Assert(!RunStateApi.Register("my mod", new CountProvider()), "An id with a space was registered.");
    Assert(Logged("contains ' '"), "An invalid id was not explained.");
    Assert(!RunStateApi.Register("Test.Null", null), "A null provider was registered.");

    Assert(RunStateApi.Register("Test.Async", new AsyncProvider()), "A Task<T> capture was refused.");
    Assert(RunStateProviders.All.Single().CapturesAsync, "A Task<T> capture was not recognized.");
    Assert(RunStateApi.Register("Test.Async", new AsyncProvider()), "Registering again was refused.");
    Assert(RunStateProviders.All.Length == 1, "Registering an id again did not replace it.");
    RunStateApi.Unregister("Test.Async");
    Assert(!RunStateApi.IsRegistered("Test.Async"), "Unregister kept the provider.");
    Console.WriteLine("PASS run state providers are checked when they register");
  }

  private static void CapturesWhenARunStarts()
  {
    Reset();
    var counter = new CountProvider { Next = 3 };
    var pending = new AsyncProvider();
    RunStateApi.Register("Test.Count", counter);
    RunStateApi.Register("Test.Async", pending);
    RunStateApi.Register("Test.Throws", new ThrowingProvider());
    RunStateApi.Register("Test.Skips", new SkippingProvider());

    RunStateCapture capture = RunStateCapture.Begin("run-capture");
    counter.LastReturned.Count = 99; // Changing the object later must not change what is saved.
    pending.Source.SetResult(new CountState { Count = 8 });
    WaitFor(() => capture.PendingCount == 0, "The async capture did not finish.");
    RunStateRecord[] records = capture.Close().OrderBy(record => record.StateId).ToArray();

    Assert(
      string.Join(",", records.Select(record => record.StateId)) == "Test.Async,Test.Count",
      "The wrong providers were saved: " + string.Join(",", records.Select(record => record.StateId))
    );
    Assert(
      records[1].ValueJson == "{\"Count\":3}",
      "The value was not saved as it was returned: " + records[1].ValueJson
    );
    Assert(records[0].ValueJson == "{\"Count\":8}", "The async value was not saved.");
    Assert(Logged("Test.Throws: ThrowingProvider.CaptureRunState threw"), "A failing capture was not reported.");
    Console.WriteLine("PASS run states are captured when a run starts, including Task results");
  }

  private static void CapturesThatFinishLateAreDropped()
  {
    Reset();
    var pending = new AsyncProvider();
    RunStateApi.Register("Test.Async", pending);
    RunStateCapture capture = RunStateCapture.Begin("run-late");
    Assert(capture.Close().Count == 0, "An unfinished capture was saved.");
    pending.Source.SetResult(new CountState { Count = 1 });
    WaitFor(() => capture.PendingCount == 0, "The late capture did not finish.");
    Assert(Logged("finished after the run ended"), "A late capture was not reported.");
    Console.WriteLine("PASS run state captures that finish after the run are dropped");
  }

  private static void SavesAndCascadesWithTheRun(string root)
  {
    Reset();
    Open(root, "run-states.sqlite");
    RunStateApi.Register("Test.Count", new CountProvider { Next = 5 });
    SaveRun("first", 0, RunStateCapture.Begin("first").Close());
    SaveRun("second", 1, RunStateCapture.Begin("second").Close());

    List<StoredRunState> stored = RunStateRepository.Load("first");
    Assert(stored.Count == 1 && stored[0].ValueJson == "{\"Count\":5}", "A saved state was not read back.");
    Assert(Count("run_state_shapes") == 1, "Runs with the same shape stored the shape twice.");
    Assert(RunStateRepository.ListShapes("Test.Count").Single().RunCount == 2, "Shape usage was not counted.");

    Assert(RunRepository.Delete("first"), "The run could not be deleted.");
    Assert(RunStateRepository.Load("first").Count == 0, "Deleting a run kept its states.");
    Assert(Count("run_states") == 1, "Deleting one run removed another run's states.");
    Console.WriteLine("PASS run states save with their run and are deleted with it");
  }

  private static void ExistingDatabaseGainsRunStateTables(string root)
  {
    Open(root, "run-state-upgrade.sqlite");
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "DROP TABLE run_states; DROP TABLE run_state_shapes;";
    command.ExecuteNonQuery();
    ActivitySchema.Ensure(connection);
    command.CommandText =
      "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('run_states','run_state_shapes')";
    Assert(Convert.ToInt32(command.ExecuteScalar()) == 2, "An existing database did not gain the run state tables.");
    command.CommandText = "PRAGMA user_version;";
    Assert(
      Convert.ToInt32(command.ExecuteScalar()) == ActivitySchema.Version,
      "The run state tables changed the version."
    );
    Console.WriteLine("PASS existing activity databases gain the run state tables in place");
  }

  private static void HandsStatesBackDuringReplays(string root)
  {
    Reset();
    Open(root, "run-state-replay.sqlite");
    var follower = new FollowingProvider();
    RunStateApi.Register("Test.Follow", follower);
    RunStateApi.Register("Test.Begin", new BeginOnlyProvider());
    follower.Next = 11;
    SaveRun("recorded", 0, RunStateCapture.Begin("recorded").Close());

    RunStateReplaySession session = RunStateReplays.Begin(
      "recorded",
      render: true,
      RunStateRepository.Load("recorded")
    );
    Assert(follower.Recorded?.Count == 11 && follower.Render, "OnReplayBegin did not get the saved value.");
    Assert(RunStateApi.IsReplaying && RunStateApi.IsRendering, "The API did not report the render.");
    Assert(RunStateApi.ReplayRunId == "recorded", "The API did not report the run.");
    Assert(BeginOnlyProvider.LastRecorded == null, "A provider without a saved value got one.");

    session.End();
    session.End();
    Assert(follower.Ends == 1, "OnReplayEnd was not called exactly once.");
    Assert(!follower.ReplayingDuringEnd, "IsReplaying was still true inside OnReplayEnd.");
    Assert(!RunStateApi.IsReplaying, "The API still reported a replay.");

    RunStateReplaySession first = RunStateReplays.Begin("missing", render: false, Array.Empty<StoredRunState>());
    Assert(
      follower.Recorded == null && !follower.Render,
      "A run without a saved value did not reach OnReplayBegin as null."
    );
    RunStateReplays.Begin("recorded", render: false, RunStateRepository.Load("recorded"));
    Assert(follower.Ends == 2, "Beginning another replay did not end the previous one.");
    first.End();
    Assert(follower.Ends == 2, "Ending a replaced session called OnReplayEnd again.");

    RunStateApi.Unregister("Test.Follow");
    RunStateReplays.Active.End();
    Assert(follower.Ends == 2, "An unregistered provider got OnReplayEnd.");

    var damaged = new StoredRunState
    {
      StateId = "Test.Begin",
      ShapeId = "unknown",
      ShapeJson = RunStateModel.For(typeof(SpeedText)).ShapeJson,
      ValueJson = "{\"Speed\":\"1x\"}",
    };
    RunStateReplays.Begin("damaged", render: false, new[] { damaged }).End();
    Assert(BeginOnlyProvider.LastRecorded == null, "An unreadable value reached OnReplayBegin.");
    Assert(Logged("Test.Begin: this run's saved value can't be read"), "An unreadable value was not reported.");
    Console.WriteLine("PASS run states are handed back while a run is replayed or rendered");
  }

  private static void ReportsUnreadableRunsAtRegistration(string root)
  {
    Reset();
    Open(root, "run-state-diagnostics.sqlite");
    RunStateApi.Register("Test.Speed", new OldSpeedProvider());
    SaveRun("old-speed", 0, RunStateCapture.Begin("old-speed").Close());
    RunStateApi.Register("Test.Speed", new NewSpeedWithoutUpgradeProvider());
    RunStateDiagnostics.Check(RunStateProviders.All.Single());
    Assert(
      Logged("Test.Speed: 1 saved run can't be read: SpeedNumber.Speed was string and is now double."),
      "Runs the new provider can't read were not reported at registration."
    );
    Console.WriteLine("PASS run state providers learn at registration which saved runs they can't read");
  }

  private static void PublicApiMatchesTheContract()
  {
    // Bind the way another mod does: by name, without referencing TUFReplay.
    Type api = AppDomain
      .CurrentDomain.GetAssemblies()
      .First(assembly => assembly.GetName().Name == "TUFReplay")
      .GetType("TUFReplay.RunStateApi", throwOnError: true);
    Assert(api.GetField("ApiVersion").GetValue(null) is 1, "The API version changed.");
    var register =
      (Func<string, object, bool>)
        Delegate.CreateDelegate(typeof(Func<string, object, bool>), api.GetMethod("Register"));
    var unregister = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), api.GetMethod("Unregister"));
    var replaying =
      (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), api.GetProperty("IsReplaying").GetGetMethod());
    Reset();
    Assert(register("Test.Count", new CountProvider()), "Registering through a delegate failed.");
    unregister("Test.Count");
    Assert(!replaying(), "IsReplaying was true outside a replay.");
    Console.WriteLine("PASS RunStateApi binds by reflection like other optional mod APIs");
  }

  private static void WaitFor(Func<bool> condition, string message)
  {
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (!condition())
    {
      if (DateTime.UtcNow > deadline)
        throw new InvalidOperationException(message);
      Thread.Sleep(5);
    }
  }

  private static void Open(string root, string name)
  {
    ActivityDatabaseSuite.SetDatabasePath(Path.Combine(root, name));
    using SqliteConnection connection = Database.OpenConnection();
    ActivitySchema.Ensure(connection);
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText =
      @"INSERT INTO app_sessions(id,started_at_utc,ended_at_utc,recorder_utc_offset_minutes)
VALUES('app','2026-11-07T00:00:00Z',NULL,0);
INSERT INTO levels(id,identity_key,source_kind,adofai_path,first_seen_at_utc,last_seen_at_utc)
VALUES('level','local:test',0,'test.adofai','2026-11-07T00:00:00Z','2026-11-07T00:00:00Z');
INSERT INTO level_sessions(id,level_id,app_session_id,opened_at_utc,closed_at_utc)
VALUES('level-session','level','app','2026-11-07T00:00:00Z',NULL);";
    command.ExecuteNonQuery();
  }

  private static void SaveRun(string id, int index, IReadOnlyList<RunStateRecord> states)
  {
    RunRepository.Save(
      new RunRecord
      {
        Id = id,
        LevelSessionId = "level-session",
        RunIndex = index,
        StartedAtUtc = "2026-11-07T01:00:00Z",
        EndedAtUtc = "2026-11-07T01:02:00Z",
        LastTile = 40,
        Result = "failed",
        RunStates = states,
      }
    );
  }

  private static int Count(string table)
  {
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM " + table;
    return Convert.ToInt32(command.ExecuteScalar());
  }

  // ---- State types ----

  public enum Hand
  {
    Left,
    Right,
  }

  public enum ExtendedHand
  {
    Left,
    Right,
    Both,
  }

  [Flags]
  public enum Modifiers
  {
    None = 0,
    Shift = 1,
  }

  public struct Point
  {
    public int X;
    public int Y;
  }

  public sealed class Nested
  {
    public string Name;
    public int Value;
  }

  public sealed class EveryKind
  {
    public bool Flag;
    public int Count;
    public long Total;
    public float Ratio;
    public double Speed;
    public string Label;
    public Hand Hand;
    public DateTime At;
    public byte[] Blob;
    public int? MaybeCount;
    public Hand? MaybeHand;
    public List<int> Numbers = new List<int>();
    public string[] Names;
    public Dictionary<string, Dictionary<string, int>> ByTab = new Dictionary<string, Dictionary<string, int>>(
      StringComparer.OrdinalIgnoreCase
    );
    public Point Origin;
    public Nested Child;
    public List<Nested> Children;
    public int Property { get; set; }
    public int Computed => Count * 2;

    [NonSerialized]
    public string Cache;

    public static EveryKind Sample() =>
      new EveryKind
      {
        Flag = true,
        Count = 7,
        Total = 1L << 40,
        Ratio = 0.25f,
        Speed = 1.5,
        Label = "Attempt",
        Hand = Hand.Right,
        At = new DateTime(2026, 11, 7, 1, 2, 3, DateTimeKind.Utc),
        Blob = new byte[] { 1, 2, 3 },
        MaybeHand = Hand.Left,
        Numbers = new List<int> { 1, 2, 3 },
        Names = new[] { "a", "b" },
        ByTab = new Dictionary<string, Dictionary<string, int>>
        {
          ["hand-default"] = new Dictionary<string, int> { ["A"] = 12 },
        },
        Origin = new Point { X = 3, Y = 4 },
        Child = new Nested { Name = "child" },
        Children = new List<Nested> { new Nested { Value = 9 } },
        Property = 5,
        Cache = "not saved",
      };
  }

  public sealed class WithObject
  {
    public object Anything;
  }

  public sealed class WithInterface
  {
    public IList<int> Items;
  }

  public sealed class WithHashSet
  {
    public HashSet<string> Keys;
  }

  public sealed class WithFlags
  {
    public Modifiers Held;
  }

  public sealed class WithNumberKeys
  {
    public Dictionary<int, int> ByNumber;
  }

  public sealed class WithDecimal
  {
    public decimal Money;
  }

  public sealed class Looping
  {
    public Looping Next;
  }

  public sealed class WithReadonly
  {
    public readonly int Fixed = 1;
  }

  public sealed class WithoutConstructor
  {
    public int Value;

    public WithoutConstructor(int value) => Value = value;
  }

  public class BaseItem
  {
    public int Value;
  }

  public sealed class DerivedItem : BaseItem
  {
    public int Extra;
  }

  public sealed class WithBase
  {
    public BaseItem Item;
  }

  public sealed class WithText
  {
    public List<string> Lines;
  }

  public sealed class ScoreV1
  {
    public int Attempt;
    public string Note;
    public Hand Hand;
  }

  public sealed class ScoreV2
  {
    public long Attempt;
    public double Speed = 1.5;
    public ExtendedHand Hand;
  }

  public sealed class WithHand
  {
    public Hand Hand;
  }

  public sealed class WithExtendedHand
  {
    public ExtendedHand Hand;
  }

  public sealed class SpeedText
  {
    public string Speed;
  }

  public sealed class SpeedNumber
  {
    public double Speed;
  }

  public sealed class CountState
  {
    public int Count;
  }

  public sealed class MaybeCountState
  {
    public int? Count;
  }

  public sealed class AttemptsBeforeRename
  {
    public int FullAttempt;
  }

  public sealed class AttemptsAfterRename
  {
    public int MapAttempt;
  }

  // ---- Providers ----

  private sealed class SpeedProvider
  {
    public SpeedNumber CaptureRunState() => new SpeedNumber { Speed = 1 };

    public SpeedNumber UpgradeRunState(SpeedText old) =>
      new SpeedNumber
      {
        Speed = double.Parse(old.Speed.TrimEnd('x'), System.Globalization.CultureInfo.InvariantCulture),
      };
  }

  private sealed class OldSpeedProvider
  {
    public SpeedText CaptureRunState() => new SpeedText { Speed = "2x" };
  }

  private sealed class NewSpeedWithoutUpgradeProvider
  {
    public SpeedNumber CaptureRunState() => new SpeedNumber { Speed = 2 };
  }

  private sealed class RenamedProvider
  {
    public AttemptsAfterRename CaptureRunState() => new AttemptsAfterRename();

    public AttemptsAfterRename UpgradeRunState(AttemptsBeforeRename old) =>
      new AttemptsAfterRename { MapAttempt = old.FullAttempt };
  }

  private sealed class AfterRenameOnlyProvider
  {
    public AttemptsAfterRename CaptureRunState() => new AttemptsAfterRename();
  }

  private sealed class EmptyProvider
  {
    public CountState Capture() => new CountState();
  }

  private sealed class MismatchProvider
  {
    public CountState CaptureRunState() => new CountState();

    public void OnReplayBegin(MaybeCountState recorded) { }
  }

  private sealed class CaptureWithArgumentProvider
  {
    public CountState CaptureRunState(int count) => new CountState { Count = count };
  }

  private sealed class VoidCaptureProvider
  {
    public void CaptureRunState() { }
  }

  private sealed class StructProvider
  {
    public Point CaptureRunState() => new Point();
  }

  private sealed class CountProvider
  {
    public int Next;
    public CountState LastReturned;

    public CountState CaptureRunState() => LastReturned = new CountState { Count = Next };
  }

  private sealed class AsyncProvider
  {
    public readonly TaskCompletionSource<CountState> Source = new TaskCompletionSource<CountState>(
      TaskCreationOptions.RunContinuationsAsynchronously
    );

    public Task<CountState> CaptureRunState() => Source.Task;
  }

  private sealed class ThrowingProvider
  {
    public CountState CaptureRunState() => throw new InvalidOperationException("broken");
  }

  private sealed class SkippingProvider
  {
    public CountState CaptureRunState() => null;
  }

  private sealed class FollowingProvider
  {
    public int Next;
    public CountState Recorded;
    public bool Render;
    public int Ends;
    public bool ReplayingDuringEnd;

    public CountState CaptureRunState() => new CountState { Count = Next };

    public void OnReplayBegin(CountState recorded, bool render)
    {
      Recorded = recorded;
      Render = render;
    }

    public void OnReplayEnd()
    {
      Ends++;
      ReplayingDuringEnd = RunStateApi.IsReplaying;
    }
  }

  private sealed class BeginOnlyProvider
  {
    public static SpeedNumber LastRecorded;

    public void OnReplayBegin(SpeedNumber recorded) => LastRecorded = recorded;
  }
}
