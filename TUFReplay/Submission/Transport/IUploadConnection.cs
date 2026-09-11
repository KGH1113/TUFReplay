using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TUFReplay.Submission.Protocol;

namespace TUFReplay.Submission.Transport;

public interface IUploadConnection : IDisposable
{
  Task Connect(CancellationToken cancellation);
  Task SendControl(object control, CancellationToken cancellation);
  Task SendFrame(UploadFrame frame, CancellationToken cancellation);
  Task<JObject> Receive(CancellationToken cancellation);
}
