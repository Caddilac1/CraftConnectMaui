using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CraftConnect_Mobile_App.Services
{
    /// <summary>
    /// Service for handling AI Feed Chat API communication
    /// </summary>
    public class AiFeedChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public AiFeedChatService()
        {
            // Create our own HttpClient with custom handler (like ChatService does)
            var handler = new HttpClientHandler();

#if DEBUG
            // Allow self-signed certificates and HTTP in development
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) => true;

            // For local development - USE HTTP (not HTTPS) for local IP addresses
            _baseUrl = "https://192.168.29.112:7023/api/ai/feed-chat";

            // Alternative options based on your setup:
            // _baseUrl = "http://10.0.2.2:7023/api/ai/feed-chat"; // Android Emulator -> localhost on host PC
            // _baseUrl = "http://localhost:7023/api/ai/feed-chat"; // iOS Simulator
            // _baseUrl = "http://192.168.1.x:7023/api/ai/feed-chat"; // Replace x with your PC's local IP
#else
            // For production - use your actual HTTPS API
            _baseUrl = "https://your-production-api.com/api/ai/feed-chat";
#endif

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(60) // Longer timeout for AI responses
            };

            Debug.WriteLine($"[AI FEED SERVICE] Initialized with base URL: {_baseUrl}");
        }

        /// <summary>
        /// Sets the authorization token for API requests
        /// </summary>
        public void SetAuthToken(string token)
        {
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
                Debug.WriteLine("[AI FEED SERVICE] ✅ Auth token set");
            }
        }

        /// <summary>
        /// Get token from SecureStorage and set it in the HTTP client
        /// </summary>
        private async Task<bool> SetAuthHeaderAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");

                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[AI FEED SERVICE] ❌ No auth token found in SecureStorage");
                    return false;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                Debug.WriteLine($"[AI FEED SERVICE] ✅ Auth header set. Token length: {token.Length}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI FEED SERVICE] ❌ Error setting auth header: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Tests connection to the API
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                Debug.WriteLine($"[AI FEED SERVICE] Testing connection to: {_baseUrl}");
                var response = await _httpClient.GetAsync(_baseUrl.Replace("/api/ai/feed-chat", "/api/health"));
                Debug.WriteLine($"[AI FEED SERVICE] Connection test result: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI FEED SERVICE] ❌ Connection test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sends a chat message to the AI and gets a response
        /// </summary>
        public async Task<ChatApiResponse?> SendMessageAsync(Guid sessionId, string message)
        {
            try
            {
                // Set auth header from SecureStorage before making request
                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var request = new
                {
                    sessionId = sessionId,
                    message = message
                };

                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine($"[AI FEED SERVICE] Sending message to: {_baseUrl}/message");
                Debug.WriteLine($"[AI FEED SERVICE] SessionId: {sessionId}");
                Debug.WriteLine($"[AI FEED SERVICE] Message: {message}");

                var response = await _httpClient.PostAsync($"{_baseUrl}/message", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AI FEED SERVICE] Response Status: {(int)response.StatusCode}");
                Debug.WriteLine($"[AI FEED SERVICE] Response: {responseJson}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ChatApiResponse>(responseJson,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    return result;
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine($"[AI FEED SERVICE] ❌ Unauthorized - Token may be expired");
                    SecureStorage.Remove("auth_token");
                    throw new UnauthorizedAccessException("Session expired. Please login again.");
                }
                else
                {
                    Debug.WriteLine($"[AI FEED SERVICE] ❌ Error: {response.StatusCode} - {responseJson}");
                    return null;
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[AI FEED SERVICE] ❌ HTTP Error: {ex.Message}");
                Debug.WriteLine($"[AI FEED SERVICE] Inner Exception: {ex.InnerException?.Message}");
                throw new Exception("Network error. Please check your connection.", ex);
            }
            catch (TaskCanceledException ex)
            {
                Debug.WriteLine($"[AI FEED SERVICE] ❌ Timeout: {ex.Message}");
                throw new Exception("Request timed out. Please try again.", ex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI FEED SERVICE] ❌ Unexpected error: {ex.Message}");
                Debug.WriteLine($"[AI FEED SERVICE] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Uploads a file (invoice or document) to the server
        /// </summary>
        public async Task<FileUploadResponse?> UploadFileAsync(
            Guid sessionId,
            Stream fileStream,
            string fileName,
            string fileType)
        {
            try
            {
                // Set auth header before making request
                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                using var content = new MultipartFormDataContent();

                // Add session ID
                content.Add(new StringContent(sessionId.ToString()), "sessionId");

                // Add file type (invoice or document)
                content.Add(new StringContent(fileType), "fileType");

                // Add file
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                content.Add(fileContent, "file", fileName);

                Debug.WriteLine($"[AI FEED SERVICE] Uploading file: {fileName}");
                Debug.WriteLine($"[AI FEED SERVICE] File type: {fileType}");

                var response = await _httpClient.PostAsync($"{_baseUrl}/upload", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AI FEED SERVICE] Upload response: {responseJson}");

                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<FileUploadResponse>(responseJson,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine($"[AI FEED SERVICE] ❌ Upload unauthorized");
                    SecureStorage.Remove("auth_token");
                    throw new UnauthorizedAccessException("Session expired. Please login again.");
                }
                else
                {
                    Debug.WriteLine($"[AI FEED SERVICE] ❌ Upload failed: {response.StatusCode}");
                    return null;
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI FEED SERVICE] ❌ Upload error: {ex.Message}");
                throw new Exception("Failed to upload file. Please try again.", ex);
            }
        }

        /// <summary>
        /// Creates the final feed post on the server
        /// </summary>
        public async Task<CreateFeedResponse?> CreateFeedAsync(Guid sessionId)
        {
            try
            {
                // Set auth header before making request
                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var request = new { sessionId = sessionId };
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                Debug.WriteLine($"[AI FEED SERVICE] Creating feed for session: {sessionId}");

                var response = await _httpClient.PostAsync($"{_baseUrl}/create", content);
                var responseJson = await response.Content.ReadAsStringAsync();

                Debug.WriteLine($"[AI FEED SERVICE] Create response: {responseJson}");

                if (response.IsSuccessStatusCode)
                {
                    return JsonSerializer.Deserialize<CreateFeedResponse>(responseJson,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    Debug.WriteLine($"[AI FEED SERVICE] ❌ Create unauthorized");
                    SecureStorage.Remove("auth_token");
                    throw new UnauthorizedAccessException("Session expired. Please login again.");
                }
                else
                {
                    Debug.WriteLine($"[AI FEED SERVICE] ❌ Create failed: {response.StatusCode}");
                    return null;
                }
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AI FEED SERVICE] ❌ Create error: {ex.Message}");
                throw new Exception("Failed to create feed. Please try again.", ex);
            }
        }
    }

    #region Response Models

    public class ChatApiResponse
    {
        public string Message { get; set; } = "";
        public string Stage { get; set; } = "";
        public bool IsTyping { get; set; }
        public bool ReadyToCreate { get; set; }
        public FeedDraftResponse? Draft { get; set; }
    }

    public class FeedDraftResponse
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public string Location { get; set; } = "";
        public string Priority { get; set; } = "";
    }

    public class FileUploadResponse
    {
        public bool Success { get; set; }
        public string FileName { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class CreateFeedResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public Guid? FeedId { get; set; }
    }

    #endregion
}