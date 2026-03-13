using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    // ═══════════════════════════════════════════════════════════════
    // PRIVATE API RESPONSE MODELS — match backend JSON exactly
    // ═══════════════════════════════════════════════════════════════

    // ProductsApiController shape  →  ApiResponse<PaginatedResult<ProductSummaryDto>>
    internal class ProductsApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public ProductsPaginatedData? Data { get; set; }
    }

    internal class ProductsPaginatedData
    {
        public List<ProductSummaryApiDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    internal class ProductSummaryApiDto
    {
        public int ProductCompanyBusinessLocationId { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SecondaryName { get; set; }
        public string? Description { get; set; }
        public string? Manufacturer { get; set; }
        public string? StoreName { get; set; }
        public string? ProductTypeName { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal? PromotionalPrice { get; set; }
        public int? DiscountPercentage { get; set; }
        public int QuantityOnHand { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public string? ThumbnailUrl { get; set; }
        public bool IsPopular { get; set; }
        public bool IsFeatured { get; set; }
    }

    // ProductsApiController list endpoints  →  ApiResponse<List<ProductSummaryDto>>
    internal class ProductListApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ProductSummaryApiDto>? Data { get; set; }
    }

    // EcommerceApiController categories  →  { Success, Data: [...] }
    internal class CategoriesApiResponse
    {
        public bool Success { get; set; }
        public List<CategoryApiDto>? Data { get; set; }
    }

    internal class CategoryApiDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public int ProductCount { get; set; }
    }

    // EcommerceApiController cart
    internal class CartApiResponse
    {
        public bool Success { get; set; }
        public CartDataDto? Data { get; set; }
    }

    internal class CartDataDto
    {
        public List<CartItemApiDto> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public int ItemCount { get; set; }
        public int CartId { get; set; }
    }

    internal class CartItemApiDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? ProductImage { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public string? CompanyBusinessName { get; set; }
        public bool IsInStock { get; set; }
    }

    internal class CartActionResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // STORE SERVICE
    // ═══════════════════════════════════════════════════════════════

    public class StoreService : IStoreService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public StoreService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');

#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[STORE SSL] Host: {message.RequestUri?.Host}, Errors: {errors}");
                    return true;
                }
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[STORE SSL] Host: {message.RequestUri?.Host}, Errors: {errors}");
                    return true;
                }
            };
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[STORE SERVICE] Initialized. BaseUrl: {_baseUrl}");
        }

        // ── Auth header ───────────────────────────────────────────────

        private async Task SetAuthHeaderAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (!string.IsNullOrEmpty(token))
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", token);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ Auth header error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET PRODUCTS  →  GET /api/products
        // Uses the cleaner ProductsApiController
        // ═══════════════════════════════════════════════════════════════

        public async Task<StoreProductsResult> GetProductsAsync(
            int page = 1,
            int pageSize = 20,
            string? search = null,
            int? categoryId = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string sortBy = "popular")
        {
            try
            {
                Debug.WriteLine($"[STORE SERVICE] 📡 GetProducts page={page} search={search}");

                var url = BuildProductsUrl(page, pageSize, search, categoryId, minPrice, maxPrice, sortBy);

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(url);
                sw.Stop();

                Debug.WriteLine($"[STORE SERVICE] 📥 {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[STORE SERVICE] ❌ GetProducts failed: {response.StatusCode}");
                    return new StoreProductsResult();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ProductsApiResponse>(json, _jsonOptions);

                if (result?.Data == null)
                    return new StoreProductsResult();

                return new StoreProductsResult
                {
                    Items = result.Data.Items.Select(MapToStoreItem).ToList(),
                    TotalItems = result.Data.TotalCount,
                    Page = result.Data.Page,
                    PageSize = result.Data.PageSize,
                    TotalPages = result.Data.TotalPages,
                    HasNextPage = result.Data.HasNextPage
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ GetProducts exception: {ex.Message}");
                return new StoreProductsResult();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET FEATURED  →  GET /api/products/featured
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<StoreItem>> GetFeaturedProductsAsync(int limit = 8)
        {
            try
            {
                Debug.WriteLine($"[STORE SERVICE] 📡 GetFeatured limit={limit}");

                var response = await _httpClient.GetAsync($"/api/products/featured?limit={limit}");

                if (!response.IsSuccessStatusCode)
                    return new List<StoreItem>();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ProductListApiResponse>(json, _jsonOptions);

                return result?.Data?.Select(MapToStoreItem).ToList() ?? new List<StoreItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ GetFeatured exception: {ex.Message}");
                return new List<StoreItem>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET PROMOTIONS  →  GET /api/products/promotions
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<StoreItem>> GetPromotionsAsync(int limit = 10)
        {
            try
            {
                Debug.WriteLine($"[STORE SERVICE] 📡 GetPromotions limit={limit}");

                var response = await _httpClient.GetAsync($"/api/products/promotions?limit={limit}");

                if (!response.IsSuccessStatusCode)
                    return new List<StoreItem>();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ProductListApiResponse>(json, _jsonOptions);

                return result?.Data?.Select(MapToStoreItem).ToList() ?? new List<StoreItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ GetPromotions exception: {ex.Message}");
                return new List<StoreItem>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET CATEGORIES  →  GET /api/ecommerce/categories
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<StoreCategoryDto>> GetCategoriesAsync()
        {
            try
            {
                Debug.WriteLine("[STORE SERVICE] 📡 GetCategories");

                var response = await _httpClient.GetAsync("/api/ecommerceapi/categories");

                if (!response.IsSuccessStatusCode)
                    return new List<StoreCategoryDto>();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CategoriesApiResponse>(json, _jsonOptions);

                return result?.Data?.Select(c => new StoreCategoryDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    ImageUrl = c.ImageUrl,
                    ProductCount = c.ProductCount
                }).ToList() ?? new List<StoreCategoryDto>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ GetCategories exception: {ex.Message}");
                return new List<StoreCategoryDto>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ADD TO CART  →  POST /api/ecommerce/cart/add
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> AddToCartAsync(int productId, int quantity)
        {
            try
            {
                await SetAuthHeaderAsync();

                var payload = JsonSerializer.Serialize(new { ProductId = productId, Quantity = quantity });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("/api/ecommerceapi/cart/add", content);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[STORE SERVICE] ❌ AddToCart failed {response.StatusCode}: {err}");
                    return false;
                }

                Debug.WriteLine($"[STORE SERVICE] ✅ Added product {productId} x{quantity} to cart");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ AddToCart exception: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE CART ITEM  →  PUT /api/ecommerce/cart/update
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> UpdateCartItemAsync(int cartItemId, int quantity)
        {
            try
            {
                await SetAuthHeaderAsync();

                var payload = JsonSerializer.Serialize(new { CartItemId = cartItemId, Quantity = quantity });
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync("/api/ecommerceapi/cart/update", content);

                Debug.WriteLine($"[STORE SERVICE] UpdateCartItem {cartItemId} → {(int)response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ UpdateCartItem exception: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // REMOVE FROM CART  →  DELETE /api/ecommerce/cart/remove/{id}
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> RemoveFromCartAsync(int cartItemId)
        {
            try
            {
                await SetAuthHeaderAsync();

                var response = await _httpClient.DeleteAsync($"/api/ecommerceapi/cart/remove/{cartItemId}");

                Debug.WriteLine($"[STORE SERVICE] RemoveFromCart {cartItemId} → {(int)response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ RemoveFromCart exception: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET CART  →  GET /api/ecommerce/cart
        // ═══════════════════════════════════════════════════════════════

        public async Task<StoreCartResult?> GetCartAsync()
        {
            try
            {
                await SetAuthHeaderAsync();

                var response = await _httpClient.GetAsync("/api/ecommerceapi/cart");

                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<CartApiResponse>(json, _jsonOptions);

                if (result?.Data == null) return null;

                return new StoreCartResult
                {
                    TotalAmount = result.Data.TotalAmount,
                    ItemCount = result.Data.ItemCount,
                    CartId = result.Data.CartId,
                    Items = result.Data.Items.Select(ci => new StoreCartItemDto
                    {
                        Id = ci.Id,
                        ProductId = ci.ProductId,
                        ProductName = ci.ProductName ?? string.Empty,
                        ProductImage = ci.ProductImage,
                        UnitPrice = ci.UnitPrice,
                        Quantity = ci.Quantity,
                        TotalPrice = ci.TotalPrice,
                        CompanyBusinessName = ci.CompanyBusinessName,
                        IsInStock = ci.IsInStock
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ GetCart exception: {ex.Message}");
                return null;
            }
        }

        // ── Private helpers ───────────────────────────────────────────

        private static string BuildProductsUrl(
            int page, int pageSize, string? search,
            int? categoryId, decimal? minPrice, decimal? maxPrice, string sortBy)
        {
            var sb = new StringBuilder($"/api/products?page={page}&pageSize={pageSize}&sortBy={sortBy}");

            if (!string.IsNullOrWhiteSpace(search))
                sb.Append($"&search={Uri.EscapeDataString(search)}");

            if (categoryId.HasValue)
                sb.Append($"&categoryId={categoryId.Value}");

            if (minPrice.HasValue)
                sb.Append($"&minPrice={minPrice.Value}");

            if (maxPrice.HasValue)
                sb.Append($"&maxPrice={maxPrice.Value}");

            return sb.ToString();
        }

        /// <summary>
        /// Maps a backend ProductSummaryApiDto to the mobile StoreItem model.
        /// StoreItemType is always Product here — services come from a separate
        /// endpoint and will be added when that API is ready.
        /// </summary>
        private static StoreItem MapToStoreItem(ProductSummaryApiDto dto) => new()
        {
            // Use the location ID as the stable identifier for cart/order calls
            Id = new Guid(dto.ProductCompanyBusinessLocationId.ToString()
                               .PadLeft(32, '0').Insert(8, "-").Insert(13, "-")
                               .Insert(18, "-").Insert(23, "-")),
            ApiProductId = dto.ProductCompanyBusinessLocationId,
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            Price = dto.PromotionalPrice ?? dto.SellingPrice,
            OriginalPrice = dto.PromotionalPrice.HasValue ? dto.SellingPrice : (decimal?)null,
            ImageUrl = dto.ThumbnailUrl ?? string.Empty,
            Category = dto.ProductTypeName ?? string.Empty,
            Type = StoreItemType.Product,
            SellerName = dto.StoreName ?? string.Empty,
            Rating = dto.AverageRating,
            ReviewCount = dto.TotalReviews,
            StockQuantity = dto.QuantityOnHand,
            RequiresQuote = false,
            Duration = null
        };
    }
}