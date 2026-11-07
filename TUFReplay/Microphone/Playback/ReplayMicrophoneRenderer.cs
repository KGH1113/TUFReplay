using System;

namespace TUFReplay.Microphone.Playback;

// Places the recording on the audio clock: WAV frame AnchorFrame plays at DSP
// time AnchorDspTime, and the recording advances one WAV second per DSP second.
internal sealed class ReplayMicrophoneSchedule
{
  public ReplayMicrophoneSchedule(double anchorDspTime, double anchorFrame)
  {
    AnchorDspTime = anchorDspTime;
    AnchorFrame = anchorFrame;
  }

  public double AnchorDspTime { get; }
  public double AnchorFrame { get; }

  public double FrameAt(double dspTime, int sampleRate) => AnchorFrame + (dspTime - AnchorDspTime) * sampleRate;
}

internal readonly struct ReplayMicrophoneRenderStats
{
  public ReplayMicrophoneRenderStats(long blocks, long missingFrames, long prefetchSeeks, long jumps)
  {
    Blocks = blocks;
    MissingFrames = missingFrames;
    PrefetchSeeks = prefetchSeeks;
    Jumps = jumps;
  }

  public long Blocks { get; }

  // Frames inside the recording that were not read in time and played silent.
  public long MissingFrames { get; }
  public long PrefetchSeeks { get; }

  // Blocks whose DSP time moved away from the previous block's end, such as
  // after a paused listener, so playback continued from the new position.
  public long Jumps { get; }
}

internal readonly struct ReplayMicrophoneFirstBlock
{
  public ReplayMicrophoneFirstBlock(double scheduleDspTime, double dspTime, double frame, int frames, int channels)
  {
    ScheduleDspTime = scheduleDspTime;
    DspTime = dspTime;
    Frame = frame;
    Frames = frames;
    Channels = channels;
  }

  public double ScheduleDspTime { get; }
  public double DspTime { get; }
  public double Frame { get; }
  public int Frames { get; }
  public int Channels { get; }
}

// Writes the recording into the audio blocks Unity mixes, on the audio thread.
// Each block's WAV position comes from the block's own DSP time, so playback
// starts on the scheduled sample and follows any jump of the clock without
// asking Unity to seek. Unity's streamed clips did not reliably apply a start
// position, and played the 800 ms of silence they had read ahead instead.
internal sealed class ReplayMicrophoneRenderer : IDisposable
{
  private const int InitialWindowFrames = 8192;

  private readonly object _gate = new object();
  private readonly Pcm16PrefetchBuffer _prefetch;
  private readonly int _sampleRate;
  private readonly int _channels;
  private readonly long _frameCount;
  private readonly int _frameBytes;

  // WAV frames per output frame.
  private readonly double _step;

  // Decoded WAV frames [_windowStart, _windowStart + _windowFrames). The
  // prefetch's next frame is always the one after the window.
  private float[] _window;
  private byte[] _bytes;
  private long _windowStart;
  private int _windowFrames;
  private int _generation;
  private ReplayMicrophoneSchedule _schedule;
  private ReplayMicrophoneSchedule _renderedSchedule;
  private double _cursor;
  private bool _cursorValid;
  private long _audibleFromFrame;
  private float _gain = 1f;
  private bool _disposed;
  private Exception _failure;
  private bool _firstBlockPending;
  private bool _firstBlockReady;
  private ReplayMicrophoneFirstBlock _firstBlock;
  private long _blocks;
  private long _missingFrames;
  private long _prefetchSeeks;
  private long _jumps;

  public ReplayMicrophoneRenderer(
    Pcm16PrefetchBuffer prefetch,
    int sampleRate,
    int channels,
    long frameCount,
    int outputSampleRate
  )
  {
    _prefetch = prefetch ?? throw new ArgumentNullException(nameof(prefetch));
    _sampleRate = Math.Max(1, sampleRate);
    _channels = Math.Max(1, channels);
    _frameCount = Math.Max(0L, frameCount);
    _frameBytes = _channels * 2;
    _step = _sampleRate / (double)Math.Max(1, outputSampleRate);
    _window = new float[InitialWindowFrames * _channels];
    _bytes = new byte[InitialWindowFrames * _frameBytes];
    _generation = prefetch.Generation;
  }

  public float Gain
  {
    set
    {
      lock (_gate)
        _gain = value;
    }
  }

