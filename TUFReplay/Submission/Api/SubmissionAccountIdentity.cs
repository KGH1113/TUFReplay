using System;
using Newtonsoft.Json.Linq;

namespace TUFReplay.Submission.Api;

/// <summary>Validated account fields returned by the authenticated Rust account endpoint.</summary>
public sealed class SubmissionAccountIdentity
{
  public string OwnerId { get; }
  public Guid GrantId { get; }
  public string ClientId { get; }
  public string Username { get; }
  public string Nickname { get; }
  public bool CanSubmit { get; }
  public string DenialReason { get; }

  private SubmissionAccountIdentity(
    string ownerId,
    Guid grantId,
    string clientId,
    string username,
    string nickname,
    bool canSubmit,
    string denialReason
  )
  {
    OwnerId = ownerId;
    GrantId = grantId;
    ClientId = clientId;
    Username = username;
    Nickname = nickname;
    CanSubmit = canSubmit;
    DenialReason = denialReason;
  }

  public static SubmissionAccountIdentity Parse(JObject value)
  {
    string ownerId = (string)value?["owner_id"];
    string grantIdValue = (string)value?["grant_id"];
    string clientId = (string)value?["client_id"];
    string username = (string)value?["username"];
    JToken nicknameToken = value?["nickname"];
    string nickname = nicknameToken?.Type == JTokenType.String ? (string)nicknameToken : null;
    bool canSubmit = (bool?)value?["can_submit"] == true;
    JToken denialToken = value?["denial_reason"];
    string denialReason = denialToken?.Type == JTokenType.String ? (string)denialToken : null;

    if (
      string.IsNullOrWhiteSpace(ownerId)
      || ownerId.Length > 128
      || !Guid.TryParse(grantIdValue, out Guid grantId)
      || string.IsNullOrWhiteSpace(clientId)
      || clientId.Length > 64
      || string.IsNullOrWhiteSpace(username)
      || username.Length > 128
      || (nicknameToken != null && nicknameToken.Type != JTokenType.Null && nicknameToken.Type != JTokenType.String)
      || (nickname != null && nickname.Length > 128)
      || (value?["can_submit"] != null && value["can_submit"].Type != JTokenType.Boolean)
      || (denialToken != null && denialToken.Type != JTokenType.Null && denialToken.Type != JTokenType.String)
      || !IsKnownDenialReason(denialReason)
    )
      throw new InvalidOperationException("invalid_account_identity");

    return new SubmissionAccountIdentity(ownerId, grantId, clientId, username, nickname, canSubmit, denialReason);
  }

  private static bool IsKnownDenialReason(string value) =>
    value == null || value == "auto_submission_disabled" || value == "auto_submission_tester_required";
}
