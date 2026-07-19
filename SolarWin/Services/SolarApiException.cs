using System.Net;
using SolarWin.Helpers;

namespace SolarWin.Services;

/// <summary>HTTP / API failure raised by <see cref="ISolarApiClient"/>.</summary>
public sealed class SolarApiException : Exception
{
    public SolarApiException(string message, HttpStatusCode? statusCode = null, string? responseBody = null, Exception? inner = null)
        : base(BuildMessage(message, responseBody), inner)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        ApiMessage = ApiErrorParser.TryGetMessage(responseBody);
    }

    public HttpStatusCode? StatusCode { get; }

    public string? ResponseBody { get; }

    /// <summary>Parsed server message when available.</summary>
    public string? ApiMessage { get; }

    private static string BuildMessage(string message, string? responseBody)
    {
        var api = ApiErrorParser.TryGetMessage(responseBody);
        if (string.IsNullOrWhiteSpace(api))
        {
            return message;
        }

        // Prefer server message; keep status context when present.
        if (message.Contains("API request failed", StringComparison.OrdinalIgnoreCase))
        {
            return $"{message} — {api}";
        }

        return string.Equals(message, api, StringComparison.Ordinal) ? message : $"{message} — {api}";
    }
}
