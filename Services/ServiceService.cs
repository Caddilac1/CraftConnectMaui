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
    // PRIVATE API RESPONSE MODELS  —  match backend JSON exactly
    // ServicesApiController  →  ApiResponse<T>
    // ═══════════════════════════════════════════════════════════════════════

    // ── Shared wrapper ───────────────────────────────────────────────────

    internal class ServiceApiResponse<T>
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public T? Data { get; set; }
    }

    // ── Paginated list  GET /api/services ────────────────────────────────

    internal class ServicePaginatedData
    {
        public List<ServiceSummaryApiDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
        public bool HasPreviousPage { get; set; }
    }

    internal class ServiceSummaryApiDto
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

    // ── Detail  GET /api/services/{id} ───────────────────────────────────

    internal class ServiceDetailApiDto
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public int ServiceId { get; set; }
        public int? CompanyBusinessId { get; set; }
        public int CompanyBusinessLocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SecondaryName { get; set; }
        public string? Description { get; set; }
        public string? StoreName { get; set; }
        public string? ServiceTypeName { get; set; }
        public decimal ServicePrice { get; set; }
        public decimal? PromotionalPrice { get; set; }
        public int? DiscountPercentage { get; set; }
        public List<string> Images { get; set; } = new();
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalBookings { get; set; }
        public List<ServiceReviewApiDto> Reviews { get; set; } = new();
        public List<ServiceScheduleDayApiDto> AvailableDays { get; set; } = new();
        public List<ServiceLocationSummaryApiDto> OtherLocations { get; set; } = new();
    }

    internal class ServiceReviewApiDto
    {
        public int ReviewId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Comment { get; set; }
        public double Rating { get; set; }
        public DateTime CommentDate { get; set; }
    }

    internal class ServiceScheduleDayApiDto
    {
        public string DayName { get; set; } = string.Empty;
        public int DayOrder { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    internal class ServiceLocationSummaryApiDto
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public int CompanyBusinessLocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public decimal ServicePrice { get; set; }
        public string? ThumbnailUrl { get; set; }
        public bool HasAvailability { get; set; }
    }

    // ── Availability  GET /api/services/{id}/availability ────────────────

    internal class ServiceAvailabilityApiDto
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public DateTime Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string? UnavailableReason { get; set; }
        public List<TimeSlotApiDto> AvailableSlots { get; set; } = new();
    }

    internal class TimeSlotApiDto
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsBooked { get; set; }
    }

    // ── Booking  POST /api/services/{id}/book ────────────────────────────

    internal class BookingApiDto
    {
        public int BookingId { get; set; }
        public int ServiceCompanyBusinessLocationId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string? StoreName { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime BookingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string BookingStatus { get; set; } = string.Empty;
        public string? PaymentStatus { get; set; }
        public string? CustomerName { get; set; }
        public decimal ServicePrice { get; set; }
    }

    // ── Location services  GET /api/services/location/{locationId} ───────

    internal class LocationServiceApiDto
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public int ServiceId { get; set; }
        public int CompanyBusinessLocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SecondaryName { get; set; }
        public string? Description { get; set; }
        public decimal ServicePrice { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC RESULT / DTO MODELS  —  consumed by PageModels
    // ═══════════════════════════════════════════════════════════════════════

    public class ServiceListResult
    {
        public List<ServiceItem> Items { get; set; } = new();
        public int TotalItems { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasNextPage { get; set; }
    }

    public class ServiceItem
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

        // Computed helpers for UI binding
        public string DisplayPrice => $"GH₵ {ServicePrice:F2}";
        public string RatingDisplay => $"{AverageRating:F1} ({TotalReviews})";
    }

    public class ServiceDetailResult
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public int ServiceId { get; set; }
        public int? CompanyBusinessId { get; set; }
        public int CompanyBusinessLocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SecondaryName { get; set; }
        public string? Description { get; set; }
        public string? StoreName { get; set; }
        public string? ServiceTypeName { get; set; }
        public decimal ServicePrice { get; set; }
        public decimal? PromotionalPrice { get; set; }
        public int? DiscountPercentage { get; set; }
        public List<string> Images { get; set; } = new();
        public double AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int TotalBookings { get; set; }
        public List<ServiceReviewResult> Reviews { get; set; } = new();
        public List<ServiceScheduleDayResult> AvailableDays { get; set; } = new();
        public List<ServiceLocationSummaryResult> OtherLocations { get; set; } = new();

        public decimal ActivePrice => PromotionalPrice ?? ServicePrice;
        public bool HasDiscount => PromotionalPrice.HasValue && PromotionalPrice < ServicePrice;
    }

    public class ServiceReviewResult
    {
        public int ReviewId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Comment { get; set; }
        public double Rating { get; set; }
        public DateTime CommentDate { get; set; }
    }

    public class ServiceScheduleDayResult
    {
        public string DayName { get; set; } = string.Empty;
        public int DayOrder { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string DisplayHours => $"{StartTime:hh\\:mm} – {EndTime:hh\\:mm}";
    }

    public class ServiceLocationSummaryResult
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public int CompanyBusinessLocationId { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public decimal ServicePrice { get; set; }
        public string? ThumbnailUrl { get; set; }
        public bool HasAvailability { get; set; }
    }

    public class ServiceAvailabilityResult
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public DateTime Date { get; set; }
        public string DayName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public string? UnavailableReason { get; set; }
        public List<TimeSlotResult> AvailableSlots { get; set; } = new();
    }

    public class TimeSlotResult
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public bool IsBooked { get; set; }
        public bool IsAvailable => !IsBooked;
        public string DisplayTime => $"{StartTime:hh\\:mm} – {EndTime:hh\\:mm}";
    }

    public class BookingResult
    {
        public int BookingId { get; set; }
        public int ServiceCompanyBusinessLocationId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string? StoreName { get; set; }
        public string? ThumbnailUrl { get; set; }
        public DateTime BookingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string BookingStatus { get; set; } = string.Empty;
        public string? PaymentStatus { get; set; }
        public string? CustomerName { get; set; }
        public decimal ServicePrice { get; set; }
        public string DisplaySlot => $"{BookingDate:dd MMM yyyy}  {StartTime:hh\\:mm} – {EndTime:hh\\:mm}";
    }

    public class CreateBookingRequest
    {
        public DateTime BookingDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string CustomerName { get; set; } = string.Empty;
    }

    public class LocationServiceItem
    {
        public int ServiceCompanyBusinessLocationId { get; set; }
        public int ServiceId { get; set; }
        public int CompanyBusinessLocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SecondaryName { get; set; }
        public string? Description { get; set; }
        public decimal ServicePrice { get; set; }
        public string DisplayPrice => $"GH₵ {ServicePrice:F2}";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INTERFACE
    // ═══════════════════════════════════════════════════════════════════════

    public interface IServiceService
    {
        // ── Public (no auth) ─────────────────────────────────────────────
        Task<ServiceListResult> GetServicesAsync(
            int page = 1, int pageSize = 20,
            int? serviceTypeId = null, string? search = null,
            decimal? minPrice = null, decimal? maxPrice = null,
            string sortBy = "popular");

        Task<ServiceDetailResult?> GetServiceDetailAsync(int serviceCompanyBusinessLocationId);

        Task<List<ServiceItem>> SearchServicesAsync(string query, int limit = 10);

        Task<List<ServiceItem>> GetFeaturedServicesAsync(int limit = 6);

        Task<ServiceListResult> GetServicesByTypeAsync(
            int serviceTypeId, int page = 1, int pageSize = 20, string sortBy = "popular");

        Task<ServiceAvailabilityResult?> GetAvailabilityAsync(
            int serviceCompanyBusinessLocationId, DateTime date);

        Task<List<LocationServiceItem>> GetServicesByLocationAsync(int locationId);

        // ── Protected (auth required) ────────────────────────────────────
        Task<BookingResult?> CreateBookingAsync(
            int serviceCompanyBusinessLocationId, CreateBookingRequest request);

        Task<List<BookingResult>> GetMyBookingsAsync();

        Task<BookingResult?> CancelBookingAsync(int bookingId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IMPLEMENTATION
    // ═══════════════════════════════════════════════════════════════════════

    public class ServiceService : IServiceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ServiceService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');

            // ── Platform HTTP handler with SSL bypass for dev ────────────
            // TODO: Remove the custom validation callback before production.
            //       Replace with proper cert pinning or remove the handler override.
#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SERVICE SSL] Host: {message.RequestUri?.Host}, Errors: {errors}");
                    return true;
                }
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SERVICE SSL] Host: {message.RequestUri?.Host}, Errors: {errors}");
                    return true;
                }
            };
