using System;

namespace TUFReplay.Microphone.Playback;

// One playable slot of the queue: a clip that can be filled and started on the
// audio clock.
internal interface IReplayMicrophoneVoice
{
  bool IsPlaying { get; }
  int PositionFrames { get; }
  void Play(float[] samples, double startDspTime);
  void EndAt(double dspTime);
  void Stop();
}

internal readonly struct ReplayMicrophoneQueueStats
{
  public ReplayMicrophoneQueueStats(long chunks, long underruns, long lostVoices)
  {
    Chunks = chunks;
    Underruns = underruns;
    LostVoices = lostVoices;
  }

  public long Chunks { get; }

  // The queue ran out, because the main thread or the disk fell behind it.
  public long Underruns { get; }

  // A voice stopped on its own, such as after an audio device change.
  public long LostVoices { get; }
}

// Keeps recorded audio scheduled ahead of the audio clock. Consecutive chunks
// start exactly where the previous one ends. Everything here runs on the main
// thread, so a garbage collection or a long frame cannot stall the audio mixer;
// the mixer just keeps playing what is already scheduled.
internal sealed class ReplayMicrophoneChunkQueue
{
  // A stopped source may still be fading out of the mix, so its clip is not
  // rewritten until this much later.
  private const double VoiceReuseDelaySeconds = 0.05d;
  private const double PlayingCheckMarginSeconds = 0.05d;
  private const double StartMeasureMarginSeconds = 0.02d;

  private struct VoiceState
  {
    public bool Active;
    public double Start;
    public double End;
    public double FreeAt;
  }

  private readonly ReplayMicrophoneRenderer _renderer;
  private readonly IReplayMicrophoneVoice[] _voices;
  private readonly VoiceState[] _states;
  private readonly float[] _samples;
  private readonly int _chunkFrames;
  private readonly int _outputChannels;
  private readonly int _outputSampleRate;
  private readonly double _startLeadSeconds;
  private bool _restartPending = true;
  private double _runStart;
  private long _runChunks;
  private double _nextStart;
  private int _nextVoice;
  private bool _watchNextStart;
  private int _watchedVoice = -1;
  private double _watchedStart;
  private long _chunks;
  private long _underruns;
  private long _lostVoices;

  public ReplayMicrophoneChunkQueue(
    ReplayMicrophoneRenderer renderer,
    IReplayMicrophoneVoice[] voices,
    int chunkFrames,
    int outputChannels,
    int outputSampleRate,
    double startLeadSeconds
  )
  {
    _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    _voices = voices ?? throw new ArgumentNullException(nameof(voices));
    _states = new VoiceState[voices.Length];
    _chunkFrames = Math.Max(1, chunkFrames);
    _outputChannels = Math.Max(1, outputChannels);
    _outputSampleRate = Math.Max(1, outputSampleRate);
    _startLeadSeconds = Math.Max(0d, startLeadSeconds);
    _samples = new float[_chunkFrames * _outputChannels];
  }

  // The next chunk starts a new run from the renderer's schedule. What is
  // queued keeps playing until that chunk is ready, so a small move of the
  // schedule leaves no gap.
  public void Restart() => _restartPending = true;

  // Silences everything now.
  public void Stop(double now)
  {
    for (int i = 0; i < _voices.Length; i++)
    {
      if (!_states[i].Active)
        continue;
      _voices[i].Stop();
      _states[i].Active = false;
      _states[i].FreeAt = _states[i].Start <= now ? now + VoiceReuseDelaySeconds : now;
    }
    _restartPending = true;
  }

  // Schedules at most one chunk, so filling the queue is spread over frames.
  public void Pump(double now)
  {
    ReleaseFinishedVoices(now);
    if (HasLostVoice(now))
    {
      _lostVoices++;
      Stop(now);
    }
    else if (!_restartPending && _nextStart < now + _startLeadSeconds)
    {
      _underruns++;
      _restartPending = true;
    }

    bool newRun = _restartPending;
    double start = newRun ? AlignToOutputSample(now + _startLeadSeconds) : _nextStart;
    int voice = FindVoice(start, now, newRun);
    if (voice < 0 || !_renderer.TryRender(start, _samples, _outputChannels, continuous: !newRun))
      return;

    if (newRun)
    {
      HandOverTo(start);
      _runStart = start;
      _runChunks = 0L;
      _restartPending = false;
    }
    Play(voice, start);
  }

