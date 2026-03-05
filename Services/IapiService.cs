using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    /// <summary>
    /// Contract for all Home-page API calls.
    /// Mirrors the web HomeController's data-access pattern.
    /// </summary>
    public interface IApiService
    {
        // ─── Static / cached data ────────────────────────────────────────────────
        Task<IEnumerable<PromotionCarouselModel>> GetCurrentPromotionsAsync();
        Task<IEnumerable<BroadCategoryModel>> GetBroadCategoriesAsync();
        Task<IEnumerable<EcommerceItemModel>> GetFeaturedItemsAsync();
        Task<IEnumerable<SpecialDealModel>> GetSpecialDealsAsync();
        Task<IEnumerable<ServiceViewModel>> GetFeaturedServicesAsync();
        Task<IEnumerable<FeedViewModel>> GetFeaturedFeedsAsync();
        Task<IEnumerable<CategoryViewModel>> GetTrendingCategoriesAsync();
        Task<IEnumerable<EcommerceItemModel>> GetRecentlyViewedAsync();

        // ─── Paginated product grid ──────────────────────────────────────────────
        Task<HomeItemsResponse> GetHomeItemsAsync(HomeItemsRequest request);

        // ─── User / auth ─────────────────────────────────────────────────────────
        Task<UserInfoViewModel?> GetCurrentUserInfoAsync();
        Task<int> GetCartCountAsync();

        // ─── Cart ────────────────────────────────────────────────────────────────
        Task<AddToCartResult> AddToCartAsync(int itemId, string? itemType);

        // ─── Wishlist ────────────────────────────────────────────────────────────
        Task AddToWishlistAsync(int itemId, string? itemType);
        Task RemoveFromWishlistAsync(int itemId, string? itemType);

        // ─── Recently viewed ─────────────────────────────────────────────────────
        Task ClearRecentlyViewedAsync();
    }
}