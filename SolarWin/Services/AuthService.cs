using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Padlock challenge-response auth for Solar Network.
/// Flow matches official Solian web client:
/// POST challenge → factors → PATCH password → POST token (grant_type=authorization_code, code=challengeId).
/// Tokens live in PasswordVault only; never written to logs.
/// </summary>
public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);
    private const int MaxPollAttempts = 15;

    private readonly ISolarApiClient _api;
    private readonly ITokenStorage _tokenStorage;
    private DateTimeOffset? _accessExpiresAt;

    public AuthService(ISolarApiClient api, ITokenStorage tokenStorage)
    {
        _api = api;
        _tokenStorage = tokenStorage;
    }

    public bool IsAuthenticated { get; private set; }

    public SnAccount? CurrentAccount { get; private set; }

    public event EventHandler? AuthenticationStateChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _accessExpiresAt = await _tokenStorage.GetAccessTokenExpiresAtAsync(cancellationToken).ConfigureAwait(false);
        var token = await GetTokenAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(token))
        {
            IsAuthenticated = false;
            CurrentAccount = null;
            RaiseAuthChanged();
            return;
        }

        try
        {
            CurrentAccount = await GetMeAsync(cancellationToken).ConfigureAwait(false);
            IsAuthenticated = true;
        }
        catch (SolarApiException)
        {
            await _tokenStorage.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _api.SetBearerTokenAsync(null, cancellationToken).ConfigureAwait(false);
            _accessExpiresAt = null;
            IsAuthenticated = false;
            CurrentAccount = null;
        }

        RaiseAuthChanged();
    }

    public async Task<TokenExchangeResponse> LoginAsync(
        string accountName,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        // 1) Create challenge
        SnAuthChallenge challenge;
        try
        {
            challenge = await StartChallengeAsync(accountName.Trim(), cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
        {
            throw new SolarApiException("账户不存在或无法创建登录挑战。", ex.StatusCode, ex.ResponseBody, ex);
        }

        // 2) Poll challenge status
        challenge = await PollChallengeAsync(challenge.Id, cancellationToken).ConfigureAwait(false);

        // 3) Resolve password factor + submit
        var factors = await GetChallengeFactorsAsync(challenge.Id, cancellationToken).ConfigureAwait(false);
        var passwordFactor = factors.FirstOrDefault(f => f.Type == AccountAuthFactorType.Password)
            ?? factors.FirstOrDefault();

        if (passwordFactor is null)
        {
            throw new SolarApiException("该账户没有可用的认证方式。");
        }

        try
        {
            challenge = await SubmitFactorAsync(challenge.Id, passwordFactor.Id, password, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
        {
            throw new SolarApiException("密码错误或验证失败。", ex.StatusCode, ex.ResponseBody, ex);
        }

        // Poll until challenge completed (step_remain == 0)
        challenge = await PollChallengeUntilCompletedAsync(challenge, cancellationToken).ConfigureAwait(false);

        if (!challenge.IsCompleted)
        {
            throw new SolarApiException(
                $"登录挑战未完成（剩余 {challenge.StepRemain}/{challenge.StepTotal} 步）。当前客户端仅支持密码单因素登录。");
        }

        // 4) Exchange token — Solian web uses grant_type=authorization_code + code=challengeId
        //    (NOT grant_type=challenge; that returns HTTP 400 on current Padlock).
        var tokens = await ExchangeTokenAsync(challenge.Id, cancellationToken).ConfigureAwait(false);

        try
        {
            CurrentAccount = await GetMeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            // Token is valid even if profile load fails; keep session.
            CurrentAccount = null;
        }

        IsAuthenticated = true;
        RaiseAuthChanged();
        return tokens;
    }

    public async Task<TokenExchangeResponse> LoginWithDeviceCodeAsync(
        string clientId,
        Action<DeviceAuthorizationResponse> onUserCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(onUserCode);

        // RFC 8628 via Padlock OIDC:
        //   POST /padlock/auth/open/device/code  (form: client_id, scope)
        //   POST /padlock/auth/open/token        (form: grant_type=device_code, ...)
        using var http = new HttpClient { BaseAddress = new Uri(SolarApiClient.BaseUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SolarWin/1.0");

        var deviceBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId.Trim(),
            ["scope"] = "openid profile email",
        });

        using var deviceResp = await http.PostAsync("padlock/auth/open/device/code", deviceBody, cancellationToken)
            .ConfigureAwait(false);
        var deviceJson = await deviceResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!deviceResp.IsSuccessStatusCode)
        {
            throw new SolarApiException(
                "无法启动 OAuth 设备码登录。请确认 client_id 已在 Solar Network 注册为公共客户端。",
                deviceResp.StatusCode,
                deviceJson);
        }

        var device = JsonSerializer.Deserialize<DeviceAuthorizationResponse>(deviceJson, JsonDefaults.Options)
            ?? throw new SolarApiException("设备码响应为空。");

        if (string.IsNullOrWhiteSpace(device.DeviceCode) || string.IsNullOrWhiteSpace(device.UserCode))
        {
            throw new SolarApiException("设备码响应缺少 device_code / user_code。", responseBody: deviceJson);
        }

        onUserCode(device);

        var interval = Math.Max(3, device.Interval <= 0 ? 5 : device.Interval);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(device.ExpiresIn > 0 ? device.ExpiresIn : 600);

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);

            var tokenBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                ["device_code"] = device.DeviceCode!,
                ["client_id"] = clientId.Trim(),
            });

            using var tokenResp = await http.PostAsync("padlock/auth/open/token", tokenBody, cancellationToken)
                .ConfigureAwait(false);
            var tokenJson = await tokenResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var oauth = JsonSerializer.Deserialize<OAuthTokenResponse>(tokenJson, JsonDefaults.Options);

            if (tokenResp.IsSuccessStatusCode)
            {
                var tokens = oauth?.ToTokenExchange()
                    ?? throw new SolarApiException("OAuth token 响应无效。", tokenResp.StatusCode, tokenJson);
                await PersistTokensAsync(tokens, cancellationToken).ConfigureAwait(false);

                try
                {
                    CurrentAccount = await GetMeAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (SolarApiException)
                {
                    CurrentAccount = null;
                }

                IsAuthenticated = true;
                RaiseAuthChanged();
                return tokens;
            }

            var err = oauth?.Error ?? string.Empty;
            if (err is "authorization_pending" or "slow_down")
            {
                if (err == "slow_down")
                {
                    interval += 2;
                }

                continue;
            }

            if (err is "expired_token" or "access_denied")
            {
                throw new SolarApiException(
                    oauth?.ErrorDescription ?? "设备码登录已取消或过期。",
                    tokenResp.StatusCode,
                    tokenJson);
            }

            // Unexpected error — keep waiting unless hard failure
            if ((int)tokenResp.StatusCode is 400 or 401)
            {
                if (err is "invalid_grant" or "invalid_client" or "unauthorized_client")
                {
                    throw new SolarApiException(
                        oauth?.ErrorDescription ?? $"OAuth 失败：{err}",
                        tokenResp.StatusCode,
                        tokenJson);
                }
            }
        }

        throw new SolarApiException("设备码登录超时，请重试。");
    }

    public async Task<TokenExchangeResponse> RefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var refresh = await _tokenStorage.GetRefreshTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(refresh))
        {
            throw new SolarApiException("没有可用的 refresh_token，请重新登录。", HttpStatusCode.Unauthorized);
        }

        var request = new TokenExchangeRequest
        {
            GrantType = "refresh_token",
            RefreshToken = refresh,
        };

        TokenExchangeResponse tokens;
        try
        {
            // Prefer dedicated refresh endpoint when available.
            tokens = await _api
                .PostAsync<TokenExchangeRequest, TokenExchangeResponse>("/padlock/auth/refresh", request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            tokens = await _api
                .PostAsync<TokenExchangeRequest, TokenExchangeResponse>("/padlock/auth/token", request, cancellationToken)
                .ConfigureAwait(false);
        }

        await PersistTokensAsync(tokens, cancellationToken).ConfigureAwait(false);
        IsAuthenticated = true;
        RaiseAuthChanged();
        return tokens;
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _api.PostAsync("/padlock/auth/logout", cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException)
        {
            // Best-effort server logout.
        }

        await _tokenStorage.ClearAsync(cancellationToken).ConfigureAwait(false);
        await _api.SetBearerTokenAsync(null, cancellationToken).ConfigureAwait(false);
        _accessExpiresAt = null;
        CurrentAccount = null;
        IsAuthenticated = false;
        RaiseAuthChanged();
    }

    public async Task<string?> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var access = await _tokenStorage.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        _accessExpiresAt ??= await _tokenStorage.GetAccessTokenExpiresAtAsync(cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(access) && !IsAccessExpired())
        {
            await _api.SetBearerTokenAsync(access, cancellationToken).ConfigureAwait(false);
            IsAuthenticated = true;
            return access;
        }

        var refresh = await _tokenStorage.GetRefreshTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(refresh))
        {
            IsAuthenticated = false;
            return null;
        }

        try
        {
            var tokens = await RefreshTokenAsync(cancellationToken).ConfigureAwait(false);
            return tokens.Token;
        }
        catch (SolarApiException)
        {
            await _tokenStorage.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _api.SetBearerTokenAsync(null, cancellationToken).ConfigureAwait(false);
            _accessExpiresAt = null;
            IsAuthenticated = false;
            CurrentAccount = null;
            RaiseAuthChanged();
            return null;
        }
    }

    public Task<SnAccount> GetMeAsync(CancellationToken cancellationToken = default)
        => _api.GetMeAsync(cancellationToken);

    private Task<SnAuthChallenge> StartChallengeAsync(string account, CancellationToken cancellationToken)
    {
        var request = new ChallengeRequest
        {
            Account = account,
            DeviceId = DeviceInfoHelper.GetDeviceId(),
            DeviceName = DeviceInfoHelper.GetDeviceName(),
            Platform = ClientPlatform.Windows, // = 5
        };

        return _api.PostAsync<ChallengeRequest, SnAuthChallenge>("/padlock/auth/challenge", request, cancellationToken);
    }

    private Task<SnAuthChallenge> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
        => _api.GetAsync<SnAuthChallenge>($"/padlock/auth/challenge/{challengeId}", cancellationToken);

    private async Task<IReadOnlyList<SnAccountAuthFactor>> GetChallengeFactorsAsync(
        Guid challengeId,
        CancellationToken cancellationToken)
    {
        var factors = await _api
            .GetAsync<List<SnAccountAuthFactor>>($"/padlock/auth/challenge/{challengeId}/factors", cancellationToken)
            .ConfigureAwait(false);
        return factors;
    }

    private Task<SnAuthChallenge> SubmitFactorAsync(
        Guid challengeId,
        Guid factorId,
        string password,
        CancellationToken cancellationToken)
    {
        var request = new PerformChallengeRequest
        {
            FactorId = factorId,
            Password = password,
        };

        return _api.PatchAsync<PerformChallengeRequest, SnAuthChallenge>(
            $"/padlock/auth/challenge/{challengeId}",
            request,
            cancellationToken);
    }

    private async Task<TokenExchangeResponse> ExchangeTokenAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        // Official Solian front-end:
        //   POST /padlock/auth/token  { grant_type: "authorization_code", code: challengeId }
        var primary = new TokenExchangeRequest
        {
            GrantType = "authorization_code",
            Code = challengeId.ToString(),
        };

        try
        {
            var tokens = await _api
                .PostAsync<TokenExchangeRequest, TokenExchangeResponse>("/padlock/auth/token", primary, cancellationToken)
                .ConfigureAwait(false);
            await PersistTokensAsync(tokens, cancellationToken).ConfigureAwait(false);
            return tokens;
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.BadRequest)
        {
            // Fallback for older/experimental deployments.
            var fallback = new TokenExchangeRequest
            {
                GrantType = "challenge",
                Code = challengeId.ToString(),
            };

            try
            {
                var tokens = await _api
                    .PostAsync<TokenExchangeRequest, TokenExchangeResponse>("/padlock/auth/token", fallback, cancellationToken)
                    .ConfigureAwait(false);
                await PersistTokensAsync(tokens, cancellationToken).ConfigureAwait(false);
                return tokens;
            }
            catch (SolarApiException)
            {
                throw new SolarApiException(
                    "令牌交换失败。服务端拒绝了 authorization_code / challenge 换票。",
                    ex.StatusCode,
                    ex.ResponseBody,
                    ex);
            }
        }
    }

    private async Task<SnAuthChallenge> PollChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        SnAuthChallenge? last = null;
        for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
        {
            last = await GetChallengeAsync(challengeId, cancellationToken).ConfigureAwait(false);
            // Ready when we can query factors (challenge exists and not declined).
            if (last.DeclinedAt is null && last.Id != Guid.Empty)
            {
                return last;
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }

        return last ?? throw new SolarApiException("登录挑战状态轮询超时。");
    }

    private async Task<SnAuthChallenge> PollChallengeUntilCompletedAsync(
        SnAuthChallenge challenge,
        CancellationToken cancellationToken)
    {
        if (challenge.IsCompleted)
        {
            return challenge;
        }

        for (var attempt = 0; attempt < MaxPollAttempts; attempt++)
        {
            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            challenge = await GetChallengeAsync(challenge.Id, cancellationToken).ConfigureAwait(false);
            if (challenge.IsCompleted)
            {
                return challenge;
            }

            if (challenge.DeclinedAt is not null)
            {
                throw new SolarApiException("登录挑战已被拒绝。");
            }
        }

        return challenge;
    }

    private async Task PersistTokensAsync(TokenExchangeResponse tokens, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokens.Token))
        {
            throw new SolarApiException("令牌交换返回了空的 access token。");
        }

        DateTimeOffset? expiresAt = tokens.ExpiresIn > 0
            ? DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn)
            : null;

        // Clock skew buffer: treat as expired 30s early when checking.
        _accessExpiresAt = expiresAt;

        await _tokenStorage
            .SaveTokensAsync(tokens.Token, tokens.RefreshToken, expiresAt, cancellationToken)
            .ConfigureAwait(false);
        await _api.SetBearerTokenAsync(tokens.Token, cancellationToken).ConfigureAwait(false);
    }

    private bool IsAccessExpired()
    {
        if (_accessExpiresAt is null)
        {
            // Unknown expiry: assume still valid until API rejects.
            return false;
        }

        return DateTimeOffset.UtcNow >= _accessExpiresAt.Value.AddSeconds(-30);
    }

    private void RaiseAuthChanged()
        => AuthenticationStateChanged?.Invoke(this, EventArgs.Empty);
}