  // Frames before this hold the wait of an EnhancedCountdown frozen start.
  public long AudibleFromFrame
  {
    set
    {
      lock (_gate)
        _audibleFromFrame = Math.Max(0L, value);
    }
  }

  public Exception Failure
  {
    get
    {
      lock (_gate)
      {
        if (_failure != null || _disposed)
          return _failure;
      }
      return _prefetch.Failure;
    }
  }

  // Null plays silence.
  public void Publish(ReplayMicrophoneSchedule schedule)
  {
    lock (_gate)
    {
      _schedule = schedule;
      if (schedule != null)
      {
        _firstBlockPending = true;
        _firstBlockReady = false;
      }
    }
  }

  // Moves the prefetch so the data from this frame on is read before a block
  // needs it. A frame already read ahead keeps the buffered data.
  public void Prime(long frame)
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      frame = Math.Max(0L, Math.Min(frame, _frameCount));
      long bufferedEnd = _windowStart + _windowFrames + _prefetch.BufferedBytes / _frameBytes;
      if (_generation == _prefetch.Generation && frame >= _windowStart && frame < bufferedEnd)
        return;
      SeekTo(frame);
    }
  }

  public void Render(float[] data, int outputChannels, double dspTime)
  {
    if (data == null)
      return;
    lock (_gate)
    {
      Array.Clear(data, 0, data.Length);
      if (_disposed || _failure != null)
        return;
      try
      {
        RenderLocked(data, outputChannels, dspTime);
      }
      catch (Exception exception)
      {
        _failure = exception;
        _schedule = null;
        Array.Clear(data, 0, data.Length);
      }
    }
  }

  public bool TryTakeFirstBlock(out ReplayMicrophoneFirstBlock block)
  {
    lock (_gate)
    {
      block = _firstBlock;
      if (!_firstBlockReady)
        return false;
      _firstBlockReady = false;
      return true;
    }
  }

  public ReplayMicrophoneRenderStats TakeStats()
  {
    lock (_gate)
    {
      var stats = new ReplayMicrophoneRenderStats(_blocks, _missingFrames, _prefetchSeeks, _jumps);
      _blocks = 0L;
      _missingFrames = 0L;
      _prefetchSeeks = 0L;
      _jumps = 0L;
      return stats;
    }
  }

  public void Dispose()
  {
    lock (_gate)
    {
      if (_disposed)
        return;
      _disposed = true;
      _schedule = null;
    }
    _prefetch.Dispose();
  }

  private void RenderLocked(float[] data, int outputChannels, double dspTime)
  {
    ReplayMicrophoneSchedule schedule = _schedule;
    if (schedule == null || outputChannels <= 0)
    {
      _cursorValid = false;
      return;
    }
    int outputFrames = data.Length / outputChannels;
    if (outputFrames == 0)
      return;
    _blocks++;

    double wanted = schedule.FrameAt(dspTime, _sampleRate);
    if (!_cursorValid || !ReferenceEquals(schedule, _renderedSchedule))
    {
      _cursor = AlignToOutput(wanted);
      _cursorValid = true;
      _renderedSchedule = schedule;
    }
    else if (Math.Abs(wanted - _cursor) > ContinuityToleranceFrames(outputFrames))
    {
      _cursor = AlignToOutput(wanted);
      _jumps++;
    }

    double first = _cursor;
    _cursor = first + outputFrames * _step;
    double last = first + (outputFrames - 1) * _step;
    long neededFirst = Math.Max(_audibleFromFrame, (long)Math.Floor(first));
    long neededLast = Math.Min(_frameCount - 1L, (long)Math.Floor(last) + 1L);
    if (neededFirst > neededLast)
      return;
    Fill(Math.Max(0L, neededFirst), neededLast);

    bool wrote = false;
    for (int i = 0; i < outputFrames; i++)
    {
      double position = first + i * _step;
      if (position < _audibleFromFrame || position < 0d || position >= _frameCount)
        continue;
      long frame = (long)Math.Floor(position);
      long offset = frame - _windowStart;
      if (offset < 0L || offset >= _windowFrames)
      {
        _missingFrames++;
        continue;
      }
      double fraction = offset + 1L < _windowFrames ? position - frame : 0d;
      WriteFrame(data, i * outputChannels, outputChannels, (int)offset * _channels, fraction);
      wrote = true;
    }

    if (wrote && _firstBlockPending)
    {
      _firstBlockPending = false;
      _firstBlockReady = true;
      _firstBlock = new ReplayMicrophoneFirstBlock(
        schedule.AnchorDspTime,
        dspTime,
        first,
        outputFrames,
        outputChannels
      );
    }
  }

  // The DSP time of consecutive blocks should continue the previous block
  // exactly. Small disagreements keep playing continuously instead of
  // clicking; anything larger is a real jump of the clock.
  private double ContinuityToleranceFrames(int outputFrames) =>
    Math.Max(_sampleRate * 0.002d, 2d * outputFrames * _step);

  // At equal rates, start on a whole WAV frame so samples copy unchanged.
  private double AlignToOutput(double frame) => _step == 1d ? Math.Round(frame) : frame;

  private void Fill(long firstFrame, long lastFrame)
  {
    if (firstFrame < _windowStart || _generation != _prefetch.Generation)
    {
      SeekTo(firstFrame);
    }
    else if (firstFrame > _windowStart)
    {
      long drop = Math.Min(firstFrame - _windowStart, _windowFrames);
      int keep = _windowFrames - (int)drop;
      if (keep > 0)
        Array.Copy(_window, (int)drop * _channels, _window, 0, keep * _channels);
      _windowStart += drop;
      _windowFrames = keep;

      if (_windowFrames == 0 && firstFrame > _windowStart)
      {
        long gapBytes = (firstFrame - _windowStart) * _frameBytes;
        if (gapBytes <= _prefetch.BufferedBytes && _prefetch.TrySkip(_generation, (int)gapBytes, _frameBytes))
          _windowStart = firstFrame;
        else if (gapBytes > _prefetch.Capacity / 2)
          SeekTo(firstFrame);
        else
          return; // Still reading; the next block skips ahead.
      }
    }

    long wantedFrames = lastFrame + 1L - (_windowStart + _windowFrames);
    if (wantedFrames <= 0L)
      return;
    int frames = (int)Math.Min(wantedFrames, int.MaxValue / 4 / _frameBytes);
    EnsureWindowCapacity(_windowFrames + frames);
    int bytesRead = _prefetch.Read(_bytes, 0, frames * _frameBytes, _frameBytes);
    int framesRead = bytesRead / _frameBytes;
    int samples = framesRead * _channels;
    int target = _windowFrames * _channels;
    for (int i = 0; i < samples; i++)
    {
      int byteIndex = i * 2;
      _window[target + i] = (short)(_bytes[byteIndex] | (_bytes[byteIndex + 1] << 8)) / 32768f;
    }
    _windowFrames += framesRead;
  }

  private void SeekTo(long frame)
  {
    frame = Math.Max(0L, Math.Min(frame, _frameCount));
    _generation = _prefetch.Seek(frame * _frameBytes);
    _windowStart = frame;
    _windowFrames = 0;
    _prefetchSeeks++;
  }

  private void EnsureWindowCapacity(int frames)
  {
    if (_window.Length < frames * _channels)
    {
      var window = new float[Math.Max(frames, _window.Length / _channels * 2) * _channels];
      Array.Copy(_window, window, _windowFrames * _channels);
      _window = window;
    }
    if (_bytes.Length < frames * _frameBytes)
      _bytes = new byte[_window.Length / _channels * _frameBytes];
  }

  private void WriteFrame(float[] data, int outputIndex, int outputChannels, int windowIndex, double fraction)
  {
    float gain = _gain;
    float left = SampleAt(windowIndex, 0, fraction) * gain;
    float right = _channels > 1 ? SampleAt(windowIndex, 1, fraction) * gain : left;
    if (outputChannels == 1)
    {
      data[outputIndex] = Clamp((left + right) * 0.5f);
      return;
    }
    data[outputIndex] = Clamp(left);
    data[outputIndex + 1] = Clamp(right);
    // A mono recording fills every speaker; a stereo one leaves the rest silent.
    if (_channels == 1)
    {
      for (int channel = 2; channel < outputChannels; channel++)
        data[outputIndex + channel] = Clamp(left);
    }
  }

  private float SampleAt(int windowIndex, int channel, double fraction)
  {
    float sample = _window[windowIndex + channel];
    if (fraction <= 0d)
      return sample;
    float next = _window[windowIndex + _channels + channel];
    return (float)(sample + (next - sample) * fraction);
  }

  private static float Clamp(float sample) =>
    sample < -1f ? -1f
    : sample > 1f ? 1f
    : sample;
}
