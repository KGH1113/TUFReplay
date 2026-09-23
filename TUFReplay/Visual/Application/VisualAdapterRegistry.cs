using System;
using System.Collections.Generic;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing;
using TUFReplay.Visual.Importing.Abstractions;

namespace TUFReplay.Visual.Application;

public sealed class VisualAdapterRegistry
{
  private readonly IReadOnlyDictionary<VisualSource, IVisualSourceAdapter> _adapters;

  public VisualAdapterRegistry(IEnumerable<IVisualSourceAdapter> adapters)
  {
    if (adapters == null)
      throw new ArgumentNullException(nameof(adapters));
    var indexed = new Dictionary<VisualSource, IVisualSourceAdapter>();
    foreach (IVisualSourceAdapter adapter in adapters)
    {
      if (adapter == null)
        throw new ArgumentException("Visual adapters cannot contain null entries.", nameof(adapters));
      if (indexed.ContainsKey(adapter.Source))
        throw new ArgumentException("Each visual source must have exactly one adapter.", nameof(adapters));
      indexed.Add(adapter.Source, adapter);
    }
    _adapters = indexed;
  }

  public IVisualSourceAdapter Get(VisualSource source)
  {
    if (_adapters.TryGetValue(source, out IVisualSourceAdapter adapter))
      return adapter;
    throw new VisualImportException("visual_source_unsupported", "The selected visual source is unsupported.");
  }
}
