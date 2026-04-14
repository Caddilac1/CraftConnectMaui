using CraftConnect_Mobile_App.Models;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    // CONSTANTS
    // ═══════════════════════════════════════════════════════════════════════

    internal static class ApiRoutes
    {
        public const string Profiles = "/api/ProfilesApi";
        public const string MyProfile = "/api/ProfilesApi/MyProfile";
        public const string ProfileById = "/api/ProfilesApi/{0}";
        public const string HasArtisanProfile = "/api/ProfilesApi/HasArtisanProfile";
        public const string DeleteArtisan = "/api/ProfilesApi/ArtisanProfile";
        public const string DeleteUser = "/api/ProfilesApi/UserProfile";

        public const string TrustScore = "/api/trust-score/{0}";
        public const string TrustScoreHistory = "/api/trust-score/{0}/history";
        public const string WorkReferrals = "/api/trust-score/{0}/referrals/work";
        public const string VendorReferrals = "/api/trust-score/{0}/referrals/vendor";
        public const string ColleagueReferrals = "/api/trust-score/{0}/referrals/colleague";
    }

    internal static class CacheKeys
    {
        public static string TrustScore(int id) => $"ts:{id}";
        public static string TrustHistory(int id) => $"th:{id}";
        public static string WorkReferrals(int id) => $"wr:{id}";
        public static string VendorReferrals(int id) => $"vr:{id}";
        public static string ColleagueReferrals(int id) => $"cr:{id}";
        public static string AllReferrals(int id) => $"ar:{id}";
        public static string MyProfile => "my_profile";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE API RESPONSE MODELS — match backend JSON shapes exactly
    // ═══════════════════════════════════════════════════════════════════════

    internal sealed class ProfilesResponse
    {
        public List<ArtisanProfileDto> Profiles { get; set; }
    }

    internal sealed class ProfileDetailsResponse
    {
        public string IdentityUserId { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public UserProfileDto UserProfile { get; set; }
        public ArtisanProfileDto ArtisanProfile { get; set; }
    }

    internal sealed class ArtisanProfileDto
    {
        public string Id { get; set; }
        public string BusinessName { get; set; }
        public string Specialization { get; set; }
        public int YearsOfExperience { get; set; }
        public string ExperienceLevel { get; set; }
        public string LicenseNumber { get; set; }
        public string Certification { get; set; }
        public string BusinessRegistration { get; set; }
        public string TaxId { get; set; }
        public string InsuranceDetails { get; set; }
        public string AvailabilityStatus { get; set; }
        public decimal? HourlyRate { get; set; }
        public int? ServiceRadius { get; set; }
        public string About { get; set; }
        public string ServicesOffered { get; set; }
        public string ArtisanSpeciality { get; set; }
        public string ProfessionalBio { get; set; }
        public string BusinessAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Slug { get; set; }
    }

    internal sealed class UserProfileDto
    {
        public string FullName { get; set; }
        public string Bio { get; set; }
        public string Address { get; set; }
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

    internal sealed class HasProfileResponse
    {
        public bool HasProfile { get; set; }
        public string Error { get; set; }
    }

    internal sealed class ApiResponse
    {
        public string Message { get; set; }
        public string Error { get; set; }
    }

    // ─── Trust-score DTOs (mirror backend shapes) ───────────────────────

    internal sealed class ApiResult<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
    }

    internal sealed class TrustScoreResponseDto
    {
        public int CompanyId { get; set; }
        public decimal Score { get; set; }
        public string Band { get; set; }   // e.g. "Gold", "Silver"
        public DateTime CalculatedAt { get; set; }
        public Dictionary<string, decimal> Breakdown { get; set; }
    }

    internal sealed class TrustScoreHistoryItemDto
    {
        public decimal Score { get; set; }
        public string Band { get; set; }
        public DateTime RecordedAt { get; set; }
        public string ChangeReason { get; set; }
    }

    internal sealed class WorkReferralDto
    {
        public int Id { get; set; }
        public string ReferrerName { get; set; }
        public string ProjectTitle { get; set; }
        public decimal Rating { get; set; }
        public string Comment { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    internal sealed class VendorReferralDto
    {
        public int Id { get; set; }
        public string VendorName { get; set; }
        public string Category { get; set; }
        public decimal Rating { get; set; }
        public string Comment { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    internal sealed class ColleagueReferralDto
    {
        public int Id { get; set; }
        public string ColleagueName { get; set; }
        public string Relationship { get; set; }
        public decimal Rating { get; set; }
        public string Comment { get; set; }
        public DateTime SubmittedAt { get; set; }
    }

    // ─── Request models ─────────────────────────────────────────────────

    internal sealed class CreateArtisanProfileRequest
    {
        public string BusinessName { get; set; }
        public string Specialization { get; set; }
        public int YearsOfExperience { get; set; }
        public string ExperienceLevel { get; set; }
        public string LicenseNumber { get; set; }
        public string Certification { get; set; }
        public string BusinessRegistration { get; set; }
        public string TaxId { get; set; }
        public string InsuranceDetails { get; set; }
        public string AvailabilityStatus { get; set; }
        public decimal? HourlyRate { get; set; }
        public int? ServiceRadius { get; set; }
        public string About { get; set; }
        public string ServicesOffered { get; set; }
        public string ArtisanSpeciality { get; set; }
        public string ProfessionalBio { get; set; }
        public string BusinessAddress { get; set; }
    }

    internal sealed class UpdateProfileRequest
    {
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public UserProfileDto UserProfile { get; set; }
        public ArtisanProfileDto ArtisanProfile { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC MOBILE MODELS
    // ═══════════════════════════════════════════════════════════════════════

    public sealed class MobileArtisanProfile
    {
        public string Id { get; set; }
        public string BusinessName { get; set; }
        public string Specialization { get; set; }
        public int YearsOfExperience { get; set; }
        public string ExperienceLevel { get; set; }
        public string LicenseNumber { get; set; }
        public string Certification { get; set; }
        public string BusinessRegistration { get; set; }
        public string TaxId { get; set; }
        public string InsuranceDetails { get; set; }
        public string AvailabilityStatus { get; set; }
        public decimal? HourlyRate { get; set; }
        public int? ServiceRadius { get; set; }
        public string About { get; set; }
        public string ServicesOffered { get; set; }
        public string ArtisanSpeciality { get; set; }
        public string ProfessionalBio { get; set; }
        public string BusinessAddress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Slug { get; set; }

        // Computed display helpers
        public string DisplayName => string.IsNullOrEmpty(BusinessName) ? "Unnamed Business" : BusinessName;
        public string DisplayRate => HourlyRate.HasValue ? $"${HourlyRate:F2}/hr" : "Rate not set";
        public string DisplayExperience => $"{YearsOfExperience} years ({ExperienceLevel})";
        public bool IsAvailable => AvailabilityStatus == "Available";
    }

    public sealed class MobileUserProfile
    {
        public string FullName { get; set; }
        public string Bio { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }
        public string PostalCode { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string PreferredLanguage { get; set; }
        public string Timezone { get; set; }

        public string FullAddress => string.IsNullOrEmpty(Address)
            ? "Address not set"
            : $"{Address}, {City}, {State} {PostalCode}, {Country}";
        public string DisplayName => string.IsNullOrEmpty(FullName) ? "User" : FullName;
    }

    public sealed class MobileProfileDetails
    {
        public string IdentityUserId { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public MobileUserProfile UserProfile { get; set; }
        public MobileArtisanProfile ArtisanProfile { get; set; }

        public bool HasArtisanProfile => ArtisanProfile != null;
        public bool HasUserProfile => UserProfile != null;
        public string DisplayName => UserProfile?.DisplayName ?? Email ?? "User";
    }

    public sealed class CreateMobileArtisanProfile
    {
        public string BusinessName { get; set; }
        public string Specialization { get; set; }
        public int YearsOfExperience { get; set; } = 1;
        public string ExperienceLevel { get; set; } = "Beginner";
        public string LicenseNumber { get; set; }
        public string Certification { get; set; }
        public string BusinessRegistration { get; set; }
        public string TaxId { get; set; }
        public string InsuranceDetails { get; set; }
        public string AvailabilityStatus { get; set; } = "Available";
        public decimal? HourlyRate { get; set; }
        public int? ServiceRadius { get; set; }
        public string About { get; set; }
        public string ServicesOffered { get; set; } = "General Services";
        public string ArtisanSpeciality { get; set; }
        public string ProfessionalBio { get; set; }
        public string BusinessAddress { get; set; }
    }

    public sealed class UpdateMobileProfile
    {
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public MobileUserProfile UserProfile { get; set; }
        public MobileArtisanProfile ArtisanProfile { get; set; }
    }

    // ─── Trust-score public models ───────────────────────────────────────

    public sealed class MobileTrustScore
    {
        public int CompanyId { get; set; }
        public decimal Score { get; set; }
        public string Band { get; set; }
        public DateTime CalculatedAt { get; set; }
        public IReadOnlyDictionary<string, decimal> Breakdown { get; set; }

        // Display helpers
        public string DisplayScore => $"{Score:F1}";
        public string DisplayBand => string.IsNullOrEmpty(Band) ? "Unrated" : Band;
        public bool IsGold => Band?.Equals("Gold", StringComparison.OrdinalIgnoreCase) == true;
        public bool IsSilver => Band?.Equals("Silver", StringComparison.OrdinalIgnoreCase) == true;
    }

    public sealed class MobileTrustScoreHistoryItem
    {
        public decimal Score { get; set; }
        public string Band { get; set; }
        public DateTime RecordedAt { get; set; }
        public string ChangeReason { get; set; }
    }

    public sealed class MobileWorkReferral
    {
        public int Id { get; set; }
        public string ReferrerName { get; set; }
        public string ProjectTitle { get; set; }
        public decimal Rating { get; set; }
        public string Comment { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string DisplayRating => $"{Rating:F1} ★";
    }

    public sealed class MobileVendorReferral
    {
        public int Id { get; set; }
        public string VendorName { get; set; }
        public string Category { get; set; }
        public decimal Rating { get; set; }
        public string Comment { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string DisplayRating => $"{Rating:F1} ★";
    }

    public sealed class MobileColleagueReferral
    {
        public int Id { get; set; }
        public string ColleagueName { get; set; }
        public string Relationship { get; set; }
        public decimal Rating { get; set; }
        public string Comment { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string DisplayRating => $"{Rating:F1} ★";
    }

    /// <summary>
    /// Combines all referral types and the current trust score into one
    /// hydrated snapshot — fetched in parallel for maximum speed.
    /// </summary>
    public sealed class MobileTrustScoreSnapshot
    {
        public MobileTrustScore CurrentScore { get; set; }
        public IReadOnlyList<MobileWorkReferral> WorkReferrals { get; set; }
        public IReadOnlyList<MobileVendorReferral> VendorReferrals { get; set; }
        public IReadOnlyList<MobileColleagueReferral> ColleagueReferrals { get; set; }

        public int TotalReferrals =>
            (WorkReferrals?.Count ?? 0) +
            (VendorReferrals?.Count ?? 0) +
            (ColleagueReferrals?.Count ?? 0);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SERVICE INTERFACE
    // ═══════════════════════════════════════════════════════════════════════

    public interface IProfileApiService
    {
        // ── Profile ──────────────────────────────────────────────────────
        Task<List<MobileArtisanProfile>> GetAllProfilesAsync(CancellationToken ct = default);
        Task<MobileProfileDetails> GetMyProfileAsync(CancellationToken ct = default);
        Task<MobileArtisanProfile> GetProfileAsync(string id, CancellationToken ct = default);
        Task<MobileArtisanProfile> CreateArtisanProfileAsync(CreateMobileArtisanProfile profile, CancellationToken ct = default);
        Task<bool> UpdateProfileAsync(UpdateMobileProfile profile, CancellationToken ct = default);
        Task<bool> DeleteArtisanProfileAsync(CancellationToken ct = default);
        Task<bool> DeleteUserProfileAsync(CancellationToken ct = default);
        Task<bool> HasArtisanProfileAsync(CancellationToken ct = default);
        Task<int> GetTotalProfilesCountAsync(CancellationToken ct = default);

        // ── Trust Score ───────────────────────────────────────────────────
        Task<MobileTrustScore> GetTrustScoreAsync(int companyId, CancellationToken ct = default);
        Task<IReadOnlyList<MobileTrustScoreHistoryItem>> GetTrustScoreHistoryAsync(int companyId, int maxRecords = 24, CancellationToken ct = default);
        Task<IReadOnlyList<MobileWorkReferral>> GetWorkReferralsAsync(int companyId, int page = 1, int pageSize = 20, CancellationToken ct = default);
        Task<IReadOnlyList<MobileVendorReferral>> GetVendorReferralsAsync(int companyId, int page = 1, int pageSize = 20, CancellationToken ct = default);
        Task<IReadOnlyList<MobileColleagueReferral>> GetColleagueReferralsAsync(int companyId, int page = 1, int pageSize = 20, CancellationToken ct = default);

        /// <summary>Fetches score + all three referral types in parallel — one round-trip cost.</summary>
        Task<MobileTrustScoreSnapshot> GetTrustScoreSnapshotAsync(int companyId, int referralPageSize = 20, CancellationToken ct = default);

        // ── Diagnostics ───────────────────────────────────────────────────
        Task<bool> TestProfileApiAsync(CancellationToken ct = default);
        Task<bool> TestAuthAsync(CancellationToken ct = default);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════════════════

    public sealed class ProfileApiService : IProfileApiService, IDisposable
    {
        // ── Statics ──────────────────────────────────────────────────────

        // Single shared SocketsHttpHandler for connection pooling across the app lifetime.
        // Rotate DNS every 5 min to handle server-side IP changes.
        private static readonly SocketsHttpHandler _sharedHandler = new()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true,
            // TLS hardening: TLS 1.2 minimum, validate cert properly
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                                    | System.Security.Authentication.SslProtocols.Tls13,
                CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.Online
            },
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Brotli
        };

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        // Short-lived in-memory cache. Avoids redundant network calls for read-heavy
        // trust-score data that the backend itself caches for 15–60 s.
        private readonly IMemoryCache _cache;
        private readonly MemoryCacheEntryOptions _shortTtl = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15) };
        private readonly MemoryCacheEntryOptions _mediumTtl = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) };
        private readonly MemoryCacheEntryOptions _longTtl = new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60) };

        private readonly HttpClient _httpClient;

        // ── Constructor ──────────────────────────────────────────────────

        public ProfileApiService(ApiConfig config, IMemoryCache cache = null)
        {
            if (string.IsNullOrWhiteSpace(config?.BaseUrl))
                throw new ArgumentException("BaseUrl must not be empty.", nameof(config));

            _cache = cache ?? new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });

            _httpClient = new HttpClient(_sharedHandler, disposeHandler: false)
            {
                BaseAddress = new Uri(config.BaseUrl.TrimEnd('/')),
                Timeout = TimeSpan.FromSeconds(20)
            };

            // Prefer HTTP/2, accept Brotli/GZip for reduced payload size
            _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br, gzip");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            Log("Initialised", $"BaseUrl={config.BaseUrl}");
        }

        // ── Auth header (never log the raw token) ────────────────────────

        /// <summary>
        /// Attaches the Bearer token from SecureStorage.
        /// Validates the token is structurally a JWT (3 dot-separated Base64 segments)
        /// before setting the header, to avoid sending obviously corrupt credentials.
        /// </summary>
        private async Task<string> GetValidatedTokenAsync()
        {
            string token;
            try { token = await SecureStorage.GetAsync("auth_token"); }
            catch (Exception ex)
            {
                Log("GetToken", $"SecureStorage error: {ex.GetType().Name}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                Log("GetToken", "No token found");
                return null;
            }

            // Lightweight structural check — 3 Base64url segments
            if (token.Split('.').Length != 3)
            {
                Log("GetToken", "Token has unexpected structure — clearing");
                SecureStorage.Remove("auth_token");
                return null;
            }

            return token;
        }

        private async Task<bool> ApplyAuthHeaderAsync()
        {
            var token = await GetValidatedTokenAsync();
            if (token is null) return false;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        // ─────────────────────────────────────────────────────────────────
        // PROFILE METHODS
        // ─────────────────────────────────────────────────────────────────

        public async Task<List<MobileArtisanProfile>> GetAllProfilesAsync(CancellationToken ct = default)
        {
            await RequireAuthAsync(ct);
            var response = await SendAsync(HttpMethod.Get, ApiRoutes.Profiles, ct: ct);
            var dtos = await DeserializeAsync<List<ArtisanProfileDto>>(response, ct);
            return dtos?.Select(MapToMobileArtisanProfile).ToList() ?? new List<MobileArtisanProfile>();
        }

        public async Task<MobileProfileDetails> GetMyProfileAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKeys.MyProfile, out MobileProfileDetails cached))
                return cached;

            await RequireAuthAsync(ct);
            var response = await SendAsync(HttpMethod.Get, ApiRoutes.MyProfile, ct: ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new MobileProfileDetails();

            EnsureSuccess(response, nameof(GetMyProfileAsync));

            var result = await DeserializeAsync<ProfileDetailsResponse>(response, ct);
            if (result is null) return new MobileProfileDetails();

            var profile = new MobileProfileDetails
            {
                IdentityUserId = result.IdentityUserId,
                Email = result.Email,
                PhoneNumber = result.PhoneNumber,
                UserProfile = result.UserProfile != null ? MapToMobileUserProfile(result.UserProfile) : null,
                ArtisanProfile = result.ArtisanProfile != null ? MapToMobileArtisanProfile(result.ArtisanProfile) : null
            };

            _cache.Set(CacheKeys.MyProfile, profile, _longTtl);
            return profile;
        }

        public async Task<MobileArtisanProfile> GetProfileAsync(string id, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Profile ID required.", nameof(id));

            await RequireAuthAsync(ct);
            var response = await SendAsync(HttpMethod.Get, string.Format(ApiRoutes.ProfileById, Uri.EscapeDataString(id)), ct: ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            EnsureSuccess(response, nameof(GetProfileAsync));

            var dto = await DeserializeAsync<ArtisanProfileDto>(response, ct);
            return dto is null ? null : MapToMobileArtisanProfile(dto);
        }

        public async Task<MobileArtisanProfile> CreateArtisanProfileAsync(CreateMobileArtisanProfile profile, CancellationToken ct = default)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));

            await RequireAuthAsync(ct);

            var request = new CreateArtisanProfileRequest
            {
                BusinessName = profile.BusinessName,
                Specialization = profile.Specialization,
                YearsOfExperience = profile.YearsOfExperience,
                ExperienceLevel = profile.ExperienceLevel,
                LicenseNumber = profile.LicenseNumber,
                Certification = profile.Certification,
                BusinessRegistration = profile.BusinessRegistration,
                TaxId = profile.TaxId,
                InsuranceDetails = profile.InsuranceDetails,
                AvailabilityStatus = profile.AvailabilityStatus,
                HourlyRate = profile.HourlyRate,
                ServiceRadius = profile.ServiceRadius,
                About = profile.About,
                ServicesOffered = profile.ServicesOffered,
                ArtisanSpeciality = profile.ArtisanSpeciality,
                ProfessionalBio = profile.ProfessionalBio,
                BusinessAddress = profile.BusinessAddress
            };

            var response = await SendAsync(HttpMethod.Post, ApiRoutes.Profiles, body: request, ct: ct);

            if (response.StatusCode == HttpStatusCode.BadRequest) return null;
            EnsureSuccess(response, nameof(CreateArtisanProfileAsync));

            _cache.Remove(CacheKeys.MyProfile);
            var dto = await DeserializeAsync<ArtisanProfileDto>(response, ct);
            return dto is null ? null : MapToMobileArtisanProfile(dto);
        }

        public async Task<bool> UpdateProfileAsync(UpdateMobileProfile profile, CancellationToken ct = default)
        {
            if (profile is null) throw new ArgumentNullException(nameof(profile));

            await RequireAuthAsync(ct);

            var request = new UpdateProfileRequest
            {
                Email = profile.Email,
                PhoneNumber = profile.PhoneNumber,
                UserProfile = profile.UserProfile != null ? new UserProfileDto
                {
                    FullName = profile.UserProfile.FullName,
                    Bio = profile.UserProfile.Bio,
                    Address = profile.UserProfile.Address,
                    City = profile.UserProfile.City,
                    State = profile.UserProfile.State,
                    Country = profile.UserProfile.Country,
                    PostalCode = profile.UserProfile.PostalCode,
                    ProfilePictureUrl = profile.UserProfile.ProfilePictureUrl,
                    PreferredLanguage = profile.UserProfile.PreferredLanguage,
                    Timezone = profile.UserProfile.Timezone
                } : null,
                ArtisanProfile = profile.ArtisanProfile != null ? new ArtisanProfileDto
                {
                    BusinessName = profile.ArtisanProfile.BusinessName,
                    Specialization = profile.ArtisanProfile.Specialization,
                    YearsOfExperience = profile.ArtisanProfile.YearsOfExperience,
                    ExperienceLevel = profile.ArtisanProfile.ExperienceLevel,
                    LicenseNumber = profile.ArtisanProfile.LicenseNumber,
                    Certification = profile.ArtisanProfile.Certification,
                    BusinessRegistration = profile.ArtisanProfile.BusinessRegistration,
                    TaxId = profile.ArtisanProfile.TaxId,
                    InsuranceDetails = profile.ArtisanProfile.InsuranceDetails,
                    AvailabilityStatus = profile.ArtisanProfile.AvailabilityStatus,
                    HourlyRate = profile.ArtisanProfile.HourlyRate,
                    ServiceRadius = profile.ArtisanProfile.ServiceRadius,
                    About = profile.ArtisanProfile.About,
                    ServicesOffered = profile.ArtisanProfile.ServicesOffered,
                    ArtisanSpeciality = profile.ArtisanProfile.ArtisanSpeciality,
                    ProfessionalBio = profile.ArtisanProfile.ProfessionalBio,
                    BusinessAddress = profile.ArtisanProfile.BusinessAddress
                } : null
            };

            var response = await SendAsync(HttpMethod.Put, ApiRoutes.Profiles, body: request, ct: ct);
            EnsureSuccess(response, nameof(UpdateProfileAsync));

            _cache.Remove(CacheKeys.MyProfile);
            return true;
        }

        public async Task<bool> DeleteArtisanProfileAsync(CancellationToken ct = default)
        {
            await RequireAuthAsync(ct);
            var response = await SendAsync(HttpMethod.Delete, ApiRoutes.DeleteArtisan, ct: ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            EnsureSuccess(response, nameof(DeleteArtisanProfileAsync));

            _cache.Remove(CacheKeys.MyProfile);
            return true;
        }

        public async Task<bool> DeleteUserProfileAsync(CancellationToken ct = default)
        {
            await RequireAuthAsync(ct);
            var response = await SendAsync(HttpMethod.Delete, ApiRoutes.DeleteUser, ct: ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return false;
            EnsureSuccess(response, nameof(DeleteUserProfileAsync));

            _cache.Remove(CacheKeys.MyProfile);
            return true;
        }

        public async Task<bool> HasArtisanProfileAsync(CancellationToken ct = default)
        {
            await RequireAuthAsync(ct);
            var response = await SendAsync(HttpMethod.Get, ApiRoutes.HasArtisanProfile, ct: ct);
            if (!response.IsSuccessStatusCode) return false;

            var result = await DeserializeAsync<HasProfileResponse>(response, ct);
            return result?.HasProfile ?? false;
        }

        public async Task<int> GetTotalProfilesCountAsync(CancellationToken ct = default)
        {
            try { return (await GetAllProfilesAsync(ct))?.Count ?? 0; }
            catch { return 0; }
        }

        // ─────────────────────────────────────────────────────────────────
        // TRUST SCORE METHODS
        // ─────────────────────────────────────────────────────────────────

        public async Task<MobileTrustScore> GetTrustScoreAsync(int companyId, CancellationToken ct = default)
        {
            ValidateCompanyId(companyId);

            var key = CacheKeys.TrustScore(companyId);
            if (_cache.TryGetValue(key, out MobileTrustScore hit)) return hit;

            await RequireAuthAsync(ct);
            var url = string.Format(ApiRoutes.TrustScore, companyId);
            var response = await SendAsync(HttpMethod.Get, url, ct: ct);

            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new UnauthorizedAccessException("Access denied to this company's trust score.");
            EnsureSuccess(response, nameof(GetTrustScoreAsync));

            var envelope = await DeserializeAsync<ApiResult<TrustScoreResponseDto>>(response, ct);
            if (envelope?.Success != true || envelope.Data is null) return null;

            var result = MapToMobileTrustScore(envelope.Data);
            _cache.Set(key, result, _shortTtl);
            return result;
        }

        public async Task<IReadOnlyList<MobileTrustScoreHistoryItem>> GetTrustScoreHistoryAsync(
            int companyId, int maxRecords = 24, CancellationToken ct = default)
        {
            ValidateCompanyId(companyId);

            var key = CacheKeys.TrustHistory(companyId);
            if (_cache.TryGetValue(key, out IReadOnlyList<MobileTrustScoreHistoryItem> hit)) return hit;

            await RequireAuthAsync(ct);
            var url = $"{string.Format(ApiRoutes.TrustScoreHistory, companyId)}?maxRecords={maxRecords}";
            var response = await SendAsync(HttpMethod.Get, url, ct: ct);

            EnsureSuccessOrForbid(response, nameof(GetTrustScoreHistoryAsync));

            var envelope = await DeserializeAsync<ApiResult<IReadOnlyList<TrustScoreHistoryItemDto>>>(response, ct);
            var result = envelope?.Data?.Select(MapToMobileHistoryItem).ToList()
                           ?? new List<MobileTrustScoreHistoryItem>();

            _cache.Set(key, (IReadOnlyList<MobileTrustScoreHistoryItem>)result, _longTtl);
            return result;
        }

        public async Task<IReadOnlyList<MobileWorkReferral>> GetWorkReferralsAsync(
            int companyId, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            ValidateCompanyId(companyId);

            var key = CacheKeys.WorkReferrals(companyId);
            if (page == 1 && _cache.TryGetValue(key, out IReadOnlyList<MobileWorkReferral> hit)) return hit;

            await RequireAuthAsync(ct);
            var url = $"{string.Format(ApiRoutes.WorkReferrals, companyId)}?page={page}&pageSize={pageSize}";
            var response = await SendAsync(HttpMethod.Get, url, ct: ct);

            EnsureSuccessOrForbid(response, nameof(GetWorkReferralsAsync));

            var envelope = await DeserializeAsync<ApiResult<IReadOnlyList<WorkReferralDto>>>(response, ct);
            var result = envelope?.Data?.Select(MapToMobileWorkReferral).ToList()
                           ?? new List<MobileWorkReferral>();

            if (page == 1) _cache.Set(key, (IReadOnlyList<MobileWorkReferral>)result, _mediumTtl);
            return result;
        }

        public async Task<IReadOnlyList<MobileVendorReferral>> GetVendorReferralsAsync(
            int companyId, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            ValidateCompanyId(companyId);

            var key = CacheKeys.VendorReferrals(companyId);
            if (page == 1 && _cache.TryGetValue(key, out IReadOnlyList<MobileVendorReferral> hit)) return hit;

            await RequireAuthAsync(ct);
            var url = $"{string.Format(ApiRoutes.VendorReferrals, companyId)}?page={page}&pageSize={pageSize}";
            var response = await SendAsync(HttpMethod.Get, url, ct: ct);

            EnsureSuccessOrForbid(response, nameof(GetVendorReferralsAsync));

            var envelope = await DeserializeAsync<ApiResult<IReadOnlyList<VendorReferralDto>>>(response, ct);
            var result = envelope?.Data?.Select(MapToMobileVendorReferral).ToList()
                           ?? new List<MobileVendorReferral>();

            if (page == 1) _cache.Set(key, (IReadOnlyList<MobileVendorReferral>)result, _mediumTtl);
            return result;
        }

        public async Task<IReadOnlyList<MobileColleagueReferral>> GetColleagueReferralsAsync(
            int companyId, int page = 1, int pageSize = 20, CancellationToken ct = default)
        {
            ValidateCompanyId(companyId);

            var key = CacheKeys.ColleagueReferrals(companyId);
            if (page == 1 && _cache.TryGetValue(key, out IReadOnlyList<MobileColleagueReferral> hit)) return hit;

            await RequireAuthAsync(ct);
            var url = $"{string.Format(ApiRoutes.ColleagueReferrals, companyId)}?page={page}&pageSize={pageSize}";
            var response = await SendAsync(HttpMethod.Get, url, ct: ct);

            EnsureSuccessOrForbid(response, nameof(GetColleagueReferralsAsync));

            var envelope = await DeserializeAsync<ApiResult<IReadOnlyList<ColleagueReferralDto>>>(response, ct);
            var result = envelope?.Data?.Select(MapToMobileColleagueReferral).ToList()
                           ?? new List<MobileColleagueReferral>();

            if (page == 1) _cache.Set(key, (IReadOnlyList<MobileColleagueReferral>)result, _mediumTtl);
            return result;
        }

        /// <summary>
        /// Fires all five trust-score requests concurrently with Task.WhenAll.
        /// Total latency ≈ slowest single request rather than the sum of all five.
        /// </summary>
        public async Task<MobileTrustScoreSnapshot> GetTrustScoreSnapshotAsync(
            int companyId, int referralPageSize = 20, CancellationToken ct = default)
        {
            ValidateCompanyId(companyId);
            await RequireAuthAsync(ct);

            var (scoreTask, workTask, vendorTask, colleagueTask) = (
                GetTrustScoreAsync(companyId, ct),
                GetWorkReferralsAsync(companyId, pageSize: referralPageSize, ct: ct),
                GetVendorReferralsAsync(companyId, pageSize: referralPageSize, ct: ct),
                GetColleagueReferralsAsync(companyId, pageSize: referralPageSize, ct: ct)
            );

            await Task.WhenAll(scoreTask, workTask, vendorTask, colleagueTask);

            return new MobileTrustScoreSnapshot
            {
                CurrentScore = scoreTask.Result,
                WorkReferrals = workTask.Result,
                VendorReferrals = vendorTask.Result,
                ColleagueReferrals = colleagueTask.Result
            };
        }

        // ─────────────────────────────────────────────────────────────────
        // DIAGNOSTICS
        // ─────────────────────────────────────────────────────────────────

        public async Task<bool> TestProfileApiAsync(CancellationToken ct = default)
        {
            try
            {
                var response = await _httpClient.GetAsync(ApiRoutes.Profiles, ct);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex) { Log("TestProfileApi", ex.GetType().Name); return false; }
        }

        public async Task<bool> TestAuthAsync(CancellationToken ct = default)
        {
            try
            {
                if (!await ApplyAuthHeaderAsync()) return false;
                var response = await _httpClient.GetAsync(ApiRoutes.MyProfile, ct);
                return response.IsSuccessStatusCode ||
                       response.StatusCode == HttpStatusCode.NotFound; // profile absent ≠ auth failure
            }
            catch (Exception ex) { Log("TestAuth", ex.GetType().Name); return false; }
        }

        // ─────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Central HTTP dispatch. Serialises optional body, always logs timing.
        /// Never throws on non-2xx — callers inspect the status code themselves.
        /// </summary>
        private async Task<HttpResponseMessage> SendAsync(
            HttpMethod method,
            string relativeUrl,
            object body = null,
            CancellationToken ct = default)
        {
            var request = new HttpRequestMessage(method, relativeUrl);

            if (body is not null)
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, _jsonOptions),
                    System.Text.Encoding.UTF8,
                    "application/json");

            var sw = Stopwatch.StartNew();
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            sw.Stop();

            Log(method.Method, $"{relativeUrl} → {(int)response.StatusCode} [{sw.ElapsedMilliseconds} ms]");
            return response;
        }

        private async Task<T> DeserializeAsync<T>(HttpResponseMessage response, CancellationToken ct)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(stream, _jsonOptions, ct);
        }

        private async Task RequireAuthAsync(CancellationToken ct)
        {
            if (!await ApplyAuthHeaderAsync())
                throw new UnauthorizedAccessException("Not authenticated. Please log in first.");
        }

        private static void EnsureSuccess(HttpResponseMessage response, string caller)
        {
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"{caller} failed with HTTP {(int)response.StatusCode}.");
        }

        private static void EnsureSuccessOrForbid(HttpResponseMessage response, string caller)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new UnauthorizedAccessException($"{caller}: access denied.");
            EnsureSuccess(response, caller);
        }

        private static void ValidateCompanyId(int companyId)
        {
            if (companyId <= 0)
                throw new ArgumentOutOfRangeException(nameof(companyId), "Company ID must be a positive integer.");
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private static void Log(string tag, string message,
            [CallerMemberName] string member = "")
            => Debug.WriteLine($"[PROFILE:{tag}] ({member}) {message}");

        // ─────────────────────────────────────────────────────────────────
        // MAPPERS
        // ─────────────────────────────────────────────────────────────────

        private static MobileArtisanProfile MapToMobileArtisanProfile(ArtisanProfileDto d) => d is null ? null : new()
        {
            Id = d.Id,
            BusinessName = d.BusinessName,
            Specialization = d.Specialization,
            YearsOfExperience = d.YearsOfExperience,
            ExperienceLevel = d.ExperienceLevel,
            LicenseNumber = d.LicenseNumber,
            Certification = d.Certification,
            BusinessRegistration = d.BusinessRegistration,
            TaxId = d.TaxId,
            InsuranceDetails = d.InsuranceDetails,
            AvailabilityStatus = d.AvailabilityStatus,
            HourlyRate = d.HourlyRate,
            ServiceRadius = d.ServiceRadius,
            About = d.About,
            ServicesOffered = d.ServicesOffered,
            ArtisanSpeciality = d.ArtisanSpeciality,
            ProfessionalBio = d.ProfessionalBio,
            BusinessAddress = d.BusinessAddress,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt,
            Slug = d.Slug
        };

        private static MobileUserProfile MapToMobileUserProfile(UserProfileDto d) => d is null ? null : new()
        {
            FullName = d.FullName,
            Bio = d.Bio,
            Address = d.Address,
            City = d.City,
            State = d.State,
            Country = d.Country,
            PostalCode = d.PostalCode,
            ProfilePictureUrl = d.ProfilePictureUrl,
            PreferredLanguage = d.PreferredLanguage,
            Timezone = d.Timezone
        };

        private static MobileTrustScore MapToMobileTrustScore(TrustScoreResponseDto d) => d is null ? null : new()
        {
            CompanyId = d.CompanyId,
            Score = d.Score,
            Band = d.Band,
            CalculatedAt = d.CalculatedAt,
            Breakdown = d.Breakdown
        };

        private static MobileTrustScoreHistoryItem MapToMobileHistoryItem(TrustScoreHistoryItemDto d) => new()
        {
            Score = d.Score,
            Band = d.Band,
            RecordedAt = d.RecordedAt,
            ChangeReason = d.ChangeReason
        };

        private static MobileWorkReferral MapToMobileWorkReferral(WorkReferralDto d) => new()
        {
            Id = d.Id,
            ReferrerName = d.ReferrerName,
            ProjectTitle = d.ProjectTitle,
            Rating = d.Rating,
            Comment = d.Comment,
            SubmittedAt = d.SubmittedAt
        };

        private static MobileVendorReferral MapToMobileVendorReferral(VendorReferralDto d) => new()
        {
            Id = d.Id,
            VendorName = d.VendorName,
            Category = d.Category,
            Rating = d.Rating,
            Comment = d.Comment,
            SubmittedAt = d.SubmittedAt
        };

        private static MobileColleagueReferral MapToMobileColleagueReferral(ColleagueReferralDto d) => new()
        {
            Id = d.Id,
            ColleagueName = d.ColleagueName,
            Relationship = d.Relationship,
            Rating = d.Rating,
            Comment = d.Comment,
            SubmittedAt = d.SubmittedAt
        };

        // ─────────────────────────────────────────────────────────────────

        public void Dispose() => _httpClient.Dispose();
    }
}