using System.Security.Cryptography;
using System.Text;
using System.Runtime.InteropServices;
using SolarWin.Services;

namespace SolarWin.Tests;

public sealed class MlsNativeIntegrationTests
{
    [Fact]
    public async Task TwoDevices_CanJoinExchangeMessagesAndReopenEncryptedState()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64 || !File.Exists(Path.Combine(AppContext.BaseDirectory, "SolarWin.Mls.Native.dll")))
        {
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "solarwin-mls-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var alicePath = Path.Combine(root, "alice.db");
        var bobPath = Path.Combine(root, "bob.db");
        var aliceKey = RandomNumberGenerator.GetBytes(32);
        var bobKey = RandomNumberGenerator.GetBytes(32);
        var group = Encoding.UTF8.GetBytes("room:" + Guid.NewGuid().ToString("D"));
        try
        {
            await using (var alice = await MlsNativeEngine.OpenAsync(alicePath, aliceKey))
            await using (var bob = await MlsNativeEngine.OpenAsync(bobPath, bobKey))
            {
                var aliceIdentity = await alice.GenerateIdentityAsync();
                var bobIdentity = await bob.GenerateIdentityAsync();
                var bobKp = await bob.CreateKeyPackageAsync(bobIdentity.Signer, bobIdentity.PublicKey, Encoding.UTF8.GetBytes("bob-device"));

                await alice.CreateGroupAsync(aliceIdentity.Signer, aliceIdentity.PublicKey, Encoding.UTF8.GetBytes("alice-device"), group);
                var add = await alice.AddMembersAsync(group, aliceIdentity.Signer, [bobKp]);
                await bob.JoinWelcomeAsync(bobIdentity.Signer, add.Welcome, null);

                Assert.Equal(1, await alice.GetEpochAsync(group));
                Assert.Equal(1, await bob.GetEpochAsync(group));

                var firstPlaintext = Encoding.UTF8.GetBytes("SolarWin Alice to Bob");
                var firstCiphertext = await alice.EncryptAsync(group, aliceIdentity.Signer, firstPlaintext);
                var first = await bob.ProcessAsync(group, firstCiphertext);
                Assert.Equal("application", first.MessageType);
                Assert.Equal(firstPlaintext, first.ApplicationMessage);

                var secondPlaintext = Encoding.UTF8.GetBytes("SolarWin Bob to Alice");
                var secondCiphertext = await bob.EncryptAsync(group, bobIdentity.Signer, secondPlaintext);
                var second = await alice.ProcessAsync(group, secondCiphertext);
                Assert.Equal(secondPlaintext, second.ApplicationMessage);

            }

            // The engine holds an exclusive lock while open. After clean close, SQLCipher must
            // not expose application plaintext in the database file.
            var databaseBytes = await File.ReadAllBytesAsync(alicePath);
            Assert.DoesNotContain("SolarWin Bob to Alice", Encoding.UTF8.GetString(databaseBytes), StringComparison.Ordinal);

            await using var reopened = await MlsNativeEngine.OpenAsync(alicePath, aliceKey);
            Assert.True(await reopened.IsGroupActiveAsync(group));
            Assert.Equal(1, await reopened.GetEpochAsync(group));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aliceKey);
            CryptographicOperations.ZeroMemory(bobKey);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
