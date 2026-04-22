using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DesktopHub.Infrastructure.Logging;

namespace DesktopHub.Infrastructure.Firebase;

/// <summary>
/// Owns the client-side Firebase Auth lifecycle.
///
/// Flow:
///   1. POST to the `issueToken` Cloud Function with {licenseKey, username, deviceId}
///      — returns a short-lived custom token whose claims carry the caller's tier.
///   2. Exchange that custom token for an ID token via the Identity Toolkit
///      `signInWithCustomToken` REST endpoint.
///   3. Cache the ID token and its expiry. All RTDB REST calls then append
///      `?auth=&lt;idToken&gt;` which Firebase evaluates against the database rules.
///
/// The client NEVER holds any service-account credentials. The only "secret"
/// it needs is the Web API key, which is public by design (identifies the
/// Firebase project and is shipped in every web/mobile Firebase app).
/// </summary>
public sealed class FirebaseAuth : IDisposable
{
    // The Web API key is public information — visible in the Firebase console
    // under Project Settings → General → Web API Key. It identifies the project
    // for public APIs like Identity Toolkit. It does NOT grant any privileged
    // access; all writes are still gated by custom-token claims + RTDB rules.
    private const string WebApiKey = "AIzaSyBq0-qnsspK-FOm-SvCBgBbBHL_Tx1k8WI";

    private const string IssueTokenUrl =
        "https://us-central1-licenses-ff136.cloudfunctions.net/issueToken";

    private const string SignInWithCustomTokenUrl =
        "https://identitytoolkit.googleapis.com/v1/accounts:signInWithCustomToken?key=" + WebApiKey;

    private const string RefreshTokenUrl =
        "https://securetoken.googleapis.com/v1/token?key=" + WebApiKey;

    // Refresh a bit before the token technically expires to avoid racing.
    private static readonly TimeSpan RefreshGrace = TimeSpan.FromMinutes(5);

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _idToken;
    private string? _refreshToken;
    private DateTime _expiresAtUtc = DateTime.MinValue;
    private string? _tier;
    private string? _username;
    private string? _licenseKey;
    private string? _deviceId;

