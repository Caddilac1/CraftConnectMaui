// MAUI/Services/UserFeedService.cs
using System.Net.Http.Json;
using System.Net.Http.Headers;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public class UserFeedService : IUserFeedService
    {
        private readonly HttpClient _httpClient;

        public UserFeedService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        private async Task AddAuthHeaderAsync()
        {
            try
            {
                // Get token from secure storage
                var token = await SecureStorage.GetAsync("auth_token");
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting auth token: {ex.Message}");
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
                    queryString += $"&status={status}";
                if (!string.IsNullOrEmpty(category))
                    queryString += $"&category={category}";
                if (!string.IsNullOrEmpty(location))
                    queryString += $"&location={location}";

                var response = await _httpClient.GetAsync($"api/userfeeds{queryString}");

                if (!response.IsSuccessStatusCode)
                    return (new List<UserFeedDto>(), 0, 0);

                var result = await response.Content.ReadFromJsonAsync<FeedResponse>();

                return (result?.Feeds ?? new List<UserFeedDto>(),
                        result?.TotalCount ?? 0,
                        result?.TotalPages ?? 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching feeds: {ex.Message}");
                return (new List<UserFeedDto>(), 0, 0);
            }
        }

        public async Task<UserFeedDto?> GetUserFeedByIdAsync(Guid id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/userfeeds/{id}");

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<UserFeedDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching feed: {ex.Message}");
                return null;
            }
        }

        public async Task<List<UserFeedDto>> GetMyFeedsAsync()
        {
            try
            {
                await AddAuthHeaderAsync();

                var response = await _httpClient.GetAsync("api/userfeeds/my-feeds");

                if (!response.IsSuccessStatusCode)
                    return new List<UserFeedDto>();

                return await response.Content.ReadFromJsonAsync<List<UserFeedDto>>()
                    ?? new List<UserFeedDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching my feeds: {ex.Message}");
                return new List<UserFeedDto>();
            }
        }

        public async Task<List<UserFeedDto>> GetFeaturedFeedsAsync(int limit = 10)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/userfeeds/featured?limit={limit}");

                if (!response.IsSuccessStatusCode)
                    return new List<UserFeedDto>();

                return await response.Content.ReadFromJsonAsync<List<UserFeedDto>>()
                    ?? new List<UserFeedDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching featured feeds: {ex.Message}");
                return new List<UserFeedDto>();
            }
        }

        public async Task<List<string>> GetCategoriesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/userfeeds/categories");

                if (!response.IsSuccessStatusCode)
                    return new List<string>();

                return await response.Content.ReadFromJsonAsync<List<string>>()
                    ?? new List<string>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching categories: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<UserFeedDto?> CreateUserFeedAsync(CreateUserFeedDto feed)
        {
            try
            {
                await AddAuthHeaderAsync();

                var response = await _httpClient.PostAsJsonAsync("api/userfeeds", feed);

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<UserFeedDto>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating feed: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> UpdateUserFeedAsync(Guid id, UpdateUserFeedDto feed)
        {
            try
            {
                await AddAuthHeaderAsync();

                var response = await _httpClient.PutAsJsonAsync($"api/userfeeds/{id}", feed);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating feed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteUserFeedAsync(Guid id)
        {
            try
            {
                await AddAuthHeaderAsync();

                var response = await _httpClient.DeleteAsync($"api/userfeeds/{id}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting feed: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> LikeFeedAsync(Guid id)
        {
            try
            {
                await AddAuthHeaderAsync();

                var response = await _httpClient.PostAsync($"api/userfeeds/{id}/like", null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error liking feed: {ex.Message}");
                return false;
            }
        }

        private class FeedResponse
        {
            public List<UserFeedDto> Feeds { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
        }
    }
}