using System;
using System.Net.Http;
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

  public Task<JObject> CreateAsync(SubmissionAccount account, JObject body, CancellationToken cancellation)
  {
    return _records.Send(account, HttpMethod.Post, "api/v1/visual-presets", body, cancellation);
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
