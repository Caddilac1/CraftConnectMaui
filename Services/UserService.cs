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

        // ── JWT claims ────────────────────────────────────────────────

        private async Task<(string userId, string email, string phone, string role)> GetUserClaimsFromTokenAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return (string.Empty, string.Empty, string.Empty, string.Empty);

                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                var userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value ?? string.Empty;

                var email = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Email || c.Type == "email")?.Value ?? string.Empty;

                var phone = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "phone")?.Value ?? string.Empty;

                var role = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "role" ||
                    c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    ?.Value ?? "Customer";

                return (userId, email, phone, role);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error reading token claims: {ex.Message}");
                return (string.Empty, string.Empty, string.Empty, string.Empty);
            }
        }

        // ── IUserService implementation ───────────────────────────────

        public UserProfile GetCurrentUser() => _cachedUser;

        public async Task<string> GetCurrentUserIdAsync()
        {
            var (userId, _, _, _) = await GetUserClaimsFromTokenAsync();
            return userId;
        }

        public async Task<string> GetCurrentUserNameAsync()
        {
            var (_, email, phone, _) = await GetUserClaimsFromTokenAsync();
            return !string.IsNullOrEmpty(email) ? email : phone;
        }

        public async Task<string> GetCurrentUserProfileImageAsync()
            => _cachedUser?.ProfileImageUrl ?? string.Empty;

        public async Task<bool> HasUnreadUpdatesAsync() => false;
        public async Task<bool> HasMissedCallsAsync() => false;

        // ── Profile loading ───────────────────────────────────────────

        /// <summary>
        /// Loads the user profile from the correct endpoint based on role.
        ///
        /// The API always wraps responses: { success, message, data: { ... } }
        /// We unwrap the envelope then map into the mobile UserProfile/ArtisanUser model.
        ///
        ///   Customer → GET api/profilesapi/customer/me  → data is flat UserDto
        ///   Staff    → GET api/profilesapi/staff/me     → data is StaffProfileViewModel
        ///   Artisan  → GET api/profilesapi/artisan/me   → data is ArtisanProfileDto
        ///                                                  user fields nested under data.user
        /// </summary>
        public async Task<UserProfile> LoadUserProfileAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return null;

                SetBearerToken(token);

                var (_, _, _, role) = await GetUserClaimsFromTokenAsync();

                // ── Pick endpoint ─────────────────────────────────────
                string endpoint;
                if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase) ||
                    role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    endpoint = "api/profilesapi/staff/me";
                else if (role.Equals("Artisan", StringComparison.OrdinalIgnoreCase))
                    endpoint = "api/profilesapi/artisan/me";
                else
                    endpoint = "api/profilesapi/customer/me";

                Debug.WriteLine($"[USER SERVICE] Loading profile from: {endpoint} (role: {role})");

                var response = await _httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[USER SERVICE] Profile load failed: {response.StatusCode}");
                    return null;
                }

                // ── Unwrap envelope, map to mobile model ──────────────

                if (role.Equals("Artisan", StringComparison.OrdinalIgnoreCase))
                {
                    // Artisan response: top-level artisan fields + nested "user" object
                    var envelope = await response.Content
                        .ReadFromJsonAsync<ApiResponse<ArtisanProfileApiDto>>();

                    if (envelope?.Data != null)
                    {
                        var api = envelope.Data;
                        _cachedUser = new ArtisanUser
                        {
                            // artisan-level fields
                            BusinessName = api.BusinessName,
                            Specialization = api.Specialization,
                            ExperienceLevel = api.ExperienceLevel,
                            YearsOfExperience = api.YearsOfExperience,
                            AverageRating = api.AverageRating,
                            TotalReviews = api.TotalReviews,
                            CompletedProjects = api.CompletedProjects,
                            AvailabilityStatus = api.AvailabilityStatus,
                            HourlyRate = api.HourlyRate,
                            About = api.About,
                            ProfessionalBio = api.ProfessionalBio,
                            BusinessAddress = api.BusinessAddress,
                            IsVerified = api.IsVerified,
                            CreatedAt = api.CreatedAt,
                            UpdatedAt = api.UpdatedAt,

                            // user-level fields from nested "user" object
                            Id = api.User?.Id,
                            FullName = api.User?.FullName,
                            Email = api.User?.Email,
                            PhoneNumber = api.User?.PhoneNumber,
                            ProfilePicture = api.User?.ProfilePicture,
                            City = api.User?.City,
                            State = api.User?.State,
                            Country = api.User?.Country,
                            Bio = api.User?.Bio,
                            Address = api.User?.Address,
                            PostalCode = api.User?.PostalCode,
                            DateJoined = api.User?.DateJoined,
                            Role = "Artisan"
                        };
                    }
                }
                else if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase) ||
                         role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    // Staff response: FirstName + LastName separate, image is CurrentProfileImageUrl
                    var envelope = await response.Content
                        .ReadFromJsonAsync<ApiResponse<StaffProfileApiDto>>();

                    if (envelope?.Data != null)
                    {
                        var s = envelope.Data;
                        _cachedUser = new UserProfile
                        {
                            Id = s.StaffId.ToString(),
                            FullName = $"{s.FirstName} {s.LastName}".Trim(),
                            Email = s.Email,
                            PhoneNumber = s.Phone,
                            ProfilePicture = s.CurrentProfileImageUrl,
                            Address = s.Address,
                            Role = role
                        };
                    }
                }
                else
                {
                    // Customer response: flat UserDto.
                    // "profilePicture" maps to ProfilePicture via [JsonPropertyName] on the model.
                    var envelope = await response.Content
                        .ReadFromJsonAsync<ApiResponse<UserProfile>>();

                    if (envelope?.Data != null)
                    {
                        envelope.Data.Role = "Customer";
                        _cachedUser = envelope.Data;
                    }
                }

                if (_cachedUser != null)
                    Debug.WriteLine(
                        $"[USER SERVICE] Loaded: {_cachedUser.FullName} | {_cachedUser.Email} | {_cachedUser.Role}");
                else
                    Debug.WriteLine("[USER SERVICE] Envelope parsed but data was null — check JSON shape.");

                return _cachedUser;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error loading user profile: {ex.Message}");
                return null;
            }
        }

        // ── Artisan profile existence check ───────────────────────────

        public async Task<bool> HasArtisanProfileAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.GetAsync("api/profilesapi/artisan/me/exists");

                if (!response.IsSuccessStatusCode) return false;

                var result = await response.Content
                    .ReadFromJsonAsync<ApiResponse<HasProfileResult>>();

                return result?.Data?.HasProfile ?? false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error checking artisan profile: {ex.Message}");
                return false;
            }
        }

        // ── Profile update ────────────────────────────────────────────

        public async Task<bool> UpdateUserAsync(UserProfile user)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);

                // Send only the fields the PUT customer/me endpoint accepts
                var payload = new
                {
                    fullName = user.FullName,
                    bio = user.Bio,
                    address = user.Address,
                    city = user.City,
                    state = user.State,
                    country = user.Country,
                    postalCode = user.PostalCode
                };

                var response = await _httpClient.PutAsJsonAsync("api/profilesapi/customer/me", payload);

                if (response.IsSuccessStatusCode)
                {
                    _cachedUser = user;
                    return true;
                }

                Debug.WriteLine($"[USER SERVICE] UpdateUserAsync failed: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error updating user: {ex.Message}");
                return false;
            }
        }

        // ── Identity field updates ────────────────────────────────────

        public async Task<bool> UpdateEmailAsync(string newEmail)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PutAsJsonAsync("api/User/update-email", new { Email = newEmail });

                if (response.IsSuccessStatusCode)
                {
                    if (_cachedUser != null) _cachedUser.Email = newEmail;
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
                var response = await _httpClient.PutAsJsonAsync("api/User/update-phone", new { PhoneNumber = phoneNumber });

                if (response.IsSuccessStatusCode)
                {
                    if (_cachedUser != null) _cachedUser.PhoneNumber = phoneNumber;
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

        // ── Notification preferences ──────────────────────────────────

        public async Task<bool> UpdateNotificationPreferenceAsync(bool enabled)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PutAsJsonAsync(
                    "api/User/update-notification-preference", new { PushNotifications = enabled });

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
                var response = await _httpClient.PutAsJsonAsync(
                    "api/User/update-notification-preference", new { EmailNotifications = enabled });

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER SERVICE] Error updating email notification preference: {ex.Message}");
                return false;
            }
        }

        // ── Account deletion & logout ─────────────────────────────────

        public async Task<bool> DeleteAccountAsync(string password)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;

                SetBearerToken(token);
                var response = await _httpClient.PostAsJsonAsync(
                    "api/User/delete-account", new { Password = password });

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

        // ── Helper ────────────────────────────────────────────────────

        private void SetBearerToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        // ── Private API shape DTOs ────────────────────────────────────
        // Mirror the exact JSON the API sends for each endpoint.
        // Kept private — mobile domain models (UserProfile, ArtisanUser)
        // are populated manually from these after deserialization.

        private class ApiResponse<T>
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public T Data { get; set; }
        }

        /// <summary>Matches ArtisanProfileDto from GET api/profilesapi/artisan/me</summary>
        private class ArtisanProfileApiDto
        {
            public string Id { get; set; }
            public string BusinessName { get; set; }
            public string Slug { get; set; }
            public string Specialization { get; set; }
            public string ExperienceLevel { get; set; }
            public int YearsOfExperience { get; set; }
            public decimal AverageRating { get; set; }
            public int TotalReviews { get; set; }
            public int CompletedProjects { get; set; }
            public string AvailabilityStatus { get; set; }
            public decimal? HourlyRate { get; set; }
            public string About { get; set; }
            public string ProfessionalBio { get; set; }
            public string BusinessAddress { get; set; }
            public bool IsVerified { get; set; }
            public DateTime? CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
            public UserApiDto User { get; set; }
        }

        /// <summary>Nested user object inside ArtisanProfileDto</summary>
        private class UserApiDto
        {
            public string Id { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public string ProfilePicture { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Country { get; set; }
            public string Bio { get; set; }
            public string Address { get; set; }
            public string PostalCode { get; set; }
            public DateTime? DateJoined { get; set; }
        }

        /// <summary>Matches StaffProfileViewModel from GET api/profilesapi/staff/me</summary>
        private class StaffProfileApiDto
        {
            public int StaffId { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public string CurrentProfileImageUrl { get; set; }
            public string StaffTypeName { get; set; }
            public string UserName { get; set; }
        }

        private class HasProfileResult
        {
            public bool HasProfile { get; set; }
            public string UserId { get; set; }
        }
    }
}