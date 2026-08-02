using System.Text.Json;
using System.Text.Json.Serialization;

namespace SolarWin.Models;

// The Padlock contract exposes MLS wire values as OpenAPI `format: byte`.
// System.Text.Json therefore serializes these byte arrays as standard Base64.

public sealed class EnableE2eeRequest
{
    [JsonPropertyName("encryption_mode")]
    public int EncryptionMode { get; set; } = 3;
}

public sealed class EnableMlsRequest
{
    [JsonPropertyName("mls_group_id")]
    public string? MlsGroupId { get; set; }

    [JsonPropertyName("e2ee_policy")]
    public Dictionary<string, JsonElement>? E2eePolicy { get; set; }
}

public sealed class PublishMlsKeyPackageBody
{
    [JsonPropertyName("key_package")]
    public required byte[] KeyPackage { get; set; }

    [JsonPropertyName("ciphersuite")]
    public string? Ciphersuite { get; set; }

    [JsonPropertyName("device_id")]
    public required string DeviceId { get; set; }

    [JsonPropertyName("device_label")]
    public string? DeviceLabel { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class SnMlsKeyPackage
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_label")]
    public string? DeviceLabel { get; set; }

    [JsonPropertyName("key_package")]
    public byte[]? KeyPackage { get; set; }

    [JsonPropertyName("ciphersuite")]
    public string? Ciphersuite { get; set; }

    [JsonPropertyName("is_consumed")]
    public bool IsConsumed { get; set; }

    [JsonPropertyName("consumed_at")]
    public DateTimeOffset? ConsumedAt { get; set; }

    [JsonPropertyName("consumed_by_account_id")]
    public Guid? ConsumedByAccountId { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class MlsKeyPackageStatusResponse
{
    [JsonPropertyName("needs_more_kps")]
    public bool NeedsMoreKeyPackages { get; set; }

    [JsonPropertyName("devices_needing_kps")]
    public List<MlsDeviceKpStatus>? DevicesNeedingKeyPackages { get; set; }
}

public sealed class MlsDeviceKpStatus
{
    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_label")]
    public string? DeviceLabel { get; set; }

    [JsonPropertyName("available_count")]
    public int AvailableCount { get; set; }
}

public sealed class MlsDeviceKeyPackageResponse
{
    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("device_label")]
    public string? DeviceLabel { get; set; }

    [JsonPropertyName("ciphersuite")]
    public string? Ciphersuite { get; set; }

    [JsonPropertyName("key_package")]
    public byte[]? KeyPackage { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class BatchCheckMlsReadyRequest
{
    [JsonPropertyName("account_ids")]
    public required List<Guid> AccountIds { get; set; }
}

public sealed class BatchCheckMlsReadyResponse
{
    [JsonPropertyName("users")]
    public List<MlsUserAvailability>? Users { get; set; }
}

public sealed class MlsUserAvailability
{
    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("is_ready")]
    public bool IsReady { get; set; }

    [JsonPropertyName("available_key_packages")]
    public int AvailableKeyPackages { get; set; }
}

public sealed class CheckMlsReadyResponse
{
    [JsonPropertyName("is_ready")]
    public bool IsReady { get; set; }

    [JsonPropertyName("available_key_packages")]
    public int AvailableKeyPackages { get; set; }
}

public sealed class BootstrapMlsGroupBody
{
    [JsonPropertyName("epoch")]
    public long Epoch { get; set; }

    [JsonPropertyName("state_version")]
    public long? StateVersion { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class CommitMlsGroupBody
{
    [JsonPropertyName("epoch")]
    public long Epoch { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class SnMlsGroupState
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("mls_group_id")]
    public string? MlsGroupId { get; set; }

    [JsonPropertyName("epoch")]
    public long Epoch { get; set; }

    [JsonPropertyName("state_version")]
    public long StateVersion { get; set; }

    [JsonPropertyName("last_commit_at")]
    public DateTimeOffset? LastCommitAt { get; set; }

    [JsonPropertyName("group_info")]
    public byte[]? GroupInfo { get; set; }

    [JsonPropertyName("ratchet_tree")]
    public byte[]? RatchetTree { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class MlsGroupInfoResponse
{
    [JsonPropertyName("group_id")]
    public string? GroupId { get; set; }

    [JsonPropertyName("epoch")]
    public long Epoch { get; set; }

    [JsonPropertyName("group_info")]
    public byte[]? GroupInfo { get; set; }

    [JsonPropertyName("ratchet_tree")]
    public byte[]? RatchetTree { get; set; }
}

public sealed class FanoutEnvelopeItemBody
{
    [JsonPropertyName("recipient_device_id")]
    public string? RecipientDeviceId { get; set; }

    [JsonPropertyName("client_message_id")]
    public string? ClientMessageId { get; set; }

    [JsonPropertyName("ciphertext")]
    public required byte[] Ciphertext { get; set; }

    [JsonPropertyName("header")]
    public byte[]? Header { get; set; }

    [JsonPropertyName("signature")]
    public byte[]? Signature { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class FanoutMlsWelcomeBody
{
    [JsonPropertyName("recipient_account_id")]
    public Guid? RecipientAccountId { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("payloads")]
    public required List<FanoutEnvelopeItemBody> Payloads { get; set; }
}

public sealed class MarkMlsReshareRequiredBody
{
    [JsonPropertyName("target_account_id")]
    public Guid TargetAccountId { get; set; }

    [JsonPropertyName("target_device_id")]
    public required string TargetDeviceId { get; set; }

    [JsonPropertyName("epoch")]
    public long Epoch { get; set; }

    [JsonPropertyName("reason")]
    public required string Reason { get; set; }
}

public sealed class SnMlsDeviceMembership
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("mls_group_id")]
    public string? MlsGroupId { get; set; }

    [JsonPropertyName("account_id")]
    public Guid AccountId { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("joined_epoch")]
    public long JoinedEpoch { get; set; }

    [JsonPropertyName("last_seen_epoch")]
    public long? LastSeenEpoch { get; set; }

    [JsonPropertyName("last_reshare_required_at")]
    public DateTimeOffset? LastReshareRequiredAt { get; set; }

    [JsonPropertyName("last_reshare_completed_at")]
    public DateTimeOffset? LastReshareCompletedAt { get; set; }
}

public sealed class UploadGroupInfoBody
{
    [JsonPropertyName("epoch")]
    public long Epoch { get; set; }

    [JsonPropertyName("group_info")]
    public required byte[] GroupInfo { get; set; }

    [JsonPropertyName("ratchet_tree")]
    public required byte[] RatchetTree { get; set; }
}

public sealed class FanoutEnvelopeBody
{
    [JsonPropertyName("recipient_account_id")]
    public Guid RecipientAccountId { get; set; }

    [JsonPropertyName("session_id")]
    public Guid? SessionId { get; set; }

    // The contract provides only numeric values and no semantic names.
    [JsonPropertyName("type")]
    public int? Type { get; set; }

    [JsonPropertyName("group_id")]
    public string? GroupId { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("include_sender_copy")]
    public bool? IncludeSenderCopy { get; set; }

    [JsonPropertyName("payloads")]
    public required List<FanoutEnvelopeItemBody> Payloads { get; set; }
}

public sealed class FanoutMlsCommitBody
{
    [JsonPropertyName("epoch")]
    public long Epoch { get; set; }

    [JsonPropertyName("ciphertext")]
    public required byte[] Ciphertext { get; set; }

    [JsonPropertyName("header")]
    public byte[]? Header { get; set; }

    [JsonPropertyName("signature")]
    public byte[]? Signature { get; set; }

    [JsonPropertyName("client_message_id")]
    public string? ClientMessageId { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class FanoutMlsGroupMessageBody
{
    [JsonPropertyName("ciphertext")]
    public required byte[] Ciphertext { get; set; }

    [JsonPropertyName("header")]
    public byte[]? Header { get; set; }

    [JsonPropertyName("signature")]
    public byte[]? Signature { get; set; }

    [JsonPropertyName("client_message_id")]
    public string? ClientMessageId { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class SnE2eeEnvelope
{
    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; set; }

    [JsonPropertyName("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("sender_id")]
    public Guid SenderId { get; set; }

    [JsonPropertyName("sender_device_id")]
    public string? SenderDeviceId { get; set; }

    [JsonPropertyName("recipient_id")]
    public Guid RecipientId { get; set; }

    [JsonPropertyName("recipient_account_id")]
    public Guid RecipientAccountId { get; set; }

    [JsonPropertyName("recipient_device_id")]
    public string? RecipientDeviceId { get; set; }

    [JsonPropertyName("session_id")]
    public Guid? SessionId { get; set; }

    [JsonPropertyName("type")]
    public int Type { get; set; }

    [JsonPropertyName("group_id")]
    public string? GroupId { get; set; }

    [JsonPropertyName("client_message_id")]
    public string? ClientMessageId { get; set; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }

    [JsonPropertyName("ciphertext")]
    public byte[]? Ciphertext { get; set; }

    [JsonPropertyName("header")]
    public byte[]? Header { get; set; }

    [JsonPropertyName("signature")]
    public byte[]? Signature { get; set; }

    [JsonPropertyName("delivery_status")]
    public int DeliveryStatus { get; set; }

    [JsonPropertyName("delivered_at")]
    public DateTimeOffset? DeliveredAt { get; set; }

    [JsonPropertyName("acked_at")]
    public DateTimeOffset? AckedAt { get; set; }

    [JsonPropertyName("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("legacy_account_scoped")]
    public bool LegacyAccountScoped { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, JsonElement>? Meta { get; set; }
}

public sealed class AddMlsDeviceMembershipBody
{
    [JsonPropertyName("group_id")]
    public required string GroupId { get; set; }

    [JsonPropertyName("epoch")]
    public long Epoch { get; set; }
}

public sealed class ResetMlsGroupBody
{
    [JsonPropertyName("new_epoch")]
    public long? NewEpoch { get; set; }

    [JsonPropertyName("state_version")]
    public long? StateVersion { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
