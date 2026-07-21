using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using SolarWin.Helpers;
using SolarWin.Models;

namespace SolarWin.Services;

/// <summary>
/// Padlock challenge-response auth for Solar Network.
/// Flow matches official Solian web client:
/// POST challenge → factors → PATCH password → (optional MFA) → POST token (grant_type=authorization_code).
/// Also supports OAuth device code and QR login. Tokens live in PasswordVault only.
/// </summary>
public sealed class AuthService : IAuthService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);
    private const int MaxPollAttempts = 15;
    private static readonly TimeSpan QrPollInterval = TimeSpan.FromSeconds(2);

    private readonly ISolarApiClient _api;
    private readonly ITokenStorage _tokenStorage;
    private readonly SocialLoginService _socialLogin;
    private readonly IAccountSessionService _sessions;
    private DateTimeOffset? _accessExpiresAt;

    public AuthService(
        ISolarApiClient api,
        ITokenStorage tokenStorage,
        SocialLoginService socialLogin,
        IAccountSessionService sessions)
    {
        _api = api;
        _tokenStorage = tokenStorage;
        _socialLogin = socialLogin;
        _sessions = sessions;
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
            if (CurrentAccount is not null)
            {
                await _sessions.RememberProfileAsync(CurrentAccount, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await _tokenStorage.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _api.SetBearerTokenAsync(null, cancellationToken).ConfigureAwait(false);
            _accessExpiresAt = null;
            IsAuthenticated = false;
            CurrentAccount = null;
        }
        catch (SolarApiException)
        {
            // Offline / 5xx: try disk cache for last account, keep tokens for retry.
            if (_sessions.ActiveAccountId is { } aid
                && OfflineCache.TryGetJson<SnAccount>($"account_me_{aid:N}", out var cached, allowExpired: true)
                && cached is not null)
            {
                CurrentAccount = cached;
                IsAuthenticated = true;
            }
            else
            {
                IsAuthenticated = !string.IsNullOrWhiteSpace(token);
                CurrentAccount = null;
            }
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

        // Poll once — may still need more factors
        challenge = await PollChallengeUntilCompletedAsync(challenge, cancellationToken).ConfigureAwait(false);

        if (!challenge.IsCompleted)
        {
            var remaining = await GetRemainingFactorsAsync(challenge, passwordFactor.Id, cancellationToken)
                .ConfigureAwait(false);
            throw new MultiFactorRequiredException(challenge.Id, remaining, challenge);
        }

        return await FinishLoginAsync(challenge.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TokenExchangeResponse> CompleteFactorAsync(
        Guid challengeId,
        Guid factorId,
        string secret,
        CancellationToken cancellationToken = default)
    {
        if (challengeId == Guid.Empty)
        {
            throw new ArgumentException("challengeId 无效。", nameof(challengeId));
        }

        if (factorId == Guid.Empty)
        {
            throw new ArgumentException("factorId 无效。", nameof(factorId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        SnAuthChallenge challenge;
        try
        {
            challenge = await SubmitFactorAsync(challengeId, factorId, secret.Trim(), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex) when (ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
        {
            throw new SolarApiException("验证码或第二因素错误。", ex.StatusCode, ex.ResponseBody, ex);
        }

        challenge = await PollChallengeUntilCompletedAsync(challenge, cancellationToken).ConfigureAwait(false);

        if (!challenge.IsCompleted)
        {
            var remaining = await GetRemainingFactorsAsync(challenge, factorId, cancellationToken)
                .ConfigureAwait(false);
            throw new MultiFactorRequiredException(challenge.Id, remaining, challenge);
        }

        return await FinishLoginAsync(challenge.Id, cancellationToken).ConfigureAwait(false);
    }

    public Task RequestFactorCodeAsync(Guid challengeId, Guid factorId, CancellationToken cancellationToken = default)
    {
        if (challengeId == Guid.Empty)
        {
            throw new ArgumentException("challengeId 无效。", nameof(challengeId));
        }

        if (factorId == Guid.Empty)
        {
            throw new ArgumentException("factorId 无效。", nameof(factorId));
        }

        return _api.RequestChallengeFactorAsync(challengeId, factorId, cancellationToken);
    }

    public async Task<TokenExchangeResponse> WaitForChallengeCompletionAsync(
        Guid challengeId,
        CancellationToken cancellationToken = default)
    {
        if (challengeId == Guid.Empty)
        {
            throw new ArgumentException("challengeId 无效。", nameof(challengeId));
        }

        // Longer poll window for in-app approval (up to ~3 minutes).
        var deadline = DateTimeOffset.UtcNow.AddMinutes(3);
        SnAuthChallenge challenge = await GetChallengeAsync(challengeId, cancellationToken).ConfigureAwait(false);

        while (!challenge.IsCompleted && DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (challenge.DeclinedAt is not null)
            {
                throw new SolarApiException("登录挑战已被拒绝。");
            }

            await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken).ConfigureAwait(false);
            challenge = await GetChallengeAsync(challengeId, cancellationToken).ConfigureAwait(false);
        }

        if (!challenge.IsCompleted)
        {
            throw new SolarApiException("等待应用内确认超时。请在已登录设备上批准，或改用验证码。");
        }

        return await FinishLoginAsync(challenge.Id, cancellationToken).ConfigureAwait(false);
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
        using var http = new HttpClient
        {
            BaseAddress = new Uri(SolarApiClient.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SolarWin/1.1");

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
                await LoadCurrentAccountBestEffortAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task<TokenExchangeResponse> LoginWithQrAsync(
        Action<QrGenerateResponse> onQrGenerated,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onQrGenerated);

        // QR generate/status are public endpoints — use a bare HttpClient without Bearer.
        // Attaching a stale vault token via SolarApiClient can make Padlock return 401
        // and block the entire unauthenticated login flow.
        using var http = CreateAnonymousHttpClient();

        var requestBody = new QrGenerateRequest
        {
            DeviceId = DeviceInfoHelper.GetDeviceId(),
            DeviceName = DeviceInfoHelper.GetDeviceName(),
            Platform = ClientPlatform.Windows,
        };

        using var genContent = new StringContent(
            JsonSerializer.Serialize(requestBody, JsonDefaults.Options),
            System.Text.Encoding.UTF8,
            "application/json");

        using var genResp = await http
            .PostAsync("padlock/auth/qr/generate", genContent, cancellationToken)
            .ConfigureAwait(false);
        var genJson = await genResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!genResp.IsSuccessStatusCode)
        {
            throw new SolarApiException(
                "无法生成登录二维码。",
                genResp.StatusCode,
                genJson);
        }

        var generated = JsonSerializer.Deserialize<QrGenerateResponse>(genJson, JsonDefaults.Options)
            ?? throw new SolarApiException("二维码响应为空。", responseBody: genJson);

        if (generated.QrChallengeId == Guid.Empty)
        {
            throw new SolarApiException("二维码响应缺少 qr_challenge_id。", responseBody: genJson);
        }

        // Ensure phone-scannable payload even if server omits qr_data.
        if (string.IsNullOrWhiteSpace(generated.QrData))
        {
            generated.QrData = $"solian://auth/qr/{generated.QrChallengeId:D}";
        }

        onQrGenerated(generated);

        var deadline = generated.ExpiresAt
            ?? DateTimeOffset.UtcNow.AddSeconds(generated.ExpiresInSeconds > 0 ? generated.ExpiresInSeconds : 300);

        // Safety cap: never poll longer than 10 minutes.
        if (deadline > DateTimeOffset.UtcNow.AddMinutes(10))
        {
            deadline = DateTimeOffset.UtcNow.AddMinutes(10);
        }

        Guid? authChallengeId = generated.AuthChallengeId is { } id && id != Guid.Empty ? id : null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(QrPollInterval, cancellationToken).ConfigureAwait(false);

            using var statusResp = await http
                .GetAsync($"padlock/auth/qr/{generated.QrChallengeId:D}", cancellationToken)
                .ConfigureAwait(false);
            var statusJson = await statusResp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (statusResp.StatusCode is HttpStatusCode.NotFound)
            {
                throw new SolarApiException("二维码已失效，请重新生成。", statusResp.StatusCode, statusJson);
            }

            if (!statusResp.IsSuccessStatusCode)
            {
                // Transient errors — keep polling until deadline.
                continue;
            }

            var status = JsonSerializer.Deserialize<QrStatusResponse>(statusJson, JsonDefaults.Options);
            if (status is null)
            {
                continue;
            }

            if (status.AuthChallengeId is { } aid && aid != Guid.Empty)
            {
                authChallengeId = aid;
            }

            if (status.IsDeclined)
            {
                throw new SolarApiException("已在手机上拒绝扫码登录。");
            }

            if (status.IsExpired)
            {
                throw new SolarApiException("二维码已过期，请重新生成。");
            }

            if (!status.IsApproved)
            {
                continue;
            }

            // Phone approved — finish via auth challenge token exchange.
            if (authChallengeId is null || authChallengeId == Guid.Empty)
            {
                throw new SolarApiException("扫码已批准，但缺少 auth_challenge_id，无法换票。");
            }

            var challenge = await GetChallengeAsync(authChallengeId.Value, cancellationToken).ConfigureAwait(false);
            challenge = await PollChallengeUntilCompletedAsync(challenge, cancellationToken).ConfigureAwait(false);

            if (!challenge.IsCompleted)
            {
                // Some deployments complete the QR without step_remain==0 immediately — still try exchange.
                try
                {
                    return await FinishLoginAsync(authChallengeId.Value, cancellationToken).ConfigureAwait(false);
                }
                catch (SolarApiException)
                {
                    throw new SolarApiException("扫码已批准，但登录挑战尚未完成。请重试或改用密码登录。");
                }
            }

            return await FinishLoginAsync(authChallengeId.Value, cancellationToken).ConfigureAwait(false);
        }

        throw new SolarApiException("扫码登录超时，请重新生成二维码。");
    }

    public Task<SnAccount> RegisterAsync(AccountCreateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return _api.RegisterAccountAsync(request, cancellationToken);
    }

    public async Task<TokenExchangeResponse> RecoverAsync(
        RecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tokens = await _api.RecoverAccountAsync(request, cancellationToken).ConfigureAwait(false);
        await PersistTokensAsync(tokens, cancellationToken).ConfigureAwait(false);
        await LoadCurrentAccountBestEffortAsync(cancellationToken).ConfigureAwait(false);
        IsAuthenticated = true;
        RaiseAuthChanged();
        return tokens;
    }

    public async Task<TokenExchangeResponse> LoginWithPasskeyAsync(
        Func<string, CancellationToken, Task<string?>> runCeremony,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runCeremony);

        PasskeyLoginStartResponse start;
        try
        {
            start = await _api.StartPasskeyLoginAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SolarApiException ex)
        {
            throw new SolarApiException("无法启动 Passkey 登录。", ex.StatusCode, ex.ResponseBody, ex);
        }

        var options = start.PublicKey ?? start;
        var optionsJson = JsonSerializer.Serialize(options, JsonDefaults.Options);
        var assertionJson = await runCeremony(optionsJson, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(assertionJson))
        {
            throw new OperationCanceledException("已取消 Passkey。");
        }

        var complete = ParsePasskeyAssertion(assertionJson);
        var challengeId = start.AuthChallengeId != Guid.Empty
            ? start.AuthChallengeId
            : throw new SolarApiException("Passkey 响应缺少 auth_challenge_id。");

        SnAuthChallenge challenge;
        try
        {
            challenge = await _api.CompletePasskeyLoginAsync(challengeId, complete, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SolarApiException ex)
        {
            throw new SolarApiException("Passkey 验证失败。", ex.StatusCode, ex.ResponseBody, ex);
        }

        challenge = await PollChallengeUntilCompletedAsync(challenge, cancellationToken).ConfigureAwait(false);
        if (!challenge.IsCompleted)
        {
            // Still try token exchange — some deployments complete on passkey alone.
            try
            {
                return await FinishLoginAsync(challenge.Id != Guid.Empty ? challenge.Id : challengeId, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (SolarApiException)
            {
                var remaining = await GetRemainingFactorsAsync(challenge, Guid.Empty, cancellationToken)
                    .ConfigureAwait(false);
                throw new MultiFactorRequiredException(
                    challenge.Id != Guid.Empty ? challenge.Id : challengeId,
                    remaining,
                    challenge);
            }
        }

        return await FinishLoginAsync(challenge.Id != Guid.Empty ? challenge.Id : challengeId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TokenExchangeResponse> LoginWithSocialAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _socialLogin.LoginWithProviderAsync(provider, cancellationToken).ConfigureAwait(false);
        await PersistTokensAsync(tokens, cancellationToken).ConfigureAwait(false);
        await LoadCurrentAccountBestEffortAsync(cancellationToken).ConfigureAwait(false);
        IsAuthenticated = true;
        RaiseAuthChanged();
        return tokens;
    }

    private static PasskeyAuthenticationCompleteRequest ParsePasskeyAssertion(string assertionJson)
    {
        using var doc = JsonDocument.Parse(assertionJson);
        var root = doc.RootElement;

        string? Get(params string[] names)
        {
            foreach (var n in names)
            {
                if (root.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
                {
                    return p.GetString();
                }
            }

            return null;
        }

        var credentialId = Get("credential_id", "rawId", "id")
            ?? throw new SolarApiException("Passkey 结果缺少 credential id。");
        var clientData = Get("client_data_json", "clientDataJSON")
            ?? throw new SolarApiException("Passkey 结果缺少 clientDataJSON。");
        var authData = Get("authenticator_data", "authenticatorData")
            ?? throw new SolarApiException("Passkey 结果缺少 authenticatorData。");
        var signature = Get("signature")
            ?? throw new SolarApiException("Passkey 结果缺少 signature。");

        return new PasskeyAuthenticationCompleteRequest
        {
            CredentialId = credentialId,
            ClientDataJson = clientData,
            AuthenticatorData = authData,
            Signature = signature,
            UserHandle = Get("user_handle", "userHandle"),
        };
    }

    private static HttpClient CreateAnonymousHttpClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri(SolarApiClient.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "SolarWin/1.1");
        return http;
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

        await _sessions.OnLoggedOutAsync(removeProfile: false, cancellationToken).ConfigureAwait(false);
        await _api.SetBearerTokenAsync(null, cancellationToken).ConfigureAwait(false);
        _accessExpiresAt = null;
        CurrentAccount = null;
        IsAuthenticated = false;
        RaiseAuthChanged();
    }

    public async Task SwitchAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await _sessions.SwitchToAsync(accountId, cancellationToken).ConfigureAwait(false);
        await _api.SetBearerTokenAsync(null, cancellationToken).ConfigureAwait(false);
        _accessExpiresAt = null;
        CurrentAccount = null;
        IsAuthenticated = false;
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task<TokenExchangeResponse> FinishLoginAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        var tokens = await ExchangeTokenAsync(challengeId, cancellationToken).ConfigureAwait(false);
        await LoadCurrentAccountBestEffortAsync(cancellationToken).ConfigureAwait(false);
        IsAuthenticated = true;
        RaiseAuthChanged();
        return tokens;
    }

    private async Task LoadCurrentAccountBestEffortAsync(CancellationToken cancellationToken)
    {
        try
        {
            CurrentAccount = await GetMeAsync(cancellationToken).ConfigureAwait(false);
            if (CurrentAccount is not null)
            {
                await _sessions.RememberProfileAsync(CurrentAccount, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (SolarApiException)
        {
            // Token is valid even if profile load fails; try offline cache.
            if (_sessions.ActiveAccountId is { } aid
                && OfflineCache.TryGetJson<SnAccount>($"account_me_{aid:N}", out var cached, allowExpired: true))
            {
                CurrentAccount = cached;
            }
            else
            {
                CurrentAccount = null;
            }
        }
    }

    private async Task<IReadOnlyList<SnAccountAuthFactor>> GetRemainingFactorsAsync(
        SnAuthChallenge challenge,
        Guid justUsedFactorId,
        CancellationToken cancellationToken)
    {
        var factors = await GetChallengeFactorsAsync(challenge.Id, cancellationToken).ConfigureAwait(false);
        var blacklist = challenge.BlacklistFactors is { Count: > 0 }
            ? new HashSet<Guid>(challenge.BlacklistFactors)
            : [];

        blacklist.Add(justUsedFactorId);

        return factors
            .Where(f => f.Id != Guid.Empty && !blacklist.Contains(f.Id))
            .Where(f => f.Type is not AccountAuthFactorType.Password)
            .OrderBy(f => FactorSortOrder(f.Type))
            .ToList();
    }

    private static int FactorSortOrder(AccountAuthFactorType type) => type switch
    {
        AccountAuthFactorType.Totp => 0,
        AccountAuthFactorType.Email => 1,
        AccountAuthFactorType.PhoneCode => 2,
        AccountAuthFactorType.InApp => 3,
        AccountAuthFactorType.Recovery => 4,
        AccountAuthFactorType.Passkey => 5,
        _ => 10,
    };

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

            // If step_remain stopped decreasing, stop early so MFA UI can continue.
            if (challenge.StepRemain > 0 && attempt >= 3)
            {
                return challenge;
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
