using SolarWin.Models;

namespace SolarWin.Services;

public sealed record MlsOutgoingMessage(SendMessageRequest Request, string ClientMessageId, long Epoch);

public interface IMlsClientService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task EnsureRoomAsync(SnChatRoom room, bool addMissingMembers = false, CancellationToken cancellationToken = default);
    Task<SnChatRoom> EnableRoomAsync(Guid roomId, CancellationToken cancellationToken = default);
    Task<MlsOutgoingMessage> EncryptMessageAsync(
        SnChatRoom room,
        string? content,
        IReadOnlyList<string> attachmentIds,
        Guid? repliedMessageId,
        Guid? forwardedMessageId = null,
        CancellationToken cancellationToken = default);
    Task<bool> TryDecryptMessageAsync(SnChatRoom room, SnChatMessage message, CancellationToken cancellationToken = default);
    Task<int> ProcessPendingEnvelopesAsync(CancellationToken cancellationToken = default);
    Task ResetAndRebootstrapAsync(SnChatRoom room, CancellationToken cancellationToken = default);
    Task ResetAsync(CancellationToken cancellationToken = default);
}
