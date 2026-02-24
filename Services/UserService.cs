using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public class UserService : IUserService
    {
        private readonly AuthService _authService;
        private readonly HttpClient _httpClient;
        private UserProfile _cachedUser;

        public UserService(AuthService authService, ApiConfig config)
        {
            _authService = authService;

            var handler = new HttpClientHandler();
#if DEBUG
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(config.BaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        // ── Get user info from JWT token ──────────────────────────────

        private async Task<(string userId, string email, string phone)> GetUserClaimsFromTokenAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return (string.Empty, string.Empty, string.Empty);

                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                var userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value ?? string.Empty;

                var email = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Email || c.Type == "email")?.Value ?? string.Empty;

                var phone = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "phone")?.Value ?? string.Empty;

                return (userId, email, phone);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error reading token claims: {ex.Message}");
                return (string.Empty, string.Empty, string.Empty);
            }
        }

        // ── IUserService implementation ───────────────────────────────

        public UserProfile GetCurrentUser() => _cachedUser;

        public async Task<string> GetCurrentUserIdAsync()
        {
            var (userId, _, _) = await GetUserClaimsFromTokenAsync();
            return userId;
        }

        public async Task<string> GetCurrentUserNameAsync()
        {
            var (_, email, phone) = await GetUserClaimsFromTokenAsync();
            return !string.IsNullOrEmpty(email) ? email : phone;
        }

        public async Task<string> GetCurrentUserProfileImageAsync()
            => _cachedUser?.ProfileImageUrl ?? string.Empty;

        public async Task<bool> HasUnreadUpdatesAsync() => false;

        public async Task<bool> HasMissedCallsAsync() => false;

        // ── API calls ─────────────────────────────────────────────────

        public async Task<bool> UpdateEmailAsync(string newEmail)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PutAsJsonAsync("/api/User/update-email", new { Email = newEmail });

                if (response.IsSuccessStatusCode)
                {
                    if (_cachedUser != null)
                        _cachedUser.Email = newEmail;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error updating email: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdatePhoneNumberAsync(string phoneNumber)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PutAsJsonAsync("/api/User/update-phone", new { PhoneNumber = phoneNumber });

                if (response.IsSuccessStatusCode)
                {
                    if (_cachedUser != null)
                        _cachedUser.PhoneNumber = phoneNumber;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error updating phone: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(UserProfile user)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PutAsJsonAsync("/api/User/update-profile", user);

                if (response.IsSuccessStatusCode)
                {
                    _cachedUser = user;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error updating user: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateNotificationPreferenceAsync(bool enabled)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PutAsJsonAsync("/api/User/update-notification-preference",
                    new { PushNotifications = enabled });

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error updating notification preference: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateEmailNotificationPreferenceAsync(bool enabled)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PutAsJsonAsync("/api/User/update-notification-preference",
                    new { EmailNotifications = enabled });

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error updating email notification preference: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteAccountAsync(string password)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PostAsJsonAsync("/api/User/delete-account",
                    new { Password = password });

                if (response.IsSuccessStatusCode)
                {
                    await LogoutAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error deleting account: {ex.Message}");
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            _cachedUser = null;
            await _authService.LogoutAsync();
        }

        public async Task<UserProfile> LoadUserProfileAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return null;

                SetBearerToken(token);
                var response = await _httpClient.GetAsync("api/ProfilesApi/me");

                if (response.IsSuccessStatusCode)
                {
                    // Determine role from JWT claims
                    var (_, _, _) = await GetUserClaimsFromTokenAsync();
                    var jwt = new JwtSecurityTokenHandler()
                        .ReadJwtToken(token);

                    var role = jwt.Claims
                        .FirstOrDefault(c => c.Type == "role" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                        ?.Value ?? "Customer";

                    if (role.Equals("Artisan", StringComparison.OrdinalIgnoreCase))
                        _cachedUser = await response.Content.ReadFromJsonAsync<ArtisanUser>();
                    else
                        _cachedUser = await response.Content.ReadFromJsonAsync<UserProfile>();

                    return _cachedUser;
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error loading user profile: {ex.Message}");
                return null;
            }
        }

        // ── Helper ────────────────────────────────────────────────────

        private void SetBearerToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
    }
}