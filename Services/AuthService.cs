using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;

namespace CraftConnect_Mobile_App.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        // ── Static options — created once, reused forever (major speed win) ──
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

        public AuthService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');

            Debug.WriteLine($"[AUTH SERVICE] BaseUrl: '{_baseUrl}'");

            // DEBUG: SSL bypass enabled for development (self-signed certs).
            // RELEASE: Full certificate validation is enforced — no bypass.
#if ANDROID && DEBUG
            Debug.WriteLine("[AUTH SERVICE] Platform: ANDROID DEBUG — SSL bypass ON (dev only)");
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#elif ANDROID
            Debug.WriteLine("[AUTH SERVICE] Platform: ANDROID RELEASE — SSL validation enforced");
            var handler = new Xamarin.Android.Net.AndroidMessageHandler();
#elif DEBUG
            Debug.WriteLine("[AUTH SERVICE] Platform: OTHER DEBUG — SSL bypass ON (dev only)");
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#else
            Debug.WriteLine("[AUTH SERVICE] Platform: OTHER RELEASE — SSL validation enforced");
            var handler = new HttpClientHandler();
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(15) // Reduced from 30s — better UX on mobile
            };

            Debug.WriteLine($"[AUTH SERVICE] Initialized. BaseAddress: {_httpClient.BaseAddress}, Timeout: {_httpClient.Timeout.TotalSeconds}s");
        }

        // ============================================================
        // PASSWORD LOGIN  →  POST /api/auth/login/password
        // ============================================================
        public async Task<AuthResult> LoginWithPasswordAsync(PasswordLoginRequest request, CancellationToken cancellationToken = default)
        {
            Debug.WriteLine($"\n[LOGIN/PASSWORD START] Email: {request.Email}");

            try
            {
                var json = Serialize(request);
                Debug.WriteLine($"[LOGIN/PASSWORD] Serialized JSON: {json}");

                var endpoint = "/api/auth/login/password";
                Debug.WriteLine($"[LOGIN/PASSWORD] Full URL: {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    using var message = BuildRequest(HttpMethod.Post, endpoint,
                        new StringContent(json, Encoding.UTF8, "application/json"));
                    response = await _httpClient.SendAsync(message, cancellationToken);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[LOGIN/PASSWORD] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[LOGIN/PASSWORD] Message: {httpEx.Message}");
                    Debug.WriteLine($"[LOGIN/PASSWORD] StatusCode: {httpEx.StatusCode}");
                    Debug.WriteLine($"[LOGIN/PASSWORD] InnerException: {httpEx.InnerException?.Message}");
                    return AuthResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[LOGIN/PASSWORD] ❌ TaskCanceledException after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return AuthResult.Fail("Request timed out. Check your network connection.");
                }

                sw.Stop();
                Debug.WriteLine($"[LOGIN/PASSWORD] ✅ Got response in {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[LOGIN/PASSWORD] Status: {(int)response.StatusCode} {response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[LOGIN/PASSWORD] Response body: {body}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = Deserialize<TokenResponse>(body);
                    Debug.WriteLine($"[LOGIN/PASSWORD] Token parsed: {!string.IsNullOrEmpty(tokenResponse?.Token)}");
                    if (!string.IsNullOrEmpty(tokenResponse?.Token))
                    {
                        await SaveTokenAsync(tokenResponse.Token);
                        return AuthResult.Ok(tokenResponse.Token);
                    }
                }

                var error = TryParseErrors(body);
                Debug.WriteLine($"[LOGIN/PASSWORD] Error parsed: {error}");
                return AuthResult.Fail(error ?? $"Login failed ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOGIN/PASSWORD] ❌ Unexpected exception: {ex.GetType().FullName}: {ex.Message}");
                return AuthResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // SEND OTP  →  POST /api/auth/login/otp/send
        // ============================================================
        public async Task<AuthResult> SendOtpAsync(OtpSendRequest request, CancellationToken cancellationToken = default)
        {
            Debug.WriteLine($"\n[OTP/SEND START] Phone: {request.Phone}");

            try
            {
                var json = Serialize(request);
                Debug.WriteLine($"[OTP/SEND] Serialized JSON: {json}");

                var endpoint = "/api/auth/login/otp/send";
                Debug.WriteLine($"[OTP/SEND] Full URL: {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    using var message = BuildRequest(HttpMethod.Post, endpoint,
                        new StringContent(json, Encoding.UTF8, "application/json"));
                    response = await _httpClient.SendAsync(message, cancellationToken);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/SEND] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[OTP/SEND] Message: {httpEx.Message}");
                    Debug.WriteLine($"[OTP/SEND] InnerException: {httpEx.InnerException?.Message}");
                    return AuthResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/SEND] ❌ TaskCanceledException after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return AuthResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[OTP/SEND] ✅ Got response in {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[OTP/SEND] Status: {(int)response.StatusCode} {response.StatusCode}");

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent ||
                    response.IsSuccessStatusCode)
                    return AuthResult.Ok();

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[OTP/SEND] Error body: {body}");

                var error = TryParseErrors(body);

                if ((int)response.StatusCode == 429)
                    return AuthResult.Fail(error ?? "Too many OTP requests. Please wait a few minutes.");

                return AuthResult.Fail(error ?? $"Failed to send OTP ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP/SEND] ❌ Unexpected exception: {ex.GetType().FullName}: {ex.Message}");
                return AuthResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // VERIFY OTP  →  POST /api/auth/login/otp/verify
        // ============================================================
        public async Task<AuthResult> VerifyOtpAsync(OtpVerifyRequest request, CancellationToken cancellationToken = default)
        {
            Debug.WriteLine($"\n[OTP/VERIFY START] Phone: {request.Phone}");

            try
            {
                var json = Serialize(request);
                var endpoint = "/api/auth/login/otp/verify";
                Debug.WriteLine($"[OTP/VERIFY] Full URL: {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    using var message = BuildRequest(HttpMethod.Post, endpoint,
                        new StringContent(json, Encoding.UTF8, "application/json"));
                    response = await _httpClient.SendAsync(message, cancellationToken);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/VERIFY] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    Debug.WriteLine($"[OTP/VERIFY] InnerException: {httpEx.InnerException?.Message}");
                    return AuthResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/VERIFY] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return AuthResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[OTP/VERIFY] ✅ Got response in {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[OTP/VERIFY] Status: {(int)response.StatusCode} {response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[OTP/VERIFY] Response: {body}");

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = Deserialize<TokenResponse>(body);
                    if (!string.IsNullOrEmpty(tokenResponse?.Token))
                    {
                        await SaveTokenAsync(tokenResponse.Token);
                        return AuthResult.Ok(tokenResponse.Token);
                    }
                }

                var error = TryParseErrors(body);
                return AuthResult.Fail(error ?? "Invalid or expired OTP.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP/VERIFY] ❌ Unexpected exception: {ex.GetType().FullName}: {ex.Message}");
                return AuthResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // GET CAPTCHA  →  GET /api/auth/captcha
        // ============================================================
        public async Task<CaptchaResult> GetCaptchaAsync(CancellationToken cancellationToken = default)
        {
            Debug.WriteLine($"\n[CAPTCHA] GET {_baseUrl}/api/auth/captcha");

            try
            {
                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    using var message = BuildRequest(HttpMethod.Get, "/api/auth/captcha");
                    response = await _httpClient.SendAsync(message, cancellationToken);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[CAPTCHA] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    Debug.WriteLine($"[CAPTCHA] InnerException: {httpEx.InnerException?.Message}");
                    return FallbackCaptcha();
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[CAPTCHA] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return FallbackCaptcha();
                }

                sw.Stop();
                Debug.WriteLine($"[CAPTCHA] ✅ Got response in {sw.ElapsedMilliseconds}ms");

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"[CAPTCHA] Status: {(int)response.StatusCode}, Body: {body}");

                if (response.IsSuccessStatusCode)
                {
                    var captcha = Deserialize<CaptchaResponse>(body);
                    if (captcha != null)
                        return new CaptchaResult { Success = true, Id = captcha.Id, Question = captcha.Question };
                }

                return FallbackCaptcha();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CAPTCHA] ❌ Unexpected exception: {ex.GetType().FullName}: {ex.Message}");
                return FallbackCaptcha();
            }
        }

        // ============================================================
        // AUTHENTICATED HELPERS
        // ============================================================
        public async Task<T?> GetAuthenticatedAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                throw new UnauthorizedAccessException("No authentication token found.");

            using var message = BuildRequest(HttpMethod.Get, endpoint);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(message, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await LogoutAsync();
                throw new UnauthorizedAccessException("Session expired. Please login again.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        public async Task<TResponse?> PostAuthenticatedAsync<TRequest, TResponse>(string endpoint, TRequest data, CancellationToken cancellationToken = default)
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                throw new UnauthorizedAccessException("No authentication token found.");

            var json = Serialize(data);
            using var message = BuildRequest(HttpMethod.Post, endpoint,
                new StringContent(json, Encoding.UTF8, "application/json"));
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(message, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await LogoutAsync();
                throw new UnauthorizedAccessException("Session expired. Please login again.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
        }

        // ============================================================
        // TOKEN / SESSION MANAGEMENT
        // ============================================================
        private async Task SaveTokenAsync(string token)
        {
            try
            {
                await SecureStorage.SetAsync("auth_token", token);
                Debug.WriteLine("✅ Token saved to SecureStorage.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ SaveToken error: {ex.Message}");
            }
        }

        public async Task<string> GetTokenAsync()
        {
            try { return await SecureStorage.GetAsync("auth_token") ?? string.Empty; }
            catch { return string.Empty; }
        }

        /// <summary>
        /// Checks token exists AND is not expired.
        /// Replaces the old string-only check to prevent silent 401s.
        /// </summary>
        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;

            try
            {
                var jwt = _jwtHandler.ReadJwtToken(token);
                // 1-minute buffer so we don't use a token that expires mid-request
                return jwt.ValidTo > DateTime.UtcNow.AddMinutes(1);
            }
            catch
            {
                // Malformed token — treat as unauthenticated
                return false;
            }
        }

        public Task LogoutAsync()
        {
            SecureStorage.Remove("auth_token");
            Debug.WriteLine("✅ Logged out — token cleared.");
            return Task.CompletedTask;
        }

        // ============================================================
        // CONNECTION TEST
        // ============================================================
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var message = BuildRequest(HttpMethod.Get, "/api/auth/captcha");
                var response = await _httpClient.SendAsync(message);
                Debug.WriteLine($"[TEST CONNECTION] {(int)response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TEST CONNECTION] Failed: {ex.Message}");
                return false;
            }
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        /// <summary>
        /// Builds a per-request HttpRequestMessage with standard headers.
        /// Replaces DefaultRequestHeaders.Clear() which caused race conditions
        /// when two requests fired simultaneously on the shared HttpClient.
        /// </summary>
        private static HttpRequestMessage BuildRequest(HttpMethod method, string endpoint, HttpContent? content = null)
        {
            var msg = new HttpRequestMessage(method, endpoint);
            msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            msg.Headers.Add("X-Requested-With", "Mobile");
            msg.Content = content;
            return msg;
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