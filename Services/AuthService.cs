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
                Timeout = TimeSpan.FromSeconds(15)
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
                    Debug.WriteLine($"[LOGIN/PASSWORD] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return AuthResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[LOGIN/PASSWORD] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return AuthResult.Fail("Request timed out. Check your network connection.");
                }

                sw.Stop();
                Debug.WriteLine($"[LOGIN/PASSWORD] ✅ {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

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
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOGIN/PASSWORD] ❌ {ex.GetType().FullName}: {ex.Message}");
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
                var endpoint = "/api/auth/login/otp/send";

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
                    Debug.WriteLine($"[OTP/SEND] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return AuthResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/SEND] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return AuthResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[OTP/SEND] ✅ {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent ||
                    response.IsSuccessStatusCode)
                    return AuthResult.Ok();

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var error = TryParseErrors(body);

                if ((int)response.StatusCode == 429)
                    return AuthResult.Fail(error ?? "Too many OTP requests. Please wait a few minutes.");

                return AuthResult.Fail(error ?? $"Failed to send OTP ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP/SEND] ❌ {ex.GetType().FullName}: {ex.Message}");
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
                    Debug.WriteLine($"[OTP/VERIFY] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return AuthResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/VERIFY] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return AuthResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[OTP/VERIFY] ✅ {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

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
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP/VERIFY] ❌ {ex.GetType().FullName}: {ex.Message}");
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
                    Debug.WriteLine($"[CAPTCHA] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return FallbackCaptcha();
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[CAPTCHA] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return FallbackCaptcha();
                }

                sw.Stop();
                Debug.WriteLine($"[CAPTCHA] ✅ {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

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
                Debug.WriteLine($"[CAPTCHA] ❌ {ex.GetType().FullName}: {ex.Message}");
                return FallbackCaptcha();
            }
        }

        // ============================================================
        // GET BUSINESS TYPES  →  GET /api/lookup/business-types
        // ============================================================
        public async Task<IEnumerable<RegisterBusinessTypeDto>> GetBusinessTypesAsync()
        {
            Debug.WriteLine($"\n[BUSINESS TYPES] GET {_baseUrl}/api/lookup/business-types");

            try
            {
                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    using var message = BuildRequest(HttpMethod.Get, "/api/lookup/business-types");
                    response = await _httpClient.SendAsync(message);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[BUSINESS TYPES] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return Enumerable.Empty<RegisterBusinessTypeDto>();
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[BUSINESS TYPES] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return Enumerable.Empty<RegisterBusinessTypeDto>();
                }

                sw.Stop();
                Debug.WriteLine($"[BUSINESS TYPES] ✅ {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var types = Deserialize<List<RegisterBusinessTypeDto>>(body);
                    Debug.WriteLine($"[BUSINESS TYPES] Parsed {types?.Count ?? 0} types");
                    return types ?? new List<RegisterBusinessTypeDto>();
                }

                Debug.WriteLine($"[BUSINESS TYPES] ❌ Error: {TryParseErrors(body)}");
                return Enumerable.Empty<RegisterBusinessTypeDto>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BUSINESS TYPES] ❌ {ex.GetType().FullName}: {ex.Message}");
                return Enumerable.Empty<RegisterBusinessTypeDto>();
            }
        }

        // ============================================================
        // REGISTER  →  POST /api/auth/register
        // ============================================================
        public async Task<RegisterApiResponse> RegisterAsync(RegisterRequest request)
        {
            Debug.WriteLine($"\n[REGISTER START] Type: {request.RegistrationType}, Email: {request.Email}");

            try
            {
                var json = Serialize(request);
                var endpoint = "/api/auth/register";

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    using var message = BuildRequest(HttpMethod.Post, endpoint,
                        new StringContent(json, Encoding.UTF8, "application/json"));
                    response = await _httpClient.SendAsync(message);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[REGISTER] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return new RegisterApiResponse { Success = false, Message = "Network error. Check your connection." };
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[REGISTER] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return new RegisterApiResponse { Success = false, Message = "Request timed out. Check your network connection." };
                }

                sw.Stop();
                Debug.WriteLine($"[REGISTER] ✅ {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();

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
            catch (Exception ex)
            {
                Debug.WriteLine($"[REGISTER] ❌ {ex.GetType().FullName}: {ex.Message}");
                return new RegisterApiResponse { Success = false, Message = $"Unexpected error: {ex.Message}" };
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

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;

            try
            {
                var jwt = _jwtHandler.ReadJwtToken(token);
                return jwt.ValidTo > DateTime.UtcNow.AddMinutes(1);
            }
            catch { return false; }
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