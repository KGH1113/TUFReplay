using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TUFReplay.Activity.Repositories;
using TUFReplay.Replay.Models;
using TUFReplay.Replay.Playback;
using TUFReplay.Replay.Timeline;
using TUFReplay.Shared.Ipc;

namespace TUFReplay.Replay.Ipc;

internal static class RunJudgmentTimelineIpcHandlers
{
  /// <summary>One request covers the attempts a client shows at once.</summary>
  internal const int MaxRunsPerRequest = 100;

  /// <summary>Timelines in request order; unknown runs are left out, unreadable payloads come back empty.</summary>
  public static RunJudgmentTimelineData[] Read(IReadOnlyList<string> runIds)
  {
    if (runIds.Count > MaxRunsPerRequest)
      throw IpcDomainError.Create("too_many_runs", "Request judgment timelines for fewer runs at a time.");

    Dictionary<string, StoredJudgmentTimelineSource> sources = RunRepository
      .GetJudgmentTimelineSources(runIds)
      .ToDictionary(source => source.Id, StringComparer.Ordinal);
    var timelines = new List<RunJudgmentTimelineData>(runIds.Count);
    foreach (string runId in runIds)
      if (sources.TryGetValue(runId, out StoredJudgmentTimelineSource source))
        timelines.Add(Build(source));
    return timelines.ToArray();
  }

  private static RunJudgmentTimelineData Build(StoredJudgmentTimelineSource source)
  {
    List<ReplayHitContext> hits;
    try
    {
      hits = ReplayHitContextParser.Parse(source.HitContextCsv);
    }
    catch (InvalidDataException)
    {
      hits = new List<ReplayHitContext>();
    }
    ReplayMetadata meta = ReadMetadata(source.MetaJson);
    return RunJudgmentTimelineBuilder.Build(
      source.Id,
      source.Result,
      meta?.wonTimeUs,
      meta?.terminalTimeUs,
      source.JudgmentSystem,
      hits
    );
  }

  private static ReplayMetadata ReadMetadata(string json)
  {
    if (string.IsNullOrWhiteSpace(json))
      return null;
    try
    {
      return JsonConvert.DeserializeObject<ReplayMetadata>(json);
    }
    catch (JsonException)
    {
      return null;
    }
  }
}
