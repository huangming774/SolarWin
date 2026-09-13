using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SolarWin.Helpers;
using SolarWin.Models;
using SolarWin.Services;

namespace SolarWin.Tests;

public sealed class MlsContractTests
{
    private static readonly Type[] ContractModelTypes =
    [
        typeof(EnableMlsRequest),
        typeof(PublishMlsKeyPackageBody),
        typeof(SnMlsKeyPackage),
        typeof(MlsKeyPackageStatusResponse),
        typeof(MlsDeviceKpStatus),
        typeof(MlsDeviceKeyPackageResponse),
        typeof(BatchCheckMlsReadyRequest),
        typeof(BatchCheckMlsReadyResponse),
        typeof(MlsUserAvailability),
        typeof(CheckMlsReadyResponse),
        typeof(BootstrapMlsGroupBody),
        typeof(CommitMlsGroupBody),
        typeof(SnMlsGroupState),
        typeof(FanoutEnvelopeItemBody),
        typeof(FanoutMlsWelcomeBody),
        typeof(MarkMlsReshareRequiredBody),
        typeof(SnMlsDeviceMembership),
        typeof(UploadGroupInfoBody),
        typeof(FanoutEnvelopeBody),
        typeof(FanoutMlsCommitBody),
        typeof(FanoutMlsGroupMessageBody),
        typeof(SnE2eeEnvelope),
        typeof(AddMlsDeviceMembershipBody),
        typeof(ResetMlsGroupBody),
    ];

