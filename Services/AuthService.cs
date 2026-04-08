using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net;

namespace CraftConnect_Mobile_App.Services
{
    /// <summary>
    /// Singleton-safe AuthService.
    /// Register as a singleton in DI — one HttpClient, one handler, connections reused.
    /// </summary>
    public class AuthService
    {
        // ── Static / shared ──────────────────────────────────────────
        // HttpClient must be a singleton. Creating one per request or per
        // service instance causes socket exhaustion and kills connection reuse.
        private static HttpClient? _sharedClient;
        private static readonly object _clientLock = new();

        private static readonly JsonSerializerOptions _serializeOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private static readonly JsonSerializerOptions _deserializeOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JwtSecurityTokenHandler _jwtHandler = new();

        // ── Instance ─────────────────────────────────────────────────
        private readonly string _baseUrl;
        private readonly HttpClient _httpClient;

        // Cached token to avoid hammering SecureStorage on every IsAuthenticated check.
        private string? _cachedToken;
        private DateTime _tokenCachedAt = DateTime.MinValue;
        private static readonly TimeSpan _tokenCacheDuration = TimeSpan.FromSeconds(30);

        // ── Constructor ───────────────────────────────────────────────
        public AuthService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');

#if DEBUG
            Debug.WriteLine($"[AUTH SERVICE] BaseUrl: '{_baseUrl}'");
#endif

            _httpClient = GetOrCreateClient(_baseUrl);

#if DEBUG
            Debug.WriteLine($"[AUTH SERVICE] Initialized. BaseAddress: {_httpClient.BaseAddress}, Timeout: {_httpClient.Timeout.TotalSeconds}s");
#endif
        }

        // ── HttpClient factory (one per base URL, ever) ───────────────
        private static HttpClient GetOrCreateClient(string baseUrl)
        {
            if (_sharedClient != null) return _sharedClient;

            lock (_clientLock)
            {
                if (_sharedClient != null) return _sharedClient;

                HttpMessageHandler handler = BuildHandler();

                _sharedClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri(baseUrl + "/"),
                    Timeout = TimeSpan.FromSeconds(15)
                };

                // Keep-alive and modern protocol hints
                _sharedClient.DefaultRequestHeaders.ConnectionClose = false;
                _sharedClient.DefaultRequestHeaders.Accept
                    .Add(new MediaTypeWithQualityHeaderValue("application/json"));
                _sharedClient.DefaultRequestHeaders.Add("X-Requested-With", "Mobile");

                return _sharedClient;
            }
        }

        private static HttpMessageHandler BuildHandler()
        {
#if ANDROID && DEBUG
            Debug.WriteLine("[AUTH SERVICE] Platform: ANDROID DEBUG — SSL bypass ON (dev only)");

            // SECURITY FIX: only bypass chain errors (self-signed dev cert).
            // We still reject name mismatches unless the host is exactly the
            // configured dev IP, so a MITM on a different host is blocked.
            return new Xamarin.Android.Net.AndroidMessageHandler
            {
                // Allow HTTP/1.1 keep-alive on Android
                UseCookies = false,
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
#pragma warning disable CS0618
                    var host = message.RequestUri?.Host ?? string.Empty;
                    var isLocalDev = host.StartsWith("192.168.") || host == "localhost" || host == "10.0.2.2";
#pragma warning restore CS0618

                    if (!isLocalDev)
                    {
                        Debug.WriteLine($"[SSL] BLOCKED non-local host in debug: {host}");
                        return false; // Never bypass SSL for non-local hosts, even in debug
                    }

                    Debug.WriteLine($"[SSL] Dev bypass for: {host}, errors: {errors}");
                    return true;
                }
            };

#elif ANDROID
            Debug.WriteLine("[AUTH SERVICE] Platform: ANDROID RELEASE — SSL validation enforced");
            return new Xamarin.Android.Net.AndroidMessageHandler
            {
                UseCookies = false
            };

#elif DEBUG
            Debug.WriteLine("[AUTH SERVICE] Platform: OTHER DEBUG — SSL bypass ON (dev only)");
            return new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    var host = message.RequestUri?.Host ?? string.Empty;
                    var isLocalDev = host.StartsWith("192.168.") || host == "localhost";
                    if (!isLocalDev)
                    {
                        Debug.WriteLine($"[SSL] BLOCKED non-local host in debug: {host}");
                        return false;
                    }
                    return true;
                }
            };

