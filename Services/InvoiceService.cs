using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CraftConnect_Mobile_App.Services
{
    // ══════════════════════════════════════════════════════════════════════
    // INVOICE SERVICE
    // Pattern mirrors ArtisanProposalService exactly.
    // API route: api/invoices  (InvoicesApiController)
    // ══════════════════════════════════════════════════════════════════════

    public class InvoiceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly AuthService _authService;

        private const string BasePath = "/api/invoices";

        public InvoiceService(ApiConfig config, AuthService authService)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');
            _authService = authService;

            Debug.WriteLine($"[INVOICE SERVICE] BaseUrl: '{_baseUrl}'");

#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[INVOICE SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[INVOICE SSL] Host: {message.RequestUri?.Host}, Errors: {errors}");
                    return true;
                }
            };
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[INVOICE SERVICE] Initialized. BaseAddress: {_httpClient.BaseAddress}");
        }

        // ══════════════════════════════════════════════════════════════
        // GET MY INVOICES  →  GET /api/invoices
        // ══════════════════════════════════════════════════════════════

        public async Task<InvoiceListResult> GetMyInvoicesAsync()
        {
            Debug.WriteLine("\n[INVOICES/GET_ALL START]");
            try
            {
                await SetAuthHeadersAsync();
                Debug.WriteLine($"[INVOICES/GET_ALL] GET {_baseUrl}{BasePath}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;
                try { response = await _httpClient.GetAsync(BasePath); }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/GET_ALL] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    Debug.WriteLine($"[INVOICES/GET_ALL] InnerException: {httpEx.InnerException?.Message}");
                    return InvoiceListResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/GET_ALL] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return InvoiceListResult.Fail("Request timed out. Check your network connection.");
                }

                sw.Stop();
                Debug.WriteLine($"[INVOICES/GET_ALL] ✅ {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[INVOICES/GET_ALL] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                { await _authService.LogoutAsync(); return InvoiceListResult.Fail("Session expired. Please login again."); }

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<InvoiceApiResponse<List<InvoiceSummaryDto>>>(body);
                    if (result?.Data != null) return InvoiceListResult.Ok(result.Data);
                }
                return InvoiceListResult.Fail(TryParseApiError(body) ?? $"Failed to load invoices ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[INVOICES/GET_ALL] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return InvoiceListResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // GET INVOICE DETAIL  →  GET /api/invoices/{id}
        // ══════════════════════════════════════════════════════════════

        public async Task<InvoiceDetailResult> GetInvoiceAsync(string id)
        {
            Debug.WriteLine($"\n[INVOICES/GET_ONE START] Id: {id}");
            try
            {
                await SetAuthHeadersAsync();
                var endpoint = $"{BasePath}/{id}";
                Debug.WriteLine($"[INVOICES/GET_ONE] GET {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;
                try { response = await _httpClient.GetAsync(endpoint); }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/GET_ONE] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return InvoiceDetailResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/GET_ONE] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return InvoiceDetailResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[INVOICES/GET_ONE] ✅ {sw.ElapsedMilliseconds}ms — {(int)response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[INVOICES/GET_ONE] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                { await _authService.LogoutAsync(); return InvoiceDetailResult.Fail("Session expired. Please login again."); }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return InvoiceDetailResult.Fail("Invoice not found.");

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<InvoiceApiResponse<InvoiceDetailDto>>(body);
                    if (result?.Data != null) return InvoiceDetailResult.Ok(result.Data);
                }
                return InvoiceDetailResult.Fail(TryParseApiError(body) ?? $"Failed to load invoice ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[INVOICES/GET_ONE] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return InvoiceDetailResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // CREATE INVOICE  →  POST /api/invoices
        // ══════════════════════════════════════════════════════════════

        public async Task<InvoiceSummaryResult> CreateInvoiceAsync(CreateInvoiceRequest request)
        {
            Debug.WriteLine($"\n[INVOICES/CREATE START] FeedId: {request.UserFeedId}, Items: {request.LineItems.Count}");
            try
            {
                await SetAuthHeadersAsync();
                Debug.WriteLine($"[INVOICES/CREATE] POST {_baseUrl}{BasePath}");

                var json = Serialize(request);
                Debug.WriteLine($"[INVOICES/CREATE] JSON: {json}");
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;
                try { response = await _httpClient.PostAsync(BasePath, content); }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/CREATE] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    Debug.WriteLine($"[INVOICES/CREATE] InnerException: {httpEx.InnerException?.Message}");
                    return InvoiceSummaryResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/CREATE] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return InvoiceSummaryResult.Fail("Request timed out. Check your network connection.");
                }

                sw.Stop();
                Debug.WriteLine($"[INVOICES/CREATE] ✅ {sw.ElapsedMilliseconds}ms — {(int)response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[INVOICES/CREATE] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                { await _authService.LogoutAsync(); return InvoiceSummaryResult.Fail("Session expired. Please login again."); }

                if (response.StatusCode == HttpStatusCode.Created || response.IsSuccessStatusCode)
                {
                    var result = Deserialize<InvoiceApiResponse<InvoiceSummaryDto>>(body);
                    if (result?.Data != null) return InvoiceSummaryResult.Ok(result.Data, result.Message);
                }
                return InvoiceSummaryResult.Fail(TryParseApiError(body) ?? $"Failed to create invoice ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[INVOICES/CREATE] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[INVOICES/CREATE] StackTrace: {ex.StackTrace}");
                return InvoiceSummaryResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // GENERATE PDF  →  POST /api/invoices/{id}/generate
        // ══════════════════════════════════════════════════════════════

        public async Task<InvoiceActionResult> GeneratePdfAsync(string id)
        {
            Debug.WriteLine($"\n[INVOICES/GENERATE START] Id: {id}");
            try
            {
                await SetAuthHeadersAsync();
                var endpoint = $"{BasePath}/{id}/generate";
                Debug.WriteLine($"[INVOICES/GENERATE] POST {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;
                try { response = await _httpClient.PostAsync(endpoint, null); }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/GENERATE] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return InvoiceActionResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/GENERATE] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return InvoiceActionResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[INVOICES/GENERATE] ✅ {sw.ElapsedMilliseconds}ms — {(int)response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[INVOICES/GENERATE] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                { await _authService.LogoutAsync(); return InvoiceActionResult.Fail("Session expired. Please login again."); }

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<InvoiceApiResponse<string>>(body);
                    return InvoiceActionResult.Ok(result?.Message ?? "PDF generated.", result?.Data);
                }
                return InvoiceActionResult.Fail(TryParseApiError(body) ?? $"PDF generation failed ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[INVOICES/GENERATE] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return InvoiceActionResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // DOWNLOAD PDF  →  GET /api/invoices/{id}/download
        // Returns raw PDF bytes for saving/sharing on device.
        // ══════════════════════════════════════════════════════════════

        public async Task<InvoiceDownloadResult> DownloadPdfAsync(string id)
        {
            Debug.WriteLine($"\n[INVOICES/DOWNLOAD START] Id: {id}");
            try
            {
                await SetAuthHeadersAsync();
                var endpoint = $"{BasePath}/{id}/download";
                Debug.WriteLine($"[INVOICES/DOWNLOAD] GET {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;
                try { response = await _httpClient.GetAsync(endpoint); }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/DOWNLOAD] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return InvoiceDownloadResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/DOWNLOAD] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return InvoiceDownloadResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[INVOICES/DOWNLOAD] ✅ {sw.ElapsedMilliseconds}ms — {(int)response.StatusCode}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                { await _authService.LogoutAsync(); return InvoiceDownloadResult.Fail("Session expired. Please login again."); }

                if (response.IsSuccessStatusCode)
                {
                    var bytes    = await response.Content.ReadAsByteArrayAsync();
                    var fileName = response.Content.Headers.ContentDisposition?.FileName ?? $"Invoice-{id}.pdf";
                    fileName     = fileName.Trim('"');
                    Debug.WriteLine($"[INVOICES/DOWNLOAD] ✅ PDF bytes: {bytes.Length}, FileName: {fileName}");
                    return InvoiceDownloadResult.Ok(bytes, fileName);
                }
                return InvoiceDownloadResult.Fail($"Download failed ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[INVOICES/DOWNLOAD] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return InvoiceDownloadResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SUBMIT INVOICE  →  POST /api/invoices/{id}/submit
        // Marks invoice as sent + notifies feed owner.
        // ══════════════════════════════════════════════════════════════

        public async Task<InvoiceActionResult> SubmitInvoiceAsync(string id)
        {
            Debug.WriteLine($"\n[INVOICES/SUBMIT START] Id: {id}");
            try
            {
                await SetAuthHeadersAsync();
                var endpoint = $"{BasePath}/{id}/submit";
                Debug.WriteLine($"[INVOICES/SUBMIT] POST {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;
                try { response = await _httpClient.PostAsync(endpoint, null); }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/SUBMIT] ❌ {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return InvoiceActionResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[INVOICES/SUBMIT] ❌ Timeout {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return InvoiceActionResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[INVOICES/SUBMIT] ✅ {sw.ElapsedMilliseconds}ms — {(int)response.StatusCode}");
                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[INVOICES/SUBMIT] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                { await _authService.LogoutAsync(); return InvoiceActionResult.Fail("Session expired. Please login again."); }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                    return InvoiceActionResult.Fail("You don't have permission to submit this invoice.");

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return InvoiceActionResult.Fail("Invoice not found.");

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<InvoiceApiResponse<string>>(body);
                    return InvoiceActionResult.Ok(result?.Message ?? "Invoice submitted successfully.");
                }
                return InvoiceActionResult.Fail(TryParseApiError(body) ?? $"Submit failed ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[INVOICES/SUBMIT] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return InvoiceActionResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS  (mirrors ArtisanProposalService exactly)
        // ══════════════════════════════════════════════════════════════

        private async Task SetAuthHeadersAsync()
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "Mobile");

            var token = await _authService.GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                Debug.WriteLine($"[INVOICE SERVICE] Bearer token attached. Length: {token.Length}");
            }
            else
            {
                Debug.WriteLine("[INVOICE SERVICE] ⚠️ No token found — request will likely return 401");
            }
        }

        private static string? TryParseApiError(string body)
        {
            try
            {
                var r = Deserialize<InvoiceApiResponse<object>>(body);
                if (!string.IsNullOrEmpty(r?.Message) && r.Message != "string")
                    return r.Message;
            }
            catch { }
            return null;
        }

        private static string Serialize<T>(T obj) =>
            JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

        private static T? Deserialize<T>(string json) =>
            JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }

    // ══════════════════════════════════════════════════════════════════
    // API RESPONSE ENVELOPE
    // ══════════════════════════════════════════════════════════════════

    public class InvoiceApiResponse<T>
    {
        public bool    Success { get; set; }
        public string  Message { get; set; } = string.Empty;
        public T?      Data    { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════
    // DTOs
    // ══════════════════════════════════════════════════════════════════

    public class InvoiceSummaryDto
    {
        public string   Id               { get; set; } = string.Empty;
        public string   InvoiceNumber    { get; set; } = string.Empty;
        public int      RevisionNumber   { get; set; }
        public string   Status           { get; set; } = string.Empty;
        public decimal  GrandTotal       { get; set; }
        public string   Currency         { get; set; } = "GHS";
        public bool     IsArchived       { get; set; }
        public DateTime CreatedAt        { get; set; }
        public string?  UserFeedId       { get; set; }
        public string?  GeneratedPdfPath { get; set; }
    }

    public class InvoiceDetailDto : InvoiceSummaryDto
    {
        public decimal ProductsSubtotal { get; set; }
        public decimal? OverallDiscountPercent { get; set; }
        public decimal OverallDiscountAmount { get; set; }
        public decimal FinalProductsTotal { get; set; }
        public decimal Workmanship { get; set; }
        public string? Notes { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? ArtisanProposalId { get; set; }
        public List<InvoiceLineItemDto> LineItems { get; set; } = new();  // keep name
    }

    public class InvoiceLineItemDto   // make sure properties match API's LineItemDto exactly
    {
        public string Id { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineSubtotal { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal LineTotal { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════
    // REQUEST MODELS
    // ══════════════════════════════════════════════════════════════════

    public class CreateInvoiceRequest
    {
        public string?  UserFeedId             { get; set; }
        public string?  ArtisanProposalId      { get; set; }
        public decimal  Workmanship            { get; set; }
        public decimal? OverallDiscountPercent { get; set; }
        public string?  Notes                  { get; set; }
        public List<InvoiceLineItemRequest> LineItems { get; set; } = new();
    }

    public class UpdateInvoiceRequest
    {
        public decimal  Workmanship            { get; set; }
        public decimal? OverallDiscountPercent { get; set; }
        public string?  Notes                  { get; set; }
        public List<InvoiceLineItemRequest> LineItems { get; set; } = new();
    }

    public class InvoiceLineItemRequest
    {
        public int      ProductId       { get; set; }
        public int      Quantity        { get; set; }
        public decimal? DiscountPercent { get; set; }
    }

    // ══════════════════════════════════════════════════════════════════
    // RESULT TYPES
    // ══════════════════════════════════════════════════════════════════

    public class InvoiceListResult
    {
        public bool    Success  { get; private set; }
        public List<InvoiceSummaryDto>? Invoices { get; private set; }
        public string? Error    { get; private set; }

        public static InvoiceListResult Ok(List<InvoiceSummaryDto> invoices) =>
            new() { Success = true, Invoices = invoices };
        public static InvoiceListResult Fail(string error) =>
            new() { Success = false, Error = error };
    }

    public class InvoiceDetailResult
    {
        public bool    Success  { get; private set; }
        public InvoiceDetailDto? Invoice { get; private set; }
        public string? Error    { get; private set; }

        public static InvoiceDetailResult Ok(InvoiceDetailDto invoice) =>
            new() { Success = true, Invoice = invoice };
        public static InvoiceDetailResult Fail(string error) =>
            new() { Success = false, Error = error };
    }

    public class InvoiceSummaryResult
    {
        public bool    Success  { get; private set; }
        public InvoiceSummaryDto? Invoice { get; private set; }
        public string? Message  { get; private set; }
        public string? Error    { get; private set; }

        public static InvoiceSummaryResult Ok(InvoiceSummaryDto invoice, string? message = null) =>
            new() { Success = true, Invoice = invoice, Message = message };
        public static InvoiceSummaryResult Fail(string error) =>
            new() { Success = false, Error = error };
    }

    public class InvoiceActionResult
    {
        public bool    Success  { get; private set; }
        public string? Message  { get; private set; }
        public string? Data     { get; private set; }
        public string? Error    { get; private set; }

        public static InvoiceActionResult Ok(string message, string? data = null) =>
            new() { Success = true, Message = message, Data = data };
        public static InvoiceActionResult Fail(string error) =>
            new() { Success = false, Error = error };
    }

    public class InvoiceDownloadResult
    {
        public bool    Success  { get; private set; }
        public byte[]? PdfBytes { get; private set; }
        public string? FileName { get; private set; }
        public string? Error    { get; private set; }

        public static InvoiceDownloadResult Ok(byte[] bytes, string fileName) =>
            new() { Success = true, PdfBytes = bytes, FileName = fileName };
        public static InvoiceDownloadResult Fail(string error) =>
            new() { Success = false, Error = error };
    }
}
