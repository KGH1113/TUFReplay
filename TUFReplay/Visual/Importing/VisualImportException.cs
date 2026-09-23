using System;

namespace TUFReplay.Visual.Importing;

public sealed class VisualImportException : Exception
{
  public string Code { get; }

  public VisualImportException(string code, string message)
    : base(message)
  {
    Code = code;
  }

  public VisualImportException(string code, string message, Exception innerException)
    : base(message, innerException)
  {
    Code = code;
  }
}
