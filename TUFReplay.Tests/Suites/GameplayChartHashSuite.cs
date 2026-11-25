using TUFReplay.Activity.Charts;
using static TestFixture;

// V5 gameplay hash: what changes judgments or run outcomes must change the hash, and
// what only changes how the level looks or sounds must not.
internal static class GameplayChartHashSuite
{
  internal static void RunAll()
  {
    TestSettingsAndAngles();
    TestEventSelectionAndOrder();
    TestTimingFieldsV4Missed();
    TestInputAndKillEvents();
    TestHitboxDecorations();
    TestHashMatching();
    Console.WriteLine("TUFReplay V5 gameplay hash tests passed.");
  }

  private static void TestSettingsAndAngles()
  {
    byte[] baseline = Hash(Chart());
    Assert(Same(Chart(), baseline), "The V5 hash is not deterministic.");
    Assert(!Same(Chart(c => c.Bpm = 321f), baseline), "BPM did not change the V5 hash.");
    Assert(!Same(Chart(c => c.Offset = 10), baseline), "Offset did not change the V5 hash.");
    Assert(!Same(Chart(c => c.CountdownTicks = 8), baseline), "Countdown ticks did not change the V5 hash.");
    Assert(
      !Same(Chart(c => c.SeparateCountdownTime = !c.SeparateCountdownTime), baseline),
      "Separate countdown time did not change the V5 hash."
    );

    Assert(
      Same(Chart(c => c.Angles = new[] { 360f, -90f, 999f, 180f, 450f }), baseline),
      "Angles naming the same direction changed the V5 hash."
    );
    Assert(
      !Same(Chart(c => c.Angles = new[] { 0f, 270f, 0f, 180f, 90f }), baseline),
      "A midspin turned into a normal tile kept the V5 hash."
    );
    Assert(!Same(Chart(c => c.Angles = new[] { 0f, 270f, 999f, 180f }), baseline), "A removed tile kept the V5 hash.");
    Assert(GameplayChartV5Hasher.NormalizeAngle(-0f) == 0f, "Negative zero survived angle normalization.");
  }

  private static void TestEventSelectionAndOrder()
  {
    byte[] baseline = Hash(Chart());

    Assert(
      Same(Chart(c => c.Events.Add(new GameplayChartItem("Flash", 1, true))), baseline),
      "An effect event changed the V5 hash."
    );
    Assert(
      Same(Chart(c => c.Events.Add(Speed(4, 2f, active: false))), baseline),
      "A disabled speed change changed the V5 hash."
    );
    Assert(
      Same(
        Chart(c =>
        {
          c.Events.Reverse();
        }),
        baseline
      ),
      "Reordering events on different tiles changed the V5 hash."
    );

    byte[] twoSpeeds = Hash(
      Chart(c =>
      {
        c.Events.Add(Speed(3, 2f));
        c.Events.Add(Speed(3, 0.5f));
      })
    );
    byte[] swappedSpeeds = Hash(
      Chart(c =>
      {
        c.Events.Add(Speed(3, 0.5f));
        c.Events.Add(Speed(3, 2f));
      })
    );
    Assert(!GameplayChartHash.Equals(twoSpeeds, swappedSpeeds), "The order of speed changes on one tile was lost.");

    Assert(
      Same(
        Chart(c => c.Events.Insert(0, new GameplayChartItem(GameplayChartV5Hasher.Twirl, 3, true))),
        Hash(Chart(c => c.Events.Add(new GameplayChartItem(GameplayChartV5Hasher.Twirl, 3, true))))
      ),
      "Where a twirl sat among other tiles' events changed the V5 hash."
    );
  }

  private static void TestTimingFieldsV4Missed()
  {
    byte[] baseline = Hash(Chart());
    Assert(
      !Same(Chart(c => c.Events[0].Set("angleOffset", 90f)), baseline),
      "A mid-tile speed change did not change the V5 hash."
    );
    Assert(
      Same(Chart(c => c.Events[0].Set("bpmMultiplier", 3f)), baseline),
      "The unused multiplier of a BPM speed change changed the V5 hash."
    );

    byte[] freeRoam = Hash(Chart(c => c.Events.Add(FreeRoam(2, 8))));
    Assert(!GameplayChartHash.Equals(freeRoam, baseline), "A free-roam tile did not change the V5 hash.");
    Assert(!Same(Chart(c => c.Events.Add(FreeRoam(2, 16))), freeRoam), "Free-roam length did not change the V5 hash.");
    Assert(
      !Same(
        Chart(c =>
        {
          c.Events.Add(FreeRoam(2, 8));
          c.Events.Add(
            new GameplayChartItem(GameplayChartV5Hasher.FreeRoamTwirl, 2, true).Set("position", new[] { 1f, 2f })
          );
        }),
        freeRoam
      ),
      "A free-roam twirl did not change the V5 hash."
    );

    byte[] auto = Hash(Chart(c => c.Events.Add(Auto(1, safety: false))));
    Assert(!Same(Chart(c => c.Events.Add(Auto(1, safety: true))), auto), "Safety tiles did not change the V5 hash.");
  }

