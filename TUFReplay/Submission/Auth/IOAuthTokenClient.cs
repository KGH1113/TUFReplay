using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Submission.Auth;

public interface IOAuthTokenClient : IDisposable
{
  Task<JObject> Token(Dictionary<string, string> fields);
  Task Revoke(string token);
}
