using System.Text.Json.Serialization;

namespace SolarWin.Services;

public sealed record MlsNativeIdentity(
    [property: JsonPropertyName("signer")] byte[] Signer,
    [property: JsonPropertyName("public_key")] byte[] PublicKey);

public sealed record MlsNativeAddMembersResult(
    [property: JsonPropertyName("commit")] byte[] Commit,
    [property: JsonPropertyName("welcome")] byte[] Welcome,
    [property: JsonPropertyName("group_info")] byte[]? GroupInfo);

public sealed record MlsNativeCommitResult(
    [property: JsonPropertyName("commit")] byte[] Commit,
    [property: JsonPropertyName("welcome")] byte[]? Welcome,
    [property: JsonPropertyName("group_info")] byte[]? GroupInfo);

public sealed record MlsNativeExternalJoinResult(
    [property: JsonPropertyName("group_id")] byte[] GroupId,
    [property: JsonPropertyName("commit")] byte[] Commit,
    [property: JsonPropertyName("group_info")] byte[]? GroupInfo);

public sealed record MlsNativeProcessedMessage(
    [property: JsonPropertyName("message_type")] string MessageType,
    [property: JsonPropertyName("sender_index")] uint? SenderIndex,
    [property: JsonPropertyName("epoch")] long Epoch,
    [property: JsonPropertyName("application_message")] byte[]? ApplicationMessage,
    [property: JsonPropertyName("has_staged_commit")] bool HasStagedCommit,
    [property: JsonPropertyName("has_proposal")] bool HasProposal);