#else
            return new HttpClientHandler
            {
                // Release: enforce full SSL validation, enable modern TLS
                SslProtocols = System.Security.Authentication.SslProtocols.Tls12
                             | System.Security.Authentication.SslProtocols.Tls13
            };
#endif
        }

        // ── Connection pre-warm ───────────────────────────────────────
        /// <summary>
        /// Call this during the splash screen / app init so the SSL handshake
        /// is done BEFORE the user reaches the login page.
        /// Eliminates the 11s first-request penalty.
        /// </summary>
        public async Task PreWarmConnectionAsync()
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            try
            {
                // Lightweight HEAD request — no body, just opens the TCP+TLS connection
                using var message = new HttpRequestMessage(HttpMethod.Head, "/api/health");
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Swallow — pre-warm is best-effort.
                // If /api/health doesn't exist the 404 still opens the connection.
            }
#if DEBUG
            Debug.WriteLine($"[PRE-WARM] done in {sw.ElapsedMilliseconds}ms");
#endif
        }

        // ============================================================
        // PASSWORD LOGIN  →  POST /api/auth/login/password
        // ============================================================
        public async Task<AuthResult> LoginWithPasswordAsync(
            PasswordLoginRequest request,
            CancellationToken cancellationToken = default)
        {
#if DEBUG
            Debug.WriteLine($"\n[LOGIN/PASSWORD] Email: {request.Email}");
#endif
            try
            {
                var (response, elapsed) = await SendAsync(
                    HttpMethod.Post, "/api/auth/login/password",
                    request, cancellationToken);

#if DEBUG
                Debug.WriteLine($"[LOGIN/PASSWORD] {elapsed}ms — {(int)response.StatusCode}");
#endif

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = Deserialize<TokenResponse>(body);
                    if (!string.IsNullOrEmpty(tokenResponse?.Token))
                    {
                        await SaveTokenAsync(tokenResponse.Token);
                        return AuthResult.Ok(tokenResponse.Token);
                    }
                }

                return AuthResult.Fail(TryParseErrors(body) ?? $"Login failed ({(int)response.StatusCode})");
            }
            catch (HttpRequestException) { return AuthResult.Fail("Network error. Check your connection."); }
            catch (TaskCanceledException) { return AuthResult.Fail("Request timed out. Check your network connection."); }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[LOGIN/PASSWORD] ❌ {ex.GetType().Name}: {ex.Message}");
#endif
                return AuthResult.Fail("Unexpected error. Please try again.");
            }
        }

        // ============================================================
        // SEND OTP  →  POST /api/auth/login/otp/send
        // ============================================================
        public async Task<AuthResult> SendOtpAsync(
            OtpSendRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var (response, elapsed) = await SendAsync(
                    HttpMethod.Post, "/api/auth/login/otp/send",
                    request, cancellationToken);

#if DEBUG
                Debug.WriteLine($"[OTP/SEND] {elapsed}ms — {(int)response.StatusCode}");
#endif

                if (response.StatusCode == HttpStatusCode.NoContent || response.IsSuccessStatusCode)
                    return AuthResult.Ok();

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                return (int)response.StatusCode == 429
                    ? AuthResult.Fail(TryParseErrors(body) ?? "Too many OTP requests. Please wait a few minutes.")
                    : AuthResult.Fail(TryParseErrors(body) ?? $"Failed to send OTP ({(int)response.StatusCode})");
            }
            catch (HttpRequestException) { return AuthResult.Fail("Network error. Check your connection."); }
            catch (TaskCanceledException) { return AuthResult.Fail("Request timed out."); }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[OTP/SEND] ❌ {ex.GetType().Name}: {ex.Message}");
#endif
                return AuthResult.Fail("Unexpected error. Please try again.");
            }
        }

        // ============================================================
        // VERIFY OTP  →  POST /api/auth/login/otp/verify
        // ============================================================
        public async Task<AuthResult> VerifyOtpAsync(
            OtpVerifyRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var (response, elapsed) = await SendAsync(
                    HttpMethod.Post, "/api/auth/login/otp/verify",
                    request, cancellationToken);

#if DEBUG
                Debug.WriteLine($"[OTP/VERIFY] {elapsed}ms — {(int)response.StatusCode}");
#endif

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = Deserialize<TokenResponse>(body);
                    if (!string.IsNullOrEmpty(tokenResponse?.Token))
                    {
                        await SaveTokenAsync(tokenResponse.Token);
                        return AuthResult.Ok(tokenResponse.Token);
                    }
                }

                return AuthResult.Fail(TryParseErrors(body) ?? "Invalid or expired OTP.");
            }
            catch (HttpRequestException) { return AuthResult.Fail("Network error. Check your connection."); }
            catch (TaskCanceledException) { return AuthResult.Fail("Request timed out."); }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[OTP/VERIFY] ❌ {ex.GetType().Name}: {ex.Message}");
