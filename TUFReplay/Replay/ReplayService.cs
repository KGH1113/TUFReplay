using System;
using Newtonsoft.Json;
using TUFReplay.Shared;

namespace TUFReplay.Replay;

public static class ReplayService
{
  private static ActiveReplayContext _activeContext;

  public static bool HasActiveContext => _activeContext != null;

  public static bool IsActiveReplayLevel(int tufLevelId)
  {
    return _activeContext != null && _activeContext.TufLevelId == tufLevelId;
  }

  public static OpenRecordResult OpenRecord(string recordId)
  {
    PlayRecord record = PlayRecordRepository.Get(recordId);
    if (record == null) return OpenRecordResult.Fail("record_not_found");

    ReplayRecordMeta meta;

    try { meta = JsonConvert.DeserializeObject<ReplayRecordMeta>(record.MetaJson); }
    catch { return OpenRecordResult.Fail("invalid_record_meta"); }

    if (meta?.levelInfo == null) return OpenRecordResult.Fail("level_info_missing");

    OpenRecordResult result = TUFHelperReplayAPI.OpenLevel(meta.levelInfo);
    if (!result.Ok) return result;

    _activeContext = new ActiveReplayContext
    {
      RecordId = record.Id,
      TufLevelId = record.TufLevelId,
      Record = record,
      OpenedAtUtc = DateTime.UtcNow.ToString("O")
    };

    return result;
  }

  public static void StopActiveReplay(string reason)
  {
    ClearActiveContext();
    Main.Instance?.Log("[ReplayService] Active replay context cleared. reason=" + reason);
  }

  public static void ClearActiveContext()
  {
    _activeContext = null;
  }
}
