using System;

namespace TUFReplay.Replay.NativeInput;

internal sealed class ReplayLatenessHistogram
{
  private const int FineBucketCount = 200;
  private const long FineBucketWidthUs = 50L;
  private readonly long[] _buckets = new long[FineBucketCount + 3];
  private long _count;
  private long _maxUs;

  public long Count => _count;
  public long MaxUs => _maxUs;

  public void Record(long latenessUs)
  {
    Record(latenessUs, 1L);
  }

  public void Record(long latenessUs, long samples)
  {
    if (samples <= 0L)
      return;
    latenessUs = Math.Max(0L, latenessUs);
    int bucket;
    if (latenessUs < 10_000L)
      bucket = (int)(latenessUs / FineBucketWidthUs);
    else if (latenessUs < 20_000L)
      bucket = FineBucketCount;
    else if (latenessUs < 50_000L)
      bucket = FineBucketCount + 1;
    else
      bucket = FineBucketCount + 2;
    _buckets[bucket] += samples;
    _count += samples;
    _maxUs = Math.Max(_maxUs, latenessUs);
  }

  public long Percentile(double percentile)
  {
    if (_count == 0)
      return 0L;
    long target = Math.Max(1L, (long)Math.Ceiling(_count * Math.Max(0d, Math.Min(1d, percentile))));
    long cumulative = 0L;
    for (int i = 0; i < _buckets.Length; i++)
    {
      cumulative += _buckets[i];
      if (cumulative < target)
        continue;
      if (i < FineBucketCount)
        return (i + 1L) * FineBucketWidthUs;
      if (i == FineBucketCount)
        return 20_000L;
      if (i == FineBucketCount + 1)
        return 50_000L;
      return _maxUs;
    }
    return _maxUs;
  }
}