    [Fact]
    public void EveryMlsContractProperty_HasExplicitSnakeCaseMapping()
    {
        foreach (var type in ContractModelTypes)
        {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                Assert.True(attribute is not null, $"{type.Name}.{property.Name} has no JsonPropertyName.");
                Assert.Matches("^[a-z][a-z0-9_]*$", attribute!.Name);
            }
        }
    }

    [Fact]
    public void EnableMlsRequest_SerializesPolicyWithContractNames()
    {
        var request = new EnableMlsRequest
        {
            MlsGroupId = "group-1",
            E2eePolicy = new Dictionary<string, JsonElement>
            {
                ["required"] = JsonSerializer.SerializeToElement(true),
            },
        };

        var json = JsonSerializer.Serialize(request, JsonDefaults.Options);

        Assert.Equal("""{"mls_group_id":"group-1","e2ee_policy":{"required":true}}""", json);
    }

    [Fact]
    public void MlsBinaryFields_UseStandardJsonBase64()
    {
        var request = new FanoutMlsGroupMessageBody
        {
            Ciphertext = [0xfb, 0xff, 0x00],
            Header = [1, 2],
            Signature = [3, 4],
            ClientMessageId = "client-1",
        };

        var json = JsonSerializer.Serialize(request, JsonDefaults.Options);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("+/8A", document.RootElement.GetProperty("ciphertext").GetString());
        Assert.Equal("AQI=", document.RootElement.GetProperty("header").GetString());
        Assert.Equal("AwQ=", document.RootElement.GetProperty("signature").GetString());
        Assert.DoesNotContain("Ciphertext", json, StringComparison.Ordinal);
    }

    [Fact]
    public void OptionalMlsRequestFields_AreOmittedInsteadOfInventingZeroValues()
    {
        var reset = JsonSerializer.Serialize(new ResetMlsGroupBody(), JsonDefaults.Options);
        var bootstrap = JsonSerializer.Serialize(new BootstrapMlsGroupBody { Epoch = 7 }, JsonDefaults.Options);

        Assert.Equal("{}", reset);
        Assert.Equal("""{"epoch":7}""", bootstrap);
    }

    [Fact]
    public void MlsResponses_DeserializeSnakeCaseAndBinaryFields()
    {
        var accountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var envelopeId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var key = Convert.ToBase64String([1, 2, 3]);
        var envelopeJson = $$"""
            {
              "id":"{{envelopeId:D}}",
              "sender_id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "recipient_id":"cccccccc-cccc-cccc-cccc-cccccccccccc",
              "recipient_account_id":"{{accountId:D}}",
              "type":3,
              "sequence":42,
              "ciphertext":"{{key}}",
              "delivery_status":1,
              "legacy_account_scoped":false
            }
            """;

        var envelope = JsonSerializer.Deserialize<SnE2eeEnvelope>(envelopeJson, JsonDefaults.Options);

        Assert.NotNull(envelope);
        Assert.Equal(envelopeId, envelope!.Id);
        Assert.Equal(42, envelope.Sequence);
        Assert.Equal([1, 2, 3], envelope.Ciphertext);
        Assert.Equal(3, envelope.Type);
    }

    public static IEnumerable<object[]> EndpointCases()
    {
        var account = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var envelope = Guid.Parse("22222222-2222-2222-2222-222222222222");

        yield return Case(MlsApiRoutes.PublishKeyPackage(), HttpMethod.Put, "/stargate/e2ee/mls/devices/me/kps");
        yield return Case(MlsApiRoutes.KeyPackageStatus(), HttpMethod.Get, "/stargate/e2ee/mls/kp/status");
        yield return Case(MlsApiRoutes.AccountDeviceKeys(account), HttpMethod.Get, $"/stargate/e2ee/mls/keys/{account:D}/devices");
        yield return Case(MlsApiRoutes.AccountDeviceKeys(account, true), HttpMethod.Get, $"/stargate/e2ee/mls/keys/{account:D}/devices?consume=true");
        yield return Case(MlsApiRoutes.UsersReadyBatch(), HttpMethod.Post, "/stargate/e2ee/mls/users/ready/batch");
        yield return Case(MlsApiRoutes.UserReady(account), HttpMethod.Get, $"/stargate/e2ee/mls/users/{account:D}/ready");
        yield return Case(MlsApiRoutes.GroupCapableDevices("group/a"), HttpMethod.Get, "/stargate/e2ee/mls/groups/group%2Fa/devices/capable");
        yield return Case(MlsApiRoutes.BootstrapGroup("g"), HttpMethod.Post, "/stargate/e2ee/mls/groups/g/bootstrap");
        yield return Case(MlsApiRoutes.CommitGroup("g"), HttpMethod.Post, "/stargate/e2ee/mls/groups/g/commit");
        yield return Case(MlsApiRoutes.FanoutWelcome("g"), HttpMethod.Post, "/stargate/e2ee/mls/groups/g/welcome/fanout");
        yield return Case(MlsApiRoutes.MarkReshareRequired("g"), HttpMethod.Post, "/stargate/e2ee/mls/groups/g/reshare-required");
        yield return Case(MlsApiRoutes.MyReshareRequired(), HttpMethod.Get, "/stargate/e2ee/mls/devices/me/reshare-required");
        yield return Case(MlsApiRoutes.CompleteReshare("g"), HttpMethod.Post, "/stargate/e2ee/mls/devices/me/reshare-required/g/complete");
        yield return Case(MlsApiRoutes.UploadGroupInfo("g"), HttpMethod.Put, "/stargate/e2ee/mls/groups/g/groupinfo");
        yield return Case(MlsApiRoutes.GetGroupInfo("g"), HttpMethod.Get, "/stargate/e2ee/mls/groups/g/groupinfo");
        yield return Case(MlsApiRoutes.FanoutMessage(), HttpMethod.Post, "/stargate/e2ee/mls/messages/fanout");
        yield return Case(MlsApiRoutes.FanoutCommit("g"), HttpMethod.Post, "/stargate/e2ee/mls/groups/g/commit/fanout");
        yield return Case(MlsApiRoutes.FanoutGroupMessage("g"), HttpMethod.Post, "/stargate/e2ee/mls/groups/g/messages/fanout");
        yield return Case(MlsApiRoutes.PendingEnvelopes(), HttpMethod.Get, "/stargate/e2ee/mls/envelopes/pending?take=100");
        yield return Case(MlsApiRoutes.AckEnvelope(envelope), HttpMethod.Post, $"/stargate/e2ee/mls/envelopes/{envelope:D}/ack");
        yield return Case(MlsApiRoutes.RevokeDevice("device/1"), HttpMethod.Post, "/stargate/e2ee/mls/devices/device%2F1/revoke");
        yield return Case(MlsApiRoutes.AddDeviceMembership("d"), HttpMethod.Post, "/stargate/e2ee/mls/devices/d/membership");
        yield return Case(MlsApiRoutes.ResetGroup("g"), HttpMethod.Post, "/stargate/e2ee/mls/groups/g/reset");
    }

    [Theory]
    [MemberData(nameof(EndpointCases))]
    public void EveryMlsEndpoint_HasContractMethodAndPath(
        MlsApiEndpoint endpoint,
        HttpMethod expectedMethod,
        string expectedPath)
    {
        Assert.Equal(expectedMethod, endpoint.Method);
        Assert.Equal(expectedPath, endpoint.Path);
    }

    [Fact]
    public async Task MlsHeaderPolicy_InjectsStableDeviceIdOnlyForMlsRoutes()
    {
        var provider = new StubDeviceIdProvider("stable-device");
        using var mlsRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");
        using var otherRequest = new HttpRequestMessage(HttpMethod.Get, "https://example.test/");

        await MlsRequestHeaders.AttachDeviceIdAsync(
            mlsRequest,
            "/stargate/e2ee/mls/kp/status",
            provider);
        await MlsRequestHeaders.AttachDeviceIdAsync(
            otherRequest,
            "/messager/chat",
            provider);

        Assert.Equal("stable-device", mlsRequest.Headers.GetValues("X-Device-Id").Single());
        Assert.False(otherRequest.Headers.Contains("X-Device-Id"));
        Assert.False(MlsApiRoutes.RequiresDeviceId("/stargate/e2ee/mls-legacy/status"));
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void PublishKeyPackage_BindsPersistedDeviceIdentityWithoutMutatingCallerBody()
    {
        var callerBody = new PublishMlsKeyPackageBody
        {
            KeyPackage = [1, 2, 3],
            DeviceId = "untrusted-caller-id",
            DeviceLabel = "Windows",
        };

        var bound = MlsRequestFactory.BindDeviceId(callerBody, "persisted-device");

        Assert.Equal("persisted-device", bound.DeviceId);
        Assert.Equal("untrusted-caller-id", callerBody.DeviceId);
        Assert.Equal(callerBody.KeyPackage, bound.KeyPackage);
    }

    [Fact]
    public async Task PersistedDeviceId_IsStableAcrossInstancesAndConcurrentCalls()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SolarWin.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "device-id");
        var seedCalls = 0;
        try
        {
            var first = new PersistentMlsDeviceIdProvider(path, () =>
            {
                Interlocked.Increment(ref seedCalls);
                return "persisted-device";
            });

            var values = await Task.WhenAll(
                Enumerable.Range(0, 24).Select(_ => first.GetDeviceIdAsync().AsTask()));
            var second = new PersistentMlsDeviceIdProvider(path, () => "different-device");
            var reloaded = await second.GetDeviceIdAsync();

            Assert.All(values, value => Assert.Equal("persisted-device", value));
            Assert.Equal("persisted-device", reloaded);
            Assert.Equal(1, seedCalls);
            Assert.Equal("persisted-device", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: false);
            }
        }
    }

    [Fact]
    public void PlaintextSendPolicy_FailsClosedForUnknownOrMlsRooms()
    {
        var plainRoom = new SnChatRoom { EncryptionMode = ChatRoomEncryptionMode.None };
        var mlsRoom = new SnChatRoom { EncryptionMode = ChatRoomEncryptionMode.Mls };

        Assert.False(MlsRoomSendPolicy.CanUsePlaintextMessageApi(
            encryptionStateKnown: false,
            requiresUnavailableMlsState: false));
        Assert.False(MlsRoomSendPolicy.CanUsePlaintextMessageApi(
            encryptionStateKnown: true,
            requiresUnavailableMlsState: MlsRoomSendPolicy.RequiresLocalMlsState(mlsRoom)));
        Assert.True(MlsRoomSendPolicy.CanUsePlaintextMessageApi(
            encryptionStateKnown: true,
            requiresUnavailableMlsState: MlsRoomSendPolicy.RequiresLocalMlsState(plainRoom)));
    }

    private static object[] Case(MlsApiEndpoint endpoint, HttpMethod method, string path)
        => [endpoint, method, path];

    private sealed class StubDeviceIdProvider(string deviceId) : IMlsDeviceIdProvider
    {
        public int CallCount { get; private set; }

        public ValueTask<string> GetDeviceIdAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(deviceId);
        }
    }
}
