using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TUFReplay.ModApi.Playback;
using UnityEngine;

namespace TUFReplay.Replay.Rendering;

/// <summary>
/// The host OrbitRender drives while it renders a TUFReplay recording (OrbitRender RenderApi 3,
/// <c>StartHostedReplay</c>). OrbitRender owns the clock, frame capture, audio and encoding;
/// this owns the replay: the recorded settings, inputs, hits, clear, death and end, and what
/// mods see through <see cref="ReplayApi"/>.
/// </summary>
/// <remarks>
/// OrbitRender binds the public members below by name and calls them on Unity's main thread, in
/// this order: <see cref="PrepareBeforeRender"/>, <see cref="Begin"/>, then per frame
/// <see cref="BeforeSimulationFrame"/> and <see cref="AfterSimulationFrame"/> around the game's
/// simulation, <see cref="End"/> and <see cref="RestoreAfterRender"/>. The replay logic follows
/// TUFReplay-Renderer's recorded replay driver.
/// </remarks>
internal sealed class RenderReplayHost
{
  private static readonly FieldInfo RawSongPosition = AccessTools.Field(typeof(scrConductor), "_songposition_minusi");
  private static readonly FieldInfo FreeroamUpTime = AccessTools.Field(typeof(scrController), "freeroamUpTime");

  private readonly RecordingBundle _bundle;
  private readonly ReplayEventCursor _cursor;
  private readonly double _endDelaySeconds;
  private readonly Dictionary<string, KeyCode> _keyCodes = new Dictionary<string, KeyCode>(StringComparer.Ordinal);
  private readonly Dictionary<string, HitMargin> _margins = new Dictionary<string, HitMargin>(StringComparer.Ordinal);
  private readonly HashSet<KeyCode> _heldKeys = new HashSet<KeyCode>();
  private readonly List<Canvas> _canvases = new List<Canvas>();

  private GameSettingsSnapshot _settings;
  private RenderTimeline _timeline;
  private Func<bool> _isCancelled;
  private Action<double> _setEndTimeSeconds;
  private Action<double> _requestStopAfterSeconds;
  private int _videoFps;
  private bool _prepared;
  private bool _begun;
  private bool _ended;
  private bool _won;
  private bool _terminalReached;
  private bool _deathAnimationCompleted;
  private long? _deathStartedVideoTimeUs;
  private RecordedHitEvent? _activeHit;
  private long _currentVideoTimeUs;

  /// <summary>The host a render is replaying with, or null.</summary>
  internal static RenderReplayHost Current { get; private set; }

  /// <summary>True while a render replays a TUFReplay recording.</summary>
  internal static bool IsActive => Current != null;

  public RenderReplayHost(RecordingBundle bundle, double endDelaySeconds)
  {
    _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
    if (
      double.IsNaN(endDelaySeconds)
      || double.IsInfinity(endDelaySeconds)
      || endDelaySeconds < 0
      || endDelaySeconds > 30
    )
      throw new ArgumentOutOfRangeException(nameof(endDelaySeconds));
    _endDelaySeconds = endDelaySeconds;
    GameplayRate = bundle.Manifest.Replay.EffectivePitch;
    _cursor = new ReplayEventCursor(bundle.Inputs, bundle.Hits);
    foreach (RecordedKeyEvent input in bundle.Inputs)
    {
      if (_keyCodes.ContainsKey(input.Key))
        continue;
      if (
        !Enum.TryParse(input.Key, false, out KeyCode code)
        || !Enum.IsDefined(typeof(KeyCode), code)
        || code == KeyCode.None
      )
        throw new RecordingFormatException(
          "render_input_key_unsupported",
          "The recording uses an unsupported key: " + input.Key + ".",
          "key"
        );
      _keyCodes.Add(input.Key, code);
    }
    foreach (RecordedHitEvent hit in bundle.Hits)
    {
      if (_margins.ContainsKey(hit.Margin))
        continue;
      if (!Enum.TryParse(hit.Margin, false, out HitMargin margin) || !Enum.IsDefined(typeof(HitMargin), margin))
        throw new RecordingFormatException(
          "render_hit_judgment_unsupported",
          "The recording uses an unsupported judgment: " + hit.Margin + ".",
          "margin"
        );
      _margins.Add(hit.Margin, margin);
    }
  }

  // ---- Members OrbitRender reads ----

  /// <summary>The video time OrbitRender's overlay clock shows; moved to each key's own time while it is applied.</summary>
  public long CurrentVideoTimeUs => _currentVideoTimeUs;

