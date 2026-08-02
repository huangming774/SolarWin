using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// SolarWin's RFC 9420 client orchestration. Cryptographic state and MLS operations live in
/// the official-client-compatible Rust engine; this class only coordinates Dyson contracts.
/// </summary>
public sealed class MlsClientService : IMlsClientService, IAsyncDisposable
{
    private const string Ciphersuite = "MLS_128_DHKEMX25519_AES128GCM_SHA256_Ed25519";
    private const string Scheme = "chat.mls.v2";
    private const int MinimumKeyPackages = 3;
    private readonly ISolarApiClient _api;
    private readonly IAccountSessionService _accounts;
    private readonly IMlsDeviceIdProvider _deviceIds;
    private readonly IMlsSecureStore _secureStore;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<Guid> _processedMessages = [];
    private Context? _context;

    public MlsClientService(
        ISolarApiClient api,
        IAccountSessionService accounts,
        IMlsDeviceIdProvider deviceIds,
        IMlsSecureStore secureStore)
    {
        _api = api;
        _accounts = accounts;
        _deviceIds = deviceIds;
        _secureStore = secureStore;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var context = await EnsureContextLockedAsync(cancellationToken).ConfigureAwait(false);
            await RefillKeyPackagesLockedAsync(context, force: context.IsNewIdentity, cancellationToken).ConfigureAwait(false);
            await ProcessPendingLockedAsync(context, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<SnChatRoom> EnableRoomAsync(Guid roomId, CancellationToken cancellationToken = default)
    {
        var existing = await _api.GetChatRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        if (existing.EncryptionMode != ChatRoomEncryptionMode.Mls)
        {
            if (existing.Type == ChatRoomType.Direct)
            {
                await EnsureDirectPeerReadyAsync(existing, cancellationToken).ConfigureAwait(false);
            }
            try
            {
                await _api.EnableRoomMlsAsync(roomId, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (SolarApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // CHAT_E2EE_ALREADY_ENABLED: already at the desired end state, continue with local bootstrap.
            }
        }
        var room = await _api.GetChatRoomAsync(roomId, cancellationToken).ConfigureAwait(false);
        await EnsureRoomAsync(room, addMissingMembers: true, cancellationToken).ConfigureAwait(false);
        return room;
    }

    private async Task EnsureDirectPeerReadyAsync(SnChatRoom room, CancellationToken cancellationToken)
    {
        var self = _accounts.ActiveAccountId;
        var members = await _api.GetChatMembersAsync(room.Id, cancellationToken).ConfigureAwait(false);
        var peers = members.Select(x => x.AccountId).Where(x => x != Guid.Empty && x != self).Distinct().ToList();
        if (peers.Count == 0) return;
        var readiness = await _api.CheckMlsUsersReadyAsync(new BatchCheckMlsReadyRequest { AccountIds = peers }, cancellationToken).ConfigureAwait(false);
        var notReady = peers.Where(id => readiness.Users?.FirstOrDefault(u => u.AccountId == id)?.IsReady != true).ToList();
        if (notReady.Count > 0)
        {
            throw new MlsClientException("对方还没有发布 MLS 密钥包的设备，暂不能为此私聊启用端到端加密；请让对方先用支持加密的客户端登录。", recoverable: true);
        }
    }

    public async Task EnsureRoomAsync(SnChatRoom room, bool addMissingMembers = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(room);
        var groupId = RequireMlsGroup(room);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var context = await EnsureContextLockedAsync(cancellationToken).ConfigureAwait(false);
            await RefillKeyPackagesLockedAsync(context, force: context.IsNewIdentity, cancellationToken).ConfigureAwait(false);
            await ProcessPendingLockedAsync(context, cancellationToken).ConfigureAwait(false);
            var groupBytes = Encoding.UTF8.GetBytes(groupId);
            if (!await IsActiveAsync(context.Engine, groupBytes, cancellationToken).ConfigureAwait(false))
            {
                await BootstrapOrRecoverLockedAsync(context, room, cancellationToken).ConfigureAwait(false);
            }
            if (addMissingMembers)
            {
                await AddRoomMembersLockedAsync(context, room, cancellationToken).ConfigureAwait(false);
            }
            await PublishGroupInfoLockedAsync(context, groupId, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<MlsOutgoingMessage> EncryptMessageAsync(
        SnChatRoom room,
        string? content,
        IReadOnlyList<string> attachmentIds,
        Guid? repliedMessageId,
        Guid? forwardedMessageId = null,
        CancellationToken cancellationToken = default)
    {
        var groupId = RequireMlsGroup(room);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var context = await EnsureContextLockedAsync(cancellationToken).ConfigureAwait(false);
            await ProcessPendingLockedAsync(context, cancellationToken).ConfigureAwait(false);
            var groupBytes = Encoding.UTF8.GetBytes(groupId);
            if (!await IsActiveAsync(context.Engine, groupBytes, cancellationToken).ConfigureAwait(false))
            {
                await BootstrapOrRecoverLockedAsync(context, room, cancellationToken).ConfigureAwait(false);
            }

            var clientMessageId = Guid.NewGuid().ToString("N");
            var plaintextEnvelope = new Dictionary<string, object?>
            {
                ["content"] = string.IsNullOrWhiteSpace(content) ? null : content,
                ["attachments_id"] = attachmentIds,
                ["client_message_id"] = clientMessageId,
                ["replied_message_id"] = repliedMessageId,
                ["forwarded_message_id"] = forwardedMessageId,
            };
            var plaintext = JsonSerializer.SerializeToUtf8Bytes(plaintextEnvelope, JsonDefaults.Options);
            try
            {
                var ciphertext = await context.Engine.EncryptAsync(groupBytes, context.Identity.Signer, plaintext, cancellationToken: cancellationToken).ConfigureAwait(false);
                var epoch = await context.Engine.GetEpochAsync(groupBytes, cancellationToken).ConfigureAwait(false);
                var deviceId = await _deviceIds.GetDeviceIdAsync(cancellationToken).ConfigureAwait(false);
                var header = JsonSerializer.SerializeToUtf8Bytes(new { v = 1, scheme = "mls", device_id = deviceId, epoch }, JsonDefaults.Options);
                var attachments = attachmentIds.Count == 0 ? null : attachmentIds.ToList();
                return new MlsOutgoingMessage(new SendMessageRequest
                {
                    Content = null,
                    Nonce = null,
                    ClientMessageId = clientMessageId,
                    RepliedMessageId = repliedMessageId,
                    ForwardedMessageId = forwardedMessageId,
                    AttachmentsId = attachments,
                    Meta = new Dictionary<string, object?>
                    {
                        ["attachments_id"] = attachments,
                        ["replied_message_id"] = repliedMessageId,
                        ["forwarded_message_id"] = forwardedMessageId,
                    },
                    EncryptionMeta = new SnChatEncryptionMeta
                    {
                        Ciphertext = ciphertext,
                        Header = header,
                        Signature = null,
                        Scheme = Scheme,
                        Epoch = epoch,
                    },
                }, clientMessageId, epoch);
            }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> TryDecryptMessageAsync(SnChatRoom room, SnChatMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!message.IsEncrypted && message.GetEncryptionMeta() is null) return true;
        var groupId = RequireMlsGroup(room);
        var meta = message.GetEncryptionMeta();
        if (meta is null || !string.Equals(meta.Scheme, Scheme, StringComparison.Ordinal)) return false;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (message.Id != Guid.Empty && _processedMessages.Contains(message.Id)) return !string.IsNullOrEmpty(message.Content);
            var context = await EnsureContextLockedAsync(cancellationToken).ConfigureAwait(false);
            await ProcessPendingLockedAsync(context, cancellationToken).ConfigureAwait(false);
            var groupBytes = Encoding.UTF8.GetBytes(groupId);
            if (!await IsActiveAsync(context.Engine, groupBytes, cancellationToken).ConfigureAwait(false)) return false;

            try
            {
                var result = await context.Engine.ProcessAsync(groupBytes, meta.Ciphertext, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(result.MessageType, "application", StringComparison.Ordinal) || result.ApplicationMessage is null) return false;
                ApplyPlaintextEnvelope(message, result.ApplicationMessage);
                if (message.Id != Guid.Empty) _processedMessages.Add(message.Id);
                return true;
            }
            catch (MlsNativeException) when (meta.Epoch is { } remoteEpoch)
            {
                var localEpoch = await context.Engine.GetEpochAsync(groupBytes, cancellationToken).ConfigureAwait(false);
                if (remoteEpoch <= localEpoch) return false;
                await ProcessPendingLockedAsync(context, cancellationToken).ConfigureAwait(false);
                var result = await context.Engine.ProcessAsync(groupBytes, meta.Ciphertext, cancellationToken).ConfigureAwait(false);
                if (result.ApplicationMessage is null) return false;
                ApplyPlaintextEnvelope(message, result.ApplicationMessage);
                if (message.Id != Guid.Empty) _processedMessages.Add(message.Id);
                return true;
            }
        }
        finally { _gate.Release(); }
    }

    public async Task<int> ProcessPendingEnvelopesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await ProcessPendingLockedAsync(await EnsureContextLockedAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task ResetAndRebootstrapAsync(SnChatRoom room, CancellationToken cancellationToken = default)
    {
        var groupId = RequireMlsGroup(room);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var context = await EnsureContextLockedAsync(cancellationToken).ConfigureAwait(false);
            var groupBytes = Encoding.UTF8.GetBytes(groupId);
            long epoch = 0;
            try { epoch = await context.Engine.GetEpochAsync(groupBytes, cancellationToken).ConfigureAwait(false); } catch (MlsNativeException) { }
            await _api.ResetMlsGroupAsync(groupId, new ResetMlsGroupBody
            {
                NewEpoch = 0,
                StateVersion = epoch + 1,
                Reason = "solarwin_manual_recovery",
            }, cancellationToken).ConfigureAwait(false);
            try { await context.Engine.DeleteGroupAsync(groupBytes, cancellationToken).ConfigureAwait(false); } catch (MlsNativeException) { }
            await BootstrapOrRecoverLockedAsync(context, room, cancellationToken).ConfigureAwait(false);
            await AddRoomMembersLockedAsync(context, room, cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await ClearContextLockedAsync().ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    private async Task<Context> EnsureContextLockedAsync(CancellationToken cancellationToken)
    {
        var accountId = _accounts.ActiveAccountId ?? throw new MlsClientException("尚未登录，无法初始化 MLS。", recoverable: true);
        if (_context?.AccountId == accountId) return _context;
        await ClearContextLockedAsync().ConfigureAwait(false);

        var stored = await _secureStore.LoadAsync(accountId, cancellationToken).ConfigureAwait(false);
        var isNew = stored is null;
        var databaseKey = stored?.DatabaseKey ?? RandomNumberGenerator.GetBytes(32);
        var engine = await MlsNativeEngine.OpenAsync(_secureStore.GetDatabasePath(accountId), databaseKey, cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            var identity = await engine.GenerateIdentityAsync(cancellationToken).ConfigureAwait(false);
            stored = new MlsSecureIdentity(databaseKey, identity.Signer, identity.PublicKey);
            await _secureStore.SaveAsync(accountId, stored, cancellationToken).ConfigureAwait(false);
        }
        _context = new Context(accountId, stored, engine, isNew);
        return _context;
    }

    private async Task RefillKeyPackagesLockedAsync(Context context, bool force, CancellationToken cancellationToken)
    {
        var deviceId = await _deviceIds.GetDeviceIdAsync(cancellationToken).ConfigureAwait(false);
        var needed = force ? MinimumKeyPackages : 0;
        if (!force)
        {
            var status = await _api.GetMlsKeyPackageStatusAsync(cancellationToken).ConfigureAwait(false);
            var own = status.DevicesNeedingKeyPackages?.FirstOrDefault(x => string.Equals(x.DeviceId, deviceId, StringComparison.Ordinal));
            if (own is not null)
            {
                needed = Math.Max(0, MinimumKeyPackages - own.AvailableCount);
            }
            else
            {
                var available = await _api.GetMlsDeviceKeyPackagesAsync(context.AccountId, consume: false, cancellationToken).ConfigureAwait(false);
                var ownCount = available.Count(x => string.Equals(x.DeviceId, deviceId, StringComparison.Ordinal));
                needed = Math.Max(0, MinimumKeyPackages - ownCount);
            }
        }
        for (var i = 0; i < needed; i++)
        {
            var keyPackage = await context.Engine.CreateKeyPackageAsync(context.Identity.Signer, context.Identity.PublicKey, Encoding.UTF8.GetBytes(deviceId), cancellationToken).ConfigureAwait(false);
            await _api.PublishMlsKeyPackageAsync(new PublishMlsKeyPackageBody
            {
                KeyPackage = keyPackage,
                DeviceId = deviceId,
                DeviceLabel = Environment.MachineName,
                Ciphersuite = Ciphersuite,
                Meta = new() { ["client"] = JsonSerializer.SerializeToElement("SolarWin") },
            }, cancellationToken).ConfigureAwait(false);
        }
        context.IsNewIdentity = false;
    }

    private async Task BootstrapOrRecoverLockedAsync(Context context, SnChatRoom room, CancellationToken cancellationToken)
    {
        var groupId = RequireMlsGroup(room);
        var groupBytes = Encoding.UTF8.GetBytes(groupId);
        var deviceId = await _deviceIds.GetDeviceIdAsync(cancellationToken).ConfigureAwait(false);
        var server = await _api.BootstrapMlsGroupAsync(groupId, new BootstrapMlsGroupBody
        {
            Epoch = 0,
            StateVersion = 1,
            Meta = new() { ["bootstrap_device_id"] = JsonSerializer.SerializeToElement(deviceId) },
        }, cancellationToken).ConfigureAwait(false);

        var owner = ReadString(server.Meta, "bootstrap_device_id");
        if (!string.IsNullOrWhiteSpace(owner) && !string.Equals(owner, deviceId, StringComparison.Ordinal))
        {
            await ProcessPendingLockedAsync(context, cancellationToken).ConfigureAwait(false);
            if (await IsActiveAsync(context.Engine, groupBytes, cancellationToken).ConfigureAwait(false)) return;
            throw new MlsClientException("此 MLS 群组已由另一台设备创建，当前设备尚未收到 Welcome。", recoverable: true);
        }

        await context.Engine.CreateGroupAsync(context.Identity.Signer, context.Identity.PublicKey, Encoding.UTF8.GetBytes(deviceId), groupBytes, cancellationToken).ConfigureAwait(false);
        var epoch = await context.Engine.GetEpochAsync(groupBytes, cancellationToken).ConfigureAwait(false);
        await _api.AddMlsDeviceMembershipAsync(deviceId, new AddMlsDeviceMembershipBody { GroupId = groupId, Epoch = epoch }, cancellationToken).ConfigureAwait(false);
        await PublishGroupInfoLockedAsync(context, groupId, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddRoomMembersLockedAsync(Context context, SnChatRoom room, CancellationToken cancellationToken)
    {
        var groupId = RequireMlsGroup(room);
        var deviceId = await _deviceIds.GetDeviceIdAsync(cancellationToken).ConfigureAwait(false);
        var members = await _api.GetChatMembersAsync(room.Id, cancellationToken).ConfigureAwait(false);
        var packages = new List<byte[]>();
        var targets = new Dictionary<Guid, List<string>>();
        foreach (var accountId in members.Select(x => x.AccountId).Where(x => x != Guid.Empty).Distinct())
        {
            var devices = await _api.GetMlsDeviceKeyPackagesAsync(accountId, consume: true, cancellationToken).ConfigureAwait(false);
            foreach (var device in devices.Where(x => x.KeyPackage is { Length: > 0 } && !string.Equals(x.DeviceId, deviceId, StringComparison.Ordinal)))
            {
                packages.Add(device.KeyPackage!);
                targets.GetOrAdd(accountId).Add(device.DeviceId!);
            }
        }
        if (packages.Count == 0) return;

        var groupBytes = Encoding.UTF8.GetBytes(groupId);
        var result = await context.Engine.AddMembersAsync(groupBytes, context.Identity.Signer, packages, cancellationToken).ConfigureAwait(false);
        var epoch = await context.Engine.GetEpochAsync(groupBytes, cancellationToken).ConfigureAwait(false);
        var messageId = Guid.NewGuid().ToString("N");
        await _api.FanoutMlsCommitAsync(groupId, new FanoutMlsCommitBody
        {
            Epoch = epoch,
            Ciphertext = result.Commit,
            Header = HeaderBytes(new { v = 1, type = 2, epoch, scheme = Scheme }),
            ClientMessageId = messageId,
            Meta = new() { ["reason"] = JsonSerializer.SerializeToElement("member_add"), ["client_message_id"] = JsonSerializer.SerializeToElement(messageId) },
        }, cancellationToken).ConfigureAwait(false);

        foreach (var target in targets)
        {
            await _api.FanoutMlsWelcomeAsync(groupId, new FanoutMlsWelcomeBody
            {
                RecipientAccountId = target.Key,
                Payloads = target.Value.Select(id => new FanoutEnvelopeItemBody
                {
                    RecipientDeviceId = id,
                    Ciphertext = result.Welcome,
                    Header = HeaderBytes(new { v = 1, type = 1, scheme = Scheme }),
                    ClientMessageId = messageId,
                }).ToList(),
            }, cancellationToken).ConfigureAwait(false);
        }
        await PublishGroupInfoLockedAsync(context, groupId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<int> ProcessPendingLockedAsync(Context context, CancellationToken cancellationToken)
    {
        var pending = await _api.GetPendingMlsEnvelopesAsync(500, cancellationToken).ConfigureAwait(false);
        var applied = 0;
        foreach (var envelope in pending.OrderBy(x => x.Sequence))
        {
            if (envelope.Id == Guid.Empty || string.IsNullOrWhiteSpace(envelope.GroupId) || envelope.Ciphertext is not { Length: > 0 }) continue;
            try
            {
                var groupBytes = Encoding.UTF8.GetBytes(envelope.GroupId);
                switch (envelope.Type)
                {
                    case 5:
                        await context.Engine.JoinWelcomeAsync(context.Identity.Signer, envelope.Ciphertext, null, cancellationToken).ConfigureAwait(false);
                        var epoch = await context.Engine.GetEpochAsync(groupBytes, cancellationToken).ConfigureAwait(false);
                        var deviceId = await _deviceIds.GetDeviceIdAsync(cancellationToken).ConfigureAwait(false);
                        await _api.AddMlsDeviceMembershipAsync(deviceId, new AddMlsDeviceMembershipBody { GroupId = envelope.GroupId, Epoch = epoch }, cancellationToken).ConfigureAwait(false);
                        await PublishGroupInfoLockedAsync(context, envelope.GroupId, cancellationToken).ConfigureAwait(false);
                        break;
                    case 4:
                    case 7:
                        await context.Engine.ProcessAsync(groupBytes, envelope.Ciphertext, cancellationToken).ConfigureAwait(false);
                        await PublishGroupInfoLockedAsync(context, envelope.GroupId, cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        continue;
                }
                await _api.AckMlsEnvelopeAsync(envelope.Id, cancellationToken).ConfigureAwait(false);
                applied++;
            }
            catch (Exception ex) when (ex is MlsNativeException or SolarApiException)
            {
                // Ordered state transitions are intentionally left unacknowledged for retry.
                break;
            }
        }
        return applied;
    }

    private async Task PublishGroupInfoLockedAsync(Context context, string groupId, CancellationToken cancellationToken)
    {
        var groupBytes = Encoding.UTF8.GetBytes(groupId);
        if (!await IsActiveAsync(context.Engine, groupBytes, cancellationToken).ConfigureAwait(false)) return;
        var epoch = await context.Engine.GetEpochAsync(groupBytes, cancellationToken).ConfigureAwait(false);
        await _api.UploadMlsGroupInfoAsync(groupId, new UploadGroupInfoBody
        {
            Epoch = epoch,
            GroupInfo = await context.Engine.ExportGroupInfoAsync(groupBytes, context.Identity.Signer, cancellationToken).ConfigureAwait(false),
            RatchetTree = await context.Engine.ExportRatchetTreeAsync(groupBytes, cancellationToken).ConfigureAwait(false),
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsActiveAsync(IMlsNativeEngine engine, byte[] groupId, CancellationToken cancellationToken)
    {
        try { return await engine.IsGroupActiveAsync(groupId, cancellationToken).ConfigureAwait(false); }
        catch (MlsNativeException) { return false; }
    }

    private static string RequireMlsGroup(SnChatRoom room)
    {
        if (room.EncryptionMode != ChatRoomEncryptionMode.Mls || string.IsNullOrWhiteSpace(room.MlsGroupId))
            throw new MlsClientException("房间未启用 MLS 或缺少 mls_group_id。", recoverable: false);
        return room.MlsGroupId;
    }

    private static string? ReadString(Dictionary<string, JsonElement>? values, string key)
        => values is not null && values.TryGetValue(key, out var value) ? value.ToString() : null;

    private static byte[] HeaderBytes(object value) => JsonSerializer.SerializeToUtf8Bytes(value, JsonDefaults.Options);

    private static void ApplyPlaintextEnvelope(SnChatMessage message, byte[] plaintext)
    {
        using var document = JsonDocument.Parse(plaintext);
        var root = document.RootElement;
        if (root.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String) message.Content = content.GetString();
        if (root.TryGetProperty("client_message_id", out var client) && client.ValueKind == JsonValueKind.String) message.ClientMessageId ??= client.GetString();
        if (root.TryGetProperty("replied_message_id", out var replied) && Guid.TryParse(replied.GetString(), out var repliedId)) message.RepliedMessageId ??= repliedId;
        if (root.TryGetProperty("forwarded_message_id", out var forwarded) && Guid.TryParse(forwarded.GetString(), out var forwardedId)) message.ForwardedMessageId ??= forwardedId;
    }

    private static void ZeroIdentity(MlsSecureIdentity identity)
    {
        CryptographicOperations.ZeroMemory(identity.DatabaseKey);
        CryptographicOperations.ZeroMemory(identity.Signer);
        CryptographicOperations.ZeroMemory(identity.PublicKey);
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await ClearContextLockedAsync().ConfigureAwait(false);
        }
        finally { _gate.Release(); _gate.Dispose(); }
    }

    private async Task ClearContextLockedAsync()
    {
        if (_context is not null)
        {
            await _context.Engine.DisposeAsync().ConfigureAwait(false);
            ZeroIdentity(_context.Identity);
            _context = null;
        }
        _processedMessages.Clear();
    }

    private sealed class Context(Guid accountId, MlsSecureIdentity identity, IMlsNativeEngine engine, bool isNewIdentity)
    {
        public Guid AccountId { get; } = accountId;
        public MlsSecureIdentity Identity { get; } = identity;
        public IMlsNativeEngine Engine { get; } = engine;
        public bool IsNewIdentity { get; set; } = isNewIdentity;
    }
}

public sealed class MlsClientException(string message, bool recoverable) : Exception(message)
{
    public bool Recoverable { get; } = recoverable;
}

internal static class DictionaryListExtensions
{
    public static List<TValue> GetOrAdd<TKey, TValue>(this Dictionary<TKey, List<TValue>> dictionary, TKey key)
        where TKey : notnull
    {
        if (!dictionary.TryGetValue(key, out var list)) dictionary[key] = list = [];
        return list;
    }
}
