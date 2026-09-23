using System;
using System.Collections.Generic;
using TUFReplay.Visual.Contracts;
using TUFReplay.Visual.Domain;
using TUFReplay.Visual.Importing.Abstractions;

namespace TUFReplay.Visual.Application;

public sealed class VisualSourceCatalog
{
  private readonly IVisualSourceLocator _locator;

  public VisualSourceCatalog(IVisualSourceLocator locator)
  {
    _locator = locator ?? throw new ArgumentNullException(nameof(locator));
  }

  public IReadOnlyList<VisualSourceInfo> List()
  {
    var result = new List<VisualSourceInfo>();
    foreach (VisualSourceDefinition definition in VisualSourceDefinitions.All)
    {
      VisualSourceInstallation installation = definition.RequiresInstallation ? _locator.Find(definition.Source) : null;
      var kinds = new List<string>();
      foreach (VisualKind kind in definition.Kinds)
        kinds.Add(VisualKindNames.ToWire(kind));
      result.Add(
        new VisualSourceInfo
        {
          Source = definition.WireName,
          Version = installation?.Version ?? definition.LatestVersion,
          Available = !definition.RequiresInstallation || installation != null,
          Kinds = kinds,
        }
      );
    }
    return result;
  }
}