  public bool TerminalReached => _terminalReached;
  public long? GameplayStartVideoTimeUs { get; private set; }
  public double GameplayRate { get; }

  // ---- Members the gameplay hooks read ----

  internal bool CompetitiveMode => _bundle.Manifest.Replay.JudgmentSystem == "ModernCompetitive";
  internal float InputCalibrationSeconds => (float)(_bundle.Manifest.Replay.GameInputOffsetMs / 1000d);
  internal bool ApplyingRecordedHit => _activeHit.HasValue;
  internal bool ApplyingTerminalDeath { get; private set; }
  internal bool ApplyingFreeroam { get; private set; }
  internal RecordedHitEvent? ActiveHit => _activeHit;
  internal HitMargin ActiveMargin => _margins[_activeHit.Value.Margin];
  internal bool HasHeldKeys => _heldKeys.Count != 0;

  internal void DeathStarted()
  {
    if (ADOBase.controller?.playerOne?.alive == false && !_deathStartedVideoTimeUs.HasValue)
      _deathStartedVideoTimeUs = _currentVideoTimeUs;
  }

  internal void DeathAnimationCompleted() => _deathAnimationCompleted = true;

  // ---- Lifecycle ----

  /// <summary>
  /// The level is ready and the offline clock set; OrbitRender has not decoded the song yet.
  /// Applies the recorded settings and takes over gameplay. Mods get the recorded keys through
  /// <see cref="ReplayApi"/> only; mods that read live input see none.
  /// </summary>
  public void PrepareBeforeRender()
  {
    if (_prepared)
      return;
    if (scrController.coopMode)
      throw new RecordingFormatException("This recording format supports one player. Turn off co-op before rendering.");
    if (ADOBase.controller == null || ADOBase.conductor == null || ADOBase.conductor.song == null)
      throw new InvalidOperationException("The level is not ready to render. Open the recorded level and try again.");
    if (Current != null && Current != this)
      throw new InvalidOperationException("Another recorded render is already running.");
    Current = this;
    _settings = new GameSettingsSnapshot();
    RenderReplayHooks.Activate(this);
    _prepared = true;
    ApplyPreparedSettings();
  }

  /// <summary>Applies the recorded settings. The game reads some of them when the level starts.</summary>
  internal void ApplyPreparedSettings()
  {
    if (!_prepared)
      return;
    ReplayManifest replay = _bundle.Manifest.Replay;
    float pitch = checked((float)GameplayRate);
    if (float.IsInfinity(pitch) || pitch <= 0)
      throw new RecordingFormatException("The recorded playback speed is outside the game's supported range.");
    scrConductor conductor = ADOBase.conductor;
    if (conductor.song != null)
      conductor.song.pitch = pitch;
    if (conductor.song2 != null)
      conductor.song2.pitch = pitch;
    if (conductor.song3 != null)
      conductor.song3.pitch = pitch;
    if (scnEditor.instance != null)
      scnEditor.instance.playbackSpeed = 1f;
    ADOBase.controller.noFail = replay.NoFailMode;
    // The conductor reads the input offset when it starts, so the recorded one is set before play.
    int inputOffset = (int)Math.Round(replay.GameInputOffsetMs);
    if (scrConductor.currentPreset.inputOffset != inputOffset)
      scrConductor.currentPreset.inputOffset = inputOffset;
    if (!string.IsNullOrEmpty(replay.JudgmentDifficulty))
    {
      if (
        !Enum.TryParse(replay.JudgmentDifficulty, out Difficulty difficulty)
        || !Enum.IsDefined(typeof(Difficulty), difficulty)
      )
        throw new RecordingFormatException("The recording uses an unsupported judgment difficulty.");
      GCS.difficulty = difficulty;
    }
    RDC.auto = false;
  }

