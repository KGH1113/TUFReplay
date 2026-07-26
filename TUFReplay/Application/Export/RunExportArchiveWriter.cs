using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using TUFReplay.Domain.Activity;
using TUFReplay.Domain.Microphone;
using TUFReplay.Domain.ReplayData;
using TUFReplay.Infrastructure.Database.Repositories;

namespace TUFReplay.Application.Export;

public sealed class RunExportArchiveResult
{
  public bool IncludedMicrophone;
}

public static class RunExportArchiveWriter
{
  public const string Format = "tufreplay";
  public const int FormatVersion = 1;

  private const string InputsPath = "replay/inputs.csv";
  private const string HitContextsPath = "replay/hit-contexts.csv";
  private const string MetaPath = "replay/meta.json";
  private const string MicrophonePath = "microphone/recording.wav";

  private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
  {
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    Formatting = Formatting.Indented,
    NullValueHandling = NullValueHandling.Ignore,
  };

  public static RunExportArchiveResult Write(
    string runId,
    Stream destination,
    CancellationToken cancellationToken,
    DateTime? exportedAtUtc = null
  )
  {
    if (string.IsNullOrWhiteSpace(runId))
      throw new ArgumentException("A run ID is required.", nameof(runId));
    if (destination == null)
      throw new ArgumentNullException(nameof(destination));
    if (!destination.CanWrite)
      throw new ArgumentException("The export destination must be writable.", nameof(destination));

    RunRecord run = RunRepository.Get(runId);
    StoredReplayRun replay = RunRepository.GetReplayRun(runId);
    if (run == null || replay == null)
      throw new InvalidOperationException("The recorded run was not found.");

    LevelSession level = LevelSessionRepository.Get(run.LevelSessionId);
    if (level == null)
      throw new InvalidDataException("The run's level session was not found.");

    cancellationToken.ThrowIfCancellationRequested();
    var manifest = CreateManifest(run, level, exportedAtUtc ?? DateTime.UtcNow);
    using (var archive = new ZipArchive(destination, ZipArchiveMode.Create, true))
    {
      manifest.Entries.Inputs = WriteBytesEntry(
        archive,
        InputsPath,
        replay.InputCsv,
        CompressionLevel.Optimal,
        cancellationToken
      );
      manifest.Entries.HitContexts = WriteBytesEntry(
        archive,
        HitContextsPath,
        replay.HitContextCsv,
        CompressionLevel.Optimal,
        cancellationToken
      );
      manifest.Entries.Meta = WriteBytesEntry(
        archive,
        MetaPath,
        Encoding.UTF8.GetBytes(replay.MetaJson ?? "{}"),
        CompressionLevel.Optimal,
        cancellationToken
      );

      manifest.Entries.Microphone = WriteMicrophoneEntry(archive, runId, cancellationToken);
      if (manifest.Entries.Microphone != null)
      {
        manifest.Microphone = manifest.Entries.Microphone.Metadata;
        manifest.Entries.Microphone.Metadata = null;
      }

      byte[] manifestBytes = Encoding.UTF8.GetBytes(
        JsonConvert.SerializeObject(manifest, JsonSettings) + "\n"
      );
      WriteBytesEntry(
        archive,
        "manifest.json",
        manifestBytes,
        CompressionLevel.Optimal,
        cancellationToken
      );
    }

    return new RunExportArchiveResult { IncludedMicrophone = manifest.Microphone != null };
  }

  private static RunExportManifest CreateManifest(RunRecord run, LevelSession level, DateTime exportedAtUtc)
  {
    byte[] gameplayHash = run.GameplayHash ?? level.GameplayHash;
    int? gameplayHashVersion = run.GameplayHash == null
      ? level.GameplayHashVersion
      : run.GameplayHashVersion;
    return new RunExportManifest
    {
      Format = Format,
      FormatVersion = FormatVersion,
      ExportedAtUtc = exportedAtUtc.ToUniversalTime().ToString("O"),
      Producer = new RunExportProducer
      {
        Name = "TUFReplay",
        Version = Main.Instance?.Version?.ToString(),
      },
      Run = new RunExportRun
      {
        Id = run.Id,
        RunIndex = run.RunIndex,
        StartedAtUtc = run.StartedAtUtc,
        EndedAtUtc = run.EndedAtUtc,
        Result = run.Result,
        LevelTileCount = run.LevelTileCount,
        StartTile = run.StartTile,
        LastTile = run.LastTile,
        GameplayStartSongPosition = run.GameplayStartSongPosition,
        LevelPitchPercent = run.LevelPitchPercent,
        EffectivePitch = run.EffectivePitch,
        XAccuracy = run.XAccuracy,
        JudgmentDifficulty = run.JudgmentDifficulty?.ToString().ToLowerInvariant(),
        JudgmentCounts = run.JudgmentCounts,
        NoFailMode = run.NoFailMode,
        InputCount = run.InputCount,
        HitContextCount = run.HitContextCount,
      },
      Level = new RunExportLevel
      {
        TufLevelId = run.TufLevelId,
        Song = level.Song,
        Author = level.Author,
        Artist = level.Artist,
        GameplayHash = Hex(gameplayHash),
        GameplayHashVersion = gameplayHashVersion,
      },
      Entries = new RunExportEntries(),
    };
  }

