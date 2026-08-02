using System.Runtime.InteropServices;
using System.Text.Json;
using SolarWin.Helpers;

namespace SolarWin.Services;

public sealed class MlsNativeEngine : IMlsNativeEngine
{
    private const string LibraryName = "SolarWin.Mls.Native.dll";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private nint _handle;

    private MlsNativeEngine(nint handle) => _handle = handle;

    public static Task<MlsNativeEngine> OpenAsync(string databasePath, byte[] databaseKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentNullException.ThrowIfNull(databaseKey);
        if (databaseKey.Length != 32) throw new ArgumentException("MLS database key must be 32 bytes.", nameof(databaseKey));
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new MlsNativeException("MLS 原生引擎当前仅提供 Windows x64 构建；该架构已按 fail-closed 禁用。 ");

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
            nint handle;
            try { handle = NativeMethods.Open(databasePath, databaseKey, (nuint)databaseKey.Length); }
            catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
            {
                throw new MlsNativeException($"无法加载 MLS 原生引擎：{ex.GetType().Name}");
            }
            if (handle == 0)
            {
                throw new MlsNativeException("无法打开 MLS SQLCipher 状态库。请确认当前为 x64 构建且 SolarWin.Mls.Native.dll 已随应用部署。");
            }
            return new MlsNativeEngine(handle);
        }, cancellationToken);
    }

    public Task<MlsNativeIdentity> GenerateIdentityAsync(CancellationToken cancellationToken = default)
        => CallAsync<object, MlsNativeIdentity>("generate_signer", new { }, cancellationToken);

    public async Task<byte[]> CreateKeyPackageAsync(byte[] signer, byte[] publicKey, byte[] identity, CancellationToken cancellationToken = default)
        => (await CallAsync<object, BytesValue>("create_key_package", new
        {
            signer,
            public_key = publicKey,
            identity,
        }, cancellationToken)).KeyPackage!;

    public Task CreateGroupAsync(byte[] signer, byte[] publicKey, byte[] identity, byte[] groupId, CancellationToken cancellationToken = default)
        => CallNoResultAsync("create_group", new { signer, public_key = publicKey, identity, group_id = groupId }, cancellationToken);

    public Task JoinWelcomeAsync(byte[] signer, byte[] welcome, byte[]? ratchetTree, CancellationToken cancellationToken = default)
        => CallNoResultAsync("join_welcome", new { signer, welcome, ratchet_tree = ratchetTree }, cancellationToken);

    public Task<MlsNativeExternalJoinResult> JoinExternalAsync(byte[] signer, byte[] publicKey, byte[] identity, byte[] groupInfo, byte[]? ratchetTree, CancellationToken cancellationToken = default)
        => CallAsync<object, MlsNativeExternalJoinResult>("join_external", new
        {
            signer,
            public_key = publicKey,
            identity,
            group_info = groupInfo,
            ratchet_tree = ratchetTree,
        }, cancellationToken);

    public async Task<long> GetEpochAsync(byte[] groupId, CancellationToken cancellationToken = default)
        => (await CallAsync<object, EpochValue>("group_epoch", new { group_id = groupId }, cancellationToken)).Epoch;

    public async Task<bool> IsGroupActiveAsync(byte[] groupId, CancellationToken cancellationToken = default)
        => (await CallAsync<object, ActiveValue>("group_active", new { group_id = groupId }, cancellationToken)).Active;

    public async Task<byte[]> ExportRatchetTreeAsync(byte[] groupId, CancellationToken cancellationToken = default)
        => (await CallAsync<object, BytesValue>("export_ratchet_tree", new { group_id = groupId }, cancellationToken)).RatchetTree!;

    public async Task<byte[]> ExportGroupInfoAsync(byte[] groupId, byte[] signer, CancellationToken cancellationToken = default)
        => (await CallAsync<object, BytesValue>("export_group_info", new { group_id = groupId, signer }, cancellationToken)).GroupInfo!;

    public Task<MlsNativeAddMembersResult> AddMembersAsync(byte[] groupId, byte[] signer, IReadOnlyList<byte[]> keyPackages, CancellationToken cancellationToken = default)
        => CallAsync<object, MlsNativeAddMembersResult>("add_members", new { group_id = groupId, signer, key_packages = keyPackages }, cancellationToken);

    public Task<MlsNativeCommitResult> RemoveMembersAsync(byte[] groupId, byte[] signer, IReadOnlyList<uint> memberIndices, CancellationToken cancellationToken = default)
        => CallAsync<object, MlsNativeCommitResult>("remove_members", new { group_id = groupId, signer, member_indices = memberIndices }, cancellationToken);

    public async Task<byte[]> EncryptAsync(byte[] groupId, byte[] signer, byte[] message, byte[]? aad = null, CancellationToken cancellationToken = default)
        => (await CallAsync<object, BytesValue>("create_message", new { group_id = groupId, signer, message, aad }, cancellationToken)).Ciphertext!;

    public Task<MlsNativeProcessedMessage> ProcessAsync(byte[] groupId, byte[] message, CancellationToken cancellationToken = default)
        => CallAsync<object, MlsNativeProcessedMessage>("process_message", new { group_id = groupId, message }, cancellationToken);

    public Task DeleteGroupAsync(byte[] groupId, CancellationToken cancellationToken = default)
        => CallNoResultAsync("delete_group", new { group_id = groupId }, cancellationToken);

    private async Task CallNoResultAsync(string operation, object request, CancellationToken cancellationToken)
        => _ = await CallAsync<object, JsonElement>(operation, request, cancellationToken);

    private async Task<TValue> CallAsync<TRequest, TValue>(string operation, TRequest request, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_handle == 0) throw new ObjectDisposedException(nameof(MlsNativeEngine));
            var requestJson = JsonSerializer.Serialize(request, JsonDefaults.Options);
            var responseJson = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pointer = NativeMethods.Call(_handle, operation, requestJson);
                if (pointer == 0) throw new MlsNativeException("MLS 原生桥接未返回结果。");
                try { return Marshal.PtrToStringUTF8(pointer) ?? string.Empty; }
                finally { NativeMethods.FreeString(pointer); }
            }, cancellationToken).ConfigureAwait(false);

            var response = JsonSerializer.Deserialize<BridgeResponse<TValue>>(responseJson, JsonDefaults.Options)
                ?? throw new MlsNativeException("MLS 原生桥接返回了无效 JSON。");
            if (!response.Ok) throw new MlsNativeException(response.Error ?? "MLS 操作失败。");
            return response.Value ?? throw new MlsNativeException("MLS 原生桥接响应缺少 value。");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var handle = Interlocked.Exchange(ref _handle, 0);
            if (handle != 0) await Task.Run(() => NativeMethods.Close(handle)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private sealed record BridgeResponse<T>(bool Ok, T? Value, string? Error);
    private sealed record EpochValue(long Epoch);
    private sealed record ActiveValue(bool Active);
    private sealed class BytesValue
    {
        public byte[]? KeyPackage { get; init; }
        public byte[]? RatchetTree { get; init; }
        public byte[]? GroupInfo { get; init; }
        public byte[]? Ciphertext { get; init; }
    }

    private static class NativeMethods
    {
        [DllImport(LibraryName, EntryPoint = "sw_mls_open", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint Open(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string databasePath,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] key,
            nuint keyLength);

        [DllImport(LibraryName, EntryPoint = "sw_mls_call", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint Call(
            nint engine,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string operation,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string requestJson);

        [DllImport(LibraryName, EntryPoint = "sw_mls_close", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Close(nint engine);

        [DllImport(LibraryName, EntryPoint = "sw_mls_free_string", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void FreeString(nint value);
    }
}

public sealed class MlsNativeException(string message) : Exception(message);
