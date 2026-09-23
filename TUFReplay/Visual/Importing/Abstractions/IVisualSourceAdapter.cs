using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;

namespace TUFReplay.Visual.Importing.Abstractions;

public interface IVisualSourceAdapter
{
  VisualSource Source { get; }

  VisualBundle Import(VisualKind kind, string presetJson = null, VisualImportOptions options = null);
}
