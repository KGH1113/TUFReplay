namespace TUFReplay.Visual.Domain;

public enum VisualKind
{
  Keyviewer,
  Overlay,
}

public static class VisualKindNames
{
  public static string ToWire(VisualKind kind) => kind == VisualKind.Keyviewer ? "keyviewer" : "overlay";

  public static bool TryParse(string value, out VisualKind kind)
  {
    kind = VisualKind.Keyviewer;
    if (string.Equals(value, "keyviewer", System.StringComparison.OrdinalIgnoreCase))
      return true;
    if (!string.Equals(value, "overlay", System.StringComparison.OrdinalIgnoreCase))
      return false;
    kind = VisualKind.Overlay;
    return true;
  }
}