  private static void TestInputAndKillEvents()
  {
    byte[] baseline = Hash(Chart());
    Assert(
      Same(Chart(c => c.Events.Add(Input(2, ignore: false, target: "flash"))), baseline),
      "An input event that only triggers effects changed the V5 hash."
    );
    Assert(
      !Same(Chart(c => c.Events.Add(Input(2, ignore: true, target: "flash"))), baseline),
      "An input event that swallows key presses did not change the V5 hash."
    );

    byte[] kill = Hash(Chart(c => c.Events.Add(Kill(2, "boom"))));
    Assert(!GameplayChartHash.Equals(kill, baseline), "A kill event did not change the V5 hash.");
    Assert(
      !Same(
        Chart(c =>
        {
          c.Events.Add(Kill(2, "boom"));
          c.Events.Add(Input(2, ignore: false, target: "boom"));
        }),
        kill
      ),
      "An input event that triggers a kill did not change the V5 hash."
    );
    Assert(
      Same(
        Chart(c =>
        {
          c.Events.Add(Kill(2, "boom"));
          c.Events.Add(Input(3, ignore: false, target: "boom"));
        }),
        kill
      ),
      "An input event on another tile was linked to the kill."
    );
    Assert(
      !Same(Chart(c => c.Events.Add(Kill(2, "boom").Set("angleOffset", 45f))), kill),
      "The kill's timing did not change the V5 hash."
    );
    Assert(
      Same(Chart(c => c.Events.Add(Kill(2, "NONE"))), Hash(Chart(c => c.Events.Add(Kill(2, ""))))),
      "A NONE tag was treated as a real tag."
    );
    Assert(GameplayChartV5Hasher.NormalizeTags("b NONE a b") == "a b", "Tags were not normalized as a set.");
  }

  private static void TestHitboxDecorations()
  {
    byte[] baseline = Hash(Chart());
    byte[] hitbox = Hash(Chart(c => c.HitboxDecorations.Add(Hitbox("wall"))));
    Assert(!GameplayChartHash.Equals(hitbox, baseline), "A kill hitbox decoration did not change the V5 hash.");
    Assert(
      !Same(Chart(c => c.HitboxDecorations.Add(Hitbox("wall").Set("position", new[] { 3f, 0f }))), hitbox),
      "Moving a hitbox decoration did not change the V5 hash."
    );
    Assert(
      Same(
        Chart(c =>
        {
          c.HitboxDecorations.Add(Hitbox("wall"));
          c.Events.Add(Move(2, "sky"));
        }),
        hitbox
      ),
      "Moving an ordinary decoration changed the V5 hash."
    );

    byte[] moved = Hash(
      Chart(c =>
      {
        c.HitboxDecorations.Add(Hitbox("wall"));
        c.Events.Add(Move(2, "wall"));
      })
    );
    Assert(!GameplayChartHash.Equals(moved, hitbox), "Moving a hitbox decoration by event kept the V5 hash.");
    Assert(
      !Same(
        Chart(c =>
        {
          c.HitboxDecorations.Add(Hitbox("wall"));
          c.Events.Add(Move(2, "wall").MarkAbsent("positionOffset"));
        }),
        moved
      ),
      "A move that leaves the position alone hashed like one that sets it."
    );
    Assert(
      Same(
        Chart(c =>
        {
          c.HitboxDecorations.Add(Hitbox("wall"));
          c.Events.Add(Move(2, "wall").Set("color", "ff0000ff"));
        }),
        moved
      ),
      "A decoration's color changed the V5 hash."
    );
  }

