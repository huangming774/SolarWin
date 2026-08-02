using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SolarWin.Helpers;

namespace SolarWin.Services;

public sealed record MlsSecureIdentity(byte[] DatabaseKey, byte[] Signer, byte[] PublicKey);

public interface IMlsSecureStore
{
    Task<MlsSecureIdentity?> LoadAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid accountId, MlsSecureIdentity identity, CancellationToken cancellationToken = default);
    string GetDatabasePath(Guid accountId);
}

public sealed class MlsSecureStore : IMlsSecureStore
{
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("SolarWin.MLS.Identity.v1");

    public string GetDatabasePath(Guid accountId)
        => Path.Combine(GetAccountDirectory(accountId), "mls_encrypted.db");

    public async Task<MlsSecureIdentity?> LoadAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var path = GetIdentityPath(accountId);
        if (!File.Exists(path)) return null;
        var protectedBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            var plaintext = ProtectedData.Unprotect(protectedBytes, GetEntropy(accountId), DataProtectionScope.CurrentUser);
            try
            {
                return JsonSerializer.Deserialize<MlsSecureIdentity>(plaintext, JsonDefaults.Options)
                    ?? throw new CryptographicException("MLS identity file is empty.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }

    public async Task SaveAsync(Guid accountId, MlsSecureIdentity identity, CancellationToken cancellationToken = default)
    {
        var directory = GetAccountDirectory(accountId);
        Directory.CreateDirectory(directory);
        var path = GetIdentityPath(accountId);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(identity, JsonDefaults.Options);
        byte[]? protectedBytes = null;
        try
        {
            protectedBytes = ProtectedData.Protect(plaintext, GetEntropy(accountId), DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(temporary, protectedBytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            if (protectedBytes is not null) CryptographicOperations.ZeroMemory(protectedBytes);
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string GetAccountDirectory(Guid accountId)
        => Path.Combine(AppPaths.RootDirectory, "mls", accountId.ToString("N"));

    private static string GetIdentityPath(Guid accountId)
        => Path.Combine(GetAccountDirectory(accountId), "identity.dpapi");

    private static byte[] GetEntropy(Guid accountId)
    {
        var entropy = new byte[Purpose.Length + 16];
        Purpose.CopyTo(entropy, 0);
        accountId.TryWriteBytes(entropy.AsSpan(Purpose.Length));
        return entropy;
    }
}
