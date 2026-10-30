using TUFReplay.Replay.Compatibility;
using static TestFixture;

internal static class ImplResourcePackAttemptsSuite
{
  internal static void RunAll()
  {
    SetsTheReplayRuleThroughTheApi();
    IgnoresMissingOrOtherApiVersions();
    Console.WriteLine("TUFReplay ImplResourcePack attempt tests passed.");
  }

  private static void SetsTheReplayRuleThroughTheApi()
  {
    SupportedCounter.Reset();
    Assert(ImplResourcePackAttempts.TryRegister(typeof(SupportedCounter)), "The supported attempt API was not used.");
    Assert(SupportedCounter.Owner == "TUFReplay", "The skip rule was not set under TUFReplay's name.");
    Assert(
      SupportedCounter.Skip == ImplResourcePackAttempts.SkipReplays,
      "The skip rule does not read whether a replay is installed."
    );
    Assert(ImplResourcePackAttempts.TryRegister(typeof(SupportedCounter)), "Setting the rule again failed.");
    Assert(SupportedCounter.Calls == 2, "Setting the rule again did not reach the API.");
  }

  private static void IgnoresMissingOrOtherApiVersions()
  {
    Assert(!ImplResourcePackAttempts.TryRegister(null), "A missing attempt API was treated as available.");
    Assert(!ImplResourcePackAttempts.TryRegister(typeof(NewerCounter)), "A different API version was used.");
    Assert(NewerCounter.Calls == 0, "A different API version received the rule.");
    Assert(!ImplResourcePackAttempts.TryRegister(typeof(CounterWithoutRule)), "An API without SetSkipRule was used.");
  }

  public static class SupportedCounter
  {
    public const int ApiVersion = ImplResourcePackAttempts.SupportedApiVersion;
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
    public const int ApiVersion = ImplResourcePackAttempts.SupportedApiVersion + 1;
    public static int Calls;

    public static void SetSkipRule(string owner, Func<bool> skip) => Calls++;
  }

  public static class CounterWithoutRule
  {
    public const int ApiVersion = ImplResourcePackAttempts.SupportedApiVersion;
  }
}