  private static RunExportEntry WriteBytesEntry(
    ZipArchive archive,
    string path,
    byte[] bytes,
    CompressionLevel compression,
    CancellationToken cancellationToken
  )
  {
    bytes ??= Array.Empty<byte>();
    cancellationToken.ThrowIfCancellationRequested();
    ZipArchiveEntry entry = archive.CreateEntry(path, compression);
    using (Stream stream = entry.Open())
      stream.Write(bytes, 0, bytes.Length);
    using SHA256 sha256 = SHA256.Create();
    return new RunExportEntry
    {
      Path = path,
      Bytes = bytes.LongLength,
      Sha256 = Hex(sha256.ComputeHash(bytes)),
    };
  }

  private static RunExportMicrophoneEntry WriteMicrophoneEntry(
    ZipArchive archive,
    string runId,
    CancellationToken cancellationToken
  )
  {
    if (!MicrophoneRecordingRepository.Exists(runId))
      return null;

    ZipArchiveEntry entry = archive.CreateEntry(MicrophonePath, CompressionLevel.NoCompression);
    StoredMicrophoneRecording recording;
    byte[] hash;
    using (SHA256 sha256 = SHA256.Create())
    using (Stream entryStream = entry.Open())
    using (var hashingStream = new CryptoStream(entryStream, sha256, CryptoStreamMode.Write))
    {
      recording = MicrophoneRecordingRepository.WriteTo(runId, hashingStream, cancellationToken);
      if (recording == null)
        throw new InvalidDataException("The microphone recording disappeared during export.");
      hashingStream.FlushFinalBlock();
      hash = sha256.Hash;
    }

    return new RunExportMicrophoneEntry
    {
      Path = MicrophonePath,
      Bytes = recording.ByteLength,
      Sha256 = Hex(hash),
      Metadata = new RunExportMicrophone
      {
        Format = recording.Format,
        SampleRate = recording.SampleRate,
        Channels = recording.Channels,
        FrameCount = recording.FrameCount,
        CaptureStartOffsetUs = recording.CaptureStartOffsetUs,
      },
    };
  }

  private static string Hex(byte[] bytes) =>
    bytes == null ? null : BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

  private sealed class RunExportManifest
  {
    public string Format;
    public int FormatVersion;
    public string ExportedAtUtc;
    public RunExportProducer Producer;
    public RunExportRun Run;
    public RunExportLevel Level;
    public RunExportMicrophone Microphone;
    public RunExportEntries Entries;
  }

  private sealed class RunExportProducer
  {
    public string Name;
    public string Version;
  }

  private sealed class RunExportRun
  {
    public string Id;
    public int RunIndex;
    public string StartedAtUtc;
    public string EndedAtUtc;
    public string Result;
    public int LevelTileCount;
    public int StartTile;
    public int? LastTile;
    public double? GameplayStartSongPosition;
    public int? LevelPitchPercent;
    public float? EffectivePitch;
    public float? XAccuracy;
    public string JudgmentDifficulty;
    public JudgmentCounts JudgmentCounts;
    public bool NoFailMode;
    public int InputCount;
    public int HitContextCount;
  }

  private sealed class RunExportLevel
  {
    public int? TufLevelId;
    public string Song;
    public string Author;
    public string Artist;
    public string GameplayHash;
    public int? GameplayHashVersion;
  }

  private sealed class RunExportMicrophone
  {
    public string Format;
    public int SampleRate;
    public int Channels;
    public long FrameCount;
    public long CaptureStartOffsetUs;
  }

  private sealed class RunExportEntries
  {
    public RunExportEntry Inputs;
    public RunExportEntry HitContexts;
    public RunExportEntry Meta;
    public RunExportMicrophoneEntry Microphone;
  }

  private class RunExportEntry
  {
    public string Path;
    public long Bytes;
    public string Sha256;
  }

  private sealed class RunExportMicrophoneEntry : RunExportEntry
  {
    [JsonIgnore]
    public RunExportMicrophone Metadata;
  }
}