  /// <summary>
  /// The music is scheduled and the song position anchored to video frame zero.
  /// </summary>
  public void Begin(
    int width,
    int height,
    int videoFps,
    Func<bool> isCancelled,
    Action<double> setEndTimeSeconds,
    Action<double> requestStopAfterSeconds
  )
  {
    if (_begun || _ended)
      throw new InvalidOperationException("The recorded render has already been used.");
    if (!_prepared)
      throw new InvalidOperationException("PrepareBeforeRender must run before the render schedules its audio.");
    if (videoFps <= 0)
      throw new ArgumentOutOfRangeException(nameof(videoFps));
    _isCancelled = isCancelled ?? throw new ArgumentNullException(nameof(isCancelled));
    _setEndTimeSeconds = setEndTimeSeconds ?? throw new ArgumentNullException(nameof(setEndTimeSeconds));
    _requestStopAfterSeconds =
      requestStopAfterSeconds ?? throw new ArgumentNullException(nameof(requestStopAfterSeconds));
    _videoFps = videoFps;
    _begun = true;
    ReplaySessions.UpdateRender(new ReplayRender(width, height, videoFps));

    double initialReplayUs =
      (ADOBase.conductor.songposition_minusi - _bundle.Manifest.Replay.GameplayStartSongPosition) * 1_000_000d;
    long initialOriginUs = checked(-(long)Math.Round(initialReplayUs / GameplayRate));
    _timeline = new RenderTimeline(initialOriginUs, GameplayRate, _bundle.Manifest.Replay.WonTimeUs);

    // Input-event effects deduplicate by the original game frame, which recordings do not store.
    if (HasInputEventEffects(ADOBase.controller))
      Main.Instance?.Log(
        "[Render] Custom input-event effects need checking in the video: recordings do not keep the original game frame numbers."
      );
  }

  public void BeforeSimulationFrame(long frameIndex, double videoSeconds)
  {
    _currentVideoTimeUs = ToMicroseconds(videoSeconds);
    ReplaySessions.Clock(_currentVideoTimeUs * 10);
  }

  public void AfterSimulationFrame(long frameIndex, double videoSeconds)
  {
    if (_ended || _isCancelled())
      return;
    _currentVideoTimeUs = ToMicroseconds(videoSeconds);
    ReplaySessions.Clock(_currentVideoTimeUs * 10);
    ReplayManifest replay = _bundle.Manifest.Replay;
    scrController controller = ADOBase.controller;
    scrConductor conductor = ADOBase.conductor;
    if (controller == null || conductor == null)
      throw new InvalidOperationException("The game left the recorded level while rendering.");
    if (!GameplayStartVideoTimeUs.HasValue)
    {
      if (controller.state != States.PlayerControl)
      {
        // Countdown keys show before the player can be judged.
        long countdownUs = checked((long)Math.Floor(_timeline.OutputToReplay(_currentVideoTimeUs)));
        _cursor.AdvanceInputsTo(countdownUs, ApplyInput);
        return;
      }
      double recordingUs = (conductor.songposition_minusi - replay.GameplayStartSongPosition) * 1_000_000d;
      GameplayStartVideoTimeUs = checked(_currentVideoTimeUs - (long)Math.Round(recordingUs / GameplayRate));
      _timeline = new RenderTimeline(GameplayStartVideoTimeUs.Value, GameplayRate, replay.WonTimeUs);
      // Leave room for the native death callback; its completion below sets the final end.
      // A missing callback must fail instead of cutting the death or rendering forever.
      _setEndTimeSeconds(
        ReplayToVideoTimeUs(replay.TerminalTimeUs) / 1_000_000d
          + _endDelaySeconds
          + (replay.Result == "failed" ? 10 : 0)
          + 1d / _videoFps
      );
    }
    long timeUs = checked((long)Math.Floor(_timeline.OutputToReplay(_currentVideoTimeUs)));
    _cursor.AdvanceTo(timeUs, ApplyInput, ApplyHit);
    if (controller.state == States.PlayerControl)
    {
      ApplyingFreeroam = true;
      try
      {
        controller.UpdateFreeroam();
      }
      finally
      {
        ApplyingFreeroam = false;
      }
    }
    if (!_won && replay.WonTimeUs.HasValue && timeUs >= replay.WonTimeUs.Value)
    {
      _won = true;
      if (controller.state != States.Won)
        using (SongPositionScope(replay.WonTimeUs.Value))
          controller.OnLandOnPortal(controller.chosenPlanet, Portal.EndOfLevel, null);
    }
    if (timeUs < replay.TerminalTimeUs)
      return;
    if (!_terminalReached)
    {
      if (!_cursor.AllHitsConsumed)
        throw new InvalidOperationException("The render ended before all recorded judgments were applied.");
      _terminalReached = true;
      if (replay.Result == "failed" && controller.playerOne.alive)
      {
        // Missed or late inputs need not have an accepted hit. The recorded result decides,
        // including failures on safe and no-fail tiles.
        ApplyingTerminalDeath = true;
        try
        {
          using (SongPositionScope(replay.TerminalTimeUs))
            controller.playerOne.Die(hitbox: true);
        }
        finally
        {
          ApplyingTerminalDeath = false;
        }
        DeathStarted();
      }
    }
    if (replay.Result != "failed" || _deathAnimationCompleted)
      _requestStopAfterSeconds(_endDelaySeconds);
    else if (!_deathStartedVideoTimeUs.HasValue || _currentVideoTimeUs - _deathStartedVideoTimeUs.Value > 5_000_000)
      throw new InvalidOperationException(
        "The game's death animation did not finish. Check the installed gameplay mods and try rendering again."
      );
  }

