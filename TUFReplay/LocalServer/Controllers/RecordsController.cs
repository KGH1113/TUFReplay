using TUFReplay.LocalServer.Binding;
using TUFReplay.LocalServer.Dtos;
using TUFReplay.LocalServer.Http;
using TUFReplay.LocalServer.Routing;
using TUFReplay.Replay;
using TUFReplay.Shared;

namespace TUFReplay.LocalServer.Controllers;

[Controller("/api/records")]
public sealed class RecordsController
{
  [Get("/")]
  public object List()
  {
    return PlayRecordRepository.ListSummaries();
  }

  [Get("/:id")]
  public ServerResponse Detail([Param("id")] string id)
  {
    PlayRecordMetadata record = PlayRecordRepository.GetMetadata(id);
    if (record == null) return ServerResponse.NotFound(new { error = "record_not_found" });

    return ServerResponse.Ok(RecordDetailDto.From(record));
  }

  [Delete("/:id")]
  public ServerResponse DeleteRecord([Param("id")] string id)
  {
    PlayRecordRepository.Delete(id);
    return ServerResponse.Ok(new { ok = true });
  }

  [Post("/:id/open")]
  public ServerResponse OpenRecord([Param("id")] string id)
  {
    OpenRecordResult result = ReplayService.OpenRecord(id);
    if (!result.Ok) return ServerResponse.BadRequest(new { error = result.Error });

    return ServerResponse.Ok(new { ok = true, status = result.Status });
  }
}
