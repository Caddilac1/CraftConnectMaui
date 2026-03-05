using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    // ═══════════════════════════════════════════════════════════════
    // PRIVATE API RESPONSE MODELS — match backend JSON shapes exactly
    // ═══════════════════════════════════════════════════════════════

    internal class ProfilesResponse
    {
        public List<ArtisanProfileDto> Profiles { get; set; }
    }

    internal class ProfileDetailsResponse
    {
        public string IdentityUserId { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public UserProfileDto UserProfile { get; set; }
        public ArtisanProfileDto ArtisanProfile { get; set; }
    }

    internal class ArtisanProfileDto
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

    internal class UserProfileDto
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

    internal class HasProfileResponse
    {
        public bool HasProfile { get; set; }
        public string Error { get; set; }
    }

    internal class ApiResponse
    {
        public string Message { get; set; }
        public string Error { get; set; }
    }

    internal class CreateArtisanProfileRequest
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

    internal class UpdateProfileRequest
    {
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public UserProfileDto UserProfile { get; set; }
        public ArtisanProfileDto ArtisanProfile { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // PUBLIC MODELS FOR THE MOBILE APP
    // ═══════════════════════════════════════════════════════════════

    public class MobileArtisanProfile
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
        
        // Computed properties for display
        public string DisplayName => !string.IsNullOrEmpty(BusinessName) ? BusinessName : "Unnamed Business";
        public string DisplayRate => HourlyRate.HasValue ? $"${HourlyRate:F2}/hr" : "Rate not set";
        public string DisplayExperience => $"{YearsOfExperience} years ({ExperienceLevel})";
        public bool IsAvailable => AvailabilityStatus == "Available";
    }

    public class MobileUserProfile
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
        
        // Computed properties
        public string FullAddress => string.IsNullOrEmpty(Address) ? "Address not set" : 
            $"{Address}, {City}, {State} {PostalCode}, {Country}";
        public string DisplayName => !string.IsNullOrEmpty(FullName) ? FullName : "User";
    }

    public class MobileProfileDetails
    {
        public string IdentityUserId { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public MobileUserProfile UserProfile { get; set; }
        public MobileArtisanProfile ArtisanProfile { get; set; }
        
        // Computed properties
        public bool HasArtisanProfile => ArtisanProfile != null;
        public bool HasUserProfile => UserProfile != null;
        public string DisplayName => UserProfile?.DisplayName ?? Email ?? "User";
    }

    public class CreateMobileArtisanProfile
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

    public class UpdateMobileProfile
    {
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public MobileUserProfile UserProfile { get; set; }
        public MobileArtisanProfile ArtisanProfile { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // PROFILE API SERVICE
    // ═══════════════════════════════════════════════════════════════

    public interface IProfileApiService
    {
        Task<List<MobileArtisanProfile>> GetAllProfilesAsync();
        Task<MobileProfileDetails> GetMyProfileAsync();
        Task<MobileArtisanProfile> GetProfileAsync(string id);
        Task<MobileArtisanProfile> CreateArtisanProfileAsync(CreateMobileArtisanProfile profile);
        Task<bool> UpdateProfileAsync(UpdateMobileProfile profile);
        Task<bool> DeleteArtisanProfileAsync();
        Task<bool> DeleteUserProfileAsync();
        Task<bool> HasArtisanProfileAsync();
        Task<int> GetTotalProfilesCountAsync();
        Task<bool> TestProfileApiAsync();
        Task<bool> TestAuthAsync();
    }

    public class ProfileApiService : IProfileApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public ProfileApiService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');

#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[PROFILE SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true; // For development only! Remove in production
                }
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[PROFILE SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true; // For development only! Remove in production
                }
            };
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[PROFILE SERVICE] Initialized with BaseUrl: {_baseUrl}");
        }

        // ═══════════════════════════════════════════════════════════════
        // AUTH HEADER
        // ═══════════════════════════════════════════════════════════════

        private async Task<bool> SetAuthHeaderAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");

                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[PROFILE SERVICE] ❌ No auth token found in SecureStorage");
                    return false;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                Debug.WriteLine($"[PROFILE SERVICE] ✅ Auth header set. Token length: {token.Length}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error setting auth header: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET ALL PROFILES  →  GET /api/ProfilesApi
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<MobileArtisanProfile>> GetAllProfilesAsync()
        {
            try
            {
                Debug.WriteLine("[PROFILE SERVICE] 📡 Fetching all profiles...");

                if (!await SetAuthHeaderAsync())
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync("/api/ProfilesApi");
                sw.Stop();

                Debug.WriteLine($"[PROFILE SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response, "GetAllProfiles");
                    return new List<MobileArtisanProfile>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<List<ArtisanProfileDto>>(json, _jsonOptions);

                var profiles = (result ?? new List<ArtisanProfileDto>()).Select(MapToMobileArtisanProfile).ToList();

                Debug.WriteLine($"[PROFILE SERVICE] ✅ Fetched {profiles.Count} profiles");
                return profiles;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error getting profiles: {ex.Message}");
                throw new Exception($"Error getting profiles: {ex.Message}", ex);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET MY PROFILE  →  GET /api/ProfilesApi/MyProfile
        // ═══════════════════════════════════════════════════════════════

        public async Task<MobileProfileDetails> GetMyProfileAsync()
        {
            try
            {
                Debug.WriteLine("[PROFILE SERVICE] 📡 Fetching my profile...");

                if (!await SetAuthHeaderAsync())
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync("/api/ProfilesApi/MyProfile");
                sw.Stop();

                Debug.WriteLine($"[PROFILE SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Debug.WriteLine("[PROFILE SERVICE] ℹ️ No profile found");
                    return new MobileProfileDetails();
                }

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response, "GetMyProfile");
                    return new MobileProfileDetails();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ProfileDetailsResponse>(json, _jsonOptions);

                if (result == null)
                {
                    Debug.WriteLine("[PROFILE SERVICE] ⚠️ Invalid response format");
                    return new MobileProfileDetails();
                }

                var profile = new MobileProfileDetails
                {
                    IdentityUserId = result.IdentityUserId,
                    Email = result.Email,
                    PhoneNumber = result.PhoneNumber,
                    UserProfile = result.UserProfile != null ? new MobileUserProfile
                    {
                        FullName = result.UserProfile.FullName,
                        Bio = result.UserProfile.Bio,
                        Address = result.UserProfile.Address,
                        City = result.UserProfile.City,
                        State = result.UserProfile.State,
                        Country = result.UserProfile.Country,
                        PostalCode = result.UserProfile.PostalCode,
                        ProfilePictureUrl = result.UserProfile.ProfilePictureUrl,
                        PreferredLanguage = result.UserProfile.PreferredLanguage,
                        Timezone = result.UserProfile.Timezone
                    } : null,
                    ArtisanProfile = result.ArtisanProfile != null ? MapToMobileArtisanProfile(result.ArtisanProfile) : null
                };

                Debug.WriteLine($"[PROFILE SERVICE] ✅ Profile fetched successfully. HasArtisan: {profile.HasArtisanProfile}");
                return profile;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error getting my profile: {ex.Message}");
                throw;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET PROFILE BY ID  →  GET /api/ProfilesApi/{id}
        // ═══════════════════════════════════════════════════════════════

        public async Task<MobileArtisanProfile> GetProfileAsync(string id)
        {
            try
            {
                Debug.WriteLine($"[PROFILE SERVICE] 📡 Fetching profile: {id}");

                if (!await SetAuthHeaderAsync())
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync($"/api/ProfilesApi/{id}");
                sw.Stop();

                Debug.WriteLine($"[PROFILE SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Debug.WriteLine($"[PROFILE SERVICE] ℹ️ Profile {id} not found");
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response, "GetProfile");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ArtisanProfileDto>(json, _jsonOptions);

                if (result == null)
                {
                    Debug.WriteLine("[PROFILE SERVICE] ⚠️ Invalid response format");
                    return null;
                }

                var profile = MapToMobileArtisanProfile(result);
                Debug.WriteLine($"[PROFILE SERVICE] ✅ Profile {id} fetched successfully");
                return profile;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error getting profile: {ex.Message}");
                throw;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CREATE ARTISAN PROFILE  →  POST /api/ProfilesApi
        // ═══════════════════════════════════════════════════════════════

        public async Task<MobileArtisanProfile> CreateArtisanProfileAsync(CreateMobileArtisanProfile profile)
        {
            try
            {
                Debug.WriteLine("[PROFILE SERVICE] 📤 Creating artisan profile...");

                if (!await SetAuthHeaderAsync())
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

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

                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.PostAsync("/api/ProfilesApi", content);
                sw.Stop();

                Debug.WriteLine($"[PROFILE SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorJson = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[PROFILE SERVICE] ⚠️ Bad request: {errorJson}");
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response, "CreateArtisanProfile");
                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ArtisanProfileDto>(responseJson, _jsonOptions);

                if (result == null)
                {
                    Debug.WriteLine("[PROFILE SERVICE] ⚠️ Invalid response format");
                    return null;
                }

                var createdProfile = MapToMobileArtisanProfile(result);
                Debug.WriteLine($"[PROFILE SERVICE] ✅ Artisan profile created successfully. ID: {createdProfile.Id}");
                return createdProfile;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error creating profile: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE PROFILE  →  PUT /api/ProfilesApi
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> UpdateProfileAsync(UpdateMobileProfile profile)
        {
            try
            {
                Debug.WriteLine("[PROFILE SERVICE] 📤 Updating profile...");

                if (!await SetAuthHeaderAsync())
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

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

                var json = JsonSerializer.Serialize(request, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.PutAsync("/api/ProfilesApi", content);
                sw.Stop();

                Debug.WriteLine($"[PROFILE SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response, "UpdateProfile");
                    return false;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse>(responseJson, _jsonOptions);

                Debug.WriteLine($"[PROFILE SERVICE] ✅ Profile updated successfully: {result?.Message ?? "Success"}");
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error updating profile: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DELETE ARTISAN PROFILE  →  DELETE /api/ProfilesApi/ArtisanProfile
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> DeleteArtisanProfileAsync()
        {
            try
            {
                Debug.WriteLine("[PROFILE SERVICE] 🗑️ Deleting artisan profile...");

                if (!await SetAuthHeaderAsync())
                    return false;

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.DeleteAsync("/api/ProfilesApi/ArtisanProfile");
                sw.Stop();

                Debug.WriteLine($"[PROFILE SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Debug.WriteLine("[PROFILE SERVICE] ℹ️ No artisan profile found to delete");
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response, "DeleteArtisanProfile");
                    return false;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse>(responseJson, _jsonOptions);

                Debug.WriteLine($"[PROFILE SERVICE] ✅ Artisan profile deleted: {result?.Message ?? "Success"}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error deleting artisan profile: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DELETE USER PROFILE (SOFT DELETE)  →  DELETE /api/ProfilesApi/UserProfile
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> DeleteUserProfileAsync()
        {
            try
            {
                Debug.WriteLine("[PROFILE SERVICE] 🗑️ Soft deleting user profile...");

                if (!await SetAuthHeaderAsync())
                    return false;

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.DeleteAsync("/api/ProfilesApi/UserProfile");
                sw.Stop();

                Debug.WriteLine($"[PROFILE SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Debug.WriteLine("[PROFILE SERVICE] ℹ️ No user profile found to delete");
                    return false;
                }

                if (!response.IsSuccessStatusCode)
                {
                    await HandleErrorResponseAsync(response, "DeleteUserProfile");
                    return false;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ApiResponse>(responseJson, _jsonOptions);

                Debug.WriteLine($"[PROFILE SERVICE] ✅ User profile soft deleted: {result?.Message ?? "Success"}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error deleting user profile: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CHECK IF USER HAS ARTISAN PROFILE  →  GET /api/ProfilesApi/HasArtisanProfile
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> HasArtisanProfileAsync()
        {
            try
            {
                Debug.WriteLine("[PROFILE SERVICE] 🔍 Checking if user has artisan profile...");

                if (!await SetAuthHeaderAsync())
                    return false;

                var response = await _httpClient.GetAsync("/api/ProfilesApi/HasArtisanProfile");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[PROFILE SERVICE] ❌ HasArtisanProfile check failed: {response.StatusCode}");
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<HasProfileResponse>(json, _jsonOptions);

                var hasProfile = result?.HasProfile ?? false;
                Debug.WriteLine($"[PROFILE SERVICE] ✅ Has artisan profile: {hasProfile}");
                return hasProfile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Error checking artisan profile: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET TOTAL PROFILES COUNT
        // ═══════════════════════════════════════════════════════════════

        public async Task<int> GetTotalProfilesCountAsync()
        {
            try
            {
                var profiles = await GetAllProfilesAsync();
                return profiles?.Count ?? 0;
            }
            catch
            {
                return 0;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // TEST METHODS
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> TestProfileApiAsync()
        {
            try
            {
                Debug.WriteLine($"[PROFILE SERVICE] Testing: {_baseUrl}/api/ProfilesApi");
                var response = await _httpClient.GetAsync("/api/ProfilesApi");
                Debug.WriteLine($"[PROFILE SERVICE] Test response: {(int)response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[PROFILE SERVICE] ✅ Test success: {content?.Length ?? 0} chars");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Test exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TestAuthAsync()
        {
            try
            {
                Debug.WriteLine("[PROFILE SERVICE] Testing authenticated endpoint...");

                if (!await SetAuthHeaderAsync())
                {
                    Debug.WriteLine("[PROFILE SERVICE] ❌ No token available");
                    return false;
                }

                var response = await _httpClient.GetAsync("/api/ProfilesApi/MyProfile");
                Debug.WriteLine($"[PROFILE SERVICE] Auth test response: {(int)response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("[PROFILE SERVICE] ✅ Auth test success");
                    return true;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine("[PROFILE SERVICE] ❌ Auth test failed: Unauthorized");
                    return false;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Auth test failed: {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROFILE SERVICE] ❌ Auth test exception: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Centralised error handler — logs details and throws for 401s.
        /// </summary>
        private async Task HandleErrorResponseAsync(HttpResponseMessage response, string caller)
        {
            var body = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[PROFILE SERVICE] ❌ {caller} error {(int)response.StatusCode}: {body}");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                SecureStorage.Remove("auth_token");
                throw new UnauthorizedAccessException("Session expired. Please login again.");
            }
        }

        private MobileArtisanProfile MapToMobileArtisanProfile(ArtisanProfileDto dto)
        {
            if (dto == null) return null;

            return new MobileArtisanProfile
            {
                Id = dto.Id,
                BusinessName = dto.BusinessName,
                Specialization = dto.Specialization,
                YearsOfExperience = dto.YearsOfExperience,
                ExperienceLevel = dto.ExperienceLevel,
                LicenseNumber = dto.LicenseNumber,
                Certification = dto.Certification,
                BusinessRegistration = dto.BusinessRegistration,
                TaxId = dto.TaxId,
                InsuranceDetails = dto.InsuranceDetails,
                AvailabilityStatus = dto.AvailabilityStatus,
                HourlyRate = dto.HourlyRate,
                ServiceRadius = dto.ServiceRadius,
                About = dto.About,
                ServicesOffered = dto.ServicesOffered,
                ArtisanSpeciality = dto.ArtisanSpeciality,
                ProfessionalBio = dto.ProfessionalBio,
                BusinessAddress = dto.BusinessAddress,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                Slug = dto.Slug
            };
        }
    }
}