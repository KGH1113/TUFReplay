using TUFReplay.Replay.Compatibility;
using static TestFixture;

internal static class ElementAttemptsSuite
{
  internal static void RunAll()
  {
    SetsTheReplayRuleThroughTheApi();
    IgnoresMissingOrOtherApiVersions();
    FindsTheCounterUnderBothModNames();
    Console.WriteLine("TUFReplay Element attempt tests passed.");
  }

  private static void SetsTheReplayRuleThroughTheApi()
  {
    SupportedCounter.Reset();
    Assert(ElementAttempts.TryRegister(typeof(SupportedCounter)), "The supported attempt API was not used.");
    Assert(SupportedCounter.Owner == "TUFReplay", "The skip rule was not set under TUFReplay's name.");
    Assert(
      SupportedCounter.Skip == ElementAttempts.SkipReplays,
      "The skip rule does not read whether a replay is installed."
    );
    Assert(ElementAttempts.TryRegister(typeof(SupportedCounter)), "Setting the rule again failed.");
    Assert(SupportedCounter.Calls == 2, "Setting the rule again did not reach the API.");
  }

  private static void IgnoresMissingOrOtherApiVersions()
  {
    Assert(!ElementAttempts.TryRegister(null), "A missing attempt API was treated as available.");
    Assert(!ElementAttempts.TryRegister(typeof(NewerCounter)), "A different API version was used.");
    Assert(NewerCounter.Calls == 0, "A different API version received the rule.");
    Assert(!ElementAttempts.TryRegister(typeof(CounterWithoutRule)), "An API without SetSkipRule was used.");
  }

  private static void FindsTheCounterUnderBothModNames()
  {
    Assert(
      ElementAttempts.ApiTypeNameFor("Element") == "Element.AttemptCounter",
      "Element's attempt counter was not looked up under its current name."
    );
    Assert(
      ElementAttempts.ApiTypeNameFor("ImplResourcePack") == "ImplResourcePack.AttemptCounter",
      "Versions from before the rename to Element were no longer looked up."
    );
    Assert(ElementAttempts.ApiTypeNameFor("ElementNote") == null, "An unrelated assembly was treated as Element.");
    Assert(ElementAttempts.ApiTypeNameFor(null) == null, "An unnamed assembly was treated as Element.");
  }

  public static class SupportedCounter
  {
    public const int ApiVersion = ElementAttempts.SupportedApiVersion;
    public static string Owner;
    public static Func<bool> Skip;
    public static int Calls;

    public static void SetSkipRule(string owner, Func<bool> skip)
    {
      Owner = owner;
      Skip = skip;
      Calls++;
    }

    public static void Reset()
    {
      Owner = null;
      Skip = null;
      Calls = 0;
    }
  }

  public static class NewerCounter
  {
    public const int ApiVersion = ElementAttempts.SupportedApiVersion + 1;
    public static int Calls;

    public static void SetSkipRule(string owner, Func<bool> skip) => Calls++;
  }

  public static class CounterWithoutRule
  {
    public const int ApiVersion = ElementAttempts.SupportedApiVersion;
  }
}
