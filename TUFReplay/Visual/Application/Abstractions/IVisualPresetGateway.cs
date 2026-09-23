using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Api;

namespace TUFReplay.Visual.Application.Abstractions;

public interface IVisualPresetGateway
{
  Task<JObject> ListAsync(SubmissionAccount account, CancellationToken cancellation);
  Task<JObject> CreateAsync(SubmissionAccount account, JObject body, CancellationToken cancellation);
  Task<JObject> RemoveAsync(SubmissionAccount account, string id, CancellationToken cancellation);
}
