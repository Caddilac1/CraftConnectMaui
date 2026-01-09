using System;
using System.Diagnostics;
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

        public UserService(AuthService authService)
        {
            _authService = authService;

            var handler = new HttpClientHandler();
#if DEBUG
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
#endif
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://192.168.188.112:7023"),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public UserProfile GetCurrentUser()
        {
            return _cachedUser;
        }

        public async Task<string> GetCurrentUserIdAsync()
        {
            try
            {
                return await SecureStorage.GetAsync("user_id") ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<string> GetCurrentUserNameAsync()
        {
            try
            {
                return await SecureStorage.GetAsync("user_name") ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<string> GetCurrentUserProfileImageAsync()
        {
            // Return cached profile image or fetch from API
            return _cachedUser?.ProfileImageUrl ?? string.Empty;
        }

        public async Task<bool> HasUnreadUpdatesAsync()
        {
            // TODO: Implement with real API call
            return false;
        }

        public async Task<bool> HasMissedCallsAsync()
        {
            // TODO: Implement with real API call
            return false;
        }

        public async Task<bool> UpdateEmailAsync(string newEmail)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PutAsJsonAsync("/api/User/update-email", new { Email = newEmail });

                if (response.IsSuccessStatusCode)
                {
                    if (_cachedUser != null)
                        _cachedUser.Email = newEmail;

                    await SecureStorage.SetAsync("user_email", newEmail);
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
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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
                if (string.IsNullOrEmpty(token))
                    return false;

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

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
            // Additional cleanup if needed
        }

        // Helper method to load user profile from API
        public async Task<UserProfile> LoadUserProfileAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return null;

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync("api/ProfilesApi/me");

                if (response.IsSuccessStatusCode)
                {
                    var userInfo = await _authService.GetCurrentUserAsync();
                    var primaryRole = userInfo?.Roles?.FirstOrDefault() ?? "Customer";

                    // Check if user is an artisan and deserialize accordingly
                    if (primaryRole.Equals("Artisan", StringComparison.OrdinalIgnoreCase))
                    {
                        _cachedUser = await response.Content.ReadFromJsonAsync<ArtisanUser>();
                    }
                    else
                    {
                        _cachedUser = await response.Content.ReadFromJsonAsync<UserProfile>();
                    }

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
    }
}