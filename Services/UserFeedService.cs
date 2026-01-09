using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Diagnostics;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public class UserFeedService : IUserFeedService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://192.168.188.112:7023"; // Match your API URL
        private readonly JsonSerializerOptions _jsonOptions;

        public UserFeedService()
        {
            var handler = new HttpClientHandler();

#if DEBUG
            // Allow self-signed certificates in development
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) => true;
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            Debug.WriteLine($"[USER FEED SERVICE] Initialized with BaseUrl: {BaseUrl}");
        }

        /// <summary>
        /// Get token from SecureStorage and set it in the HTTP client
        /// </summary>
        private async Task<bool> SetAuthHeaderAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");

                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[USER FEED SERVICE] ⚠️ No auth token found (optional for public endpoints)");
                    return false;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                Debug.WriteLine($"[USER FEED SERVICE] ✅ Auth header set. Token length: {token.Length}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error setting auth header: {ex.Message}");
                return false;
            }
        }

        public async Task<(List<UserFeedDto> feeds, int totalCount, int totalPages)> GetUserFeedsAsync(
            string? status = null,
            string? category = null,
            string? location = null,
            int page = 1,
            int pageSize = 20)
        {
            try
            {
                var queryString = $"?page={page}&pageSize={pageSize}";
                if (!string.IsNullOrEmpty(status))
                    queryString += $"&status={Uri.EscapeDataString(status)}";
                if (!string.IsNullOrEmpty(category))
                    queryString += $"&category={Uri.EscapeDataString(category)}";
                if (!string.IsNullOrEmpty(location))
                    queryString += $"&location={Uri.EscapeDataString(location)}";

                Debug.WriteLine($"[USER FEED SERVICE] 📡 Fetching feeds: /api/userfeeds{queryString}");

                var response = await _httpClient.GetAsync($"/api/userfeeds{queryString}");

                Debug.WriteLine($"[USER FEED SERVICE] 📥 Response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[USER FEED SERVICE] ❌ API Error: {response.StatusCode}");
                    Debug.WriteLine($"[USER FEED SERVICE] Error content: {errorContent}");
                    return (new List<UserFeedDto>(), 0, 0);
                }

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[USER FEED SERVICE] 📄 Response JSON length: {json.Length} chars");
                Debug.WriteLine($"[USER FEED SERVICE] Response preview: {json.Substring(0, Math.Min(300, json.Length))}...");

                var apiResponse = JsonSerializer.Deserialize<GetFeedsResponse>(json, _jsonOptions);

                if (apiResponse?.Success == true && apiResponse.Feeds != null)
                {
                    Debug.WriteLine($"[USER FEED SERVICE] ✅ Successfully fetched {apiResponse.Feeds.Count} feeds");

                    // Map the feeds to include flattened user properties
                    var mappedFeeds = apiResponse.Feeds.Select(f => new UserFeedDto
                    {
                        Id = f.Id,
                        UserId = f.UserId,
                        Title = f.Title,
                        Slug = f.Slug,
                        Description = f.Description,
                        JobCategory = f.JobCategory,
                        InvoiceImage = f.InvoiceImage,
                        Location = f.Location,
                        PreferredStartDate = f.PreferredStartDate,
                        Deadline = f.Deadline,
                        Status = f.Status,
                        Priority = f.Priority,
                        ViewsCount = f.ViewsCount,
                        CommentsCount = f.CommentsCount,
                        LikesCount = f.LikesCount,
                        DislikesCount = f.DislikesCount,
                        ReportsCount = f.ReportsCount,
                        CreatedAt = f.CreatedAt,
                        UpdatedAt = f.UpdatedAt,
                        IsActive = f.IsActive,
                        IsFeatured = f.IsFeatured,
                        IsFlagged = f.IsFlagged,
                        StatusDisplay = f.StatusDisplay,
                        PriorityDisplay = f.PriorityDisplay,
                        IsExpired = f.IsExpired,
                        User = f.User,
                        UserFullName = f.User?.FullName ?? string.Empty,
                        UserProfileImage = f.User?.ProfilePicture ?? string.Empty,
                        UserPhoneNumber = f.User?.PhoneNumber ?? string.Empty
                    }).ToList();

                    return (mappedFeeds, apiResponse.TotalCount, apiResponse.TotalPages);
                }

                Debug.WriteLine($"[USER FEED SERVICE] ⚠️ API returned success=false or no feeds");
                return (new List<UserFeedDto>(), 0, 0);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] 🌐 Network error: {ex.Message}");
                return (new List<UserFeedDto>(), 0, 0);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] 📋 JSON parsing error: {ex.Message}");
                return (new List<UserFeedDto>(), 0, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Unexpected error: {ex.Message}");
                Debug.WriteLine($"[USER FEED SERVICE] StackTrace: {ex.StackTrace}");
                return (new List<UserFeedDto>(), 0, 0);
            }
        }

        public async Task<UserFeedDto?> GetUserFeedByIdAsync(Guid id)
        {
            try
            {
                Debug.WriteLine($"[USER FEED SERVICE] 📡 Fetching feed: {id}");

                var response = await _httpClient.GetAsync($"/api/userfeeds/{id}");

                Debug.WriteLine($"[USER FEED SERVICE] Response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[USER FEED SERVICE] ❌ Error: {errorContent}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<GetFeedResponse>(json, _jsonOptions);

                if (apiResponse?.Success == true && apiResponse.Feed != null)
                {
                    var f = apiResponse.Feed;
                    Debug.WriteLine($"[USER FEED SERVICE] ✅ Successfully fetched feed: {f.Title}");

                    return new UserFeedDto
                    {
                        Id = f.Id,
                        UserId = f.UserId,
                        Title = f.Title,
                        Slug = f.Slug,
                        Description = f.Description,
                        JobCategory = f.JobCategory,
                        InvoiceImage = f.InvoiceImage,
                        Location = f.Location,
                        PreferredStartDate = f.PreferredStartDate,
                        Deadline = f.Deadline,
                        Status = f.Status,
                        Priority = f.Priority,
                        ViewsCount = f.ViewsCount,
                        CommentsCount = f.CommentsCount,
                        LikesCount = f.LikesCount,
                        DislikesCount = f.DislikesCount,
                        ReportsCount = f.ReportsCount,
                        CreatedAt = f.CreatedAt,
                        UpdatedAt = f.UpdatedAt,
                        IsActive = f.IsActive,
                        IsFeatured = f.IsFeatured,
                        IsFlagged = f.IsFlagged,
                        StatusDisplay = f.StatusDisplay,
                        PriorityDisplay = f.PriorityDisplay,
                        IsExpired = f.IsExpired,
                        User = f.User,
                        UserFullName = f.User?.FullName ?? string.Empty,
                        UserProfileImage = f.User?.ProfilePicture ?? string.Empty,
                        UserPhoneNumber = f.User?.PhoneNumber ?? string.Empty
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error fetching feed: {ex.Message}");
                return null;
            }
        }

        public async Task<List<UserFeedDto>> GetMyFeedsAsync()
        {
            try
            {
                Debug.WriteLine("[USER FEED SERVICE] 📡 Fetching my feeds...");

                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var response = await _httpClient.GetAsync("/api/userfeeds/my-feeds");

                Debug.WriteLine($"[USER FEED SERVICE] Response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[USER FEED SERVICE] ❌ Error: {errorContent}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        SecureStorage.Remove("auth_token");
                        throw new UnauthorizedAccessException("Session expired. Please login again.");
                    }

                    return new List<UserFeedDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<GetMyFeedsResponse>(json, _jsonOptions);

                if (apiResponse?.Success == true && apiResponse.Feeds != null)
                {
                    Debug.WriteLine($"[USER FEED SERVICE] ✅ Fetched {apiResponse.Feeds.Count} personal feeds");
                    return apiResponse.Feeds;
                }

                return new List<UserFeedDto>();
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error fetching my feeds: {ex.Message}");
                return new List<UserFeedDto>();
            }
        }

        public async Task<List<UserFeedDto>> GetFeaturedFeedsAsync(int limit = 10)
        {
            try
            {
                Debug.WriteLine($"[USER FEED SERVICE] 📡 Fetching featured feeds (limit: {limit})...");

                var response = await _httpClient.GetAsync($"/api/userfeeds/featured?limit={limit}");

                Debug.WriteLine($"[USER FEED SERVICE] 📥 Response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[USER FEED SERVICE] ❌ Error: {errorContent}");
                    return new List<UserFeedDto>();
                }

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[USER FEED SERVICE] 📄 Response preview: {json.Substring(0, Math.Min(300, json.Length))}...");

                var apiResponse = JsonSerializer.Deserialize<GetFeaturedFeedsResponse>(json, _jsonOptions);

                if (apiResponse?.Success == true && apiResponse.Feeds != null)
                {
                    Debug.WriteLine($"[USER FEED SERVICE] ✅ Successfully fetched {apiResponse.Feeds.Count} featured feeds");

                    // Map the featured feeds (they have less fields)
                    var mappedFeeds = apiResponse.Feeds.Select(f => new UserFeedDto
                    {
                        Id = f.Id,
                        Title = f.Title,
                        Description = f.Description,
                        JobCategory = f.JobCategory,
                        Location = f.Location,
                        Status = f.Status,
                        Priority = f.Priority,
                        ViewsCount = f.ViewsCount,
                        LikesCount = f.LikesCount,
                        CreatedAt = f.CreatedAt,
                        User = f.User != null ? new UserBasicDto
                        {
                            FullName = f.User.FullName,
                            ProfilePicture = f.User.ProfilePicture
                        } : null,
                        UserFullName = f.User?.FullName ?? string.Empty,
                        UserProfileImage = f.User?.ProfilePicture ?? string.Empty,
                        IsFeatured = true
                    }).ToList();

                    return mappedFeeds;
                }

                Debug.WriteLine($"[USER FEED SERVICE] ⚠️ No featured feeds returned");
                return new List<UserFeedDto>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error fetching featured feeds: {ex.Message}");
                Debug.WriteLine($"[USER FEED SERVICE] StackTrace: {ex.StackTrace}");
                return new List<UserFeedDto>();
            }
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            try
            {
                Debug.WriteLine("[USER FEED SERVICE] 📡 Fetching categories...");

                var response = await _httpClient.GetAsync("/api/userfeeds/categories");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[USER FEED SERVICE] ❌ Error: {response.StatusCode}");
                    return new List<string>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<GetCategoriesResponse>(json, _jsonOptions);

                if (apiResponse?.Success == true && apiResponse.Categories != null)
                {
                    Debug.WriteLine($"[USER FEED SERVICE] ✅ Fetched {apiResponse.Categories.Count} categories");
                    return apiResponse.Categories;
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error fetching categories: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<UserFeedDto?> CreateUserFeedAsync(CreateUserFeedDto feed)
        {
            try
            {
                Debug.WriteLine("[USER FEED SERVICE] 📤 Creating new feed...");

                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var json = JsonSerializer.Serialize(feed, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/userfeeds", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[USER FEED SERVICE] ❌ Error: {errorContent}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        SecureStorage.Remove("auth_token");
                        throw new UnauthorizedAccessException("Session expired. Please login again.");
                    }

                    return null;
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                var apiResponse = JsonSerializer.Deserialize<CreateFeedResponse>(responseJson, _jsonOptions);

                if (apiResponse?.Success == true && apiResponse.FeedId != Guid.Empty)
                {
                    Debug.WriteLine($"[USER FEED SERVICE] ✅ Feed created successfully: {apiResponse.FeedId}");
                    // Fetch the created feed
                    return await GetUserFeedByIdAsync(apiResponse.FeedId);
                }

                return null;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error creating feed: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateUserFeedAsync(Guid id, UpdateUserFeedDto feed)
        {
            try
            {
                Debug.WriteLine($"[USER FEED SERVICE] 📤 Updating feed: {id}");

                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var json = JsonSerializer.Serialize(feed, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"/api/userfeeds/{id}", content);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("[USER FEED SERVICE] ✅ Feed updated successfully");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error: {errorContent}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    SecureStorage.Remove("auth_token");
                    throw new UnauthorizedAccessException("Session expired. Please login again.");
                }

                return false;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error updating feed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteUserFeedAsync(Guid id)
        {
            try
            {
                Debug.WriteLine($"[USER FEED SERVICE] 🗑️ Deleting feed: {id}");

                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var response = await _httpClient.DeleteAsync($"/api/userfeeds/{id}");

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("[USER FEED SERVICE] ✅ Feed deleted successfully");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error: {errorContent}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    SecureStorage.Remove("auth_token");
                    throw new UnauthorizedAccessException("Session expired. Please login again.");
                }

                return false;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error deleting feed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LikeFeedAsync(Guid id)
        {
            try
            {
                Debug.WriteLine($"[USER FEED SERVICE] 👍 Liking feed: {id}");

                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var response = await _httpClient.PostAsync($"/api/userfeeds/{id}/like", null);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("[USER FEED SERVICE] ✅ Feed liked successfully");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error: {errorContent}");

                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    SecureStorage.Remove("auth_token");
                    throw new UnauthorizedAccessException("Session expired. Please login again.");
                }

                return false;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Error liking feed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test endpoint to verify feed API is working
        /// </summary>
        public async Task<bool> TestFeedApiAsync()
        {
            try
            {
                Debug.WriteLine($"[USER FEED SERVICE] Testing connection to: {BaseUrl}/api/userfeeds/categories");

                var response = await _httpClient.GetAsync("/api/userfeeds/categories");

                Debug.WriteLine($"[USER FEED SERVICE] Test response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[USER FEED SERVICE] ✅ Test success! Response: {content.Substring(0, Math.Min(200, content.Length))}...");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[USER FEED SERVICE] ❌ Test failed! Error: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[USER FEED SERVICE] ❌ Test exception: {ex.Message}");
                return false;
            }
        }

        #region Response Models

        private class GetFeedsResponse
        {
            public bool Success { get; set; }
            public List<FeedResponseDto> Feeds { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
        }

        private class GetFeedResponse
        {
            public bool Success { get; set; }
            public FeedResponseDto? Feed { get; set; }
        }

        private class GetMyFeedsResponse
        {
            public bool Success { get; set; }
            public List<UserFeedDto> Feeds { get; set; } = new();
        }

        private class GetFeaturedFeedsResponse
        {
            public bool Success { get; set; }
            public List<FeaturedFeedDto> Feeds { get; set; } = new();
        }

        private class GetCategoriesResponse
        {
            public bool Success { get; set; }
            public List<string> Categories { get; set; } = new();
        }

        private class CreateFeedResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public Guid FeedId { get; set; }
        }

        private class FeedResponseDto
        {
            public Guid Id { get; set; }
            public Guid UserId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Slug { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string JobCategory { get; set; } = string.Empty;
            public string InvoiceImage { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public DateTime? PreferredStartDate { get; set; }
            public DateTime? Deadline { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public int ViewsCount { get; set; }
            public int CommentsCount { get; set; }
            public int LikesCount { get; set; }
            public int DislikesCount { get; set; }
            public int ReportsCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public bool IsActive { get; set; }
            public bool IsFeatured { get; set; }
            public bool IsFlagged { get; set; }
            public UserBasicDto? User { get; set; }
            public string StatusDisplay { get; set; } = string.Empty;
            public string PriorityDisplay { get; set; } = string.Empty;
            public bool IsExpired { get; set; }
        }

        private class FeaturedFeedDto
        {
            public Guid Id { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string JobCategory { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Priority { get; set; } = string.Empty;
            public int ViewsCount { get; set; }
            public int LikesCount { get; set; }
            public DateTime CreatedAt { get; set; }
            public FeaturedUserDto? User { get; set; }
        }

        private class FeaturedUserDto
        {
            public string FullName { get; set; } = string.Empty;
            public string ProfilePicture { get; set; } = string.Empty;
        }

        #endregion
    }
}