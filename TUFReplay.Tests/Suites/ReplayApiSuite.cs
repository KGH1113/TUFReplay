#pragma warning disable CS0649 // State types are filled by reflection or never at all.

using System.Reflection;
using Microsoft.Data.Sqlite;
using TUFReplay;
using TUFReplay.Activity.Migrations;
using TUFReplay.Activity.Models;
using TUFReplay.Activity.Repositories;
using TUFReplay.ModApi.Diagnostics;
using TUFReplay.ModApi.Mapping;
using TUFReplay.ModApi.Models;
using TUFReplay.ModApi.Playback;
using TUFReplay.ModApi.Providers;
using TUFReplay.ModApi.Recording;
using TUFReplay.ModApi.Repositories;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Playback;
using TUFReplay.Shared.Database;
using TUFReplay.Shared.NativeInput;
using UnityEngine;
using static TestFixture;

// Mod states saved with each run (ReplayApi).
internal static class ReplayApiSuite
{
  private static readonly List<string> Messages = new List<string>();

  internal static void RunAll(string root)
  {
    ModApiLog.Sink = message =>
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
      AddsEventsDuringARun();
      LimitsEventsPerRun();
      SavesAndCascadesWithTheRun(root);
      SavesEventsWithTheRun(root);
      ExistingDatabaseGainsRunStateTables(root);
      HandsStatesBackDuringReplays(root);
      HandsEventsBackAtTheirTimes();
      UpgradesOldEventsThroughTheProvider();
      ReportsUnreadableRunsAtRegistration(root);
      PublicApiMatchesTheContract();
      BindsProvidersWithoutAState();
      RefusesReplayMembersWithTheWrongSignature();
      DispatchesTheReplayToProviders();
      LimitsLogsFromFailingFrameCalls();
      FeedsRecordedKeysAtTheirTimes();
      TellsProvidersWhatTheRendererTakes();
      CollectsOverlayVideosAfterTheLastFrame(root);
    }
    finally
    {
      ModApiLog.Sink = null;
      Reset();
    }
    Console.WriteLine("TUFReplay run state tests passed.");
  }

  private static void Reset()
  {
    ReplayProviders.ResetForTests();
    ReplaySessions.ResetForTests();
    RunStateCapture.ResetForTests();
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
    ExpectValueProblem(RunStateModel.For(typeof(WithText)), huge, "one saved value can hold up to 64 KiB");
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
    ReplayProvider provider = ReplayProvider.Bind("Test.Speed", new SpeedProvider());
    RunStateModel old = RunStateModel.For(typeof(SpeedText));
    RunStateReadPlan plan = provider.State.PlanFor(old.ShapeId, old.ShapeJson);
    Assert(plan.IsReadable && plan.Upgrade != null, "The matching UpgradeRunState was not chosen.");
    var upgraded = (SpeedNumber)
      provider.Read(plan, RunStateValueWriter.Write(old.Type, new SpeedText { Speed = "1.25x" }));
    Assert(upgraded.Speed == 1.25, "UpgradeRunState did not convert the old value.");

    // A rename reads automatically (one member dropped, one defaulted), but an upgrade written
    // for exactly that old shape wins, so the value is not lost.
    ReplayProvider renamed = ReplayProvider.Bind("Test.Renamed", new RenamedProvider());
    RunStateModel before = RunStateModel.For(typeof(AttemptsBeforeRename));
    RunStateReadPlan renamePlan = renamed.State.PlanFor(before.ShapeId, before.ShapeJson);
    Assert(renamePlan.Upgrade != null, "An exact old shape did not use its upgrade method.");
    var moved = (AttemptsAfterRename)
      renamed.Read(renamePlan, RunStateValueWriter.Write(before.Type, new AttemptsBeforeRename { FullAttempt = 30 }));
    Assert(moved.MapAttempt == 30, "The renamed member lost its value.");

    ReplayProvider noUpgrade = ReplayProvider.Bind("Test.NoUpgrade", new AfterRenameOnlyProvider());
    RunStateReadPlan silent = noUpgrade.State.PlanFor(before.ShapeId, before.ShapeJson);
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
    Assert(!ReplayApi.Register("Test.Empty", new EmptyProvider()), "A provider without methods was registered.");
    Assert(
      Logged("EmptyProvider has none of the ReplayApi methods") && Logged("Public methods found: Capture"),
      "A provider without contract methods was not explained."
    );
    Assert(!ReplayApi.Register("Test.Mismatch", new MismatchProvider()), "Mismatched state types were registered.");
    Assert(
      Logged("MismatchProvider.CaptureRunState returns CountState but OnReplayState takes MaybeCountState"),
      "Mismatched state types were not explained."
    );
    Assert(
      !ReplayApi.Register("Test.Args", new CaptureWithArgumentProvider()),
      "A capture with parameters was registered."
    );
    Assert(Logged("CaptureRunState must take no parameters"), "A capture with parameters was not explained.");
    Assert(!ReplayApi.Register("Test.Void", new VoidCaptureProvider()), "A void capture was registered.");
    Assert(Logged("must return the state, or a Task<state>"), "A void capture was not explained.");
    Assert(!ReplayApi.Register("Test.Struct", new StructProvider()), "A struct state was registered.");
    Assert(Logged("Use a class with fields"), "A struct state was not explained.");
    Assert(!ReplayApi.Register("my mod", new CountProvider()), "An id with a space was registered.");
    Assert(Logged("contains ' '"), "An invalid id was not explained.");
    Assert(!ReplayApi.Register("Test.Null", null), "A null provider was registered.");

    Assert(ReplayApi.Register("Test.Async", new AsyncProvider()), "A Task<T> capture was refused.");
    Assert(ReplayProviders.All.Single().CapturesAsync, "A Task<T> capture was not recognized.");
    Assert(ReplayApi.Register("Test.Async", new AsyncProvider()), "Registering again was refused.");
    Assert(ReplayProviders.All.Length == 1, "Registering an id again did not replace it.");
    ReplayApi.Unregister("Test.Async");
    Assert(!ReplayApi.IsRegistered("Test.Async"), "Unregister kept the provider.");
    Console.WriteLine("PASS run state providers are checked when they register");
  }

  private static void CapturesWhenARunStarts()
  {
    Reset();
    var counter = new CountProvider { Next = 3 };
    var pending = new AsyncProvider();
    ReplayApi.Register("Test.Count", counter);
    ReplayApi.Register("Test.Async", pending);
    ReplayApi.Register("Test.Throws", new ThrowingProvider());
    ReplayApi.Register("Test.Skips", new SkippingProvider());

    RunStateCapture capture = RunStateCapture.Begin("run-capture");
    counter.LastReturned.Count = 99; // Changing the object later must not change what is saved.
    pending.Source.SetResult(new CountState { Count = 8 });
    WaitFor(() => capture.PendingCount == 0, "The async capture did not finish.");
    RunStateRecord[] records = capture.Close().States.OrderBy(record => record.StateId).ToArray();

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
    ReplayApi.Register("Test.Async", pending);
    RunStateCapture capture = RunStateCapture.Begin("run-late");
    Assert(capture.Close().IsEmpty, "An unfinished capture was saved.");
    pending.Source.SetResult(new CountState { Count = 1 });
    WaitFor(() => capture.PendingCount == 0, "The late capture did not finish.");
    Assert(Logged("finished after the run ended"), "A late capture was not reported.");
    Console.WriteLine("PASS run state captures that finish after the run are dropped");
  }

  private static void AddsEventsDuringARun()
  {
    Reset();
    ReplayApi.Register("Test.Overlay", new OverlayProvider { Start = 1 });
    ReplayApi.Register("Test.Count", new RecordOnlyProvider());
    ReplayApi.Register("Test.KeyViewer", new KeyViewerProvider());
    ReplayEventStore overlay = ReplayApi.GetEventStore("Test.Overlay");
    Assert(ReferenceEquals(overlay, ReplayApi.GetEventStore("Test.Overlay")), "One id gave two event stores.");
    Assert(ReplayApi.GetEventStore("my mod") == null, "An invalid id got an event store.");
    Assert(Logged("No event store was given: the id \"my mod\" contains ' '"), "The invalid id was not explained.");
    Assert(!overlay.AddEvent(new ChatSent { Text = "early" }), "An event was added outside a run.");
    Assert(!Logged("AddEvent"), "Adding outside a run was reported as a problem.");

    long now = 0;
    RunStateCapture capture = RunStateCapture.Begin("run-events", () => now);
    now = 1_000;
    var layout = new LayoutChanged { Size = 2 };
    Assert(overlay.AddEvent(layout), "An event during the run was refused.");
    layout.Size = 99; // Changing the object later must not change what is saved.
    now = 2_000;
    // Every event is kept, several at one moment too, such as chat while the game is paused.
    Assert(overlay.AddEvent(new ChatSent { Text = "a" }), "An event was refused.");
    Assert(overlay.AddEvent(new ChatSent { Text = "b" }), "An event at the same moment was refused.");
    now = 1_500; // A clock that steps back counts as the same moment, never an earlier one.
    Assert(overlay.AddEvent(new LayoutChanged { Size = 3 }), "An event was refused.");

    Assert(!overlay.AddEvent(new CountState()), "An event no OnReplayEvent takes was added.");
    Assert(
      Logged(
        "Test.Overlay: AddEvent got CountState, but OverlayProvider has no OnReplayEvent(CountState e, long ticks)."
      ),
      "An event of another type was not explained."
    );
    Assert(!ReplayApi.GetEventStore("Test.Missing").AddEvent(new ChatSent()), "An event for an unknown id was added.");
    Assert(
      Logged("Test.Missing: AddEvent found no provider registered under \"Test.Missing\". Register it first."),
      "A missing provider was not explained."
    );
    Assert(!ReplayApi.GetEventStore("Test.Count").AddEvent(new ChatSent()), "A provider without events took one.");
    Assert(
      Logged("Test.Count: AddEvent needs OnReplayEvent(YourEvent e, long ticks) on RecordOnlyProvider"),
      "A provider without events was not explained."
    );
    Assert(!overlay.AddEvent(null), "Null was added.");
    Assert(Logged("Test.Overlay: AddEvent got null. Pass an event the provider takes:"), "Null was not explained.");

    CapturedRunStates captured = capture.Close();
    Assert(captured.States.Single().StateId == "Test.Overlay", "The start value was not captured beside the events.");
    string events = string.Join(
      ",",
      captured.Events.Select(saved => saved.EventType + "=" + saved.ValueJson + "@" + saved.TimeUs)
    );
    Assert(
      events
        == "ReplayApiSuite.LayoutChanged={\"Size\":2}@1000,"
          + "ReplayApiSuite.ChatSent={\"Text\":\"a\"}@2000,"
          + "ReplayApiSuite.ChatSent={\"Text\":\"b\"}@2000,"
          + "ReplayApiSuite.LayoutChanged={\"Size\":3}@2000",
      "Events were not kept in order with their types and times: " + events
    );
    Assert(!overlay.AddEvent(new ChatSent()), "An event was added after the run ended.");

    RunStateCapture discarded = RunStateCapture.Begin("run-discarded", () => 5);
    overlay.AddEvent(new ChatSent { Text = "gone" });
    discarded.Discard();
    Assert(discarded.Close().IsEmpty, "A discarded run kept its events.");
    Assert(!overlay.AddEvent(new ChatSent()), "An event was added after a discard.");
    Console.WriteLine("PASS events added during a run are kept with their types and times on the run's timeline");
  }

  private static void LimitsEventsPerRun()
  {
    Reset();
    ReplayApi.Register("Test.Overlay", new OverlayProvider());
    ReplayApi.Register("Test.Other", new OverlayProvider());
    ReplayEventStore overlay = ReplayApi.GetEventStore("Test.Overlay");
    long now = 0;
    RunStateCapture capture = RunStateCapture.Begin("run-limit", () => now);
    for (int index = 0; index < RunStateCapture.MaxEventsPerRun + 5; index++)
    {
      now = index * 10;
      overlay.AddEvent(new LayoutChanged { Size = index });
    }
    Assert(
      ReplayApi.GetEventStore("Test.Other").AddEvent(new LayoutChanged()),
      "One provider's limit stopped another."
    );
    CapturedRunStates captured = capture.Close();
    Assert(
      captured.Events.Count(saved => saved.StateId == "Test.Overlay") == RunStateCapture.MaxEventsPerRun,
      "A run kept more events than its limit."
    );
    int reported;
    lock (Messages)
      reported = Messages.Count(message => message.Contains("the most one run keeps", StringComparison.Ordinal));
    Assert(reported == 1, "Reaching the limit was reported " + reported + " times instead of once.");
    Console.WriteLine("PASS one provider can add a limited number of events per run");
  }

  private static void SavesAndCascadesWithTheRun(string root)
  {
    Reset();
    Open(root, "run-states.sqlite");
    ReplayApi.Register("Test.Count", new CountProvider { Next = 5 });
    SaveRun("first", 0, RunStateCapture.Begin("first").Close());
    SaveRun("second", 1, RunStateCapture.Begin("second").Close());

    IReadOnlyList<StoredRunState> stored = RunStateRepository.Load("first").States;
    Assert(stored.Count == 1 && stored[0].ValueJson == "{\"Count\":5}", "A saved state was not read back.");
    Assert(Count("run_state_shapes") == 1, "Runs with the same shape stored the shape twice.");
    Assert(RunStateRepository.ListShapes("Test.Count").Single().RunCount == 2, "Shape usage was not counted.");

    Assert(RunRepository.Delete("first"), "The run could not be deleted.");
    Assert(RunStateRepository.Load("first").States.Count == 0, "Deleting a run kept its states.");
    Assert(Count("run_states") == 1, "Deleting one run removed another run's states.");
    Console.WriteLine("PASS run states save with their run and are deleted with it");
  }

  private static void SavesEventsWithTheRun(string root)
  {
    Reset();
    Open(root, "run-events.sqlite");
    ReplayApi.Register("Test.Overlay", new OverlayProvider { Start = 1 });
    ReplayEventStore overlay = ReplayApi.GetEventStore("Test.Overlay");
    long now = 0;
    RunStateCapture capture = RunStateCapture.Begin("with-events", () => now);
    now = 3_000;
    overlay.AddEvent(new LayoutChanged { Size = 2 });
    now = 1_000_000;
    overlay.AddEvent(new ChatSent { Text = "hi" });
    SaveRun("with-events", 0, capture.Close());
    SaveRun("start-only", 1, RunStateCapture.Begin("start-only").Close());

    StoredRunStates stored = RunStateRepository.Load("with-events");
    Assert(stored.States.Single().ValueJson == "{\"Count\":1}", "The start value was not read back.");
    string events = string.Join(
      ",",
      stored.Events.Select(saved => saved.EventType + "=" + saved.ValueJson + "@" + saved.TimeUs)
    );
    Assert(
      events == "ReplayApiSuite.LayoutChanged={\"Size\":2}@3000,ReplayApiSuite.ChatSent={\"Text\":\"hi\"}@1000000",
      "Events were not read back in time order with their types: " + events
    );
    Assert(Count("run_state_shapes") == 3, "The state and event shapes were not stored once each.");
    List<StoredRunStateShape> shapes = RunStateRepository.ListShapes("Test.Overlay");
    Assert(
      shapes.Count == 3
        && shapes.Single(shape => shape.EventType == null).RunCount == 2
        && shapes.Single(shape => shape.EventType == "ReplayApiSuite.ChatSent").RunCount == 1,
      "Shape usage was not counted per start value and event type."
    );
    Assert(RunRepository.Delete("with-events"), "The run could not be deleted.");
    Assert(Count("run_events") == 0, "Deleting a run kept its events.");
    Assert(RunStateRepository.Load("start-only").Events.Count == 0, "A run without events got some.");
    Console.WriteLine("PASS events added during a run save with it and are deleted with it");
  }

  private static void ExistingDatabaseGainsRunStateTables(string root)
  {
    Open(root, "run-state-upgrade.sqlite");
    using SqliteConnection connection = Database.OpenConnection();
    using SqliteCommand command = connection.CreateCommand();
    command.CommandText = "DROP TABLE run_events; DROP TABLE run_states; DROP TABLE run_state_shapes;";
    command.ExecuteNonQuery();
    ActivitySchema.Ensure(connection);
    command.CommandText =
      "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('run_states','run_state_shapes','run_events')";
    Assert(Convert.ToInt32(command.ExecuteScalar()) == 3, "An existing database did not gain the run state tables.");
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
    ReplayApi.Register("Test.Follow", follower);
    ReplayApi.Register("Test.Begin", new StateOnlyProvider());
    follower.Next = 11;
    SaveRun("recorded", 0, RunStateCapture.Begin("recorded").Close());

    ReplaySession session = ReplaySessions.Begin(
      "recorded",
      render: new ReplayRender(1920, 1080, 60),
      RunStateRepository.Load("recorded")
    );
    Assert(follower.Recorded?.Count == 11 && follower.Render, "OnReplayState did not get the saved value.");
    Assert(
      follower.Calls.SequenceEqual(new[] { "begin", "state 11 at 0" }),
      "The start value did not follow OnReplayBegin: " + string.Join(", ", follower.Calls)
    );
    Assert(ReplayApi.IsReplaying && ReplayApi.IsRendering, "The API did not report the render.");
    Assert(ReplayApi.ReplayRunId == "recorded", "The API did not report the run.");
    Assert(
      StateOnlyProvider.Calls == 1 && StateOnlyProvider.LastState == null,
      "A provider without a saved value did not get null once."
    );

    session.End();
    session.End();
    Assert(follower.Ends == 1, "OnReplayEnd was not called exactly once.");
    Assert(!follower.ReplayingDuringEnd, "IsReplaying was still true inside OnReplayEnd.");
    Assert(!ReplayApi.IsReplaying, "The API still reported a replay.");

    ReplaySession first = ReplaySessions.Begin("missing", render: null, StoredRunStates.Empty);
    Assert(
      follower.Recorded == null && !follower.Render,
      "A run without a saved value did not reach OnReplayState as null."
    );
    ReplaySessions.Begin("recorded", render: null, RunStateRepository.Load("recorded"));
    Assert(follower.Ends == 2, "Beginning another replay did not end the previous one.");
    first.End();
    Assert(follower.Ends == 2, "Ending a replaced session called OnReplayEnd again.");

    ReplayApi.Unregister("Test.Follow");
    ReplaySessions.Active.End();
    Assert(follower.Ends == 2, "An unregistered provider got OnReplayEnd.");

    var damaged = new StoredRunState
    {
      StateId = "Test.Begin",
      ShapeId = "unknown",
      ShapeJson = RunStateModel.For(typeof(SpeedText)).ShapeJson,
      ValueJson = "{\"Speed\":\"1x\"}",
    };
    StateOnlyProvider.Calls = 0;
    ReplaySessions.Begin("damaged", render: null, new StoredRunStates(new[] { damaged }, null)).End();
    Assert(
      StateOnlyProvider.Calls == 1 && StateOnlyProvider.LastState == null,
      "An unreadable value did not reach OnReplayState as null."
    );
    Assert(Logged("Test.Begin: this run's start value can't be read"), "An unreadable value was not reported.");
    Console.WriteLine("PASS run states are handed back while a run is replayed or rendered");
  }

  private static void HandsEventsBackAtTheirTimes()
  {
    Reset();
    var overlay = new OverlayProvider();
    ReplayApi.Register("Test.Overlay", overlay);
    ReplayApi.Register("Test.KeyViewer", new KeyViewerProvider());
    RunStateModel state = RunStateModel.For(typeof(CountState));
    RunStateModel layout = RunStateModel.ForEvent(typeof(LayoutChanged));
    RunStateModel chat = RunStateModel.ForEvent(typeof(ChatSent));
    StoredRunEvent At(
      long timeUs,
      RunStateModel model,
      string valueJson,
      string id = "Test.Overlay",
      string type = null
    ) =>
      new StoredRunEvent
      {
        StateId = id,
        EventType = type ?? RunStateTypeNames.Qualified(model.ClrType),
        ShapeId = model.ShapeId,
        ShapeJson = model.ShapeJson,
        ValueJson = valueJson,
        TimeUs = timeUs,
      };
    var saved = new StoredRunStates(
      new[]
      {
        new StoredRunState
        {
          StateId = "Test.Overlay",
          ShapeId = state.ShapeId,
          ShapeJson = state.ShapeJson,
          ValueJson = "{\"Count\":1}",
        },
      },
      new[]
      {
        At(1_000, layout, "{\"Size\":2}"),
        At(1_500, chat, "{\"Text\":\"other\"}", id: "Test.Removed"),
        At(2_000, layout, "{\"Size\":\"broken\"}"),
        // Saved before the class moved to another namespace: found by its type name.
        At(2_000, chat, "{\"Text\":\"b\"}", type: "Old.Namespace.ChatSent"),
        At(2_500, chat, "{\"Text\":\"x\"}", type: "ReplayApiSuite.Removed"),
        At(3_000, chat, "{\"Text\":\"c\"}"),
      }
    );
    void Expect(string message, params string[] calls)
    {
      Assert(overlay.Calls.SequenceEqual(calls), message + ": " + string.Join(", ", overlay.Calls));
      overlay.Calls.Clear();
    }

    ReplaySession session = ReplaySessions.Begin("run", render: null, saved);
    Expect("The start value did not arrive at 0", "state 1 at 0");
    Assert(
      Logged(
        "Test.Overlay: this run has Removed events, but OverlayProvider has no OnReplayEvent(Removed e, long ticks), so they are skipped."
      ),
      "Events no OnReplayEvent takes were not reported."
    );
    ReplaySessions.AdvanceEvents(500, 10_000);
    Expect("An event arrived before its time");
    ReplaySessions.AdvanceEvents(1_500, 20_000);
    Expect("A late event did not get its time", "layout 2 at 15000");
    ReplaySessions.AdvanceEvents(3_000, 40_000);
    Expect("Events were missed or an unreadable one was handed over", "chat b at 30000", "chat c at 40000");
    Assert(
      Logged("Test.Overlay: a LayoutChanged event in this run can't be read"),
      "An unreadable event was not reported."
    );

    // A seek starts over: the start value, then every event up to there, all at the seek's time.
    ReplaySessions.SeekEvents(2_000, 50_000);
    Expect("A seek did not start over", "state 1 at 50000", "layout 2 at 50000", "chat b at 50000");
    ReplaySessions.AdvanceEvents(3_000, 60_000);
    Expect("The events after a seek did not follow", "chat c at 60000");
    ReplaySessions.SeekEvents(100, 70_000);
    Expect("A seek before the first event did not give the start value alone", "state 1 at 70000");
    ReplaySessions.AdvanceEvents(1_000, 80_000);
    Expect("The events after a seek back were not handed over again", "layout 2 at 80000");
    session.End();
    ReplaySessions.AdvanceEvents(5_000, 90_000);
    Expect("An event arrived after the replay ended");

    // Renders hand events over at their video time.
    ReplaySession render = ReplaySessions.Begin("run", new ReplayRender(1, 1, 60), saved);
    ReplaySessions.AdvanceEvents(3_000, replayTimeUs => replayTimeUs * 20);
    Expect(
      "A render did not hand events over at their video times",
      "state 1 at 0",
      "layout 2 at 20000",
      "chat b at 40000",
      "chat c at 60000"
    );
    render.End();

    // A provider unregistered since the replay began gets nothing more.
    ReplaySessions.Begin("run", render: null, saved);
    ReplayApi.Unregister("Test.Overlay");
    ReplaySessions.AdvanceEvents(5_000, 10_000);
    ReplaySessions.SeekEvents(1_000, 20_000);
    Expect("An unregistered provider got events", "state 1 at 0");
    ReplaySessions.Active.End();
    Console.WriteLine("PASS events reach mods at their times, and a seek starts over from the start value");
  }

  private static void UpgradesOldEventsThroughTheProvider()
  {
    Reset();
    var provider = new LayoutUpgradeProvider();
    ReplayApi.Register("Test.Layout", provider);
    RunStateModel old = RunStateModel.ForEvent(typeof(LayoutChangedV1));
    var saved = new StoredRunStates(
      null,
      new[]
      {
        new StoredRunEvent
        {
          StateId = "Test.Layout",
          EventType = RunStateTypeNames.Qualified(typeof(LayoutChanged)),
          ShapeId = old.ShapeId,
          ShapeJson = old.ShapeJson,
          ValueJson = "{\"Size\":\"4\"}",
          TimeUs = 10,
        },
      }
    );
    ReplaySessions.Begin("run", render: null, saved);
    ReplaySessions.AdvanceEvents(10, 100);
    Assert(provider.Sizes.SequenceEqual(new[] { 4 }), "UpgradeRunEvent did not read an older event.");
    ReplaySessions.Active.End();
    Console.WriteLine("PASS events saved with an older type are read through UpgradeRunEvent");
  }

  private static void ReportsUnreadableRunsAtRegistration(string root)
  {
    Reset();
    Open(root, "run-state-diagnostics.sqlite");
    ReplayApi.Register("Test.Speed", new OldSpeedProvider());
    SaveRun("old-speed", 0, RunStateCapture.Begin("old-speed").Close());
    ReplayApi.Register("Test.Speed", new NewSpeedWithoutUpgradeProvider());
    RunStateDiagnostics.Check(ReplayProviders.All.Single());
    Assert(
      Logged("Test.Speed: 1 saved run can't be read: SpeedNumber.Speed was string and is now double."),
      "Runs the new provider can't read were not reported at registration."
    );

    // Events saved with an older shape, and events of a type the provider no longer takes.
    RunStateModel old = RunStateModel.ForEvent(typeof(LayoutChangedV1));
    RunEventRecord OldEvent(string type) =>
      new RunEventRecord
      {
        StateId = "Test.Layout",
        EventType = type,
        ShapeId = old.ShapeId,
        ShapeJson = old.ShapeJson,
        ValueJson = "{\"Size\":\"4\"}",
      };
    SaveRun(
      "old-layout",
      1,
      new CapturedRunStates(
        null,
        new[] { OldEvent(RunStateTypeNames.Qualified(typeof(LayoutChanged))), OldEvent("ReplayApiSuite.Removed") }
      )
    );
    ReplayApi.Register("Test.Layout", new LayoutWithoutUpgradeProvider());
    RunStateDiagnostics.Check(ReplayProviders.Find("Test.Layout"));
    Assert(
      Logged("Test.Layout: LayoutChanged events in 1 saved run can't be read: LayoutChanged.Size was string"),
      "Events the provider can't read were not reported at registration."
    );
    Assert(
      Logged("Add UpgradeRunEvent(the old type) to LayoutWithoutUpgradeProvider, or replays skip those events."),
      "The event upgrade was not suggested."
    );
    Assert(
      Logged(
        "Test.Layout: saved runs have Removed events, but LayoutWithoutUpgradeProvider has no OnReplayEvent(Removed e, long ticks), so replays skip them."
      ),
      "Events no OnReplayEvent takes were not reported at registration."
    );
    Console.WriteLine("PASS providers learn at registration which saved runs and events they can't read");
  }

  private static void PublicApiMatchesTheContract()
  {
    // Bind the way another mod does: by name, without referencing TUFReplay.
    Type api = AppDomain
      .CurrentDomain.GetAssemblies()
      .First(assembly => assembly.GetName().Name == "TUFReplay")
      .GetType("TUFReplay.ReplayApi", throwOnError: true);
    Assert(api.GetField("ApiVersion").GetValue(null) is 2, "The API version changed.");
    var getEventStore =
      (Func<string, object>)Delegate.CreateDelegate(typeof(Func<string, object>), api.GetMethod("GetEventStore"));
    var register =
      (Func<string, object, bool>)
        Delegate.CreateDelegate(typeof(Func<string, object, bool>), api.GetMethod("Register"));
    var unregister = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), api.GetMethod("Unregister"));
    var replaying =
      (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), api.GetProperty("IsReplaying").GetGetMethod());
    var recording =
      (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), api.GetProperty("IsRecording").GetGetMethod());
    var takesOverlayVideos =
      (Func<bool>)
        Delegate.CreateDelegate(typeof(Func<bool>), api.GetProperty("RenderTakesOverlayVideos").GetGetMethod());
    var ffmpegPath =
      (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), api.GetProperty("RenderFfmpegPath").GetGetMethod());
    Reset();
    Assert(!takesOverlayVideos() && ffmpegPath() == null, "Overlay video support was reported outside a render.");
    Assert(register("Test.Overlay", new OverlayProvider()), "Registering through a delegate failed.");
    object store = getEventStore("Test.Overlay");
    var addEvent =
      (Func<object, bool>)
        Delegate.CreateDelegate(typeof(Func<object, bool>), store, store.GetType().GetMethod("AddEvent"));
    Assert(!recording(), "IsRecording was true outside a run.");
    Assert(!addEvent(new ChatSent()), "An event was added outside a run.");
    long now = 0;
    RunStateCapture capture = RunStateCapture.Begin("public-api", () => now);
    Assert(recording(), "IsRecording was false while a run recorded.");
    Assert(addEvent(new ChatSent { Text = "bound" }), "AddEvent through a delegate failed.");
    Assert(capture.Close().Events.Single().ValueJson == "{\"Text\":\"bound\"}", "The bound event was not saved.");
    Assert(!recording(), "IsRecording was still true after the run was saved.");
    RunStateCapture.Begin("public-api-dropped").Discard();
    Assert(!recording(), "IsRecording was still true after the run was dropped.");
    unregister("Test.Overlay");
    Assert(!replaying(), "IsReplaying was true outside a replay.");
    Console.WriteLine("PASS ReplayApi binds by reflection like other optional mod APIs");
  }

  private static void BindsProvidersWithoutAState()
  {
    Reset();
    var keyViewer = new KeyViewerProvider();
    Assert(ReplayApi.Register("Test.KeyViewer", keyViewer), "A provider without a state was refused.");
    ReplayProvider bound = ReplayProviders.All.Single();
    Assert(bound.Model == null, "A provider without a state got one.");
    Assert(
      Logged("Registered Test.KeyViewer (replay begin/end, keys, clock, render canvases, frame capture)"),
      "The registration log did not say what the provider takes part in."
    );
    Assert(RunStateCapture.Begin("stateless").Close().IsEmpty, "A provider without a state saved one.");
    Assert(
      !ReplayApi.Register("Test.Upgrade", new UpgradeWithoutStateProvider()),
      "An upgrade without a state was accepted."
    );
    Assert(Logged("UpgradeWithoutStateProvider.UpgradeRunState has no state to upgrade to"), "It was not explained.");
    Console.WriteLine("PASS ReplayApi takes providers that save no state");
  }

  private static void RefusesReplayMembersWithTheWrongSignature()
  {
    Reset();
    Assert(!ReplayApi.Register("Test.Clock", new IntClockProvider()), "OnReplayClock(int) was accepted.");
    Assert(
      Logged("IntClockProvider.OnReplayClock must be void OnReplayClock(long ticks)"),
      "The clock was not explained."
    );
    Assert(!ReplayApi.Register("Test.Key", new StringKeyProvider()), "OnReplayKey(string, ...) was accepted.");
    Assert(
      Logged(
        "StringKeyProvider.OnReplayKey must be void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks)"
      ),
      "The key method was not explained."
    );
    Assert(
      !ReplayApi.Register("Test.Canvases", new IntCanvasesProvider()),
      "GetReplayCanvases returning int was accepted."
    );
    Assert(!ReplayApi.Register("Test.Begin", new TwoBoolBeginProvider()), "OnReplayBegin(bool, bool) was accepted.");
    Assert(
      !ReplayApi.Register("Test.StateBegin", new StateBeginProvider()),
      "OnReplayBegin(CountState, bool) was accepted."
    );
    Assert(
      Logged(
        "StateBeginProvider.OnReplayBegin must be void OnReplayBegin() or void OnReplayBegin(bool render). The run's state no longer comes with it: take it in void OnReplayState(CountState state, long ticks)"
      ),
      "A begin that takes the state was not explained."
    );
    Assert(!ReplayApi.Register("Test.Event", new TicklessEventProvider()), "OnReplayEvent without ticks was accepted.");
    Assert(
      Logged("TicklessEventProvider.OnReplayEvent must be void OnReplayEvent(YourEvent e, long ticks)"),
      "The event method was not explained."
    );
    Assert(!ReplayApi.Register("Test.OldEvent", new OldEventProvider()), "The old OnReplayEvent(state) was accepted.");
    Assert(
      Logged(
        "OldEventProvider.OnReplayEvent(CountState e, long ticks) no longer gets the run's start value. Take it in void OnReplayState(CountState state, long ticks)"
      ),
      "The move to OnReplayState was not explained."
    );
    Assert(
      !ReplayApi.Register("Test.NoState", new EventsWithoutStateProvider()),
      "Events without OnReplayState were accepted."
    );
    Assert(
      Logged(
        "EventsWithoutStateProvider takes events but has no OnReplayState. Add void OnReplayState(YourState state, long ticks)"
      ),
      "Events without OnReplayState were not explained."
    );
    Assert(!ReplayApi.Register("Test.StructEvent", new StructEventProvider()), "A struct event was accepted.");
    Assert(
      Logged("StructEventProvider.OnReplayEvent(Point): The event type is Point. Use a class with fields"),
      "A struct event was not explained."
    );
    Assert(
      !ReplayApi.Register("Test.EventUpgrade", new WrongEventUpgradeProvider()),
      "An UpgradeRunEvent returning no event type was accepted."
    );
    Assert(
      Logged(
        "WrongEventUpgradeProvider.UpgradeRunEvent must take one older event and return a type OnReplayEvent takes (ChatSent)"
      ),
      "The event upgrade was not explained."
    );
    Assert(
      !ReplayApi.Register("Test.Videos", new RectOverlayVideosProvider()),
      "AddReplayOverlayVideos(Action<string, Rect, Rect>) was accepted."
    );
    Assert(
      Logged(
        "RectOverlayVideosProvider.AddReplayOverlayVideos must be void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)"
      ),
      "The overlay video method was not explained."
    );
    Console.WriteLine("PASS ReplayApi explains replay methods with the wrong signature");
  }

  private static void DispatchesTheReplayToProviders()
  {
    Reset();
    var keyViewer = new KeyViewerProvider();
    ReplayApi.Register("Test.KeyViewer", keyViewer);

    ReplaySession render = ReplaySessions.Begin("run", new ReplayRender(1280, 720, 60), null);
    Assert(keyViewer.Begun == 1 && keyViewer.Render, "OnReplayBegin(bool) did not get the render flag.");
    Assert(ReplayApi.RenderWidth == 1280 && ReplayApi.RenderFps == 60, "The API did not report the video.");
    ReplaySessions.Clock(1000);
    ReplaySessions.Clock(400);
    Assert(keyViewer.Ticks == 1000 && ReplayApi.ReplayTicks == 1000, "The replay clock went backwards.");
    ReplaySessions.Key(KeyCode.A, 0, true, 900);
    Assert(keyViewer.Keys.Single() == (KeyCode.A, 0, true, 900L), "OnReplayKey did not get the key.");
    var canvases = new List<Canvas>();
    ReplaySessions.CollectCanvases(canvases);
    ReplaySessions.BeforeCapture();
    ReplaySessions.AfterCapture();
    Assert(keyViewer.CanvasRequests == 1 && keyViewer.Captures == 2, "Render calls did not reach the provider.");
    render.End();
    Assert(keyViewer.Ended == 1 && !ReplayApi.IsReplaying, "OnReplayEnd was not called.");

    ReplaySessions.Begin("run", render: null, null);
    Assert(!keyViewer.Render && ReplayApi.RenderWidth == 0, "An in-game replay looked like a render.");
    ReplaySessions.CollectCanvases(canvases);
    ReplaySessions.BeforeCapture();
    Assert(keyViewer.CanvasRequests == 1 && keyViewer.Captures == 2, "An in-game replay asked for render canvases.");
    ReplaySessions.TickInGame();
    Assert(keyViewer.Ticks >= 0 && ReplaySessions.NowTicks >= keyViewer.Ticks, "The in-game clock did not run.");
    ReplaySessions.Active.End();
    Console.WriteLine("PASS ReplayApi hands clock, keys, canvases and capture calls to providers");
  }

  private static void LimitsLogsFromFailingFrameCalls()
  {
    Reset();
    ReplayApi.Register("Test.Throwing", new ThrowingClockProvider());
    ReplaySession session = ReplaySessions.Begin("run", new ReplayRender(1, 1, 1), null);
    for (int frame = 0; frame < 10; frame++)
      ReplaySessions.Clock(frame);
    session.End();
    int logged;
    lock (Messages)
      logged = Messages.Count(message =>
        message.Contains("ThrowingClockProvider.OnReplayClock threw", StringComparison.Ordinal)
      );
    Assert(logged == 3, "A failing per-frame call was logged " + logged + " times instead of 3.");
    Console.WriteLine("PASS a failing per-frame provider call is logged a few times, not every frame");
  }

  private static void FeedsRecordedKeysAtTheirTimes()
  {
    Reset();
    var keyViewer = new KeyViewerProvider();
    ReplayApi.Register("Test.KeyViewer", keyViewer);
    ReplaySession session = ReplaySessions.Begin("run", new ReplayRender(1, 1, 1), null);
    NativeInputKeyCodeMapper.TryConvertLogicalKey(LogicalKeyboardKey.A, out int aCode);
    NativeInputKeyCodeMapper.TryConvertLogicalKey(LogicalKeyboardKey.B, out int bCode);
    // Native codes reach mods only on Windows, where they match SkyHook's.
    bool windows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
      System.Runtime.InteropServices.OSPlatform.Windows
    );
    int? a = windows ? aCode : null;
    int? b = windows ? bCode : null;
    var feed = new ReplayKeyFeed(
      new[]
      {
        new RecordedInput(1_000, aCode, RecordInputFlags.Down),
        new RecordedInput(2_000, aCode, 0),
        new RecordedInput(3_000, bCode, RecordInputFlags.Down),
      }
    );

    feed.AdvanceTo(1_500, 20_000);
    Assert(keyViewer.Keys.Single() == (KeyCode.A, a, true, 15_000L), "A late key did not get its recorded time.");
    feed.AdvanceTo(3_500, 40_000);
    Assert(
      keyViewer
        .Keys.Skip(1)
        .SequenceEqual(
          new (KeyCode, int?, bool, long)[] { (KeyCode.A, a, false, 25_000L), (KeyCode.B, b, true, 35_000L) }
        ),
      "Keys arrived out of order or at the wrong time."
    );
    feed.SkipTo(500, 50_000);
    Assert(keyViewer.Keys.Last() == (KeyCode.B, b, false, 50_000L), "Seeking did not release the held key.");
    feed.AdvanceTo(1_000, 60_000);
    Assert(keyViewer.Keys.Last() == (KeyCode.A, a, true, 60_000L), "The keys after a seek were not sent again.");
    feed.ReleaseAll(70_000);
    Assert(keyViewer.Keys.Last() == (KeyCode.A, a, false, 70_000L), "The end of a replay left a key held.");
    session.End();
    Console.WriteLine("PASS recorded keys reach mods at their recorded times and seeking releases held keys");
  }

  private static void TellsProvidersWhatTheRendererTakes()
  {
    Reset();
    var seen = new RendererReadingProvider();
    ReplayApi.Register("Test.Renderer", seen);
    ReplaySession render = ReplaySessions.Begin(
      "run",
      new ReplayRender(1280, 720, 30, takesOverlayVideos: true, ffmpegPath: "/tools/ffmpeg"),
      null
    );
    Assert(
      seen.TakesOverlayVideos && seen.FfmpegPath == "/tools/ffmpeg",
      "OnReplayBegin(render: true) could not read what the renderer takes."
    );
    // The renderer settles the video's size later; what it takes stays.
    ReplaySessions.UpdateRender(new ReplayRender(1920, 1080, 60));
    Assert(
      ReplayApi.RenderWidth == 1920
        && ReplayApi.RenderFps == 60
        && ReplayApi.RenderTakesOverlayVideos
        && ReplayApi.RenderFfmpegPath == "/tools/ffmpeg",
      "Settling the video size forgot what the renderer takes."
    );
    render.End();
    Assert(
      !ReplayApi.RenderTakesOverlayVideos && ReplayApi.RenderFfmpegPath == null,
      "Overlay video support outlived the render."
    );

    ReplaySessions.Begin("run", new ReplayRender(1280, 720, 30), null);
    Assert(
      !seen.TakesOverlayVideos && seen.FfmpegPath == null,
      "A renderer without overlay videos was reported as taking them."
    );
    ReplaySessions.Active.End();
    ReplaySessions.Begin("run", render: null, null);
    Assert(!seen.TakesOverlayVideos, "An in-game replay was reported as taking overlay videos.");
    ReplaySessions.Active.End();
    Console.WriteLine("PASS providers learn in OnReplayBegin whether the renderer takes overlay videos");
  }

  private static void CollectsOverlayVideosAfterTheLastFrame(string root)
  {
    Reset();
    string directory = Path.Combine(root, "overlay-videos");
    Directory.CreateDirectory(directory);
    string hand = Path.Combine(directory, "hand.mov");
    string foot = Path.Combine(directory, "foot.mov");
    File.WriteAllText(hand, "video");
    File.WriteAllText(foot, "video");
    var source = new RectInt(30, 30, 585, 180);
    var target = new RectInt(40, 860, 585, 180);
    var videos = new OverlayVideosProvider
    {
      Videos =
      {
        (hand, source, target),
        (Path.Combine(directory, "missing.mov"), source, target),
        ("", source, target),
        ("relative.mov", source, target),
        (foot, new RectInt(0, 0, 0, 180), target),
        (foot, source, new RectInt(0, 0, 585, -1)),
        (foot, new RectInt(0, 0, 400, 100), new RectInt(1500, 900, 200, 50)),
      },
    };
    Assert(ReplayApi.Register("Test.Videos", videos), "A provider with overlay videos was refused.");
    Assert(Logged("Registered Test.Videos (overlay videos)"), "The registration log did not mention overlay videos.");
    var throwing = new ThrowingOverlayVideosProvider();
    ReplayApi.Register("Test.ThrowingVideos", throwing);

    var collected = new List<ReplayOverlayVideo>();
    ReplaySessions.Begin("run", render: null, null);
    ReplaySessions.CollectOverlayVideos(collected);
    Assert(videos.Calls == 0 && collected.Count == 0, "An in-game replay asked for overlay videos.");
    ReplaySessions.Active.End();

    ReplaySession render = ReplaySessions.Begin("run", new ReplayRender(1920, 1080, 60, true, "/tools/ffmpeg"), null);
    ReplaySessions.CollectOverlayVideos(collected);
    Assert(videos.Calls == 1 && throwing.Calls == 1, "Providers were not asked for their overlay videos.");
    Assert(
      collected
        .Select(video => (video.ProviderId, video.Path, video.Source, video.Target))
        .SequenceEqual(
          new[]
          {
            ("Test.Videos", hand, source, target),
            ("Test.Videos", foot, new RectInt(0, 0, 400, 100), new RectInt(1500, 900, 200, 50)),
          }
        ),
      "Overlay videos were not collected in order with their rectangles."
    );
    Assert(Logged("missing.mov was left out: the file does not exist."), "A missing file was not explained.");
    Assert(Logged("overlay video  was left out: the path is empty."), "An empty path was not explained.");
    Assert(Logged("relative.mov was left out: the path is not absolute."), "A relative path was not explained.");
    Assert(Logged("was left out: the source is 0x180 pixels."), "An empty source was not explained.");
    Assert(Logged("was left out: the target is 585x-1 pixels."), "A negative target was not explained.");
    Assert(
      Logged("ThrowingOverlayVideosProvider.AddReplayOverlayVideos threw"),
      "A provider that threw was not reported."
    );

    // The callback only takes videos during the call.
    videos.Late(hand, source, target);
    Assert(collected.Count == 2, "A video added after the call was collected.");
    Assert(
      Logged("was left out: it was added after AddReplayOverlayVideos returned."),
      "A late video was not explained."
    );
    render.End();

    var afterEnd = new List<ReplayOverlayVideo>();
    ReplaySessions.CollectOverlayVideos(afterEnd);
    Assert(videos.Calls == 1 && afterEnd.Count == 0, "Overlay videos were collected after the render ended.");
    Console.WriteLine("PASS renders collect providers' overlay videos once and explain the ones left out");
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

  private static void SaveRun(string id, int index, CapturedRunStates states)
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

  public sealed class LayoutChanged
  {
    public int Size;
  }

  public sealed class LayoutChangedV1
  {
    public string Size;
  }

  public sealed class ChatSent
  {
    public string Text;
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

  private sealed class KeyViewerProvider
  {
    public int Begun;
    public bool Render;
    public int Ended;
    public long Ticks;
    public readonly List<(KeyCode, int?, bool, long)> Keys = new List<(KeyCode, int?, bool, long)>();
    public int CanvasRequests;
    public int Captures;

    public void OnReplayBegin(bool render)
    {
      Begun++;
      Render = render;
    }

    public void OnReplayEnd() => Ended++;

    public void OnReplayClock(long ticks) => Ticks = ticks;

    public void OnReplayKey(KeyCode key, int? nativeKey, bool down, long ticks) =>
      Keys.Add((key, nativeKey, down, ticks));

    public IEnumerable<Canvas> GetReplayCanvases()
    {
      CanvasRequests++;
      yield break;
    }

    public void OnBeforeFrameCapture() => Captures++;

    public void OnAfterFrameCapture() => Captures++;
  }

  private sealed class UpgradeWithoutStateProvider
  {
    public void OnReplayEnd() { }

    public CountState UpgradeRunState(MaybeCountState old) => new CountState();
  }

  private sealed class IntClockProvider
  {
    public void OnReplayClock(int ticks) { }
  }

  private sealed class StringKeyProvider
  {
    public void OnReplayKey(string key, bool down, long ticks) { }
  }

  private sealed class IntCanvasesProvider
  {
    public int GetReplayCanvases() => 0;
  }

  private sealed class TwoBoolBeginProvider
  {
    public void OnReplayBegin(bool render, bool again) { }
  }

  private sealed class StateBeginProvider
  {
    public CountState CaptureRunState() => new CountState();

    public void OnReplayBegin(CountState recorded, bool render) { }
  }

  private sealed class TicklessEventProvider
  {
    public void OnReplayEvent(CountState value) { }
  }

  private sealed class ThrowingClockProvider
  {
    public void OnReplayClock(long ticks) => throw new InvalidOperationException("clock");
  }

  private sealed class RectOverlayVideosProvider
  {
    public void AddReplayOverlayVideos(Action<string, Rect, Rect> add) { }
  }

  private sealed class RendererReadingProvider
  {
    public bool TakesOverlayVideos;
    public string FfmpegPath;

    public void OnReplayBegin(bool render)
    {
      TakesOverlayVideos = ReplayApi.RenderTakesOverlayVideos;
      FfmpegPath = ReplayApi.RenderFfmpegPath;
    }
  }

  private sealed class OverlayVideosProvider
  {
    public readonly List<(string, RectInt, RectInt)> Videos = new List<(string, RectInt, RectInt)>();
    public int Calls;
    private Action<string, RectInt, RectInt> _add;

    public void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)
    {
      Calls++;
      _add = add;
      foreach ((string path, RectInt source, RectInt target) in Videos)
        add(path, source, target);
    }

    public void Late(string path, RectInt source, RectInt target) => _add(path, source, target);
  }

  private sealed class ThrowingOverlayVideosProvider
  {
    public int Calls;

    public void AddReplayOverlayVideos(Action<string, RectInt, RectInt> add)
    {
      Calls++;
      throw new InvalidOperationException("videos");
    }
  }

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

    public void OnReplayState(MaybeCountState state, long ticks) { }
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
    public readonly List<string> Calls = new List<string>();

    public CountState CaptureRunState() => new CountState { Count = Next };

    public void OnReplayBegin(bool render)
    {
      Calls.Clear();
      Calls.Add("begin");
      Render = render;
    }

    public void OnReplayState(CountState state, long ticks)
    {
      Calls.Add("state " + (state?.Count.ToString() ?? "null") + " at " + ticks);
      Recorded = state;
    }

    public void OnReplayEnd()
    {
      Ends++;
      ReplayingDuringEnd = ReplayApi.IsReplaying;
    }
  }

  private sealed class StateOnlyProvider
  {
    public static SpeedNumber LastState;
    public static int Calls;

    public void OnReplayState(SpeedNumber state, long ticks)
    {
      Calls++;
      LastState = state;
    }
  }

  // Adds events during a run and takes the start value and events back.
  private sealed class OverlayProvider
  {
    public int Start;
    public readonly List<string> Calls = new List<string>();

    public CountState CaptureRunState() => new CountState { Count = Start };

    public void OnReplayState(CountState state, long ticks) =>
      Calls.Add("state " + (state?.Count.ToString() ?? "null") + " at " + ticks);

    public void OnReplayEvent(LayoutChanged e, long ticks) => Calls.Add("layout " + e.Size + " at " + ticks);

    public void OnReplayEvent(ChatSent e, long ticks) => Calls.Add("chat " + e.Text + " at " + ticks);
  }

  private sealed class LayoutUpgradeProvider
  {
    public readonly List<int> Sizes = new List<int>();

    public void OnReplayState(CountState state, long ticks) { }

    public void OnReplayEvent(LayoutChanged e, long ticks) => Sizes.Add(e.Size);

    public LayoutChanged UpgradeRunEvent(LayoutChangedV1 old) => new LayoutChanged { Size = int.Parse(old.Size) };
  }

  private sealed class LayoutWithoutUpgradeProvider
  {
    public void OnReplayState(CountState state, long ticks) { }

    public void OnReplayEvent(LayoutChanged e, long ticks) { }
  }

  // The ApiVersion 2 shape before events had their own types.
  private sealed class OldEventProvider
  {
    public CountState CaptureRunState() => new CountState();

    public void OnReplayEvent(CountState value, long ticks) { }
  }

  private sealed class EventsWithoutStateProvider
  {
    public void OnReplayEvent(ChatSent e, long ticks) { }
  }

  private sealed class StructEventProvider
  {
    public void OnReplayState(CountState state, long ticks) { }

    public void OnReplayEvent(Point e, long ticks) { }
  }

  private sealed class WrongEventUpgradeProvider
  {
    public void OnReplayState(CountState state, long ticks) { }

    public void OnReplayEvent(ChatSent e, long ticks) { }

    public CountState UpgradeRunEvent(LayoutChangedV1 old) => new CountState();
  }

  // Saves a start value but takes nothing back, so only the type comes from CaptureRunState.
  private sealed class RecordOnlyProvider
  {
    public CountState CaptureRunState() => null;
  }
}
