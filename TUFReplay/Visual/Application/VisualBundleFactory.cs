using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;

namespace TUFReplay.Visual.Application;

public sealed class VisualBundleFactory
{
  private readonly VisualAdapterRegistry _adapters;

  public VisualBundleFactory(VisualAdapterRegistry adapters)
  {
    _adapters = adapters ?? throw new System.ArgumentNullException(nameof(adapters));
  }

  public VisualBundle Create(
    VisualKind kind,
    VisualSource source,
    string presetJson = null,
    VisualImportOptions options = null
  )
  {
    VisualSourceDefinition definition = VisualSourceDefinitions.Get(source);
    if (!definition.Supports(kind))
      throw new VisualImportException(
        "visual_source_unsupported",
        definition.WireName + " does not support " + VisualKindNames.ToWire(kind) + " presets."
      );
    options?.Progress?.Invoke(new VisualImportProgress("reading_settings"));
    return _adapters.Get(source).Import(kind, presetJson, options);
  }
}
