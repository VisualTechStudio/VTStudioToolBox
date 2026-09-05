using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VTStudioToolBox.Helpers;
using VTStudioToolBox.Models;

namespace VTStudioToolBox.Auth;

public sealed class AuthManager : IAuthService
{
    private const string AuthConfigFile = "auth_user.json";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    // ── OAuth client credentials (loaded from secrets.json) ──
    private static string GitHubClientId     => SecretsConfig.GitHubClientId;
    private static string GitHubClientSecret => SecretsConfig.GitHubClientSecret;
    private static string MicrosoftClientId  => SecretsConfig.MicrosoftClientId;
    private static string GoogleClientId     => SecretsConfig.GoogleClientId;
    private static string SteamApiKey        => SecretsConfig.SteamApiKey;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public UserIdentity? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;

    public AuthManager()
    {
        TryLoadCachedUser();
    }

    // ────────────────────── Public API ──────────────────────

    public async Task<UserIdentity> LoginAsync(AuthProvider provider, CancellationToken ct = default)
    {
        UserIdentity identity = provider switch
        {
            AuthProvider.GitHub    => await LoginGitHubAsync(ct),
            AuthProvider.Microsoft => await LoginMicrosoftAsync(ct),
            AuthProvider.Google    => await LoginGoogleAsync(ct),
            AuthProvider.Steam     => await LoginSteamAsync(ct),
            _ => throw new NotSupportedException($"Provider {provider} is not supported.")
        };

        CurrentUser = identity;
        SaveCachedUser(identity);
        Logger.Info("Auth", $"Logged in as {identity.DisplayName} via {identity.Provider}");
        return identity;
    }

    public Task LogoutAsync()
    {
        CurrentUser = null;
        DeleteCachedUser();
        Logger.Info("Auth", "User logged out");
        return Task.CompletedTask;
    }

    // ────────────────────── GitHub (OAuth 2.0 + client_secret) ──────────────────────

