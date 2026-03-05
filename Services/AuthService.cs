using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using System.Text;

namespace CraftConnect_Mobile_App.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public AuthService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');

            Debug.WriteLine($"[AUTH SERVICE] Config null: {config == null}");
            Debug.WriteLine($"[AUTH SERVICE] BaseUrl from config: '{config.BaseUrl}'");
            Debug.WriteLine($"[AUTH SERVICE] BaseUrl after trim: '{_baseUrl}'");

#if ANDROID
            Debug.WriteLine($"[AUTH SERVICE] Platform: ANDROID — using AndroidMessageHandler");
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SSL CALLBACK] ✅ Called! Host: {message.RequestUri.Host}");
                    Debug.WriteLine($"[SSL CALLBACK] Cert subject: {cert?.Subject}");
                    Debug.WriteLine($"[SSL CALLBACK] Errors: {errors}");
                    Debug.WriteLine($"[SSL CALLBACK] Returning true (accepting cert)");
                    return true;
                }
            };
#else
            Debug.WriteLine($"[AUTH SERVICE] Platform: OTHER — using HttpClientHandler");
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SSL CALLBACK] ✅ Called! Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[AUTH SERVICE] HttpClient created. BaseAddress: {_httpClient.BaseAddress}");
            Debug.WriteLine($"[AUTH SERVICE] Timeout: {_httpClient.Timeout.TotalSeconds}s");
            Debug.WriteLine($"[AUTH SERVICE] Initialized with BaseUrl: {_baseUrl}");
        }

        // ============================================================
        // PASSWORD LOGIN  →  POST /api/auth/login/password
        // ============================================================
        public async Task<AuthResult> LoginWithPasswordAsync(PasswordLoginRequest request)
        {
            Debug.WriteLine($"\n[LOGIN/PASSWORD START] Email: {request.Email}");

            try
            {
                SetJsonHeaders();

                var json = Serialize(request);
                Debug.WriteLine($"[LOGIN/PASSWORD] Serialized JSON: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var endpoint = "/api/auth/login/password";
                var fullUrl = $"{_baseUrl}{endpoint}";
                Debug.WriteLine($"[LOGIN/PASSWORD] Full URL: {fullUrl}");
                Debug.WriteLine($"[LOGIN/PASSWORD] HttpClient BaseAddress: {_httpClient.BaseAddress}");
                Debug.WriteLine($"[LOGIN/PASSWORD] Sending request...");

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[LOGIN/PASSWORD] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[LOGIN/PASSWORD] Message: {httpEx.Message}");
                    Debug.WriteLine($"[LOGIN/PASSWORD] StatusCode: {httpEx.StatusCode}");
                    Debug.WriteLine($"[LOGIN/PASSWORD] InnerException type: {httpEx.InnerException?.GetType().FullName}");
                    Debug.WriteLine($"[LOGIN/PASSWORD] InnerException message: {httpEx.InnerException?.Message}");
                    Debug.WriteLine($"[LOGIN/PASSWORD] InnerException inner: {httpEx.InnerException?.InnerException?.Message}");
                    return AuthResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[LOGIN/PASSWORD] ❌ TaskCanceledException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[LOGIN/PASSWORD] Message: {tcEx.Message}");
                    Debug.WriteLine($"[LOGIN/PASSWORD] CancellationToken cancelled: {tcEx.CancellationToken.IsCancellationRequested}");
                    Debug.WriteLine($"[LOGIN/PASSWORD] InnerException: {tcEx.InnerException?.Message}");
                    return AuthResult.Fail("Request timed out. Check your network connection.");
                }

                sw.Stop();
                Debug.WriteLine($"[LOGIN/PASSWORD] ✅ Got response in {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[LOGIN/PASSWORD] Status: {(int)response.StatusCode} {response.StatusCode}");
                Debug.WriteLine($"[LOGIN/PASSWORD] ReasonPhrase: {response.ReasonPhrase}");

                var body = await response.Content.ReadAsStringAsync();
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
                Debug.WriteLine($"[LOGIN/PASSWORD] ❌ Unexpected exception: {ex.GetType().FullName}");
                Debug.WriteLine($"[LOGIN/PASSWORD] Message: {ex.Message}");
                Debug.WriteLine($"[LOGIN/PASSWORD] StackTrace: {ex.StackTrace}");
                return AuthResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // SEND OTP  →  POST /api/auth/login/otp/send
        // ============================================================
        public async Task<AuthResult> SendOtpAsync(OtpSendRequest request)
        {
            Debug.WriteLine($"\n[OTP/SEND START] Phone: {request.Phone}");

            try
            {
                SetJsonHeaders();

                var json = Serialize(request);
                Debug.WriteLine($"[OTP/SEND] Serialized JSON: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var endpoint = "/api/auth/login/otp/send";
                Debug.WriteLine($"[OTP/SEND] Full URL: {_baseUrl}{endpoint}");
                Debug.WriteLine($"[OTP/SEND] Sending request...");

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/SEND] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[OTP/SEND] Message: {httpEx.Message}");
                    Debug.WriteLine($"[OTP/SEND] InnerException type: {httpEx.InnerException?.GetType().FullName}");
                    Debug.WriteLine($"[OTP/SEND] InnerException message: {httpEx.InnerException?.Message}");
                    Debug.WriteLine($"[OTP/SEND] InnerException inner: {httpEx.InnerException?.InnerException?.Message}");
                    return AuthResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/SEND] ❌ TaskCanceledException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[OTP/SEND] Message: {tcEx.Message}");
                    Debug.WriteLine($"[OTP/SEND] InnerException: {tcEx.InnerException?.Message}");
                    return AuthResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[OTP/SEND] ✅ Got response in {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[OTP/SEND] Status: {(int)response.StatusCode} {response.StatusCode}");

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent ||
                    response.IsSuccessStatusCode)
                {
                    return AuthResult.Ok();
                }

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[OTP/SEND] Error body: {body}");

                var error = TryParseErrors(body);

                if ((int)response.StatusCode == 429)
                    return AuthResult.Fail(error ?? "Too many OTP requests. Please wait a few minutes.");

                return AuthResult.Fail(error ?? $"Failed to send OTP ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP/SEND] ❌ Unexpected exception: {ex.GetType().FullName}");
                Debug.WriteLine($"[OTP/SEND] Message: {ex.Message}");
                return AuthResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // VERIFY OTP  →  POST /api/auth/login/otp/verify
        // ============================================================
        public async Task<AuthResult> VerifyOtpAsync(OtpVerifyRequest request)
        {
            Debug.WriteLine($"\n[OTP/VERIFY START] Phone: {request.Phone}");

            try
            {
                SetJsonHeaders();

                var json = Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var endpoint = "/api/auth/login/otp/verify";
                Debug.WriteLine($"[OTP/VERIFY] Full URL: {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[OTP/VERIFY] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[OTP/VERIFY] Message: {httpEx.Message}");
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

                var body = await response.Content.ReadAsStringAsync();
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
        public async Task<CaptchaResult> GetCaptchaAsync()
        {
            Debug.WriteLine($"\n[CAPTCHA] GET {_baseUrl}/api/auth/captcha");

            try
            {
                SetJsonHeaders();

                Debug.WriteLine($"[CAPTCHA] Sending GET request...");
                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.GetAsync("/api/auth/captcha");
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[CAPTCHA] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[CAPTCHA] Message: {httpEx.Message}");
                    Debug.WriteLine($"[CAPTCHA] InnerException type: {httpEx.InnerException?.GetType().FullName}");
                    Debug.WriteLine($"[CAPTCHA] InnerException message: {httpEx.InnerException?.Message}");
                    Debug.WriteLine($"[CAPTCHA] InnerException inner: {httpEx.InnerException?.InnerException?.Message}");
                    return new CaptchaResult { Success = false, Question = "What is 5 + 3?", Id = 0 };
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[CAPTCHA] ❌ Timeout after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[CAPTCHA] Message: {tcEx.Message}");
                    Debug.WriteLine($"[CAPTCHA] InnerException: {tcEx.InnerException?.Message}");
                    return new CaptchaResult { Success = false, Question = "What is 5 + 3?", Id = 0 };
                }

                sw.Stop();
                Debug.WriteLine($"[CAPTCHA] ✅ Got response in {sw.ElapsedMilliseconds}ms");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[CAPTCHA] Status: {(int)response.StatusCode}, Body: {body}");

                if (response.IsSuccessStatusCode)
                {
                    var captcha = Deserialize<CaptchaResponse>(body);
                    if (captcha != null)
                        return new CaptchaResult { Success = true, Id = captcha.Id, Question = captcha.Question };
                }

                return new CaptchaResult { Success = false, Question = "What is 5 + 3?", Id = 0 };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CAPTCHA] ❌ Unexpected exception: {ex.GetType().FullName}: {ex.Message}");
                return new CaptchaResult { Success = false, Question = "What is 5 + 3?", Id = 0 };
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
                SetJsonHeaders();

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.GetAsync("/api/lookup/business-types");
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[BUSINESS TYPES] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[BUSINESS TYPES] Message: {httpEx.Message}");
                    Debug.WriteLine($"[BUSINESS TYPES] InnerException: {httpEx.InnerException?.Message}");
                    return Enumerable.Empty<RegisterBusinessTypeDto>();
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[BUSINESS TYPES] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return Enumerable.Empty<RegisterBusinessTypeDto>();
                }

                sw.Stop();
                Debug.WriteLine($"[BUSINESS TYPES] ✅ Got response in {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[BUSINESS TYPES] Status: {(int)response.StatusCode} {response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[BUSINESS TYPES] Response body: {body}");

                if (response.IsSuccessStatusCode)
                {
                    var types = Deserialize<List<RegisterBusinessTypeDto>>(body);
                    Debug.WriteLine($"[BUSINESS TYPES] Parsed {types?.Count ?? 0} types");
                    return types ?? new List<RegisterBusinessTypeDto>();
                }

                var error = TryParseErrors(body);
                Debug.WriteLine($"[BUSINESS TYPES] ❌ Error: {error}");
                return Enumerable.Empty<RegisterBusinessTypeDto>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BUSINESS TYPES] ❌ Unexpected exception: {ex.GetType().FullName}: {ex.Message}");
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
                SetJsonHeaders();

                var json = Serialize(request);
                Debug.WriteLine($"[REGISTER] Serialized JSON: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var endpoint = "/api/auth/register";
                var fullUrl = $"{_baseUrl}{endpoint}";
                Debug.WriteLine($"[REGISTER] Full URL: {fullUrl}");
                Debug.WriteLine($"[REGISTER] Sending request...");

                var sw = Stopwatch.StartNew();

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[REGISTER] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[REGISTER] Message: {httpEx.Message}");
                    Debug.WriteLine($"[REGISTER] StatusCode: {httpEx.StatusCode}");
                    Debug.WriteLine($"[REGISTER] InnerException type: {httpEx.InnerException?.GetType().FullName}");
                    Debug.WriteLine($"[REGISTER] InnerException message: {httpEx.InnerException?.Message}");
                    Debug.WriteLine($"[REGISTER] InnerException inner: {httpEx.InnerException?.InnerException?.Message}");
                    return new RegisterApiResponse { Success = false, Message = "Network error. Check your connection." };
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[REGISTER] ❌ TaskCanceledException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[REGISTER] Message: {tcEx.Message}");
                    Debug.WriteLine($"[REGISTER] CancellationToken cancelled: {tcEx.CancellationToken.IsCancellationRequested}");
                    Debug.WriteLine($"[REGISTER] InnerException: {tcEx.InnerException?.Message}");
                    return new RegisterApiResponse { Success = false, Message = "Request timed out. Check your network connection." };
                }

                sw.Stop();
                Debug.WriteLine($"[REGISTER] ✅ Got response in {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[REGISTER] Status: {(int)response.StatusCode} {response.StatusCode}");
                Debug.WriteLine($"[REGISTER] ReasonPhrase: {response.ReasonPhrase}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[REGISTER] Response body: {body}");

                if (response.IsSuccessStatusCode)
                {
                    // Try to parse a typed response first
                    var success = Deserialize<RegisterApiResponse>(body);
                    if (success != null)
                    {
                        success.Success = true;
                        return success;
                    }
                    return new RegisterApiResponse { Success = true };
                }

                // Non-success — extract the most useful error message
                var errorMsg = TryParseErrors(body);
                Debug.WriteLine($"[REGISTER] Error parsed: {errorMsg}");
                return new RegisterApiResponse
                {
                    Success = false,
                    Message = errorMsg ?? $"Registration failed ({(int)response.StatusCode})"
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REGISTER] ❌ Unexpected exception: {ex.GetType().FullName}");
                Debug.WriteLine($"[REGISTER] Message: {ex.Message}");
                Debug.WriteLine($"[REGISTER] StackTrace: {ex.StackTrace}");
                return new RegisterApiResponse { Success = false, Message = $"Unexpected error: {ex.Message}" };
            }
        }

        // ============================================================
        // AUTHENTICATED HELPERS
        // ============================================================
        public async Task<T?> GetAuthenticatedAsync<T>(string endpoint)
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                throw new UnauthorizedAccessException("No authentication token found.");

            SetJsonHeaders();
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.GetAsync(endpoint);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await LogoutAsync();
                throw new UnauthorizedAccessException("Session expired. Please login again.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>();
        }

        public async Task<TResponse?> PostAuthenticatedAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            var token = await GetTokenAsync();
            if (string.IsNullOrEmpty(token))
                throw new UnauthorizedAccessException("No authentication token found.");

            SetJsonHeaders();
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.PostAsJsonAsync(endpoint, data);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                await LogoutAsync();
                throw new UnauthorizedAccessException("Session expired. Please login again.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TResponse>();
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
            return !string.IsNullOrEmpty(token);
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
                var response = await _httpClient.GetAsync("/api/auth/captcha");
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
        private void SetJsonHeaders()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "Mobile");
        }

        private static string Serialize<T>(T obj) =>
            JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

        private static T? Deserialize<T>(string json) =>
            JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        private static string? TryParseErrors(string body)
        {
            try
            {
                var err = JsonSerializer.Deserialize<ErrorResponse>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return err?.Errors?.FirstOrDefault();
            }
            catch { return null; }
        }
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