  // Compares the first started chunk's position with the audio clock once.
  public void WatchNextStart() => _watchNextStart = true;

  public bool TryMeasureStart(double now, out double errorSeconds)
  {
    errorSeconds = 0d;
    if (_watchedVoice < 0 || now < _watchedStart + StartMeasureMarginSeconds)
      return false;

    int voice = _watchedVoice;
    _watchedVoice = -1;
    VoiceState state = _states[voice];
    if (
      !state.Active
      || state.Start != _watchedStart
      || now >= state.End - StartMeasureMarginSeconds
      || !_voices[voice].IsPlaying
    )
      return false;
    errorSeconds = _voices[voice].PositionFrames / (double)_outputSampleRate - (now - state.Start);
    return true;
  }

  public ReplayMicrophoneQueueStats TakeStats()
  {
    var stats = new ReplayMicrophoneQueueStats(_chunks, _underruns, _lostVoices);
    _chunks = 0L;
    _underruns = 0L;
    _lostVoices = 0L;
    return stats;
  }

  private void ReleaseFinishedVoices(double now)
  {
    for (int i = 0; i < _voices.Length; i++)
    {
      if (!_states[i].Active || now < _states[i].End + VoiceReuseDelaySeconds)
        continue;
      _voices[i].Stop();
      _states[i].Active = false;
      _states[i].FreeAt = now;
    }
  }

  private bool HasLostVoice(double now)
  {
    for (int i = 0; i < _voices.Length; i++)
    {
      VoiceState state = _states[i];
      if (
        state.Active
        && now > state.Start + PlayingCheckMarginSeconds
        && now < state.End - PlayingCheckMarginSeconds
        && !_voices[i].IsPlaying
      )
        return true;
    }
    return false;
  }

  // A new run may also take over a voice whose chunk it replaces.
  private int FindVoice(double start, double now, bool newRun)
  {
    int replaceable = -1;
    for (int offset = 0; offset < _voices.Length; offset++)
    {
      int voice = (_nextVoice + offset) % _voices.Length;
      VoiceState state = _states[voice];
      if (!state.Active)
      {
        if (now >= state.FreeAt)
          return voice;
      }
      else if (newRun && replaceable < 0 && state.Start >= start)
      {
        replaceable = voice;
      }
    }
    return replaceable;
  }

  // Chunks of the old run end where the new run starts.
  private void HandOverTo(double start)
  {
    for (int i = 0; i < _voices.Length; i++)
    {
      if (!_states[i].Active)
        continue;
      if (_states[i].Start >= start)
      {
        _voices[i].Stop();
        _states[i].Active = false;
        _states[i].FreeAt = 0d;
      }
      else if (_states[i].End > start)
      {
        _voices[i].EndAt(start);
        _states[i].End = start;
      }
    }
  }

  private void Play(int voice, double start)
  {
    _voices[voice].Play(_samples, start);
    _runChunks++;
    _nextStart = _runStart + _runChunks * (double)_chunkFrames / _outputSampleRate;
    _states[voice] = new VoiceState
    {
      Active = true,
      Start = start,
      End = _nextStart,
    };
    _nextVoice = (voice + 1) % _voices.Length;
    _chunks++;
    if (_watchNextStart)
    {
      _watchNextStart = false;
      _watchedVoice = voice;
      _watchedStart = start;
    }
  }

  // A quarter sample past a whole output sample, so every later start lands on
  // the same sample whether the audio engine rounds or truncates the time.
  private double AlignToOutputSample(double dspTime) =>
    (Math.Ceiling(dspTime * _outputSampleRate) + 0.25d) / _outputSampleRate;
}
