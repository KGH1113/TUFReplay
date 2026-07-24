using System.IO;

namespace TUFReplay.Bootstrap;

internal sealed class RuntimeCandidate
{
  public RuntimeCandidate(string version, string runtimePath)
  {
    Version = version;
    RuntimePath = runtimePath;
  }

  public string Version { get; }
  public string RuntimePath { get; }
  public string AssemblyPath => Path.Combine(RuntimePath, "TUFReplay.dll");
  public string UpdateEnginePath => Path.Combine(RuntimePath, "TUFReplay.UpdateEngine.dll");
}
