namespace TUFReplay.LocalServer.Http;

public sealed class ServerResponse
{
  public int StatusCode { get; }
  public object Body { get; }
  public string ContentType { get; }

  private ServerResponse(int statusCode, object body, string contentType = null)
  {
    StatusCode = statusCode;
    Body = body;
    ContentType = contentType;
  }

  public static ServerResponse Ok(object body) => new ServerResponse(200, body);
  public static ServerResponse NoContent() => new ServerResponse(204, null);
  public static ServerResponse BadRequest(object body) => new ServerResponse(400, body);
  public static ServerResponse NotFound(object body) => new ServerResponse(404, body);
  public static ServerResponse InternalServerError(object body) => new ServerResponse(500, body);
}
