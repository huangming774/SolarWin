namespace SolarWin.Services;

internal static class QrLoginRoutes
{
    internal const string Generate = "/stargate/auth/qr/generate";

    internal static string Status(Guid challengeId)
    {
        if (challengeId == Guid.Empty)
            throw new ArgumentException("QR challenge ID must not be empty.", nameof(challengeId));

        return $"/stargate/auth/qr/{challengeId:D}";
    }
}
