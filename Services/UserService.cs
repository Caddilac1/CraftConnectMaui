using System;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    /// <summary>
    /// UserService — profile loading strategy:
    ///
    /// 1. GET api/profilesapi/MyProfile  (single round-trip, returns both
    ///    UserProfile + ArtisanProfile in one response — fastest path).
    ///
    /// 2. Role is resolved first from JWT claims so we know how to interpret
    ///    the response before touching the network.
    ///
    /// 3. If the user has an ArtisanProfile in the response we always build
    ///    an ArtisanUser regardless of role claim, so a Customer who is also
    ///    an artisan gets the full artisan view.
    ///
    /// Security: JWT validated by the server. Bearer token attached on every
    /// request. Certificate bypass is DEBUG-only and stripped in Release.
    /// </summary>
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
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) => true;
#endif
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(config.BaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        // ── JWT claims ────────────────────────────────────────────────

        private async Task<(string userId, string email, string phone, string role)>
            GetUserClaimsFromTokenAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                    return (string.Empty, string.Empty, string.Empty, string.Empty);

                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                var userId = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value
                    ?? string.Empty;

                var email = jwt.Claims.FirstOrDefault(c =>
                    c.Type == JwtRegisteredClaimNames.Email || c.Type == "email")?.Value
                    ?? string.Empty;

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

        // ── IUserService ──────────────────────────────────────────────

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

        public async Task<UserProfile> LoadUserProfileAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return null;

                SetBearerToken(token);

                var (_, _, _, role) = await GetUserClaimsFromTokenAsync();

                Debug.WriteLine($"[USER SERVICE] Loading profile via /MyProfile (role from token: {role})");

                var response = await _httpClient.GetAsync("api/profilesapi/MyProfile");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    throw new UnauthorizedAccessException("Token rejected by server.");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[USER SERVICE] Profile load failed: {response.StatusCode}");
                    return null;
                }

                var envelope = await response.Content
                    .ReadFromJsonAsync<ApiResponse<MobileProfileDetailsDto>>();

                if (envelope?.Data == null)
                {
                    Debug.WriteLine("[USER SERVICE] Envelope parsed but data was null.");
                    return null;
                }

                var data = envelope.Data;

                // If artisan profile exists, build ArtisanUser regardless of role claim.
                // This handles the case of a user who holds multiple roles.
                if (data.ArtisanProfile != null)
                {
                    var ap = data.ArtisanProfile;
                    var up = data.UserProfile;

                    _cachedUser = new ArtisanUser
                    {
                        // ── From ArtisanProfile ───────────────────────
                        BusinessName = ap.BusinessName,
                        Slug = ap.Slug,
                        Specialization = ap.Specialization,
                        ArtisanSpeciality = ap.ArtisanSpeciality,
                        ExperienceLevel = ap.ExperienceLevel,
                        YearsOfExperience = ap.YearsOfExperience,
                        AverageRating = ap.AverageRating,
                        TotalReviews = ap.TotalReviews,
                        CompletedProjects = ap.CompletedProjects,
                        AvailabilityStatus = ap.AvailabilityStatus,
                        HourlyRate = ap.HourlyRate,
                        ServiceRadius = ap.ServiceRadius.HasValue
                                                ? (double?)ap.ServiceRadius.Value : null,
                        About = ap.About,
                        ProfessionalBio = ap.ProfessionalBio,
                        ServicesOffered = ap.ServicesOffered,
                        BusinessAddress = ap.BusinessAddress,
                        LicenseNumber = ap.LicenseNumber,
                        Certification = ap.Certification,
                        BusinessRegistration = ap.BusinessRegistration,
                        TaxId = ap.TaxId,
                        InsuranceDetails = ap.InsuranceDetails,
                        IsVerified = false, // artisan/me endpoint has IsVerified; MyProfile DTO doesn't expose it — extend if needed
                        CreatedAt = ap.CreatedAt,
                        UpdatedAt = ap.UpdatedAt,

                        // ── From UserProfile ──────────────────────────
                        FullName = up?.FullName ?? data.IdentityUserId,
                        Email = data.Email,
                        PhoneNumber = data.PhoneNumber,
                        ProfilePicture = up?.ProfilePictureUrl,
                        Bio = up?.Bio,
                        Address = up?.Address,
                        PostalCode = up?.PostalCode,
                        City = up?.City,
                        State = up?.State,
                        Country = up?.Country,
                        PreferredLanguage = up?.PreferredLanguage,
                        Timezone = up?.Timezone,
                        DateJoined = up?.CreatedDate,
                        Role = role  // preserve original role claim
                    };
                }
                else
                {
                    // Customer / Staff / Admin — use UserProfile only
                    var up = data.UserProfile;

                    _cachedUser = new UserProfile
                    {
                        Id = data.IdentityUserId,
                        FullName = up?.FullName,
                        Email = data.Email,
                        PhoneNumber = data.PhoneNumber,
                        ProfilePicture = up?.ProfilePictureUrl,
                        Bio = up?.Bio,
                        Address = up?.Address,
                        AddressLine2 = up?.AddressLine2,
                        PostalCode = up?.PostalCode,
                        City = up?.City,
                        State = up?.State,
                        Country = up?.Country,
                        PreferredLanguage = up?.PreferredLanguage,
                        Timezone = up?.Timezone,
                        DateJoined = up?.CreatedDate,
                        Role = role
                    };
                }

                Debug.WriteLine(
                    $"[USER SERVICE] Loaded: {_cachedUser.FullName} | {_cachedUser.Email} | {_cachedUser.Role}");

                return _cachedUser;
            }
            catch (UnauthorizedAccessException)
            {
                throw;  // re-throw so the page can redirect to login
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

                var response = await _httpClient.PutAsJsonAsync(
                    "api/profilesapi/customer/me", payload);

                if (response.IsSuccessStatusCode) { _cachedUser = user; return true; }

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
                var response = await _httpClient.PutAsJsonAsync(
                    "api/User/update-email", new { Email = newEmail });
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
                var response = await _httpClient.PutAsJsonAsync(
                    "api/User/update-phone", new { PhoneNumber = phoneNumber });
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

        public async Task<bool> UpdateNotificationPreferenceAsync(bool enabled)
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return false;
                SetBearerToken(token);
                var response = await _httpClient.PutAsJsonAsync(
                    "api/User/update-notification-preference",
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
                var response = await _httpClient.PutAsJsonAsync(
                    "api/User/update-notification-preference",
                    new { EmailNotifications = enabled });
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
                if (response.IsSuccessStatusCode) { await LogoutAsync(); return true; }
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

        private void SetBearerToken(string token) =>
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // ── Private DTOs — mirror exact JSON from /MyProfile ─────────

        private class ApiResponse<T>
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public T Data { get; set; }
        }

        private class MobileProfileDetailsDto
        {
            public string IdentityUserId { get; set; }
            public string Email { get; set; }
            public string PhoneNumber { get; set; }
            public MobileUserProfileDto UserProfile { get; set; }
            public MobileArtisanProfileDto ArtisanProfile { get; set; }
        }

        private class MobileUserProfileDto
        {
            public string FullName { get; set; }
            public string Bio { get; set; }
            public string Address { get; set; }
            public string AddressLine2 { get; set; }
            public string City { get; set; }
            public string State { get; set; }
            public string Country { get; set; }
            public string PostalCode { get; set; }
            public string ProfilePictureUrl { get; set; }
            public string PreferredLanguage { get; set; }
            public string Timezone { get; set; }
            public DateTime? CreatedDate { get; set; }
            public DateTime? ModifiedDate { get; set; }
            public bool IsActive { get; set; }
        }

        private class MobileArtisanProfileDto
        {
            public string Id { get; set; }
            public string BusinessName { get; set; }
            public string Slug { get; set; }
            public string Specialization { get; set; }
            public string ArtisanSpeciality { get; set; }
            public string ExperienceLevel { get; set; }
            public int YearsOfExperience { get; set; }
            public decimal AverageRating { get; set; }
            public int TotalReviews { get; set; }
            public int CompletedProjects { get; set; }
            public string AvailabilityStatus { get; set; }
            public decimal? HourlyRate { get; set; }
            public int? ServiceRadius { get; set; }
            public string About { get; set; }
            public string ProfessionalBio { get; set; }
            public string ServicesOffered { get; set; }
            public string BusinessAddress { get; set; }
            public string LicenseNumber { get; set; }
            public string Certification { get; set; }
            public string BusinessRegistration { get; set; }
            public string TaxId { get; set; }
            public string InsuranceDetails { get; set; }
            public DateTime? CreatedAt { get; set; }
            public DateTime? UpdatedAt { get; set; }
        }

        private class HasProfileResult
        {
            public bool HasProfile { get; set; }
            public string UserId { get; set; }
        }
    }
}