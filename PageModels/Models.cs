// Models.cs  –  place in your Models/ folder
// Contains all types referenced by HomePageModel and IApiService
// that were missing from the project (CS0246 errors).

namespace CraftConnect_Mobile_App.Models
{
    // ─── Promotion carousel ──────────────────────────────────────────────────────
    public class PromotionCarouselModel
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
    }

    // ─── Broad category (top-level nav pill) ────────────────────────────────────
    public class BroadCategoryModel
    {
        public int BroadCategoryId { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
    }

    // ─── Generic ecommerce item (products, combo products, services in a grid) ──
    public class EcommerceItemModel : System.ComponentModel.INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public double AverageRating { get; set; }
        public int TotalComments { get; set; }
        public string? Manufacturer { get; set; }
        public string? Type { get; set; }           // "Product" | "Service" | "ComboProduct"
        public int DiscountPercentage { get; set; }

        public bool HasDiscount => DiscountPercentage > 0;
        public bool HasManufacturer => !string.IsNullOrWhiteSpace(Manufacturer);

        private bool _isInWishlist;
        public bool IsInWishlist
        {
            get => _isInWishlist;
            set { _isInWishlist = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsInWishlist))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    // ─── Special deal (time-limited discount item) ───────────────────────────────
    public class SpecialDealModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
        public decimal SalePrice { get; set; }
        public decimal OriginalPrice { get; set; }
        public int DiscountPercentage { get; set; }
        public string? Type { get; set; }           // "Product" | "Service" | "ComboProduct"
        public DateTime? ExpiryDate { get; set; }
    }

    // ─── Service listing ─────────────────────────────────────────────────────────
    public class ServiceViewModel
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public string? ServiceTypeName { get; set; }
        public double AverageRating { get; set; }
    }

    // ─── CraftConnect feed / job posting ────────────────────────────────────────
    public class FeedViewModel
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? JobCategory { get; set; }
        public string? Location { get; set; }
        public string? Priority { get; set; }       // e.g. "URGENT"
        public int ViewsCount { get; set; }
        public DateTime PostedAt { get; set; }
    }

    // ─── Trending category ───────────────────────────────────────────────────────
    public class CategoryViewModel
    {
        public int CategoryId { get; set; }
        public string? Name { get; set; }
        public string? ImageUrl { get; set; }
        public int ProductCount { get; set; }
    }

    // ─── User info (returned by IApiService.GetCurrentUserInfoAsync) ────────────
    public class UserInfoViewModel
    {
        public string? GreetingName { get; set; }
        public string? ProfileImageUrl { get; set; }
    }

    // ─── Cart operation result ───────────────────────────────────────────────────
    public class AddToCartResult
    {
        public bool Success { get; set; }
        public int CartCount { get; set; }
        public string? Message { get; set; }
    }

    // ─── Paginated items response ────────────────────────────────────────────────
    public class HomeItemsResponse
    {
        public List<EcommerceItemModel> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}