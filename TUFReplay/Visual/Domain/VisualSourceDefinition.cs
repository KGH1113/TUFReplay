using System;
using System.Collections.Generic;

namespace TUFReplay.Visual.Domain;

public sealed class VisualSourceDefinition
{
  public VisualSource Source { get; }
  public string WireName { get; }
  public string LatestVersion { get; }
  public bool RequiresInstallation { get; }
  public IReadOnlyList<VisualKind> Kinds { get; }

  public VisualSourceDefinition(
    VisualSource source,
    string wireName,
    string latestVersion,
    bool requiresInstallation,
    params VisualKind[] kinds
  )
  {
    Source = source;
    WireName = wireName;
    LatestVersion = latestVersion;
    RequiresInstallation = requiresInstallation;
    Kinds = Array.AsReadOnly(kinds ?? Array.Empty<VisualKind>());
  }

  public bool Supports(VisualKind kind)
  {
    foreach (VisualKind supported in Kinds)
      if (supported == kind)
        return true;
    return false;
  }
}

public static class VisualSourceDefinitions
{
  private static readonly IReadOnlyList<VisualSourceDefinition> Definitions = Array.AsReadOnly(
    new[]
    {
      new VisualSourceDefinition(
        VisualSource.JipperResourcePack,
        "jipper-resourcepack",
        "1.5.2.0",
        true,
        VisualKind.Keyviewer,
        VisualKind.Overlay
      ),
      new VisualSourceDefinition(VisualSource.Dmnote, "dmnote", "2.0.2", false, VisualKind.Keyviewer),
      new VisualSourceDefinition(VisualSource.ImplDmnote, "impl-dmnote", "0.1.0", false, VisualKind.Keyviewer),
      new VisualSourceDefinition(VisualSource.JipperKeyviewer, "jipper-keyviewer", "1.7.2", true, VisualKind.Keyviewer),
      new VisualSourceDefinition(VisualSource.ImplResourcePack, "impl-resourcepack", "0.1.0", true, VisualKind.Overlay),
    }
  );

  public static IReadOnlyList<VisualSourceDefinition> All => Definitions;

  public static VisualSourceDefinition Get(VisualSource source)
  {
    foreach (VisualSourceDefinition definition in Definitions)
      if (definition.Source == source)
        return definition;
    throw new ArgumentOutOfRangeException(nameof(source));
  }

  public static bool TryParse(string wireName, out VisualSource source)
  {
    foreach (VisualSourceDefinition definition in Definitions)
    {
      if (!string.Equals(definition.WireName, wireName, StringComparison.OrdinalIgnoreCase))
        continue;
      source = definition.Source;
      return true;
    }
    source = default;
    return false;
  }
}
