namespace TUFReplay.LocalServer.Routing;

public sealed class GetAttribute : RouteAttribute
{
  public GetAttribute(string path = "") : base("GET", path)
  {
  }
}

public sealed class PostAttribute : RouteAttribute
{
  public PostAttribute(string path = "") : base("POST", path)
  {
  }
}

public sealed class DeleteAttribute : RouteAttribute
{
  public DeleteAttribute(string path = "") : base("DELETE", path)
  {
  }
}
