namespace SolarWin.Services;

public interface IMlsNativeEngine : IAsyncDisposable
{
    Task<MlsNativeIdentity> GenerateIdentityAsync(CancellationToken cancellationToken = default);
    Task<byte[]> CreateKeyPackageAsync(byte[] signer, byte[] publicKey, byte[] identity, CancellationToken cancellationToken = default);
    Task CreateGroupAsync(byte[] signer, byte[] publicKey, byte[] identity, byte[] groupId, CancellationToken cancellationToken = default);
    Task JoinWelcomeAsync(byte[] signer, byte[] welcome, byte[]? ratchetTree, CancellationToken cancellationToken = default);
    Task<MlsNativeExternalJoinResult> JoinExternalAsync(byte[] signer, byte[] publicKey, byte[] identity, byte[] groupInfo, byte[]? ratchetTree, CancellationToken cancellationToken = default);
    Task<long> GetEpochAsync(byte[] groupId, CancellationToken cancellationToken = default);
    Task<bool> IsGroupActiveAsync(byte[] groupId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportRatchetTreeAsync(byte[] groupId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportGroupInfoAsync(byte[] groupId, byte[] signer, CancellationToken cancellationToken = default);
    Task<MlsNativeAddMembersResult> AddMembersAsync(byte[] groupId, byte[] signer, IReadOnlyList<byte[]> keyPackages, CancellationToken cancellationToken = default);
    Task<MlsNativeCommitResult> RemoveMembersAsync(byte[] groupId, byte[] signer, IReadOnlyList<uint> memberIndices, CancellationToken cancellationToken = default);
    Task<byte[]> EncryptAsync(byte[] groupId, byte[] signer, byte[] message, byte[]? aad = null, CancellationToken cancellationToken = default);
    Task<MlsNativeProcessedMessage> ProcessAsync(byte[] groupId, byte[] message, CancellationToken cancellationToken = default);
    Task DeleteGroupAsync(byte[] groupId, CancellationToken cancellationToken = default);
}
