using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;
using TUFReplay.Visual.Application.Abstractions;

namespace TUFReplay.Visual.Infrastructure.Api;

public sealed class SubmissionVisualPresetGateway : IVisualPresetGateway
{
  private readonly SubmissionRecordsClient _records;

  public SubmissionVisualPresetGateway(SubmissionRecordsClient records)
  {
    _records = records ?? throw new ArgumentNullException(nameof(records));
  }

  public Task<JObject> ListAsync(SubmissionAccount account, CancellationToken cancellation)
  {
    return _records.Send(account, HttpMethod.Get, "api/v1/visual-presets", cancellation: cancellation);
  }

  public async Task<JObject> CreateAsync(SubmissionAccount account, JObject body, CancellationToken cancellation)
  {
    var request = (JObject)body.DeepClone();
    var assets = (JArray)request["bundle"]["assets"];
    var byHash = new Dictionary<string, JObject>(StringComparer.Ordinal);
    var references = new JArray();
    using var hasher = SHA256.Create();
    foreach (JObject asset in assets)
    {
      cancellation.ThrowIfCancellationRequested();
      byte[] data = Convert.FromBase64String((string)asset["data_base64"]);
      string sha256 = BitConverter.ToString(hasher.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
      asset["sha256"] = sha256;
      asset["bytes"] = data.Length;
      if (!byHash.ContainsKey(sha256))
      {
        byHash.Add(sha256, asset);
        references.Add(
          new JObject
          {
            ["sha256"] = sha256,
            ["bytes"] = data.Length,
            ["media_type"] = asset["media_type"],
          }
        );
      }
    }
    // Keep both the request and the bounded JSON response small for large presets.
    for (int offset = 0; offset < references.Count; offset += 128)
    {
      var batch = new JArray();
      var expected = new HashSet<string>(StringComparer.Ordinal);
      for (int index = offset; index < Math.Min(offset + 128, references.Count); index++)
      {
        batch.Add(references[index].DeepClone());
        expected.Add((string)references[index]["sha256"]);
      }
      JObject checkedAssets = await _records
        .Send(account, HttpMethod.Post, "api/v1/visual-assets/check", new JObject { ["assets"] = batch }, cancellation)
        .ConfigureAwait(false);
      if (!(checkedAssets["missing"] is JArray missing))
        throw new InvalidOperationException("visual_bundle_invalid");
      foreach (JToken missingHash in missing)
      {
        string hash = (string)missingHash;
        if (hash == null || !expected.Remove(hash) || !byHash.TryGetValue(hash, out JObject asset))
          throw new InvalidOperationException("visual_bundle_invalid");
        await _records
          .UploadAsset(
            account,
            hash,
            (string)asset["media_type"],
            Convert.FromBase64String((string)asset["data_base64"]),
            cancellation
          )
          .ConfigureAwait(false);
      }
    }
    foreach (JObject asset in assets)
      asset.Remove("data_base64");
    request["bundle"]["schema_version"] = 2;
    return await _records
      .Send(account, HttpMethod.Post, "api/v1/visual-presets", request, cancellation)
      .ConfigureAwait(false);
  }

  public Task<JObject> RemoveAsync(SubmissionAccount account, string id, CancellationToken cancellation)
  {
    return _records.Send(
      account,
      HttpMethod.Delete,
      "api/v1/visual-presets/" + Uri.EscapeDataString(id),
      cancellation: cancellation
    );
  }
}
