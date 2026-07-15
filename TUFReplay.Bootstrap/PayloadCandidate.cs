namespace TUFReplay.Bootstrap;

internal sealed class PayloadCandidate
{
  public PayloadCandidate(string version, string assemblyPath)
  {
    Version = version;
    AssemblyPath = assemblyPath;
  }

  public string Version { get; }
  public string AssemblyPath { get; }
}