  // ---- What the video shows ----

  public IEnumerable<Canvas> GetCaptureCanvases()
  {
    _canvases.Clear();
    ReplaySessions.CollectCanvases(_canvases);
    return _canvases;
  }

  public void OnBeforeCapture() => ReplaySessions.BeforeCapture();

  public void OnAfterCapture() => ReplaySessions.AfterCapture();

  // ---- End ----

  public void End(string finalState) => Dispose();

  public void RestoreAfterRender()
  {
    Dispose();
    _settings?.Restore();
    _settings = null;
  }

  /// <summary>Releases the game, input and overlay hooks once. Safe to call again.</summary>
  internal void Dispose()
  {
    if (_ended)
      return;
    _ended = true;
    try
    {
      foreach (KeyCode held in _heldKeys)
        ReplaySessions.Key(held, null, false, _currentVideoTimeUs * 10);
    }
    finally
    {
      _heldKeys.Clear();
      RenderReplayHooks.Deactivate(this);
      if (Current == this)
        Current = null;
    }
  }

  private long ReplayToVideoTimeUs(long replayTimeUs) =>
    checked((long)Math.Round(_timeline.ReplayToOutput(replayTimeUs)));

  private void ApplyInput(RecordedKeyEvent input)
  {
    KeyCode code = _keyCodes[input.Key];
    if (input.Down)
      _heldKeys.Add(code);
    else
      _heldKeys.Remove(code);
    long frameTimeUs = _currentVideoTimeUs;
    try
    {
      // Overlay clocks read the key's own time while it is delivered; the replay clock stays monotonic.
      _currentVideoTimeUs = ReplayToVideoTimeUs(input.TimeUs);
      ReplaySessions.Key(code, null, input.Down, checked(_currentVideoTimeUs * 10));
    }
    finally
    {
      _currentVideoTimeUs = frameTimeUs;
    }
  }

  private void ApplyHit(RecordedHitEvent hit)
  {
    scrController controller = ADOBase.controller;
    scrPlayer player = controller.playerOne;
    using (SongPositionScope(hit.TimeUs))
    {
      SynchronizeFreeroam(hit.FreeRoamSection);
      scrPlanet planet = player.planetarySystem.chosenPlanet;
      if (planet == null || planet.currfloor == null || planet.currfloor.seqID != hit.FloorId)
        throw new InvalidOperationException(
          "The recorded level no longer matches tile "
            + hit.FloorId
            + ". Check that the original level and required mods are installed."
        );
      bool previousAuto = RDC.auto;
      bool previousInfinite = controller.noFailInfiniteMargin;
      HitMarginLimit previousLimit = GCS.hitMarginLimit;
      scrFloor nextFloor = planet.currfloor.nextfloor;
      bool previousNextAuto = nextFloor != null && nextFloor.auto;
      try
      {
        _activeHit = hit;
        // Accepted recorded hits must not inherit live multipress penalties.
        player.consecMultipressCounter = 0;
        controller.multipressPenalty = false;
        RDC.auto = hit.RdcAuto;
        controller.noFailInfiniteMargin = hit.NoFailHit;
        // The resolved accepted hit decides; the viewer's perfect-only preference does not apply.
        GCS.hitMarginLimit = HitMarginLimit.None;
        player.midspinInfiniteMargin = hit.MidspinInfiniteMargin;
        if (player.failBar != null)
          player.failBar.overloadCounter = hit.OverloadCounter;
        // CachedAngle is the planet's absolute angle right before the original hit.
        planet.angle = hit.CachedAngle;
        planet.cachedAngle = hit.CachedAngle;
        planet.SetTargetExitAngle(hit.TargetExitAngle);
        if (nextFloor != null)
          nextFloor.auto = hit.NextFloorAuto;
        if (planet.currfloor.holdLength > -1 && planet.currfloor.holdRenderer != null)
          planet.currfloor.holdRenderer.Hit();
        player.Hit(null, hit.IsAuto);
      }
      finally
      {
        _activeHit = null;
        RDC.auto = previousAuto;
        controller.noFailInfiniteMargin = previousInfinite;
        GCS.hitMarginLimit = previousLimit;
        if (nextFloor != null)
          nextFloor.auto = previousNextAuto;
      }
    }
    player.planetarySystem.chosenPlanet?.Update_RefreshAngles();
  }

