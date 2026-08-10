using System;
using System.Collections.Generic;

namespace TUFReplay.Application.Activity;

internal sealed class TufLevelIdResolverCache
{
  private readonly object _gate = new object();
  private readonly Func<Func<string, int?>> _resolverFactory;
  private readonly Action<Exception> _onError;
  private readonly Func<int> _clock;
  private readonly int _negativeTtlMilliseconds;
  private readonly Dictionary<string, CacheEntry> _paths = new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
  private Func<string, int?> _resolver;
  private int _resolverRetryAt;
  private bool _resolverLookupAttempted;

  public TufLevelIdResolverCache(
    Func<Func<string, int?>> resolverFactory,
    Action<Exception> onError,
    int negativeTtlMilliseconds,
    Func<int> clock = null
  )
  {
    _resolverFactory = resolverFactory ?? throw new ArgumentNullException(nameof(resolverFactory));
    _onError = onError;
    if (negativeTtlMilliseconds <= 0)
      throw new ArgumentOutOfRangeException(nameof(negativeTtlMilliseconds));
    _negativeTtlMilliseconds = negativeTtlMilliseconds;
    _clock = clock ?? (() => Environment.TickCount);
  }

  public int? Resolve(string levelPath)
  {
    if (string.IsNullOrWhiteSpace(levelPath))
      return null;

    lock (_gate)
    {
      int now = _clock();
      if (_paths.TryGetValue(levelPath, out CacheEntry cached) && !cached.IsExpired(now))
        return cached.LevelId;

      Func<string, int?> resolver = GetResolver(now);
      if (resolver == null)
      {
        CacheNegative(levelPath, now);
        return null;
      }

      try
      {
        int? levelId = resolver(levelPath);
        _paths[levelPath] = levelId.HasValue ? CacheEntry.Positive(levelId.Value) : CacheEntry.Negative(Deadline(now));
        return levelId;
      }
      catch (Exception exception)
      {
        _onError?.Invoke(exception);
        CacheNegative(levelPath, now);
        return null;
      }
    }
  }

  private Func<string, int?> GetResolver(int now)
  {
    if (_resolver != null)
      return _resolver;
    if (_resolverLookupAttempted && !HasReached(now, _resolverRetryAt))
      return null;

    _resolverLookupAttempted = true;
    _resolverRetryAt = Deadline(now);
    try
    {
      _resolver = _resolverFactory();
    }
    catch (Exception exception)
    {
      _onError?.Invoke(exception);
    }
    return _resolver;
  }

  private void CacheNegative(string levelPath, int now)
  {
    _paths[levelPath] = CacheEntry.Negative(Deadline(now));
  }

  private int Deadline(int now) => unchecked(now + _negativeTtlMilliseconds);

  private static bool HasReached(int now, int deadline) => unchecked(now - deadline) >= 0;

  private readonly struct CacheEntry
  {
    public readonly int? LevelId;
    private readonly int _expiresAt;
    private readonly bool _expires;

    private CacheEntry(int? levelId, int expiresAt, bool expires)
    {
      LevelId = levelId;
      _expiresAt = expiresAt;
      _expires = expires;
    }

    public static CacheEntry Positive(int levelId) => new CacheEntry(levelId, 0, false);

    public static CacheEntry Negative(int expiresAt) => new CacheEntry(null, expiresAt, true);

    public bool IsExpired(int now) => _expires && HasReached(now, _expiresAt);
  }
}
