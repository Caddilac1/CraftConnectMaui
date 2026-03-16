using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.PageModels;

namespace CraftConnect_Mobile_App.Services
{
    // ── Internal DTOs for delivery address deserialisation ────────────
    // These are implementation details — only CartApiService uses them.
    // The public-facing type is DeliveryAddressOption (in PageModels).

    internal class DeliveryAddressesApiResponse
    {
        public bool Success { get; set; }
        public List<DeliveryAddressApiDto>? Data { get; set; }
    }

    internal class DeliveryAddressApiDto
    {
        public int StaffTownId { get; set; }
        public int TownId { get; set; }
        public string TownName { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Landmark { get; set; }
        public decimal DeliveryFee { get; set; }
        public double VatRate { get; set; }
    }

    public class CartApiService : ICartApiService
    {
        private readonly HttpClient _httpClient;

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public CartApiService(ApiConfig config)
        {
            var baseUrl = config.BaseUrl.TrimEnd('/');

#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(15)
            };

            Debug.WriteLine($"[CART API] Initialized. BaseUrl: {baseUrl}");
        }

        // ── Auth: per-request only — never touches DefaultRequestHeaders ──
        // Mutating DefaultRequestHeaders is a race condition when multiple
        // async calls run concurrently (e.g. badge refresh + cart load).

