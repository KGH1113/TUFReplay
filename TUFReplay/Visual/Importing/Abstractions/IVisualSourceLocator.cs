using TUFReplay.Visual.Domain;

namespace TUFReplay.Visual.Importing.Abstractions;

public interface IVisualSourceLocator
{
  VisualSourceInstallation Find(VisualSource source);
}
