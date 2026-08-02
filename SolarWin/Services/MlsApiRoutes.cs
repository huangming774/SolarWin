namespace SolarWin.Services;

public readonly record struct MlsApiEndpoint(HttpMethod Method, string Path);

/// <summary>Contract-derived routes for Padlock MLS delivery APIs.</summary>
public static class MlsApiRoutes
{
    public const string Prefix = "/padlock/e2ee/mls";

    public static MlsApiEndpoint PublishKeyPackage() => Put("/devices/me/kps");
    public static MlsApiEndpoint KeyPackageStatus() => Get("/kp/status");

    public static MlsApiEndpoint AccountDeviceKeys(Guid accountId, bool? consume = null)
    {
        var query = consume is null ? string.Empty : $"?consume={consume.Value.ToString().ToLowerInvariant()}";
        return Get($"/keys/{accountId:D}/devices{query}");
    }

    public static MlsApiEndpoint UsersReadyBatch() => Post("/users/ready/batch");
    public static MlsApiEndpoint UserReady(Guid accountId) => Get($"/users/{accountId:D}/ready");
    public static MlsApiEndpoint GroupCapableDevices(string groupId) => Get($"/groups/{Segment(groupId)}/devices/capable");
    public static MlsApiEndpoint BootstrapGroup(string groupId) => Post($"/groups/{Segment(groupId)}/bootstrap");
    public static MlsApiEndpoint CommitGroup(string groupId) => Post($"/groups/{Segment(groupId)}/commit");
    public static MlsApiEndpoint FanoutWelcome(string groupId) => Post($"/groups/{Segment(groupId)}/welcome/fanout");
    public static MlsApiEndpoint MarkReshareRequired(string groupId) => Post($"/groups/{Segment(groupId)}/reshare-required");
    public static MlsApiEndpoint MyReshareRequired() => Get("/devices/me/reshare-required");
    public static MlsApiEndpoint CompleteReshare(string groupId) => Post($"/devices/me/reshare-required/{Segment(groupId)}/complete");
    public static MlsApiEndpoint UploadGroupInfo(string groupId) => Put($"/groups/{Segment(groupId)}/groupinfo");
    public static MlsApiEndpoint GetGroupInfo(string groupId) => Get($"/groups/{Segment(groupId)}/groupinfo");
    public static MlsApiEndpoint FanoutMessage() => Post("/messages/fanout");
    public static MlsApiEndpoint FanoutCommit(string groupId) => Post($"/groups/{Segment(groupId)}/commit/fanout");
    public static MlsApiEndpoint FanoutGroupMessage(string groupId) => Post($"/groups/{Segment(groupId)}/messages/fanout");
    public static MlsApiEndpoint PendingEnvelopes(int take = 100) => Get($"/envelopes/pending?take={take}");
    public static MlsApiEndpoint AckEnvelope(Guid envelopeId) => Post($"/envelopes/{envelopeId:D}/ack");
    public static MlsApiEndpoint RevokeDevice(string deviceId) => Post($"/devices/{Segment(deviceId)}/revoke");
    public static MlsApiEndpoint AddDeviceMembership(string deviceId) => Post($"/devices/{Segment(deviceId)}/membership");
    public static MlsApiEndpoint ResetGroup(string groupId) => Post($"/groups/{Segment(groupId)}/reset");

    public static bool RequiresDeviceId(string relativePath)
        => string.Equals(relativePath, Prefix, StringComparison.OrdinalIgnoreCase)
           || relativePath.StartsWith(Prefix + "/", StringComparison.OrdinalIgnoreCase);

    private static MlsApiEndpoint Get(string suffix) => new(HttpMethod.Get, Prefix + suffix);
    private static MlsApiEndpoint Post(string suffix) => new(HttpMethod.Post, Prefix + suffix);
    private static MlsApiEndpoint Put(string suffix) => new(HttpMethod.Put, Prefix + suffix);

    private static string Segment(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return Uri.EscapeDataString(value.Trim());
    }
}
