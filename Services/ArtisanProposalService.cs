using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace CraftConnect_Mobile_App.Services
{
    public class ArtisanProposalService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly AuthService _authService;

        // ── Corrected base path ────────────────────────────────────────
        // The controller class is named ArtisanProposalsApiController,
        // so [Route("api/[controller]")] resolves to "api/artisanproposalsapi".
        private const string BasePath = "/api/artisanproposalsapi";

        public ArtisanProposalService(ApiConfig config, AuthService authService)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');
            _authService = authService;

            Debug.WriteLine($"[PROPOSAL SERVICE] BaseUrl: '{_baseUrl}'");

#if ANDROID
            Debug.WriteLine($"[PROPOSAL SERVICE] Platform: ANDROID — using AndroidMessageHandler");
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SSL CALLBACK] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#else
            Debug.WriteLine($"[PROPOSAL SERVICE] Platform: OTHER — using HttpClientHandler");
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[SSL CALLBACK] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[PROPOSAL SERVICE] Initialized. BaseAddress: {_httpClient.BaseAddress}");
        }

        // ============================================================
        // GET MY PROPOSALS  →  GET /api/artisanproposalsapi
        // ============================================================
        public async Task<ProposalListResult> GetMyProposalsAsync()
        {
            Debug.WriteLine($"\n[PROPOSALS/GET_ALL START]");

            try
            {
                await SetAuthHeadersAsync();

                var endpoint = BasePath;
                Debug.WriteLine($"[PROPOSALS/GET_ALL] GET {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.GetAsync(endpoint);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/GET_ALL] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[PROPOSALS/GET_ALL] Message: {httpEx.Message}");
                    Debug.WriteLine($"[PROPOSALS/GET_ALL] InnerException: {httpEx.InnerException?.Message}");
                    return ProposalListResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/GET_ALL] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return ProposalListResult.Fail("Request timed out. Check your network connection.");
                }

                sw.Stop();
                Debug.WriteLine($"[PROPOSALS/GET_ALL] ✅ Response in {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PROPOSALS/GET_ALL] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _authService.LogoutAsync();
                    return ProposalListResult.Fail("Session expired. Please login again.");
                }

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<ApiProposalResponse<List<ArtisanProposalDto>>>(body);
                    if (result?.Data != null)
                        return ProposalListResult.Ok(result.Data);
                }

                var error = TryParseApiError(body);
                return ProposalListResult.Fail(error ?? $"Failed to load proposals ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROPOSALS/GET_ALL] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return ProposalListResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // GET SINGLE PROPOSAL  →  GET /api/artisanproposalsapi/{id}
        // ============================================================
        public async Task<ProposalResult> GetProposalAsync(string id)
        {
            Debug.WriteLine($"\n[PROPOSALS/GET_ONE START] Id: {id}");

            try
            {
                await SetAuthHeadersAsync();

                var endpoint = $"{BasePath}/{id}";
                Debug.WriteLine($"[PROPOSALS/GET_ONE] GET {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.GetAsync(endpoint);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/GET_ONE] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return ProposalResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/GET_ONE] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return ProposalResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[PROPOSALS/GET_ONE] ✅ Response in {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PROPOSALS/GET_ONE] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _authService.LogoutAsync();
                    return ProposalResult.Fail("Session expired. Please login again.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return ProposalResult.Fail("Proposal not found or you don't have permission to view it.");

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<ApiProposalResponse<ArtisanProposalDto>>(body);
                    if (result?.Data != null)
                        return ProposalResult.Ok(result.Data);
                }

                var error = TryParseApiError(body);
                return ProposalResult.Fail(error ?? $"Failed to load proposal ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROPOSALS/GET_ONE] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return ProposalResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // GET PROPOSALS BY FEED  →  GET /api/artisanproposalsapi/feed/{userFeedId}
        // ============================================================
        public async Task<ProposalListResult> GetProposalsByFeedAsync(string userFeedId)
        {
            Debug.WriteLine($"\n[PROPOSALS/GET_BY_FEED START] FeedId: {userFeedId}");

            try
            {
                await SetAuthHeadersAsync();

                var endpoint = $"{BasePath}/feed/{userFeedId}";
                Debug.WriteLine($"[PROPOSALS/GET_BY_FEED] GET {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.GetAsync(endpoint);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/GET_BY_FEED] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return ProposalListResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/GET_BY_FEED] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return ProposalListResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[PROPOSALS/GET_BY_FEED] ✅ Response in {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PROPOSALS/GET_BY_FEED] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _authService.LogoutAsync();
                    return ProposalListResult.Fail("Session expired. Please login again.");
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                    return ProposalListResult.Fail("You don't have permission to view proposals for this feed.");

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return ProposalListResult.Fail("Feed not found.");

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<ApiProposalResponse<List<ArtisanProposalDto>>>(body);
                    if (result?.Data != null)
                        return ProposalListResult.Ok(result.Data);
                }

                var error = TryParseApiError(body);
                return ProposalListResult.Fail(error ?? $"Failed to load proposals ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROPOSALS/GET_BY_FEED] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return ProposalListResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // CREATE PROPOSAL  →  POST /api/artisanproposalsapi
        // ============================================================
        public async Task<ProposalResult> CreateProposalAsync(CreateProposalServiceRequest request)
        {
            Debug.WriteLine($"\n[PROPOSALS/CREATE START] FeedId: {request.UserFeedId}, Price: {request.ProposedPrice}");

            try
            {
                await SetAuthHeadersAsync();

                var endpoint = BasePath;
                Debug.WriteLine($"[PROPOSALS/CREATE] POST {_baseUrl}{endpoint}");

                var content = BuildMultipartContent(request);

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.PostAsync(endpoint, content);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/CREATE] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    Debug.WriteLine($"[PROPOSALS/CREATE] InnerException: {httpEx.InnerException?.Message}");
                    return ProposalResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/CREATE] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return ProposalResult.Fail("Request timed out. Check your network connection.");
                }

                sw.Stop();
                Debug.WriteLine($"[PROPOSALS/CREATE] ✅ Response in {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PROPOSALS/CREATE] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _authService.LogoutAsync();
                    return ProposalResult.Fail("Session expired. Please login again.");
                }

                if (response.StatusCode == HttpStatusCode.Created || response.IsSuccessStatusCode)
                {
                    var result = Deserialize<ApiProposalResponse<ArtisanProposalDto>>(body);
                    if (result?.Data != null)
                        return ProposalResult.Ok(result.Data, result.Message);
                }

                var error = TryParseApiError(body);
                return ProposalResult.Fail(error ?? $"Failed to submit proposal ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROPOSALS/CREATE] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                Debug.WriteLine($"[PROPOSALS/CREATE] StackTrace: {ex.StackTrace}");
                return ProposalResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // UPDATE PROPOSAL  →  PUT /api/artisanproposalsapi/{id}
        // ============================================================
        public async Task<ProposalResult> UpdateProposalAsync(string id, UpdateProposalServiceRequest request)
        {
            Debug.WriteLine($"\n[PROPOSALS/UPDATE START] Id: {id}, Price: {request.ProposedPrice}");

            try
            {
                await SetAuthHeadersAsync();

                var endpoint = $"{BasePath}/{id}";
                Debug.WriteLine($"[PROPOSALS/UPDATE] PUT {_baseUrl}{endpoint}");

                var content = BuildMultipartContent(request);

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.PutAsync(endpoint, content);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/UPDATE] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return ProposalResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/UPDATE] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return ProposalResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[PROPOSALS/UPDATE] ✅ Response in {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PROPOSALS/UPDATE] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _authService.LogoutAsync();
                    return ProposalResult.Fail("Session expired. Please login again.");
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                    return ProposalResult.Fail("Only pending proposals can be edited.");

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return ProposalResult.Fail("Proposal not found or you don't have permission to edit it.");

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<ApiProposalResponse<ArtisanProposalDto>>(body);
                    if (result?.Data != null)
                        return ProposalResult.Ok(result.Data, result.Message);
                }

                var error = TryParseApiError(body);
                return ProposalResult.Fail(error ?? $"Failed to update proposal ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROPOSALS/UPDATE] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return ProposalResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // DELETE PROPOSAL  →  DELETE /api/artisanproposalsapi/{id}
        // ============================================================
        public async Task<ProposalActionResult> DeleteProposalAsync(string id)
        {
            Debug.WriteLine($"\n[PROPOSALS/DELETE START] Id: {id}");

            try
            {
                await SetAuthHeadersAsync();

                var endpoint = $"{BasePath}/{id}";
                Debug.WriteLine($"[PROPOSALS/DELETE] DELETE {_baseUrl}{endpoint}");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.DeleteAsync(endpoint);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/DELETE] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return ProposalActionResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/DELETE] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return ProposalActionResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[PROPOSALS/DELETE] ✅ Response in {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PROPOSALS/DELETE] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _authService.LogoutAsync();
                    return ProposalActionResult.Fail("Session expired. Please login again.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return ProposalActionResult.Fail("Proposal not found or you don't have permission to delete it.");

                if (response.IsSuccessStatusCode)
                    return ProposalActionResult.Ok("Proposal deleted successfully.");

                var error = TryParseApiError(body);
                return ProposalActionResult.Fail(error ?? $"Failed to delete proposal ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROPOSALS/DELETE] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return ProposalActionResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // UPDATE STATUS  →  PATCH /api/artisanproposalsapi/{id}/status
        // ============================================================
        public async Task<ProposalResult> UpdateProposalStatusAsync(string id, string status)
        {
            Debug.WriteLine($"\n[PROPOSALS/STATUS START] Id: {id}, Status: {status}");

            try
            {
                await SetAuthHeadersAsync();

                var endpoint = $"{BasePath}/{id}/status";
                Debug.WriteLine($"[PROPOSALS/STATUS] PATCH {_baseUrl}{endpoint}");

                var payload = new UpdateProposalStatusPayload { Status = status };
                var json = Serialize(payload);
                Debug.WriteLine($"[PROPOSALS/STATUS] Serialized JSON: {json}");

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var sw = Stopwatch.StartNew();
                HttpResponseMessage response;

                try
                {
                    var patchRequest = new HttpRequestMessage(HttpMethod.Patch, endpoint) { Content = content };
                    response = await _httpClient.SendAsync(patchRequest);
                }
                catch (HttpRequestException httpEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/STATUS] ❌ HttpRequestException after {sw.ElapsedMilliseconds}ms: {httpEx.Message}");
                    return ProposalResult.Fail("Network error. Check your connection.");
                }
                catch (TaskCanceledException tcEx)
                {
                    sw.Stop();
                    Debug.WriteLine($"[PROPOSALS/STATUS] ❌ Timeout after {sw.ElapsedMilliseconds}ms: {tcEx.Message}");
                    return ProposalResult.Fail("Request timed out.");
                }

                sw.Stop();
                Debug.WriteLine($"[PROPOSALS/STATUS] ✅ Response in {sw.ElapsedMilliseconds}ms — Status: {(int)response.StatusCode}");

                var body = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[PROPOSALS/STATUS] Body: {body}");

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await _authService.LogoutAsync();
                    return ProposalResult.Fail("Session expired. Please login again.");
                }

                if (response.StatusCode == HttpStatusCode.Forbidden)
                    return ProposalResult.Fail("Only the feed owner can accept or reject proposals.");

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return ProposalResult.Fail("Proposal not found.");

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var error = TryParseApiError(body);
                    return ProposalResult.Fail(error ?? "Invalid status value.");
                }

                if (response.IsSuccessStatusCode)
                {
                    var result = Deserialize<ApiProposalResponse<ArtisanProposalDto>>(body);
                    if (result?.Data != null)
                        return ProposalResult.Ok(result.Data, result.Message);
                }

                var err = TryParseApiError(body);
                return ProposalResult.Fail(err ?? $"Failed to update status ({(int)response.StatusCode})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PROPOSALS/STATUS] ❌ Unexpected: {ex.GetType().FullName}: {ex.Message}");
                return ProposalResult.Fail($"Unexpected error: {ex.Message}");
            }
        }

        // ============================================================
        // PRIVATE HELPERS
        // ============================================================

        /// <summary>
        /// Retrieves the saved JWT token and attaches it as a Bearer Authorization header.
        /// Clears all previous headers to avoid stale values.
        /// </summary>
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
                Debug.WriteLine($"[PROPOSAL SERVICE] Bearer token attached. Length: {token.Length}");
            }
            else
            {
                Debug.WriteLine($"[PROPOSAL SERVICE] ⚠️ No token found — request will likely return 401");
            }
        }

        private bool IsNoProfileError(string errorMessage)
        {
            if (string.IsNullOrEmpty(errorMessage)) return false;

            // Check for the exact error message from your backend
            return errorMessage.Contains("create an artisan profile", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("need to create an artisan profile", StringComparison.OrdinalIgnoreCase) ||
                   errorMessage.Contains("no artisan profile", StringComparison.OrdinalIgnoreCase);
        }


        /// <summary>
        /// Builds a multipart/form-data body for the Create endpoint.
        /// </summary>
        private static MultipartFormDataContent BuildMultipartContent(CreateProposalServiceRequest r)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(r.UserFeedId), "UserFeedId");
            form.Add(new StringContent(r.ProposedPrice.ToString("F2")), "ProposedPrice");
            form.Add(new StringContent(r.EstimatedDuration.ToString("yyyy-MM-dd")), "EstimatedDuration");
            form.Add(new StringContent(r.Message), "Message");

            if (!string.IsNullOrEmpty(r.TermsConditions))
                form.Add(new StringContent(r.TermsConditions), "TermsConditions");

            if (!string.IsNullOrEmpty(r.PaymentTerms))
                form.Add(new StringContent(r.PaymentTerms), "PaymentTerms");

            if (r.QuoteDocumentBytes != null && r.QuoteDocumentBytes.Length > 0 && !string.IsNullOrEmpty(r.QuoteDocumentFileName))
            {
                var fileContent = new ByteArrayContent(r.QuoteDocumentBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    GetMimeType(r.QuoteDocumentFileName));
                form.Add(fileContent, "QuoteDocument", r.QuoteDocumentFileName);
                Debug.WriteLine($"[PROPOSAL SERVICE] Quote document attached: {r.QuoteDocumentFileName} ({r.QuoteDocumentBytes.Length} bytes)");
            }

            return form;
        }

        /// <summary>
        /// Builds a multipart/form-data body for the Update endpoint.
        /// </summary>
        private static MultipartFormDataContent BuildMultipartContent(UpdateProposalServiceRequest r)
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(r.UserFeedId), "UserFeedId");
            form.Add(new StringContent(r.ProposedPrice.ToString("F2")), "ProposedPrice");
            form.Add(new StringContent(r.EstimatedDuration.ToString("yyyy-MM-dd")), "EstimatedDuration");
            form.Add(new StringContent(r.Message), "Message");
            form.Add(new StringContent(r.RemoveQuoteDocument.ToString().ToLower()), "RemoveQuoteDocument");

            if (!string.IsNullOrEmpty(r.TermsConditions))
                form.Add(new StringContent(r.TermsConditions), "TermsConditions");

            if (!string.IsNullOrEmpty(r.PaymentTerms))
                form.Add(new StringContent(r.PaymentTerms), "PaymentTerms");

            if (r.QuoteDocumentBytes != null && r.QuoteDocumentBytes.Length > 0 && !string.IsNullOrEmpty(r.QuoteDocumentFileName))
            {
                var fileContent = new ByteArrayContent(r.QuoteDocumentBytes);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                    GetMimeType(r.QuoteDocumentFileName));
                form.Add(fileContent, "QuoteDocument", r.QuoteDocumentFileName);
                Debug.WriteLine($"[PROPOSAL SERVICE] Quote document attached: {r.QuoteDocumentFileName} ({r.QuoteDocumentBytes.Length} bytes)");
            }

            return form;
        }

        private static string GetMimeType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            };
        }

        private static string? TryParseApiError(string body)
        {
            try
            {
                var response = JsonSerializer.Deserialize<ApiProposalResponse<object>>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (!string.IsNullOrEmpty(response?.Message) && response.Message != "string")
                    return response.Message;
            }
            catch { /* ignored */ }

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

    // ============================================================
    // API RESPONSE ENVELOPE  (mirrors server ApiProposalResponse<T>)
    // ============================================================

    public class ApiProposalResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public object? Errors { get; set; }
    }

    // ============================================================
    // DTO  (mirrors server ArtisanProposalDto)
    // ============================================================

    public class ArtisanProposalDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserFeedId { get; set; } = string.Empty;
        public string? UserFeedTitle { get; set; }
        public string? ArtisanProfileId { get; set; }
        public string? ArtisanBusinessName { get; set; }
        public decimal ProposedPrice { get; set; }
        public DateTime EstimatedDuration { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TermsConditions { get; set; }
        public string? PaymentTerms { get; set; }
        public string? QuoteDocument { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? AcceptedAt { get; set; }
    }

    // ============================================================
    // SERVICE REQUEST MODELS
    // ============================================================

    /// <summary>
    /// Used when submitting a new proposal.
    /// Attach QuoteDocumentBytes + QuoteDocumentFileName to include a file.
    /// </summary>
    public class CreateProposalServiceRequest
    {
        public string UserFeedId { get; set; } = string.Empty;
        public decimal ProposedPrice { get; set; }
        public DateTime EstimatedDuration { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TermsConditions { get; set; }
        public string? PaymentTerms { get; set; }

        // Optional file attachment
        public byte[]? QuoteDocumentBytes { get; set; }
        public string? QuoteDocumentFileName { get; set; }
    }

    /// <summary>
    /// Used when editing an existing pending proposal.
    /// Set RemoveQuoteDocument = true to clear the existing file without uploading a new one.
    /// </summary>
    public class UpdateProposalServiceRequest
    {
        public string UserFeedId { get; set; } = string.Empty;
        public decimal ProposedPrice { get; set; }
        public DateTime EstimatedDuration { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? TermsConditions { get; set; }
        public string? PaymentTerms { get; set; }
        public bool RemoveQuoteDocument { get; set; }

        // Optional replacement file
        public byte[]? QuoteDocumentBytes { get; set; }
        public string? QuoteDocumentFileName { get; set; }
    }

    // Internal PATCH payload — not exposed outside the service
    internal class UpdateProposalStatusPayload
    {
        public string Status { get; set; } = string.Empty;
    }

    // ============================================================
    // RESULT TYPES
    // ============================================================

    /// <summary>Result for operations that return a single proposal.</summary>
    public class ProposalResult
    {
        public bool Success { get; private set; }
        public ArtisanProposalDto? Proposal { get; private set; }
        public string? Message { get; private set; }
        public string? Error { get; private set; }

        public static ProposalResult Ok(ArtisanProposalDto proposal, string? message = null) =>
            new() { Success = true, Proposal = proposal, Message = message };

        public static ProposalResult Fail(string error) =>
            new() { Success = false, Error = error };
    }

    /// <summary>Result for operations that return a list of proposals.</summary>
    public class ProposalListResult
    {
        public bool Success { get; private set; }
        public List<ArtisanProposalDto>? Proposals { get; private set; }
        public string? Error { get; private set; }

        public static ProposalListResult Ok(List<ArtisanProposalDto> proposals) =>
            new() { Success = true, Proposals = proposals };

        public static ProposalListResult Fail(string error) =>
            new() { Success = false, Error = error };
    }

    /// <summary>Result for fire-and-done operations (delete, etc.).</summary>
    public class ProposalActionResult
    {
        public bool Success { get; private set; }
        public string? Message { get; private set; }
        public string? Error { get; private set; }

        public static ProposalActionResult Ok(string message) =>
            new() { Success = true, Message = message };

        public static ProposalActionResult Fail(string error) =>
            new() { Success = false, Error = error };
    }
}