using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SolarWin.Services;

/// <summary>
/// MCP tool allowlist. Add new external AI capabilities here only after reviewing
/// their data exposure and whether they require explicit user confirmation.
/// </summary>
[McpServerToolType]
public sealed class SolarWinMcpTools
{
    private readonly IAuthService _auth;

    public SolarWinMcpTools(IAuthService auth) => _auth = auth;

    [McpServerTool, Description("Returns the local SolarWin connection and account status. This tool is read-only.")]
    public object GetSolarWinStatus()
    {
        var account = _auth.CurrentAccount;
        return new
        {
            application = "SolarWin",
            version = typeof(SolarWinMcpTools).Assembly.GetName().Version?.ToString(3) ?? "unknown",
            authenticated = _auth.IsAuthenticated,
            account = account is null
                ? null
                : new
                {
                    id = account.Id,
                    name = account.Name,
                    nick = account.Nick,
                },
        };
    }
}
