using System.Collections.Generic;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public interface IStoreService
    {
        // ── Products ──────────────────────────────────────────────────
        /// <summary>
        /// Paginated product list. Pass null filters to get everything.
        /// </summary>
        Task<StoreProductsResult> GetProductsAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            int? categoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string sortBy = "popular");

        /// <summary>
        /// Featured/high-rated products for the home carousel.
        /// </summary>
        Task<List<StoreItem>> GetFeaturedProductsAsync(int limit = 8);

        /// <summary>
        /// Products currently in an active promotion.
        /// </summary>
        Task<List<StoreItem>> GetPromotionsAsync(int limit = 10);

        // ── Categories ────────────────────────────────────────────────
        Task<List<StoreCategoryDto>> GetCategoriesAsync();

        // ── Cart ──────────────────────────────────────────────────────
        Task<bool> AddToCartAsync(int productId, int quantity);
        Task<bool> UpdateCartItemAsync(int cartItemId, int quantity);
        Task<bool> RemoveFromCartAsync(int cartItemId);
        Task<StoreCartResult?> GetCartAsync();
    }

    // ── Result / DTO types used by the interface ──────────────────────

    public class StoreProductsResult
    {
        public List<StoreItem> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
    }

    public class StoreCategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int ProductCount { get; set; }
    }

    public class StoreCartResult
    {
        public List<StoreCartItemDto> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
        public int CartId { get; set; }
    }

    public class StoreCartItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public string? CompanyBusinessName { get; set; }
        public bool IsInStock { get; set; }
    }
}