  private static void TestHashMatching()
  {
    byte[] v4 = Filled(1);
    byte[] v5 = Filled(2);
    var sameV5 = new GameplayChartHashes(Filled(3), v5);
    Assert(
      GameplayChartHashMatch.Matches(v4, GameplayChartHash.Version, v5, sameV5),
      "A chart with the stored V5 was rejected because its V4 differs."
    );
    Assert(
      !GameplayChartHashMatch.Matches(v4, GameplayChartHash.Version, v5, new GameplayChartHashes(v4, Filled(4))),
      "A chart with a different V5 matched through its V4."
    );
    Assert(
      GameplayChartHashMatch.Matches(v4, GameplayChartHash.Version, null, new GameplayChartHashes(v4, Filled(4))),
      "A run recorded before V5 rejected its own file."
    );
    Assert(
      !GameplayChartHashMatch.Matches(v4, GameplayChartHash.Version, null, sameV5),
      "A run recorded before V5 matched a chart with another V4."
    );
    Assert(
      GameplayChartHashMatch.CanTakeV5(v4, GameplayChartHash.Version, null, new GameplayChartHashes(v4, v5)),
      "The recorded file could not supply V5."
    );
    Assert(
      !GameplayChartHashMatch.CanTakeV5(v4, GameplayChartHash.Version, null, sameV5),
      "A file with another V4 supplied V5."
    );
    Assert(
      !GameplayChartHashMatch.CanTakeV5(v4, GameplayChartHash.Version, v5, new GameplayChartHashes(v4, Filled(5))),
      "A stored V5 was replaced."
    );
  }

  private static GameplayChartSnapshot Chart(Action<GameplayChartSnapshot> change = null)
  {
    var chart = new GameplayChartSnapshot
    {
      Bpm = 320f,
      Offset = 3687,
      SeparateCountdownTime = true,
      CountdownTicks = 4,
      Angles = new[] { 0f, 270f, 999f, 180f, 90f },
    };
    chart.Events.Add(Speed(1, 400f, bpm: true));
    chart.Events.Add(new GameplayChartItem(GameplayChartV5Hasher.Twirl, 2, true));
    chart.Events.Add(new GameplayChartItem(GameplayChartV5Hasher.Hold, 3, true).Set("duration", 1));
    change?.Invoke(chart);
    return chart;
  }

  private static GameplayChartItem Speed(int floor, float value, bool bpm = false, bool active = true) =>
    new GameplayChartItem(GameplayChartV5Hasher.SetSpeed, floor, active)
      .Set("speedType", bpm ? "Bpm" : "Multiplier")
      .Set("beatsPerMinute", bpm ? value : 100f)
      .Set("bpmMultiplier", bpm ? 1f : value)
      .Set("angleOffset", 0f);

  private static GameplayChartItem FreeRoam(int floor, int duration) =>
    new GameplayChartItem(GameplayChartV5Hasher.FreeRoam, floor, true)
      .Set("duration", duration)
      .Set("size", new[] { 4f, 4f })
      .Set("positionOffset", new[] { 0f, 0f })
      .Set("outTime", 4);

  private static GameplayChartItem Auto(int floor, bool safety) =>
    new GameplayChartItem(GameplayChartV5Hasher.AutoPlayTiles, floor, true)
      .Set("enabled", true)
      .Set("safetyTiles", safety);

  private static GameplayChartItem Input(int floor, bool ignore, string target) =>
    new GameplayChartItem(GameplayChartV5Hasher.SetInputEvent, floor, true)
      .Set("angleOffset", 0f)
      .Set("target", "Any")
      .Set("state", "Down")
      .Set("ignoreInput", ignore)
      .Set("targetEventTag", target);

  private static GameplayChartItem Kill(int floor, string tag) =>
    new GameplayChartItem(GameplayChartV5Hasher.KillPlayer, floor, true).Set("angleOffset", 0f).Set("eventTag", tag);

  private static GameplayChartItem Hitbox(string tag) =>
    new GameplayChartItem("AddDecoration", 1, true)
      .Set("relativeTo", "Tile")
      .Set("position", new[] { 1f, 0f })
      .Set("scale", new[] { 100f, 100f })
      .Set("tag", tag)
      .Set("hitbox", "Kill")
      .Set("failHitboxType", "Box")
      .Set("failHitboxScale", new[] { 100f, 100f });

  private static GameplayChartItem Move(int floor, string tag) =>
    new GameplayChartItem(GameplayChartV5Hasher.MoveDecorations, floor, true)
      .Set("tag", tag)
      .Set("duration", 1f)
      .Set("positionOffset", new[] { 2f, 0f })
      .Set("ease", "Linear");

  private static byte[] Hash(GameplayChartSnapshot chart) => GameplayChartV5Hasher.Compute(chart);

  private static bool Same(GameplayChartSnapshot chart, byte[] expected) =>
    GameplayChartHash.Equals(Hash(chart), expected);

  private static byte[] Filled(byte value) => Enumerable.Repeat(value, GameplayChartHash.HashSize).ToArray();
}