        private static async Task<string> GetTokenAsync()
        {
            try { return await SecureStorage.GetAsync("auth_token") ?? string.Empty; }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] Token read error: {ex.Message}");
                return string.Empty;
            }
        }

        private static HttpRequestMessage BuildRequest(
            HttpMethod method, string url, string token, HttpContent? body = null)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Content = body;
            return req;
        }

        private static StringContent JsonBody(object payload) =>
            new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        /// <summary>
        /// Unwraps ApiResponse&lt;T&gt;. Returns null if HTTP failed,
        /// if the response is HTML (SSL/auth error), or if Success == false.
        /// </summary>
        private static async Task<T?> ReadAsync<T>(HttpResponseMessage res)
        {
            if (!res.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[CART API] ❌ HTTP {(int)res.StatusCode}");
                return default;
            }

            var raw = await res.Content.ReadAsStringAsync();

            // Guard: dev cert / auth errors return HTML, not JSON
            if (raw.TrimStart().StartsWith('<'))
            {
                Debug.WriteLine("[CART API] ❌ Got HTML instead of JSON — SSL or auth issue");
                return default;
            }

            try
            {
                var wrapper = JsonSerializer.Deserialize<ApiResponseWrapper<T>>(raw, _json);
                return wrapper is { Success: true } ? wrapper.Data : default;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ Deserialise error: {ex.Message}");
                return default;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/cart
        // ═══════════════════════════════════════════════════════════════
        public async Task<CartDto?> GetCartAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                using var req = BuildRequest(HttpMethod.Get, "/api/cart", token);
                var res = await _httpClient.SendAsync(req);
                return await ReadAsync<CartDto>(res);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ GetCart: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/cart/count
        // ═══════════════════════════════════════════════════════════════
        public async Task<CartCountDto?> GetCartCountAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                using var req = BuildRequest(HttpMethod.Get, "/api/cart/count", token);
                var res = await _httpClient.SendAsync(req);
                return await ReadAsync<CartCountDto>(res);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ GetCartCount: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // POST /api/cart/items
        // ═══════════════════════════════════════════════════════════════
        public async Task<CartDto?> AddItemAsync(
            int? productCompanyBusinessLocationId, int? comboProductId, int quantity = 1)
        {
            try
            {
                var token = await GetTokenAsync();
                var body = JsonBody(new
                {
                    productCompanyBusinessLocationId,
                    comboProductId,
                    quantity
                });
                using var req = BuildRequest(HttpMethod.Post, "/api/cart/items", token, body);
                var res = await _httpClient.SendAsync(req);
                return await ReadAsync<CartDto>(res);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ AddItem: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PATCH /api/cart/items/{cartItemId}
        // ═══════════════════════════════════════════════════════════════
        public async Task<CartItemDto?> UpdateItemQuantityAsync(int cartItemId, int newQuantity)
        {
            try
            {
                var token = await GetTokenAsync();
                var body = JsonBody(new { quantity = newQuantity });
                using var req = BuildRequest(
                    HttpMethod.Patch, $"/api/cart/items/{cartItemId}", token, body);
                var res = await _httpClient.SendAsync(req);
                return await ReadAsync<CartItemDto>(res);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ UpdateItemQty: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DELETE /api/cart/items/{cartItemId}
        // ═══════════════════════════════════════════════════════════════
        public async Task<bool> RemoveItemAsync(int cartItemId)
        {
            try
            {
                var token = await GetTokenAsync();
                using var req = BuildRequest(
                    HttpMethod.Delete, $"/api/cart/items/{cartItemId}", token);
                var res = await _httpClient.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ RemoveItem: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // DELETE /api/cart
        // ═══════════════════════════════════════════════════════════════
        public async Task<bool> ClearCartAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                using var req = BuildRequest(HttpMethod.Delete, "/api/cart", token);
                var res = await _httpClient.SendAsync(req);
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ ClearCart: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // POST /api/cart/validate
        // ═══════════════════════════════════════════════════════════════
        public async Task<CartValidationDto?> ValidateCartAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                using var req = BuildRequest(HttpMethod.Post, "/api/cart/validate", token);
                var res = await _httpClient.SendAsync(req);
                return await ReadAsync<CartValidationDto>(res);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ ValidateCart: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // POST /api/ecommerceapi/orders/place
        // ═══════════════════════════════════════════════════════════════
        public async Task<bool> PlaceOrderAsync(
            string deliveryAddress,
            string paymentMethod,
            string? deliveryInstructions,
            string? paystackReference)
        {
            try
            {
                var token = await GetTokenAsync();
                var body = JsonBody(new
                {
                    DeliveryAddress = deliveryAddress,
                    PaymentMethod = paymentMethod,
                    DeliveryInstructions = deliveryInstructions ?? string.Empty,
                    PaystackReference = paystackReference ?? string.Empty
                });
                using var req = BuildRequest(
                    HttpMethod.Post, "/api/ecommerceapi/orders/place", token, body);
                var res = await _httpClient.SendAsync(req);

                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CART API] ❌ PlaceOrder {res.StatusCode}: {err}");
                    return false;
                }

                Debug.WriteLine("[CART API] ✅ Order placed");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ PlaceOrder: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /Staffs/GetDeliveryAddresses
        // ═══════════════════════════════════════════════════════════════
        public async Task<List<DeliveryAddressOption>> GetDeliveryAddressesAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                using var req = BuildRequest(
                    HttpMethod.Get, "/Staffs/GetDeliveryAddresses", token);
                var res = await _httpClient.SendAsync(req);

                if (!res.IsSuccessStatusCode) return new();

                var raw = await res.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<DeliveryAddressesApiResponse>(raw, _json);

                if (result?.Data == null) return new();

                return result.Data.ConvertAll(dto => new DeliveryAddressOption
                {
                    StaffTownId = dto.StaffTownId > 0 ? dto.StaffTownId : dto.TownId,
                    TownName = dto.TownName,
                    RegionName = dto.RegionName,
                    Address = dto.Address,
                    Landmark = dto.Landmark,
                    DeliveryFee = dto.DeliveryFee > 0 ? dto.DeliveryFee : 15.00m,
                    VatRate = dto.VatRate
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CART API] ❌ GetDeliveryAddresses: {ex.Message}");
                return new();
            }
        }

        // ── Internal wrapper for ApiResponse<T> deserialisation ───────
        private class ApiResponseWrapper<T>
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public T? Data { get; set; }
        }
    }
}