    public FirebaseAuth()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _http.DefaultRequestHeaders.Add("User-Agent", "DesktopHub-Client/1.0");
    }

    public string? Tier => _tier;
    public string? Username => _username;
    public bool IsReady => !string.IsNullOrEmpty(_idToken) && DateTime.UtcNow < _expiresAtUtc;

    /// <summary>Initial handshake. Idempotent — re-calling before expiry is a no-op.</summary>
    public async Task<bool> SignInAsync(string licenseKey, string username, string deviceId)
    {
        _licenseKey = licenseKey;
        _username = username.ToLowerInvariant();
        _deviceId = deviceId;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsReady) return true;
            return await DoSignInLocked().ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Returns a valid Firebase ID token, refreshing transparently if needed.
    /// Returns null if we don't have a refresh token yet (i.e. SignInAsync
    /// was never called or failed).
    /// </summary>
    public async Task<string?> GetIdTokenAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!string.IsNullOrEmpty(_idToken) && DateTime.UtcNow + RefreshGrace < _expiresAtUtc)
                return _idToken;

            // Prefer cheap refresh if we have one; otherwise do full sign-in again.
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                if (await TryRefreshLocked().ConfigureAwait(false))
                    return _idToken;
            }

            if (!string.IsNullOrEmpty(_licenseKey) && !string.IsNullOrEmpty(_username) &&
                !string.IsNullOrEmpty(_deviceId))
            {
                await DoSignInLocked().ConfigureAwait(false);
                return _idToken;
            }

            return null;
        }
        finally { _gate.Release(); }
    }

    private async Task<bool> DoSignInLocked()
    {
        try
        {
            // 1. Call issueToken — Firebase v2 callable format: POST { "data": {...} }
            var issueBody = JsonSerializer.Serialize(new
            {
                data = new
                {
                    licenseKey = _licenseKey,
                    username = _username,
                    deviceId = _deviceId
                }
            });

            using var issueResp = await _http.PostAsync(
                IssueTokenUrl,
                new StringContent(issueBody, Encoding.UTF8, "application/json")).ConfigureAwait(false);

            if (!issueResp.IsSuccessStatusCode)
            {
                var body = await issueResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                InfraLogger.Log($"FirebaseAuth: issueToken failed {(int)issueResp.StatusCode}: {body}");
                return false;
            }

            var issueJson = await issueResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var issueDoc = JsonDocument.Parse(issueJson);
            if (!issueDoc.RootElement.TryGetProperty("result", out var issueResult))
            {
                InfraLogger.Log("FirebaseAuth: issueToken response missing 'result'");
                return false;
            }

            var customToken = issueResult.GetProperty("token").GetString();
            _tier = issueResult.TryGetProperty("tier", out var tierEl) ? tierEl.GetString() : "user";

            if (string.IsNullOrEmpty(customToken))
            {
                InfraLogger.Log("FirebaseAuth: empty custom token from issueToken");
                return false;
            }

            // 2. Exchange the custom token for an ID token via Identity Toolkit
            var exchangeBody = JsonSerializer.Serialize(new
            {
                token = customToken,
                returnSecureToken = true
            });

            using var exchResp = await _http.PostAsync(
                SignInWithCustomTokenUrl,
                new StringContent(exchangeBody, Encoding.UTF8, "application/json")).ConfigureAwait(false);

            if (!exchResp.IsSuccessStatusCode)
            {
                var body = await exchResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                InfraLogger.Log($"FirebaseAuth: signInWithCustomToken failed {(int)exchResp.StatusCode}: {body}");
                return false;
            }

            var exchJson = await exchResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var exchDoc = JsonDocument.Parse(exchJson);
            var root = exchDoc.RootElement;

            _idToken = root.GetProperty("idToken").GetString();
            _refreshToken = root.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;
            var expiresIn = int.TryParse(
                root.TryGetProperty("expiresIn", out var ex) ? ex.GetString() : "3600",
                out var secs) ? secs : 3600;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);

            InfraLogger.Log($"FirebaseAuth: signed in as '{_username}' tier={_tier} (expires in {expiresIn}s)");
            return !string.IsNullOrEmpty(_idToken);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"FirebaseAuth: SignIn threw: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TryRefreshLocked()
    {
        try
        {
            var body = $"grant_type=refresh_token&refresh_token={Uri.EscapeDataString(_refreshToken!)}";
            using var resp = await _http.PostAsync(
                RefreshTokenUrl,
                new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded")).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var errBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                InfraLogger.Log($"FirebaseAuth: refresh failed {(int)resp.StatusCode}: {errBody}");
                return false;
            }

            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            _idToken = root.GetProperty("id_token").GetString();
            _refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : _refreshToken;
            var expiresIn = int.TryParse(
                root.TryGetProperty("expires_in", out var ex) ? ex.GetString() : "3600",
                out var secs) ? secs : 3600;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(expiresIn);

            return !string.IsNullOrEmpty(_idToken);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"FirebaseAuth: refresh threw: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Forwards a Cloud Functions callable invocation with the current ID token
    /// as the Bearer credential. Used by the developer panel for admin ops
    /// (pushForceUpdate, clearForceUpdate).
    /// </summary>
    public async Task<JsonElement?> CallFunctionAsync(string functionName, object payload)
    {
        var idToken = await GetIdTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(idToken))
        {
            InfraLogger.Log($"FirebaseAuth: CallFunction({functionName}) aborted — no ID token");
            return null;
        }

        var url = $"https://us-central1-licenses-ff136.cloudfunctions.net/{functionName}";
        var bodyJson = JsonSerializer.Serialize(new { data = payload });

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);

        using var resp = await _http.SendAsync(req).ConfigureAwait(false);
        var respBody = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            InfraLogger.Log($"FirebaseAuth: {functionName} failed {(int)resp.StatusCode}: {respBody}");
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(respBody);
            if (doc.RootElement.TryGetProperty("result", out var result))
                return result.Clone();
        }
        catch { /* malformed — treat as failure */ }

        return null;
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}