  private void SynchronizeFreeroam(int expectedSection)
  {
    scrController controller = ADOBase.controller;
    if (controller.curFreeRoamSection > expectedSection)
      throw new InvalidOperationException("The freeroam state no longer matches the recording.");
    while (controller.curFreeRoamSection < expectedSection)
    {
      int previousSection = controller.curFreeRoamSection;
      ApplyingFreeroam = true;
      try
      {
        if (FreeroamUpTime == null)
          throw new MissingFieldException(typeof(scrController).FullName, "freeroamUpTime");
        FreeroamUpTime.SetValue(controller, Math.Max((float)FreeroamUpTime.GetValue(controller), 0.10001f));
        controller.UpdateFreeroam();
      }
      finally
      {
        ApplyingFreeroam = false;
      }
      if (controller.curFreeRoamSection == previousSection)
        throw new InvalidOperationException(
          "The recorded freeroam transition could not be applied. Check the original level version."
        );
    }
  }

  private IDisposable SongPositionScope(long replayTimeUs)
  {
    scrConductor conductor = ADOBase.conductor;
    double songPosition = _bundle.Manifest.Replay.GameplayStartSongPosition + replayTimeUs / 1_000_000d;
    if (RawSongPosition == null)
      throw new MissingFieldException(typeof(scrConductor).FullName, "_songposition_minusi");
    double raw = (double)RawSongPosition.GetValue(conductor);
    double correction = raw - conductor.songposition_minusi;
    RawSongPosition.SetValue(conductor, songPosition + correction);
    return new RestoreAction(() => RawSongPosition.SetValue(conductor, raw));
  }

  private static long ToMicroseconds(double seconds) => checked((long)Math.Round(seconds * 1_000_000d));

  private static bool HasInputEventEffects(scrController controller)
  {
    if (controller.inputEventFfx == null)
      return false;
    foreach (var row in controller.inputEventFfx)
      if (row != null)
        foreach (var effect in row)
          if (effect != null)
            return true;
    return false;
  }

  private sealed class RestoreAction : IDisposable
  {
    private Action _action;

    public RestoreAction(Action action) => _action = action;

    public void Dispose()
    {
      Action restore = _action;
      _action = null;
      restore?.Invoke();
    }
  }

  /// <summary>The game settings a render changes, put back after it.</summary>
  private sealed class GameSettingsSnapshot
  {
    private readonly bool _auto = RDC.auto;
    private readonly scrController _controller = ADOBase.controller;
    private readonly bool _noFail = ADOBase.controller.noFail;
    private readonly bool _noFailInfinite = ADOBase.controller.noFailInfiniteMargin;
    private readonly Difficulty _difficulty = GCS.difficulty;
    private readonly scrConductor _conductor = ADOBase.conductor;
    private readonly float _pitch = ADOBase.conductor.song.pitch;
    private readonly float _pitch2 = ADOBase.conductor.song2 != null ? ADOBase.conductor.song2.pitch : 1f;
    private readonly float _pitch3 = ADOBase.conductor.song3 != null ? ADOBase.conductor.song3.pitch : 1f;
    private readonly scnEditor _editor = scnEditor.instance;
    private readonly float _editorSpeed = scnEditor.instance != null ? scnEditor.instance.playbackSpeed : 1f;
    private readonly int _inputOffset = scrConductor.currentPreset.inputOffset;

    public void Restore()
    {
      RDC.auto = _auto;
      GCS.difficulty = _difficulty;
      scrConductor.currentPreset.inputOffset = _inputOffset;
      if (_controller != null)
      {
        _controller.noFail = _noFail;
        _controller.noFailInfiniteMargin = _noFailInfinite;
      }
      if (_conductor != null)
      {
        if (_conductor.song != null)
          _conductor.song.pitch = _pitch;
        if (_conductor.song2 != null)
          _conductor.song2.pitch = _pitch2;
        if (_conductor.song3 != null)
          _conductor.song3.pitch = _pitch3;
      }
      if (_editor != null)
        _editor.playbackSpeed = _editorSpeed;
    }
  }
}
