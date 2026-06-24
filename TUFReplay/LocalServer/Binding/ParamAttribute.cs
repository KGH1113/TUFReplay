using System;

namespace TUFReplay.LocalServer.Binding;

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class ParamAttribute : Attribute
{
  public string Name { get; }

  public ParamAttribute(string name)
  {
    Name = name;
  }
}

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class QueryAttribute : Attribute
{
  public string Name { get; }

  public QueryAttribute(string name)
  {
    Name = name;
  }
}

[AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
public sealed class FromBodyAttribute : Attribute
{
}