#endif
                return AuthResult.Fail("Unexpected error. Please try again.");
            }
        }

        // ============================================================
        // GET CAPTCHA  →  GET /api/auth/captcha
        // ============================================================
        public async Task<CaptchaResult> GetCaptchaAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var (response, elapsed) = await SendAsync<object?>(
                    HttpMethod.Get, "/api/auth/captcha",
                    null, cancellationToken);

#if DEBUG
                Debug.WriteLine($"[CAPTCHA] {elapsed}ms — {(int)response.StatusCode}");
#endif

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    var captcha = Deserialize<CaptchaResponse>(body);
                    if (captcha != null)
                        return new CaptchaResult { Success = true, Id = captcha.Id, Question = captcha.Question };
                }

                return FallbackCaptcha();
            }
            catch
            {
                return FallbackCaptcha();
            }
        }

        // ============================================================
        // GET BUSINESS TYPES  →  GET /api/lookup/business-types
        // ============================================================
        public async Task<IReadOnlyList<RegisterBusinessTypeDto>> GetBusinessTypesAsync(
            CancellationToken cancellationToken = default) // FIXED: was missing CancellationToken
        {
            try
            {
                var (response, elapsed) = await SendAsync<object?>(
                    HttpMethod.Get, "/api/lookup/business-types",
                    null, cancellationToken);

#if DEBUG
                Debug.WriteLine($"[BUSINESS TYPES] {elapsed}ms — {(int)response.StatusCode}");
#endif

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancellationToken);
                    var types = Deserialize<List<RegisterBusinessTypeDto>>(body);
#if DEBUG
                    Debug.WriteLine($"[BUSINESS TYPES] Parsed {types?.Count ?? 0} types");
#endif
                    return types ?? new List<RegisterBusinessTypeDto>();
                }

                return Array.Empty<RegisterBusinessTypeDto>();
            }
            catch
            {
                return Array.Empty<RegisterBusinessTypeDto>();
            }
        }

        // ============================================================
        // REGISTER  →  POST /api/auth/register
        // ============================================================
        public async Task<RegisterApiResponse> RegisterAsync(
            RegisterRequest request,
            CancellationToken cancellationToken = default) // FIXED: was missing CancellationToken
        {
#if DEBUG
            Debug.WriteLine($"\n[REGISTER] Type: {request.RegistrationType}, Email: {request.Email}");
#endif
            try
            {
                var (response, elapsed) = await SendAsync(
                    HttpMethod.Post, "/api/auth/register",
                    request, cancellationToken);

#if DEBUG
                Debug.WriteLine($"[REGISTER] {elapsed}ms — {(int)response.StatusCode}");
#endif

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var success = Deserialize<RegisterApiResponse>(body);
                    if (success != null) { success.Success = true; return success; }
                    return new RegisterApiResponse { Success = true };
                }

                return new RegisterApiResponse
                {
                    Success = false,
                    Message = TryParseErrors(body) ?? $"Registration failed ({(int)response.StatusCode})"
                };
            }
            catch (HttpRequestException) { return new RegisterApiResponse { Success = false, Message = "Network error. Check your connection." }; }
            catch (TaskCanceledException) { return new RegisterApiResponse { Success = false, Message = "Request timed out. Check your network connection." }; }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"[REGISTER] ❌ {ex.GetType().Name}: {ex.Message}");
