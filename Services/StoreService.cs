using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE API RESPONSE MODELS
    // ═══════════════════════════════════════════════════════════════════════

    // ── Products  GET /api/products ──────────────────────────────────────

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

    internal class ProductListApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<ProductSummaryApiDto>? Data { get; set; }
    }

    // ── Services  GET /api/services ─────────────────────────────────────

    internal class StoreServicesApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public StoreServicesPaginatedData? Data { get; set; }
    }

    internal class StoreServicesPaginatedData
    {
        public List<StoreServiceSummaryApiDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    internal class StoreServiceSummaryApiDto
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public int ServiceId { get; set; }
        public int CompanyBusinessLocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SecondaryName { get; set; }
        public string? Description { get; set; }
        public string? StoreName { get; set; }
        public string? ServiceTypeName { get; set; }
        public decimal ServicePrice { get; set; }
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalBookings { get; set; }
        public string? ThumbnailUrl { get; set; }
        public bool HasAvailability { get; set; }
        public bool IsPopular { get; set; }
        public bool IsFeatured { get; set; }
    }

    internal class StoreServicesListApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public List<StoreServiceSummaryApiDto>? Data { get; set; }
    }

    // ── Categories ───────────────────────────────────────────────────────

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

    // ── Cart ─────────────────────────────────────────────────────────────

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

    // ═══════════════════════════════════════════════════════════════════════
    // STORE SERVICE
    // ═══════════════════════════════════════════════════════════════════════

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

        // ── Auth helper ──────────────────────────────────────────────────
        // Per-request token read — never mutates DefaultRequestHeaders.

        private static async Task<string> GetTokenAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token") ?? string.Empty;
                Debug.WriteLine($"[STORE SERVICE] 🔑 Token present: {!string.IsNullOrEmpty(token)}, length: {token.Length}");
                return token;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ Token read error: {ex.Message}");
                return string.Empty;
            }
        }

        private static HttpRequestMessage BuildAuthRequest(
            HttpMethod method, string url, string token, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;
            return request;
        }

        // ═══════════════════════════════════════════════════════════════
        // GET PRODUCTS + SERVICES (merged)
        //
        // Both API calls run in parallel via Task.WhenAll for speed.
        // Products come from  GET /api/products
        // Services come from  GET /api/services
        // They are merged into one list, products first then services,
        // both sorted by popularity from their respective APIs.
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
                Debug.WriteLine($"[STORE SERVICE] 📡 GetProductsAndServices page={page} search={search}");

                var productUrl = BuildProductsUrl(page, pageSize, search, categoryId, minPrice, maxPrice, sortBy);
                var serviceUrl = BuildServicesUrl(page, pageSize, search, minPrice, maxPrice, sortBy);

                // ── Parallel fetch — products and services at the same time ──
                var sw = Stopwatch.StartNew();
                var (productResponse, serviceResponse) = await FetchBothAsync(productUrl, serviceUrl);
                sw.Stop();
                Debug.WriteLine($"[STORE SERVICE] 📥 Both fetched in {sw.ElapsedMilliseconds}ms");

                // ── Parse products ────────────────────────────────────────
                var productItems = new List<StoreItem>();
                int totalProducts = 0;
                int totalPages = 1;
                bool hasNext = false;

                if (productResponse != null)
                {
                    var json = await productResponse.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<ProductsApiResponse>(json, _jsonOptions);
                    if (result?.Data != null)
                    {
                        productItems = result.Data.Items.Select(MapProductToStoreItem).ToList();
                        totalProducts = result.Data.TotalCount;
                        totalPages = result.Data.TotalPages;
                        hasNext = result.Data.HasNextPage;
                    }
                }

                // ── Parse services ────────────────────────────────────────
                var serviceItems = new List<StoreItem>();
                int totalServices = 0;

                if (serviceResponse != null)
                {
                    var json = await serviceResponse.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<StoreServicesApiResponse>(json, _jsonOptions);
                    if (result?.Data != null)
                    {
                        serviceItems = result.Data.Items.Select(MapServiceToStoreItem).ToList();
                        totalServices = result.Data.TotalCount;
                    }
                }

                // ── Merge: products first, then services ──────────────────
                var merged = productItems.Concat(serviceItems).ToList();

                Debug.WriteLine($"[STORE SERVICE] ✅ Merged: {productItems.Count} products + {serviceItems.Count} services = {merged.Count} total");

                return new StoreProductsResult
                {
                    Items = merged,
                    TotalItems = totalProducts + totalServices,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages,
                    HasNextPage = hasNext
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ GetProductsAndServices exception: {ex.Message}");
                return new StoreProductsResult();
            }
        }

        // Runs both HTTP GET calls in parallel and returns both responses.
        // If either fails it returns null for that one — the other still renders.
        private async Task<(HttpResponseMessage? products, HttpResponseMessage? services)> FetchBothAsync(
            string productUrl, string serviceUrl)
        {
            var productTask = SafeGetAsync(productUrl);
            var serviceTask = SafeGetAsync(serviceUrl);

            await Task.WhenAll(productTask, serviceTask);

            return (await productTask, await serviceTask);
        }

        private async Task<HttpResponseMessage?> SafeGetAsync(string url)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                Debug.WriteLine($"[STORE SERVICE] 📥 {url} → {(int)response.StatusCode}");
                return response.IsSuccessStatusCode ? response : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ SafeGet {url}: {ex.Message}");
                return null;
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
                if (!response.IsSuccessStatusCode) return new List<StoreItem>();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ProductListApiResponse>(json, _jsonOptions);
                return result?.Data?.Select(MapProductToStoreItem).ToList() ?? new List<StoreItem>();
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
                if (!response.IsSuccessStatusCode) return new List<StoreItem>();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ProductListApiResponse>(json, _jsonOptions);
                return result?.Data?.Select(MapProductToStoreItem).ToList() ?? new List<StoreItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ GetPromotions exception: {ex.Message}");
                return new List<StoreItem>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET CATEGORIES  →  GET /api/ecommerceapi/categories
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<StoreCategoryDto>> GetCategoriesAsync()
        {
            try
            {
                Debug.WriteLine("[STORE SERVICE] 📡 GetCategories");
                var response = await _httpClient.GetAsync("/api/ecommerceapi/categories");
                if (!response.IsSuccessStatusCode) return new List<StoreCategoryDto>();

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
        // CART OPERATIONS  (products only — services use booking flow)
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> AddToCartAsync(int productId, int quantity)
        {
            try
            {
                var token = await GetTokenAsync();
                var payload = JsonSerializer.Serialize(new { ProductId = productId, Quantity = quantity });
                var body = new StringContent(payload, Encoding.UTF8, "application/json");
                using var request = BuildAuthRequest(HttpMethod.Post, "/api/ecommerceapi/cart/add", token, body);
                var response = await _httpClient.SendAsync(request);

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

        public async Task<bool> UpdateCartItemAsync(int cartItemId, int quantity)
        {
            try
            {
                var token = await GetTokenAsync();
                var payload = JsonSerializer.Serialize(new { CartItemId = cartItemId, Quantity = quantity });
                var body = new StringContent(payload, Encoding.UTF8, "application/json");
                using var request = BuildAuthRequest(HttpMethod.Put, "/api/ecommerceapi/cart/update", token, body);
                var response = await _httpClient.SendAsync(request);
                Debug.WriteLine($"[STORE SERVICE] UpdateCartItem {cartItemId} → {(int)response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ UpdateCartItem exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveFromCartAsync(int cartItemId)
        {
            try
            {
                var token = await GetTokenAsync();
                using var request = BuildAuthRequest(HttpMethod.Delete, $"/api/ecommerceapi/cart/remove/{cartItemId}", token);
                var response = await _httpClient.SendAsync(request);
                Debug.WriteLine($"[STORE SERVICE] RemoveFromCart {cartItemId} → {(int)response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[STORE SERVICE] ❌ RemoveFromCart exception: {ex.Message}");
                return false;
            }
        }

        public async Task<StoreCartResult?> GetCartAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                Debug.WriteLine($"[STORE SERVICE] 📡 GetCart — token present: {!string.IsNullOrEmpty(token)}");
                using var request = BuildAuthRequest(HttpMethod.Get, "/api/ecommerceapi/cart", token);
                var response = await _httpClient.SendAsync(request);
                Debug.WriteLine($"[STORE SERVICE] 📥 GetCart response: {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode) return null;

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

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static string BuildProductsUrl(
            int page, int pageSize, string? search,
            int? categoryId, decimal? minPrice, decimal? maxPrice, string sortBy)
        {
            var sb = new StringBuilder(
                $"/api/products?page={page}&pageSize={pageSize}&sortBy={sortBy}");

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

        private static string BuildServicesUrl(
            int page, int pageSize, string? search,
            decimal? minPrice, decimal? maxPrice, string sortBy)
        {
            var sb = new StringBuilder(
                $"/api/services?page={page}&pageSize={pageSize}&sortBy={sortBy}");

            if (!string.IsNullOrWhiteSpace(search))
                sb.Append($"&search={Uri.EscapeDataString(search)}");
            if (minPrice.HasValue)
                sb.Append($"&minPrice={minPrice.Value}");
            if (maxPrice.HasValue)
                sb.Append($"&maxPrice={maxPrice.Value}");

            return sb.ToString();
        }

        // Maps a product API response to the unified StoreItem.
        // ✅ ApiProductId uses dto.ProductId (the real PK the invoice
        //    controller looks up with _context.Product.FindAsync).
        //    ProductCompanyBusinessLocationId is the junction-table ID
        //    used only for cart operations — do NOT use it for invoices.
        private static StoreItem MapProductToStoreItem(ProductSummaryApiDto dto) => new()
        {
            Id = MakeGuid(dto.ProductCompanyBusinessLocationId),
            ApiProductId = dto.ProductId,                          // ✅ real ProductId for invoice line items
            ApiServiceId = 0,
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            Price = dto.PromotionalPrice ?? dto.SellingPrice,
            OriginalPrice = dto.PromotionalPrice.HasValue ? dto.SellingPrice : null,
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

        // Maps a service API response to the unified StoreItem
        private static StoreItem MapServiceToStoreItem(StoreServiceSummaryApiDto dto) => new()
        {
            Id = MakeGuid(dto.ServiceCompanyBusinessLocationId),
            ApiProductId = 0,
            ApiServiceId = dto.ServiceCompanyBusinessLocationId,
            Name = dto.Name,
            Description = dto.Description ?? string.Empty,
            Price = dto.ServicePrice,
            OriginalPrice = null,
            ImageUrl = dto.ThumbnailUrl ?? string.Empty,
            Category = dto.ServiceTypeName ?? string.Empty,
            Type = StoreItemType.Service,
            SellerName = dto.StoreName ?? string.Empty,
            Rating = dto.AverageRating,
            ReviewCount = dto.TotalReviews,
            StockQuantity = null,       // services have no stock
            RequiresQuote = false,
            Duration = null             // pulled on detail page if needed
        };

        // Stable Guid from an int — same pattern as original StoreService
        private static Guid MakeGuid(int id) =>
            new(id.ToString().PadLeft(32, '0')
                .Insert(8, "-").Insert(13, "-")
                .Insert(18, "-").Insert(23, "-"));
    }
}