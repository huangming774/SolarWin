namespace SolarWin.Services;

// CloudFileUrlHelper only needs the API origin; persistence tests never send HTTP requests.
internal static class SolarApiClient
{
    public const string BaseUrl = "https://api.solian.app";
}