    private static async Task<UserIdentity> LoginGitHubAsync(CancellationToken ct)
    {
        string state = GenerateRandomHex(32);
        string redirectUri = await StartListenerAndGetRedirectUriAsync(ct);
        Logger.Info("Auth", $"GitHub: listening on {redirectUri}");

        string authUrl =
            $"https://github.com/login/oauth/authorize" +
            $"?client_id={GitHubClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope=read:user%20user:email" +
            $"&state={state}";

        OpenBrowser(authUrl);
        Logger.Info("Auth", "GitHub: browser opened, waiting for callback...");

        var queryParams = await WaitForCallbackAsync(redirectUri, ct);
        ValidateState(queryParams, state);
        string code = queryParams["code"] ?? throw new InvalidOperationException("Missing code in callback.");
        Logger.Info("Auth", "GitHub: authorization code received");

        // Exchange code for token (GitHub OAuth Apps use client_secret, not PKCE)
        var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = GitHubClientId,
                ["client_secret"] = GitHubClientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri
            })
        };
        tokenReq.Headers.Add("Accept", "application/json");

        using var tokenResp = await Http.SendAsync(tokenReq, ct);
        tokenResp.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(ct));

        if (tokenJson.RootElement.TryGetProperty("error", out var errorProp))
        {
            string errMsg = errorProp.GetString() ?? "unknown";
            string errDesc = tokenJson.RootElement.TryGetProperty("error_description", out var descProp) ? descProp.GetString() ?? "" : "";
            throw new InvalidOperationException($"GitHub token exchange failed: {errMsg} - {errDesc}");
        }

        string accessToken = tokenJson.RootElement.GetProperty("access_token").GetString()
                             ?? throw new InvalidOperationException("GitHub token exchange failed.");
        Logger.Info("Auth", "GitHub: access token obtained");

        // Fetch user profile
        var userReq = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userReq.Headers.Add("Authorization", $"Bearer {accessToken}");
        userReq.Headers.Add("User-Agent", "VTStudioToolBox");

        using var userResp = await Http.SendAsync(userReq, ct);
        userResp.EnsureSuccessStatusCode();
        using var userDoc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync(ct));
        var root = userDoc.RootElement;

        string displayName = root.GetProperty("login").GetString() ?? "";
        Logger.Info("Auth", $"GitHub: profile fetched - {displayName}");

        return new UserIdentity
        {
            Provider = AuthProvider.GitHub,
            UserId = root.GetProperty("id").GetInt64().ToString(),
            DisplayName = displayName,
            AvatarUrl = root.GetProperty("avatar_url").GetString() ?? "",
            Email = root.TryGetProperty("email", out var emailProp) ? emailProp.GetString() ?? "" : ""
        };
    }

    // ────────────────────── Microsoft (OAuth 2.0 + PKCE) ──────────────────────

    private static async Task<UserIdentity> LoginMicrosoftAsync(CancellationToken ct)
    {
        var (codeVerifier, codeChallenge) = GeneratePkcePair();
        string state = GenerateRandomHex(32);
        string redirectUri = await StartListenerAndGetRedirectUriAsync(ct);
        Logger.Info("Auth", $"Microsoft: listening on {redirectUri}");

        string authUrl =
            $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize" +
            $"?client_id={MicrosoftClientId}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope=openid%20profile%20email%20User.Read" +
            $"&state={state}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256";

        OpenBrowser(authUrl);
        Logger.Info("Auth", "Microsoft: browser opened, waiting for callback...");

        var queryParams = await WaitForCallbackAsync(redirectUri, ct);
        Logger.Info("Auth", $"Microsoft: callback received, params={queryParams.Count}");
        ValidateState(queryParams, state);
        string code = queryParams["code"] ?? throw new InvalidOperationException("Missing code in callback.");
        Logger.Info("Auth", "Microsoft: authorization code received");

        // Exchange code for token
        var tokenResp = await Http.PostAsync("https://login.microsoftonline.com/common/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = MicrosoftClientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = codeVerifier
            }), ct);

        if (!tokenResp.IsSuccessStatusCode)
        {
            string errBody = await tokenResp.Content.ReadAsStringAsync(ct);
            Logger.Error("Auth", $"Microsoft token exchange failed: {tokenResp.StatusCode} - {errBody}");
            tokenResp.EnsureSuccessStatusCode();
        }

        using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(ct));
        string accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()
                             ?? throw new InvalidOperationException("Microsoft token exchange failed.");
        Logger.Info("Auth", "Microsoft: access token obtained");

        // Fetch user profile (Microsoft Graph)
        var userReq = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
        userReq.Headers.Add("Authorization", $"Bearer {accessToken}");

        using var userResp = await Http.SendAsync(userReq, ct);
        userResp.EnsureSuccessStatusCode();
        using var userDoc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync(ct));
        var root = userDoc.RootElement;

        // Fetch avatar photo (separate endpoint)
        string avatarUrl = "";
        try
        {
            var photoReq = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/photo/$value");
            photoReq.Headers.Add("Authorization", $"Bearer {accessToken}");
            using var photoResp = await Http.SendAsync(photoReq, ct);
            if (photoResp.IsSuccessStatusCode)
            {
                // Convert photo to data URI for display
                byte[] photoBytes = await photoResp.Content.ReadAsByteArrayAsync(ct);
                string mediaType = photoResp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                avatarUrl = $"data:{mediaType};base64,{Convert.ToBase64String(photoBytes)}";
                Logger.Info("Auth", "Microsoft: avatar photo fetched");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Auth", $"Microsoft avatar fetch failed (non-fatal): {ex.Message}");
        }

        string displayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? "" : "";
        Logger.Info("Auth", $"Microsoft: profile fetched - {displayName}");

        return new UserIdentity
        {
            Provider = AuthProvider.Microsoft,
            UserId = root.GetProperty("id").GetString() ?? "",
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            Email = root.TryGetProperty("mail", out var mail) ? mail.GetString() ?? "" :
                    root.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() ?? "" : ""
        };
    }

    // ────────────────────── Google (OAuth 2.0 + PKCE) ──────────────────────

    private static async Task<UserIdentity> LoginGoogleAsync(CancellationToken ct)
    {
        var (codeVerifier, codeChallenge) = GeneratePkcePair();
        string state = GenerateRandomHex(32);
        string redirectUri = await StartListenerAndGetRedirectUriAsync(ct);
        Logger.Info("Auth", $"Google: listening on {redirectUri}");

        string authUrl =
            $"https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={GoogleClientId}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope=openid%20profile%20email" +
            $"&state={state}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&prompt=consent";

        OpenBrowser(authUrl);
        Logger.Info("Auth", "Google: browser opened, waiting for callback...");

        var queryParams = await WaitForCallbackAsync(redirectUri, ct);
        Logger.Info("Auth", $"Google: callback received, params={queryParams.Count}");

        // Google may return error=access_denied if user cancels
        if (queryParams.TryGetValue("error", out string? errorVal))
        {
            string desc = queryParams.GetValueOrDefault("error_description") ?? "";
            throw new InvalidOperationException($"Google auth error: {errorVal} - {desc}");
        }

        ValidateState(queryParams, state);
        string code = queryParams["code"] ?? throw new InvalidOperationException("Missing code in callback.");
        Logger.Info("Auth", "Google: authorization code received");

        // Exchange code for token
        var tokenResp = await Http.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = GoogleClientId,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = codeVerifier
            }), ct);

        if (!tokenResp.IsSuccessStatusCode)
        {
            string errBody = await tokenResp.Content.ReadAsStringAsync(ct);
            Logger.Error("Auth", $"Google token exchange failed: {tokenResp.StatusCode} - {errBody}");
            tokenResp.EnsureSuccessStatusCode();
        }

        using var tokenDoc = JsonDocument.Parse(await tokenResp.Content.ReadAsStringAsync(ct));
        string accessToken = tokenDoc.RootElement.GetProperty("access_token").GetString()
                             ?? throw new InvalidOperationException("Google token exchange failed.");
        Logger.Info("Auth", "Google: access token obtained");

        // Fetch userinfo
        var userReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v3/userinfo");
        userReq.Headers.Add("Authorization", $"Bearer {accessToken}");

        using var userResp = await Http.SendAsync(userReq, ct);
        userResp.EnsureSuccessStatusCode();
        using var userDoc = JsonDocument.Parse(await userResp.Content.ReadAsStringAsync(ct));
        var root = userDoc.RootElement;

        string displayName = root.TryGetProperty("name", out var name) ? name.GetString() ?? "" : "";
        string avatarUrl = root.TryGetProperty("picture", out var pic) ? pic.GetString() ?? "" : "";
        Logger.Info("Auth", $"Google: profile fetched - {displayName}");

        return new UserIdentity
        {
            Provider = AuthProvider.Google,
            UserId = root.GetProperty("sub").GetString() ?? "",
            DisplayName = displayName,
            AvatarUrl = avatarUrl,
            Email = root.TryGetProperty("email", out var email) ? email.GetString() ?? "" : ""
        };
    }

    // ────────────────────── Steam (OpenID 2.0) ──────────────────────

    private static async Task<UserIdentity> LoginSteamAsync(CancellationToken ct)
    {
        string redirectUri = await StartListenerAndGetRedirectUriAsync(ct);
        Logger.Info("Auth", $"Steam: listening on {redirectUri}");

        // Extract port from redirectUri for realm
        var uri = new Uri(redirectUri);
        string realm = $"http://127.0.0.1:{uri.Port}";
        string authUrl =
            "https://steamcommunity.com/openid/login" +
            $"?openid.ns={Uri.EscapeDataString("http://specs.openid.net/auth/2.0")}" +
            "&openid.mode=checkid_setup" +
            $"&openid.return_to={Uri.EscapeDataString(redirectUri)}" +
            $"&openid.realm={Uri.EscapeDataString(realm)}" +
            "&openid.identity=http://specs.openid.net/auth/2.0/identifier_select" +
            "&openid.claimed_id=http://specs.openid.net/auth/2.0/identifier_select";

        OpenBrowser(authUrl);

        var queryParams = await WaitForCallbackAsync(redirectUri, ct);

        // Verify the OpenID response with Steam
        string steamId = await VerifySteamOpenIdAsync(queryParams, ct);

        // Fetch Steam player summary
        string apiUrl = $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={SteamApiKey}&steamids={steamId}";

        using var resp = await Http.GetAsync(apiUrl, ct);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var players = doc.RootElement.GetProperty("response").GetProperty("players");
        if (players.GetArrayLength() == 0)
            throw new InvalidOperationException("Steam player not found.");

        var player = players[0];
        return new UserIdentity
        {
            Provider = AuthProvider.Steam,
            UserId = steamId,
            DisplayName = player.GetProperty("personaname").GetString() ?? "",
            AvatarUrl = player.TryGetProperty("avatarfull", out var av) ? av.GetString() ?? "" : "",
            Email = "" // Steam does not expose email via OpenID
        };
    }

    /// <summary>
    /// Verifies an OpenID 2.0 assertion against the Steam provider.
    /// Returns the SteamID64 on success.
    /// </summary>
    public static async Task<string> VerifySteamOpenIdAsync(
        Dictionary<string, string> queryParams, CancellationToken ct = default)
    {
        // Build verification request: copy all openid.* params and set mode=check_authentication
        var verificationParams = new Dictionary<string, string>(queryParams)
        {
            ["openid.mode"] = "check_authentication"
        };

        var content = new FormUrlEncodedContent(verificationParams);
        using var resp = await Http.PostAsync("https://steamcommunity.com/openid/login", content, ct);
        resp.EnsureSuccessStatusCode();
        string body = await resp.Content.ReadAsStringAsync(ct);

        if (!body.Contains("is_valid:true"))
            throw new InvalidOperationException($"Steam OpenID verification failed. Response: {body}");

        // Extract SteamID64 from the claimed_id URL: https://steamcommunity.com/openid/id/76561198XXXXXXXXX
        string claimedId = queryParams.GetValueOrDefault("openid.claimed_id") ?? "";
        int lastSlash = claimedId.LastIndexOf('/');
        if (lastSlash < 0 || lastSlash + 1 >= claimedId.Length)
            throw new InvalidOperationException("Cannot extract SteamID from claimed_id.");

        string steamId = claimedId[(lastSlash + 1)..];
        if (!long.TryParse(steamId, out _))
            throw new InvalidOperationException($"Invalid SteamID64: {steamId}");

        return steamId;
    }

    // ────────────────────── Local Loopback Listener ──────────────────────

    private static HttpListener? _activeListener;
    private static readonly object _listenerLock = new();

    private static async Task<string> StartListenerAndGetRedirectUriAsync(CancellationToken ct)
    {
        // Find an available port
        int port;
        var tcpListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        tcpListener.Start();
        port = ((System.Net.IPEndPoint)tcpListener.LocalEndpoint).Port;
        tcpListener.Stop();

        var listener = new HttpListener();
        string prefix = $"http://127.0.0.1:{port}/callback/";
        listener.Prefixes.Add(prefix);
        listener.Start();

        lock (_listenerLock)
        {
            _activeListener?.Stop();
            _activeListener = listener;
        }
        return prefix;
    }

    private static async Task<Dictionary<string, string>> WaitForCallbackAsync(
        string redirectUri, CancellationToken ct)
    {
        HttpListener listener;
        lock (_listenerLock)
        {
            listener = _activeListener ?? throw new InvalidOperationException("No active listener.");
        }
        try
        {
            // Build the HTML success page bytes once
            byte[] successHtml = Encoding.UTF8.GetBytes(
                """
                <!DOCTYPE html>
                <html><head><meta charset="utf-8"><title>Login Success</title>
                <style>
                  body{display:flex;justify-content:center;align-items:center;height:100vh;
                       font-family:system-ui;background:#1a1a2e;color:#e0e0e0;margin:0}
                  .card{background:#16213e;padding:48px 64px;border-radius:16px;text-align:center;
                        box-shadow:0 8px 32px rgba(0,0,0,.3)}
                  h1{font-size:24px;margin:0 0 12px}p{color:#a0a0a0;font-size:14px}
                </style></head><body>
                <div class="card"><h1>&#10003; Login Successful</h1>
                <p>You can close this tab and return to VTStudio ToolBox.</p></div>
                </body></html>
                """);

            byte[] emptyBytes = Array.Empty<byte>();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromMinutes(5));

            // Loop until we receive the actual OAuth callback (ignore favicon, etc.)
            while (!linkedCts.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(linkedCts.Token);
                string rawPath = context.Request.Url?.AbsolutePath ?? "";
                string rawQuery = context.Request.Url?.Query ?? "";
                Logger.Info("Auth", $"Callback hit: {rawPath} | query={rawQuery}");

                // Always respond so the browser doesn't hang
                var queryParams = ParseQueryString(rawQuery);

                foreach (var kv in queryParams)
                    Logger.Dev("Auth", $"  param: {kv.Key}={kv.Value[..Math.Min(kv.Value.Length, 40)]}");

                if (queryParams.ContainsKey("code") || queryParams.ContainsKey("openid.mode"))
                {
                    // This is the actual OAuth callback
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.ContentLength64 = successHtml.Length;
                    await context.Response.OutputStream.WriteAsync(successHtml, ct);
                    context.Response.Close();
                    Logger.Info("Auth", "OAuth callback accepted, returning params");
                    return queryParams;
                }

                // Not the callback (favicon, etc.) — respond with empty and keep listening
                context.Response.StatusCode = 204;
                context.Response.ContentLength64 = 0;
                context.Response.Close();
            }

            throw new OperationCanceledException("OAuth callback wait timed out.");
        }
        finally
        {
            try { listener.Stop(); listener.Close(); } catch (Exception ex) { Logger.Warn("Auth", $"Failed to stop listener: {ex.Message}"); }
            lock (_listenerLock) { _activeListener = null; }
        }
    }

    // ────────────────────── Helpers ──────────────────────

    private static (string Verifier, string Challenge) GeneratePkcePair()
    {
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        string verifier = Convert.ToBase64String(randomBytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        byte[] challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        string challenge = Convert.ToBase64String(challengeBytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return (verifier, challenge);
    }

    private static string GenerateRandomHex(int byteCount)
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(byteCount));
    }

    private static void ValidateState(Dictionary<string, string> queryParams, string expectedState)
    {
        string state = queryParams.GetValueOrDefault("state") ?? "";
        if (!string.Equals(state, expectedState, StringComparison.Ordinal))
            throw new InvalidOperationException("OAuth state mismatch — possible CSRF attack.");
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;

        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq < 0) continue;
            string key = Uri.UnescapeDataString(pair[..eq]);
            string value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            result[key] = value;
        }
        return result;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error("Auth", "Failed to open browser", ex);
            throw;
        }
    }

    // ────────────────────── Persistent Cache ──────────────────────

    private static string AuthCachePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", AuthConfigFile);

    private void TryLoadCachedUser()
    {
        try
        {
            if (!File.Exists(AuthCachePath)) return;
            string json = File.ReadAllText(AuthCachePath);
            CurrentUser = JsonSerializer.Deserialize<UserIdentity>(json, JsonOpts);
            if (CurrentUser is not null)
                Logger.Info("Auth", $"Restored cached user: {CurrentUser.DisplayName}");
        }
        catch (Exception ex)
        {
            Logger.Warn("Auth", $"Failed to load cached auth: {ex.Message}");
        }
    }

    private static void SaveCachedUser(UserIdentity user)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AuthCachePath)!);
            File.WriteAllText(AuthCachePath, JsonSerializer.Serialize(user, JsonOpts));
        }
        catch (Exception ex)
        {
            Logger.Warn("Auth", $"Failed to cache auth: {ex.Message}");
        }
    }

    private static void DeleteCachedUser()
    {
        try { if (File.Exists(AuthCachePath)) File.Delete(AuthCachePath); }
        catch (Exception ex) { Logger.Warn("Auth", $"Failed to delete cached auth: {ex.Message}"); }
    }
}