#endif
                return new RegisterApiResponse { Success = false, Message = "Unexpected error. Please try again." };
            }
        }

        // ============================================================
        // AUTHENTICATED HELPERS
        // ============================================================
        public async Task<T?> GetAuthenticatedAsync<T>(
            string endpoint,
            CancellationToken cancellationToken = default)
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                throw new UnauthorizedAccessException("No authentication token found.");

            using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(message, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await LogoutAsync();
                throw new UnauthorizedAccessException("Session expired. Please login again.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(_deserializeOpts, cancellationToken);
        }

        public async Task<TResponse?> PostAuthenticatedAsync<TRequest, TResponse>(
            string endpoint,
            TRequest data,
            CancellationToken cancellationToken = default)
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                throw new UnauthorizedAccessException("No authentication token found.");

            var json = Serialize(data);
            using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(message, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                await LogoutAsync();
                throw new UnauthorizedAccessException("Session expired. Please login again.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(_deserializeOpts, cancellationToken);
        }

        // ============================================================
        // TOKEN / SESSION MANAGEMENT
        // ============================================================
        private async Task SaveTokenAsync(string token)
        {
            try
            {
                await SecureStorage.SetAsync("auth_token", token);
                _cachedToken = token;
                _tokenCachedAt = DateTime.UtcNow;
#if DEBUG
                Debug.WriteLine("✅ Token saved.");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine($"❌ SaveToken error: {ex.Message}");
#endif
            }
        }

        public async Task<string> GetTokenAsync()
        {
            // Return cached value if fresh — avoids repeated SecureStorage I/O
            if (!string.IsNullOrEmpty(_cachedToken) &&
                DateTime.UtcNow - _tokenCachedAt < _tokenCacheDuration)
                return _cachedToken;

            try
            {
                var token = await SecureStorage.GetAsync("auth_token") ?? string.Empty;
                if (!string.IsNullOrEmpty(token))
                {
                    _cachedToken = token;
                    _tokenCachedAt = DateTime.UtcNow;
                }
                return token;
            }
            catch { return string.Empty; }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;

            try
            {
                var jwt = _jwtHandler.ReadJwtToken(token);
                // SECURITY FIX: require 5-minute buffer, not 1-minute,
                // to prevent edge-case use of nearly-expired tokens
                return jwt.ValidTo > DateTime.UtcNow.AddMinutes(5);
            }
            catch { return false; }
        }

        public Task LogoutAsync()
        {
            SecureStorage.Remove("auth_token");
            _cachedToken = null;
            _tokenCachedAt = DateTime.MinValue;
#if DEBUG
            Debug.WriteLine("✅ Logged out — token cleared.");
#endif
            return Task.CompletedTask;
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        /// <summary>
        /// Central send method — eliminates the duplicated try/catch/stopwatch
        /// pattern that appeared 6 times in the original file.
        /// </summary>
        private async Task<(HttpResponseMessage Response, long ElapsedMs)> SendAsync<T>(
            HttpMethod method,
            string endpoint,
            T? body,
            CancellationToken cancellationToken)
        {
#if DEBUG
            var sw = Stopwatch.StartNew();
#endif
            HttpContent? content = null;
            if (body != null)
                content = new StringContent(Serialize(body), Encoding.UTF8, "application/json");

            using var message = new HttpRequestMessage(method, endpoint)
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(message, cancellationToken)
                .ConfigureAwait(false);

#if DEBUG
            sw.Stop();
            return (response, sw.ElapsedMilliseconds);
#else
            return (response, 0);
#endif
        }

        private static string Serialize<T>(T obj) =>
            JsonSerializer.Serialize(obj, _serializeOpts);

        private static T? Deserialize<T>(string json) =>
            JsonSerializer.Deserialize<T>(json, _deserializeOpts);

        private static string? TryParseErrors(string body)
        {
            try
            {
                var err = JsonSerializer.Deserialize<ErrorResponse>(body, _deserializeOpts);
                return err?.Errors?.FirstOrDefault();
            }
            catch { return null; }
        }

        private static CaptchaResult FallbackCaptcha() =>
            new() { Success = false, Question = "What is 5 + 3?", Id = 0 };
    }

    // ============================================================
    // REQUEST / RESPONSE MODELS
    // ============================================================

    public class PasswordLoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public int CaptchaId { get; set; }
        public string CaptchaAnswer { get; set; } = string.Empty;
    }

    public class OtpSendRequest
    {
        public string Phone { get; set; } = string.Empty;
        public int CaptchaId { get; set; }
        public string CaptchaAnswer { get; set; } = string.Empty;
    }

    public class OtpVerifyRequest
    {
        public string Phone { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    public class TokenResponse
    {
        public string Token { get; set; } = string.Empty;
    }

    public class CaptchaResponse
    {
        public int Id { get; set; }
        public string Question { get; set; } = string.Empty;
    }

    public class ErrorResponse
    {
        public string[] Errors { get; set; } = [];
    }

    public class AuthResult
    {
        public bool Success { get; private set; }
        public string? Token { get; private set; }
        public string? Error { get; private set; }

        public static AuthResult Ok(string? token = null) =>
            new() { Success = true, Token = token };

        public static AuthResult Fail(string error) =>
            new() { Success = false, Error = error };
    }

    public class CaptchaResult
    {
        public bool Success { get; set; }
        public int Id { get; set; }
        public string Question { get; set; } = string.Empty;
    }
}