#endif
            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[SERVICE SERVICE] Initialized. BaseUrl: {_baseUrl}");
        }

        // ── Auth helper ──────────────────────────────────────────────────
        // Reads the JWT from SecureStorage per-request.
        // Never mutates DefaultRequestHeaders — safe for concurrent calls.

        private static async Task<string> GetTokenAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token") ?? string.Empty;
                Debug.WriteLine($"[SERVICE SERVICE] 🔑 Token present: {!string.IsNullOrEmpty(token)}, length: {token.Length}");
                return token;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ Token read error: {ex.Message}");
                return string.Empty;
            }
        }

        // Builds a per-request message with Authorization header set inline.
        // This is the only correct pattern for a singleton HttpClient.
        private static HttpRequestMessage BuildRequest(
            HttpMethod method, string url, string? token = null, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = content;
            return request;
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/services
        // Paginated list — public, no auth needed.
        // ═══════════════════════════════════════════════════════════════

        public async Task<ServiceListResult> GetServicesAsync(
            int page = 1, int pageSize = 20,
            int? serviceTypeId = null, string? search = null,
            decimal? minPrice = null, decimal? maxPrice = null,
            string sortBy = "popular")
        {
            try
            {
                var url = BuildServicesUrl(page, pageSize, serviceTypeId, search, minPrice, maxPrice, sortBy);
                Debug.WriteLine($"[SERVICE SERVICE] 📡 GetServices → {url}");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync(url);
                sw.Stop();
                Debug.WriteLine($"[SERVICE SERVICE] 📥 {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[SERVICE SERVICE] ❌ GetServices failed: {response.StatusCode}");
                    return new ServiceListResult();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<ServicePaginatedData>>(json, _jsonOptions);

                if (result?.Data == null) return new ServiceListResult();

                return new ServiceListResult
                {
                    Items      = result.Data.Items.Select(MapToServiceItem).ToList(),
                    TotalItems = result.Data.TotalCount,
                    Page       = result.Data.Page,
                    PageSize   = result.Data.PageSize,
                    TotalPages = result.Data.TotalPages,
                    HasNextPage = result.Data.HasNextPage
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ GetServices exception: {ex.Message}");
                return new ServiceListResult();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/services/{id}
        // Full detail — public, no auth needed.
        // ═══════════════════════════════════════════════════════════════

        public async Task<ServiceDetailResult?> GetServiceDetailAsync(int id)
        {
            try
            {
                Debug.WriteLine($"[SERVICE SERVICE] 📡 GetServiceDetail id={id}");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync($"/api/services/{id}");
                sw.Stop();
                Debug.WriteLine($"[SERVICE SERVICE] 📥 {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<ServiceDetailApiDto>>(json, _jsonOptions);

                return result?.Data == null ? null : MapToServiceDetail(result.Data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ GetServiceDetail exception: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/services/search?q=...&limit=...
        // Live search — public, no auth needed.
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<ServiceItem>> SearchServicesAsync(string query, int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                    return new List<ServiceItem>();

                var url = $"/api/services/search?q={Uri.EscapeDataString(query.Trim())}&limit={limit}";
                Debug.WriteLine($"[SERVICE SERVICE] 📡 Search → {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<ServiceItem>();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<List<ServiceSummaryApiDto>>>(json, _jsonOptions);

                return result?.Data?.Select(MapToServiceItem).ToList() ?? new List<ServiceItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ Search exception: {ex.Message}");
                return new List<ServiceItem>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/services/featured?limit=...
        // Featured/home screen — public, no auth needed.
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<ServiceItem>> GetFeaturedServicesAsync(int limit = 6)
        {
            try
            {
                Debug.WriteLine($"[SERVICE SERVICE] 📡 GetFeatured limit={limit}");

                var response = await _httpClient.GetAsync($"/api/services/featured?limit={limit}");
                if (!response.IsSuccessStatusCode) return new List<ServiceItem>();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<List<ServiceSummaryApiDto>>>(json, _jsonOptions);

                return result?.Data?.Select(MapToServiceItem).ToList() ?? new List<ServiceItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ GetFeatured exception: {ex.Message}");
                return new List<ServiceItem>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/services/type/{serviceTypeId}
        // By category — public, no auth needed.
        // ═══════════════════════════════════════════════════════════════

        public async Task<ServiceListResult> GetServicesByTypeAsync(
            int serviceTypeId, int page = 1, int pageSize = 20, string sortBy = "popular")
        {
            try
            {
                var url = $"/api/services/type/{serviceTypeId}?page={page}&pageSize={pageSize}&sortBy={sortBy}";
                Debug.WriteLine($"[SERVICE SERVICE] 📡 GetByType → {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new ServiceListResult();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<ServicePaginatedData>>(json, _jsonOptions);

                if (result?.Data == null) return new ServiceListResult();

                return new ServiceListResult
                {
                    Items       = result.Data.Items.Select(MapToServiceItem).ToList(),
                    TotalItems  = result.Data.TotalCount,
                    Page        = result.Data.Page,
                    PageSize    = result.Data.PageSize,
                    TotalPages  = result.Data.TotalPages,
                    HasNextPage = result.Data.HasNextPage
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ GetByType exception: {ex.Message}");
                return new ServiceListResult();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/services/{id}/availability?date=...
        // Slot availability — public, no auth needed.
        // ═══════════════════════════════════════════════════════════════

        public async Task<ServiceAvailabilityResult?> GetAvailabilityAsync(int id, DateTime date)
        {
            try
            {
                // ISO 8601 date — backend expects DateTime, this is unambiguous
                var url = $"/api/services/{id}/availability?date={date:yyyy-MM-dd}";
                Debug.WriteLine($"[SERVICE SERVICE] 📡 GetAvailability → {url}");

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<ServiceAvailabilityApiDto>>(json, _jsonOptions);

                return result?.Data == null ? null : MapToAvailability(result.Data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ GetAvailability exception: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/services/location/{locationId}
        // All services at a location — public, no auth needed.
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<LocationServiceItem>> GetServicesByLocationAsync(int locationId)
        {
            try
            {
                Debug.WriteLine($"[SERVICE SERVICE] 📡 GetByLocation id={locationId}");

                var response = await _httpClient.GetAsync($"/api/services/location/{locationId}");
                if (!response.IsSuccessStatusCode) return new List<LocationServiceItem>();

                var json = await response.Content.ReadAsStringAsync();

                // This endpoint returns a plain array, not wrapped in ApiResponse<T>
                var items = JsonSerializer.Deserialize<List<LocationServiceApiDto>>(json, _jsonOptions);

                return items?.Select(x => new LocationServiceItem
                {
                    ServiceCompanyBusinessLocationId = x.ServiceCompanyBusinessLocationId,
                    ServiceId                        = x.ServiceId,
                    CompanyBusinessLocationId        = x.CompanyBusinessLocationId,
                    Name                             = x.Name,
                    SecondaryName                    = x.SecondaryName,
                    Description                      = x.Description,
                    ServicePrice                     = x.ServicePrice
                }).ToList() ?? new List<LocationServiceItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ GetByLocation exception: {ex.Message}");
                return new List<LocationServiceItem>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // POST /api/services/{id}/book     🔒 AUTH REQUIRED
        //
        // Security: token read from SecureStorage per-request.
        //           Never stored in DefaultRequestHeaders.
        // ═══════════════════════════════════════════════════════════════

        public async Task<BookingResult?> CreateBookingAsync(int id, CreateBookingRequest bookingRequest)
        {
            try
            {
                var token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SERVICE SERVICE] ❌ CreateBooking — no auth token");
                    return null;
                }

                var payload = JsonSerializer.Serialize(new
                {
                    BookingDate  = bookingRequest.BookingDate.ToString("yyyy-MM-dd"),
                    StartTime    = bookingRequest.StartTime,
                    EndTime      = bookingRequest.EndTime,
                    CustomerName = bookingRequest.CustomerName
                });

                var body = new StringContent(payload, Encoding.UTF8, "application/json");
                using var request = BuildRequest(HttpMethod.Post, $"/api/services/{id}/book", token, body);

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);
                sw.Stop();
                Debug.WriteLine($"[SERVICE SERVICE] 📥 CreateBooking {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[SERVICE SERVICE] ❌ CreateBooking error body: {err}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<BookingApiDto>>(json, _jsonOptions);

                return result?.Data == null ? null : MapToBookingResult(result.Data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ CreateBooking exception: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET /api/services/my-bookings    🔒 AUTH REQUIRED
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<BookingResult>> GetMyBookingsAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SERVICE SERVICE] ❌ GetMyBookings — no auth token");
                    return new List<BookingResult>();
                }

                using var request = BuildRequest(HttpMethod.Get, "/api/services/my-bookings", token);

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);
                sw.Stop();
                Debug.WriteLine($"[SERVICE SERVICE] 📥 GetMyBookings {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode) return new List<BookingResult>();

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<List<BookingApiDto>>>(json, _jsonOptions);

                return result?.Data?.Select(MapToBookingResult).ToList() ?? new List<BookingResult>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ GetMyBookings exception: {ex.Message}");
                return new List<BookingResult>();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PUT /api/services/bookings/{id}/cancel    🔒 AUTH REQUIRED
        // ═══════════════════════════════════════════════════════════════

        public async Task<BookingResult?> CancelBookingAsync(int bookingId)
        {
            try
            {
                var token = await GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SERVICE SERVICE] ❌ CancelBooking — no auth token");
                    return null;
                }

                using var request = BuildRequest(
                    HttpMethod.Put, $"/api/services/bookings/{bookingId}/cancel", token);

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.SendAsync(request);
                sw.Stop();
                Debug.WriteLine($"[SERVICE SERVICE] 📥 CancelBooking {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[SERVICE SERVICE] ❌ CancelBooking error body: {err}");
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ServiceApiResponse<BookingApiDto>>(json, _jsonOptions);

                return result?.Data == null ? null : MapToBookingResult(result.Data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERVICE SERVICE] ❌ CancelBooking exception: {ex.Message}");
                return null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static string BuildServicesUrl(
            int page, int pageSize,
            int? serviceTypeId, string? search,
            decimal? minPrice, decimal? maxPrice,
            string sortBy)
        {
            var sb = new StringBuilder(
                $"/api/services?page={page}&pageSize={pageSize}&sortBy={sortBy}");

            if (serviceTypeId.HasValue)
                sb.Append($"&serviceTypeId={serviceTypeId.Value}");

            if (!string.IsNullOrWhiteSpace(search))
                sb.Append($"&search={Uri.EscapeDataString(search.Trim())}");

            if (minPrice.HasValue)
                sb.Append($"&minPrice={minPrice.Value}");

            if (maxPrice.HasValue)
                sb.Append($"&maxPrice={maxPrice.Value}");

            return sb.ToString();
        }

        private static ServiceItem MapToServiceItem(ServiceSummaryApiDto dto) => new()
        {
            ServiceCompanyBusinessLocationId = dto.ServiceCompanyBusinessLocationId,
            ServiceId                        = dto.ServiceId,
            CompanyBusinessLocationId        = dto.CompanyBusinessLocationId,
            Name                             = dto.Name,
            SecondaryName                    = dto.SecondaryName,
            Description                      = dto.Description,
            StoreName                        = dto.StoreName,
            ServiceTypeName                  = dto.ServiceTypeName,
            ServicePrice                     = dto.ServicePrice,
            AverageRating                    = dto.AverageRating,
            TotalReviews                     = dto.TotalReviews,
            TotalBookings                    = dto.TotalBookings,
            ThumbnailUrl                     = dto.ThumbnailUrl,
            HasAvailability                  = dto.HasAvailability,
            IsPopular                        = dto.IsPopular,
            IsFeatured                       = dto.IsFeatured
        };

        private static ServiceDetailResult MapToServiceDetail(ServiceDetailApiDto dto) => new()
        {
            ServiceCompanyBusinessLocationId = dto.ServiceCompanyBusinessLocationId,
            ServiceId                        = dto.ServiceId,
            CompanyBusinessId                = dto.CompanyBusinessId,
            CompanyBusinessLocationId        = dto.CompanyBusinessLocationId,
            Name                             = dto.Name,
            SecondaryName                    = dto.SecondaryName,
            Description                      = dto.Description,
            StoreName                        = dto.StoreName,
            ServiceTypeName                  = dto.ServiceTypeName,
            ServicePrice                     = dto.ServicePrice,
            PromotionalPrice                 = dto.PromotionalPrice,
            DiscountPercentage               = dto.DiscountPercentage,
            Images                           = dto.Images,
            AverageRating                    = dto.AverageRating,
            TotalReviews                     = dto.TotalReviews,
            TotalBookings                    = dto.TotalBookings,
            Reviews = dto.Reviews.Select(r => new ServiceReviewResult
            {
                ReviewId     = r.ReviewId,
                CustomerName = r.CustomerName,
                Title        = r.Title,
                Comment      = r.Comment,
                Rating       = r.Rating,
                CommentDate  = r.CommentDate
            }).ToList(),
            AvailableDays = dto.AvailableDays.Select(d => new ServiceScheduleDayResult
            {
                DayName   = d.DayName,
                DayOrder  = d.DayOrder,
                StartTime = d.StartTime,
                EndTime   = d.EndTime
            }).ToList(),
            OtherLocations = dto.OtherLocations.Select(ol => new ServiceLocationSummaryResult
            {
                ServiceCompanyBusinessLocationId = ol.ServiceCompanyBusinessLocationId,
                CompanyBusinessLocationId        = ol.CompanyBusinessLocationId,
                LocationName                     = ol.LocationName,
                ServicePrice                     = ol.ServicePrice,
                ThumbnailUrl                     = ol.ThumbnailUrl,
                HasAvailability                  = ol.HasAvailability
            }).ToList()
        };

        private static ServiceAvailabilityResult MapToAvailability(ServiceAvailabilityApiDto dto) => new()
        {
            ServiceCompanyBusinessLocationId = dto.ServiceCompanyBusinessLocationId,
            Date              = dto.Date,
            DayName           = dto.DayName,
            IsAvailable       = dto.IsAvailable,
            UnavailableReason = dto.UnavailableReason,
            AvailableSlots    = dto.AvailableSlots.Select(s => new TimeSlotResult
            {
                StartTime = s.StartTime,
                EndTime   = s.EndTime,
                IsBooked  = s.IsBooked
            }).ToList()
        };

        private static BookingResult MapToBookingResult(BookingApiDto dto) => new()
        {
            BookingId                        = dto.BookingId,
            ServiceCompanyBusinessLocationId = dto.ServiceCompanyBusinessLocationId,
            ServiceName                      = dto.ServiceName,
            StoreName                        = dto.StoreName,
            ThumbnailUrl                     = dto.ThumbnailUrl,
            BookingDate                      = dto.BookingDate,
            StartTime                        = dto.StartTime,
            EndTime                          = dto.EndTime,
            BookingStatus                    = dto.BookingStatus,
            PaymentStatus                    = dto.PaymentStatus,
            CustomerName                     = dto.CustomerName,
            ServicePrice                     = dto.ServicePrice
        };
    }
}
