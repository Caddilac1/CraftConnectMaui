// MAUI/Services/IUserFeedService.cs
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public interface IUserFeedService
    {
        Task<(List<UserFeedDto> feeds, int totalCount, int totalPages)> GetUserFeedsAsync(
            string? status = null,
            string? category = null,
            string? location = null,
            int page = 1,
            int pageSize = 20);

        Task<UserFeedDto?> GetUserFeedByIdAsync(Guid id);
        Task<List<UserFeedDto>> GetMyFeedsAsync();
        Task<List<UserFeedDto>> GetFeaturedFeedsAsync(int limit = 10);
        Task<List<string>> GetCategoriesAsync();
        Task<UserFeedDto?> CreateUserFeedAsync(CreateUserFeedDto feed);
        Task<bool> UpdateUserFeedAsync(Guid id, UpdateUserFeedDto feed);
        Task<bool> DeleteUserFeedAsync(Guid id);
        Task<bool> LikeFeedAsync(Guid id);
    }
}