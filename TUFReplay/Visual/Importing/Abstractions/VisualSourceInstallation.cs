using TUFReplay.Visual.Domain;

namespace TUFReplay.Visual.Importing.Abstractions;

public sealed class VisualSourceInstallation
{
  public VisualSource Source { get; }
  public string RootPath { get; }
  public string Version { get; }

  public VisualSourceInstallation(VisualSource source, string rootPath, string version)
  {
    Source = source;
    RootPath = rootPath;
    Version = version;
  }
}
