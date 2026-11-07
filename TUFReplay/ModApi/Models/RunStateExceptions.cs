using System;

namespace TUFReplay.ModApi.Models;

/// <summary>A provider or state type that cannot be registered. The message tells the mod author what to change.</summary>
internal sealed class RunStateModelException : Exception
{
  public RunStateModelException(string message)
    : base(message) { }
}

/// <summary>A value that cannot be saved or read back. The message names the member that caused it.</summary>
internal sealed class RunStateValueException : Exception
{
  public RunStateValueException(string message)
    : base(message) { }

  public RunStateValueException(string message, Exception inner)
    : base(message, inner) { }
}
