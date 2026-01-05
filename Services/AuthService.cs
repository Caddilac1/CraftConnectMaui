using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using System.Text;

namespace CraftConnect_Mobile_App.Services
{
    public class AuthService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://192.168.43.232:7023";

        public AuthService()
        {
            var handler = new HttpClientHandler();

#if DEBUG
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SSL VALIDATION] Host: {message.RequestUri.Host}");
                    Debug.WriteLine($"[SSL VALIDATION] Certificate Subject: {cert?.Subject}");
                    Debug.WriteLine($"[SSL VALIDATION] SSL Errors: {errors}");
                    return true;
                };
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[AUTH SERVICE] Initialized with BaseUrl: {BaseUrl}");
        }

        // ============================================================
        // UNIFIED LOGIN METHOD (Supports both OTP and Password)
        // ============================================================
        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            Debug.WriteLine($"\n[LOGIN START] =========================================");
            Debug.WriteLine($"[LOGIN] Mode: {(request.UsePassword ? "PASSWORD" : "OTP")}");
            Debug.WriteLine($"[LOGIN] EmailOrPhone: {request.EmailOrPhone}");
            Debug.WriteLine($"[LOGIN] UsePassword: {request.UsePassword}");
            Debug.WriteLine($"[LOGIN] Password Length: {(request.UsePassword ? request.Password?.Length.ToString() : "N/A (OTP Mode)")}");
            Debug.WriteLine($"[LOGIN] RememberMe: {request.RememberMe}");

            try
            {
                // Clear and set headers properly for API requests
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "Mobile");

                Debug.WriteLine($"[LOGIN HEADERS] Accept: application/json");
                Debug.WriteLine($"[LOGIN HEADERS] X-Requested-With: Mobile");

                // Serialize request with camelCase naming
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                };

                var jsonRequest = JsonSerializer.Serialize(request, jsonOptions);

                Debug.WriteLine($"[LOGIN REQUEST JSON]:");
                Debug.WriteLine(jsonRequest);
                Debug.WriteLine($"[LOGIN REQUEST END]");

                // Create content with explicit Content-Type
                var content = new StringContent(
                    jsonRequest,
                    Encoding.UTF8,
                    "application/json"
                );

                // IMPORTANT: Use the API endpoint, not /Account/Login
                var endpoint = "/api/Auth/login";  // ✅ CORRECT ENDPOINT FOR MOBILE
                var fullUrl = $"{BaseUrl}{endpoint}";
                Debug.WriteLine($"[LOGIN] Endpoint: {fullUrl}");

                var stopwatch = Stopwatch.StartNew();
                Debug.WriteLine($"[LOGIN] Sending request...");

                var response = await _httpClient.PostAsync(endpoint, content);
                stopwatch.Stop();

                Debug.WriteLine($"[LOGIN] Request completed in {stopwatch.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[LOGIN] Response Status Code: {(int)response.StatusCode} ({response.StatusCode})");

                // Log response headers
                Debug.WriteLine($"[LOGIN RESPONSE HEADERS]:");
                foreach (var header in response.Headers)
                {
                    Debug.WriteLine($"  {header.Key}: {string.Join(", ", header.Value)}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[LOGIN RESPONSE BODY]:");
                Debug.WriteLine(responseContent);
                Debug.WriteLine($"[LOGIN RESPONSE END]");

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(
                            responseContent,
                            new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }
                        );

                        Debug.WriteLine($"[LOGIN PARSE SUCCESS] Success: {loginResponse?.Success}");
                        Debug.WriteLine($"[LOGIN PARSE SUCCESS] Message: {loginResponse?.Message}");
                        Debug.WriteLine($"[LOGIN PARSE SUCCESS] RequiresOtp: {loginResponse?.RequiresOtp}");
                        Debug.WriteLine($"[LOGIN PARSE SUCCESS] HasPassword: {loginResponse?.HasPassword}");

                        if (loginResponse?.RequiresOtp == true)
                        {
                            Debug.WriteLine($"[LOGIN] OTP required for: {loginResponse.Email}");
                            Debug.WriteLine($"[LOGIN] OTP Token present: {!string.IsNullOrEmpty(loginResponse.OtpToken)}");
                        }

                        // Password authentication successful
                        if (loginResponse?.Success == true && !string.IsNullOrEmpty(loginResponse.Token))
                        {
                            Debug.WriteLine($"[LOGIN] Login successful! Token received.");
                            Debug.WriteLine($"[LOGIN] Token Length: {loginResponse.Token.Length}");
                            await SaveAuthDataAsync(loginResponse);
                        }

                        Debug.WriteLine($"[LOGIN END] ===========================================\n");
                        return loginResponse ?? new LoginResponse
                        {
                            Success = false,
                            Message = "Failed to parse response"
                        };
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"[LOGIN JSON PARSE ERROR]: {jsonEx.Message}");
                        Debug.WriteLine($"[LOGIN JSON PARSE STACK]: {jsonEx.StackTrace}");

                        Debug.WriteLine($"[LOGIN END] ===========================================\n");
                        return new LoginResponse
                        {
                            Success = false,
                            Message = $"Invalid response format: {jsonEx.Message}"
                        };
                    }
                }
                else
                {
                    Debug.WriteLine($"[LOGIN HTTP ERROR] Status: {response.StatusCode}");

                    // Try to parse error response
                    try
                    {
                        if (!string.IsNullOrEmpty(responseContent))
                        {
                            var errorResponse = JsonSerializer.Deserialize<LoginResponse>(
                                responseContent,
                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                            );

                            Debug.WriteLine($"[LOGIN ERROR] Message: {errorResponse?.Message}");
                            Debug.WriteLine($"[LOGIN END] ===========================================\n");

                            return errorResponse ?? new LoginResponse
                            {
                                Success = false,
                                Message = $"Login failed: {response.StatusCode}"
                            };
                        }
                    }
                    catch (Exception parseEx)
                    {
                        Debug.WriteLine($"[LOGIN ERROR PARSE FAILED]: {parseEx.Message}");
                    }

                    Debug.WriteLine($"[LOGIN END] ===========================================\n");
                    return new LoginResponse
                    {
                        Success = false,
                        Message = $"Login failed: {response.StatusCode}"
                    };
                }
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"[LOGIN HTTP REQUEST EXCEPTION]: {httpEx.Message}");
                Debug.WriteLine($"[LOGIN HTTP REQUEST EXCEPTION INNER]: {httpEx.InnerException?.Message}");
                Debug.WriteLine($"[LOGIN HTTP REQUEST EXCEPTION STATUS]: {httpEx.StatusCode}");

                Debug.WriteLine($"[LOGIN END] ===========================================\n");
                return new LoginResponse
                {
                    Success = false,
                    Message = httpEx.StatusCode == null ?
                        "Network error. Check your connection." :
                        $"Network error: {httpEx.StatusCode}"
                };
            }
            catch (TaskCanceledException timeoutEx)
            {
                Debug.WriteLine($"[LOGIN TIMEOUT EXCEPTION]: {timeoutEx.Message}");
                Debug.WriteLine($"[LOGIN END] ===========================================\n");
                return new LoginResponse
                {
                    Success = false,
                    Message = "Request timeout. Please check your network connection."
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOGIN GENERAL EXCEPTION]: {ex.Message}");
                Debug.WriteLine($"[LOGIN EXCEPTION TYPE]: {ex.GetType().FullName}");
                Debug.WriteLine($"[LOGIN EXCEPTION STACK]: {ex.StackTrace}");

                Debug.WriteLine($"[LOGIN END] ===========================================\n");
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Error during login: {ex.Message}"
                };
            }
        }

        // ============================================================
        // VERIFY OTP
        // ============================================================
        public async Task<LoginResponse> VerifyOtpAsync(VerifyOtpRequest request)
        {
            Debug.WriteLine($"\n[VERIFY OTP START] =========================================");
            Debug.WriteLine($"[VERIFY OTP] Email: {request.Email}");
            Debug.WriteLine($"[VERIFY OTP] OTP: {request.Otp}");
            Debug.WriteLine($"[VERIFY OTP] Token Length: {request.Token?.Length ?? 0}");

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "Mobile");

                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                var jsonRequest = JsonSerializer.Serialize(request, jsonOptions);

                Debug.WriteLine($"[VERIFY OTP REQUEST JSON]:");
                Debug.WriteLine(jsonRequest);

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Use API endpoint
                var endpoint = "/api/Auth/verify-otp";  // ✅ CORRECT API ENDPOINT
                var fullUrl = $"{BaseUrl}{endpoint}";
                Debug.WriteLine($"[VERIFY OTP] Endpoint: {fullUrl}");

                var stopwatch = Stopwatch.StartNew();
                Debug.WriteLine($"[VERIFY OTP] Sending request...");

                var response = await _httpClient.PostAsync(endpoint, content);
                stopwatch.Stop();

                Debug.WriteLine($"[VERIFY OTP] Request completed in {stopwatch.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[VERIFY OTP] Response Status: {response.StatusCode}");

                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[VERIFY OTP RESPONSE CONTENT]:");
                Debug.WriteLine(responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var loginResponse = JsonSerializer.Deserialize<LoginResponse>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    if (loginResponse?.Success == true && !string.IsNullOrEmpty(loginResponse.Token))
                    {
                        Debug.WriteLine($"[VERIFY OTP] OTP verified! Token received.");
                        Debug.WriteLine($"[VERIFY OTP] Token Length: {loginResponse.Token.Length}");
                        await SaveAuthDataAsync(loginResponse);
                    }
                    else
                    {
                        Debug.WriteLine($"[VERIFY OTP] Verification failed in response");
                    }

                    Debug.WriteLine($"[VERIFY OTP END] =========================================\n");
                    return loginResponse ?? new LoginResponse
                    {
                        Success = false,
                        Message = "Failed to parse response"
                    };
                }
                else
                {
                    Debug.WriteLine($"[VERIFY OTP] Failed with status: {response.StatusCode}");

                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<LoginResponse>(
                            responseContent,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                        );

                        Debug.WriteLine($"[VERIFY OTP END] =========================================\n");
                        return errorResponse ?? new LoginResponse
                        {
                            Success = false,
                            Message = "Invalid or expired OTP"
                        };
                    }
                    catch
                    {
                        Debug.WriteLine($"[VERIFY OTP END] =========================================\n");
                        return new LoginResponse
                        {
                            Success = false,
                            Message = "Invalid or expired OTP"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VERIFY OTP EXCEPTION]: {ex.Message}");
                Debug.WriteLine($"[VERIFY OTP END] =========================================\n");
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Error verifying OTP: {ex.Message}"
                };
            }
        }

        // ============================================================
        // RESEND OTP
        // ============================================================
        public async Task<ResendOtpResponse> ResendOtpAsync(string email)
        {
            Debug.WriteLine($"\n[RESEND OTP START] =========================================");
            Debug.WriteLine($"[RESEND OTP] Email: {email}");

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "Mobile");

                var request = new { email = email };

                var jsonRequest = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                Debug.WriteLine($"[RESEND OTP REQUEST JSON]:");
                Debug.WriteLine(jsonRequest);

                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Use API endpoint
                var endpoint = "/api/Auth/resend-otp";  // ✅ CORRECT API ENDPOINT
                var fullUrl = $"{BaseUrl}{endpoint}";
                Debug.WriteLine($"[RESEND OTP] Endpoint: {fullUrl}");

                var stopwatch = Stopwatch.StartNew();
                Debug.WriteLine($"[RESEND OTP] Sending request...");

                var response = await _httpClient.PostAsync(endpoint, content);
                stopwatch.Stop();

                Debug.WriteLine($"[RESEND OTP] Request completed in {stopwatch.ElapsedMilliseconds}ms");
                Debug.WriteLine($"[RESEND OTP] Response Status: {response.StatusCode}");

                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[RESEND OTP RESPONSE CONTENT]:");
                Debug.WriteLine(responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var resendResponse = JsonSerializer.Deserialize<ResendOtpResponse>(
                        responseContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );

                    Debug.WriteLine($"[RESEND OTP] Success: {resendResponse?.Success}");
                    Debug.WriteLine($"[RESEND OTP] OTP Token present: {!string.IsNullOrEmpty(resendResponse?.OtpToken)}");
                    Debug.WriteLine($"[RESEND OTP END] =========================================\n");

                    return resendResponse ?? new ResendOtpResponse
                    {
                        Success = false,
                        Message = "Failed to parse response"
                    };
                }
                else
                {
                    Debug.WriteLine($"[RESEND OTP] Failed with status: {response.StatusCode}");
                    Debug.WriteLine($"[RESEND OTP END] =========================================\n");
                    return new ResendOtpResponse
                    {
                        Success = false,
                        Message = "Failed to resend OTP"
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RESEND OTP EXCEPTION]: {ex.Message}");
                Debug.WriteLine($"[RESEND OTP END] =========================================\n");
                return new ResendOtpResponse
                {
                    Success = false,
                    Message = $"Error resending OTP: {ex.Message}"
                };
            }
        }

        // ============================================================
        // SAVE AUTH DATA
        // ============================================================
        private async Task SaveAuthDataAsync(LoginResponse response)
        {
            try
            {
                Debug.WriteLine($"[SAVE AUTH DATA] Saving auth data...");
                Debug.WriteLine($"[SAVE AUTH DATA] UserId: {response.UserId}");
                Debug.WriteLine($"[SAVE AUTH DATA] Email: {response.Email}");
                Debug.WriteLine($"[SAVE AUTH DATA] Token Length: {response.Token.Length}");

                await SecureStorage.SetAsync("auth_token", response.Token);
                await SecureStorage.SetAsync("user_id", response.UserId);
                await SecureStorage.SetAsync("user_email", response.Email);
                await SecureStorage.SetAsync("user_name", response.FullName ?? "");

                if (response.Roles != null && response.Roles.Any())
                {
                    await SecureStorage.SetAsync("user_roles", string.Join(",", response.Roles));
                    Debug.WriteLine($"[SAVE AUTH DATA] Roles: {string.Join(", ", response.Roles)}");
                }

                Debug.WriteLine("✅ Auth data saved successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ SaveAuthData error: {ex.Message}");
            }
        }

        // ============================================================
        // REST OF YOUR EXISTING METHODS (IsAuthenticated, GetToken, etc.)
        // ============================================================

        public async Task<bool> IsAuthenticatedAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                return !string.IsNullOrEmpty(token);
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetTokenAsync()
        {
            try
            {
                return await SecureStorage.GetAsync("auth_token") ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<UserInfo> GetCurrentUserAsync()
        {
            try
            {
                return new UserInfo
                {
                    UserId = await SecureStorage.GetAsync("user_id") ?? string.Empty,
                    Email = await SecureStorage.GetAsync("user_email") ?? string.Empty,
                    FullName = await SecureStorage.GetAsync("user_name") ?? string.Empty,
                    Roles = (await SecureStorage.GetAsync("user_roles"))?.Split(',').ToList() ?? new List<string>()
                };
            }
            catch
            {
                return new UserInfo();
            }
        }

        public async Task LogoutAsync()
        {
            SecureStorage.Remove("auth_token");
            SecureStorage.Remove("user_id");
            SecureStorage.Remove("user_email");
            SecureStorage.Remove("user_name");
            SecureStorage.Remove("user_roles");
            Debug.WriteLine("✅ Logged out successfully");
        }

        public async Task<T> GetAuthenticatedAsync<T>(string endpoint)
        {
            var token = await GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                throw new UnauthorizedAccessException("No authentication token found");
            }

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

        public async Task<TResponse> PostAuthenticatedAsync<TRequest, TResponse>(string endpoint, TRequest data)
        {
            var token = await GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                throw new UnauthorizedAccessException("No authentication token found");
            }

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

        public async Task<bool> TestConnectionAsync()
        {
            Debug.WriteLine($"[TEST CONNECTION] Testing connection to: {BaseUrl}/api/Auth/test");

            try
            {
                var response = await _httpClient.GetAsync("/api/Auth/test");
                Debug.WriteLine($"[TEST CONNECTION] Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[TEST CONNECTION] Success! Response: {content}");
                }

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TEST CONNECTION] Exception: {ex.Message}");
                return false;
            }
        }
    }

    // ============================================================
    // MODELS
    // ============================================================
    public class LoginRequest
    {
        public string EmailOrPhone { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UsePassword { get; set; } = false;
        public bool RememberMe { get; set; } = false;
    }

    public class VerifyOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }

    public class ResendOtpResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string OtpToken { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();

        // OTP-related fields
        public bool RequiresOtp { get; set; }
        public string OtpToken { get; set; } = string.Empty;
        public bool HasPassword { get; set; }
    }

    public class UserInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new List<string>();
    }
}