using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using CraftConnect_Mobile_App.Models; // Add this to use existing GroupChatItem

namespace CraftConnect_Mobile_App.Services
{
    // Response models - defined outside class to match interface
    public class GroupsResponse
    {
        public bool Success { get; set; }
        public List<GroupChatItem> Groups { get; set; }
        public int TotalGroups { get; set; }
    }

    public class MessagesResponse
    {
        public bool Success { get; set; }
        public List<GroupMessageItem> Messages { get; set; }
        public int TotalMessages { get; set; }
    }



    public class ChatService : IChatService
    {
        private readonly HttpClient _httpClient;
        private const string BaseUrl = "https://192.168.43.232:7023"; // Match your AuthService URL

        public ChatService()
        {
            var handler = new HttpClientHandler();

#if DEBUG
            // Allow self-signed certificates in development
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) => true;
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[CHAT SERVICE] Initialized with BaseUrl: {BaseUrl}");
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
                    Debug.WriteLine("[CHAT SERVICE] ❌ No auth token found in SecureStorage");
                    return false;
                }

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                Debug.WriteLine($"[CHAT SERVICE] ✅ Auth header set. Token length: {token.Length}");
                Debug.WriteLine($"[CHAT SERVICE] Token preview: {token.Substring(0, Math.Min(30, token.Length))}...");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error setting auth header: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all groups the current user is a member of
        /// </summary>
        public async Task<List<GroupChatItem>> GetMyGroupsAsync()
        {
            try
            {
                Debug.WriteLine("[CHAT SERVICE] 📡 Fetching user groups...");

                // Set auth header from SecureStorage
                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                Debug.WriteLine($"[CHAT SERVICE] Making request to: {BaseUrl}/api/chat/my-groups");

                // Make the request
                var response = await _httpClient.GetAsync("/api/chat/my-groups");

                Debug.WriteLine($"[CHAT SERVICE] 📥 Response status: {response.StatusCode}");

                // Check if request was successful
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ❌ API Error: {response.StatusCode}");
                    Debug.WriteLine($"[CHAT SERVICE] Error content: {errorContent}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        // Clear token and throw
                        SecureStorage.Remove("auth_token");
                        throw new UnauthorizedAccessException("Token expired or invalid. Please login again.");
                    }

                    throw new HttpRequestException($"API Error: {response.StatusCode} - {errorContent}");
                }

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[CHAT SERVICE] 📄 Response JSON length: {json.Length} chars");
                Debug.WriteLine($"[CHAT SERVICE] Response preview: {json.Substring(0, Math.Min(200, json.Length))}...");

                // Parse the response - matches your API structure
                var result = JsonSerializer.Deserialize<GroupsResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Success == true)
                {
                    Debug.WriteLine($"[CHAT SERVICE] ✅ Successfully fetched {result.TotalGroups} groups");
                    return result.Groups ?? new List<GroupChatItem>();
                }
                else
                {
                    Debug.WriteLine($"[CHAT SERVICE] ⚠️ API returned success=false: {result?.Success}");
                    return new List<GroupChatItem>();
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] 🔒 Unauthorized: {ex.Message}");
                throw;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] 🌐 Network error: {ex.Message}");
                throw new Exception($"Network error: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] 📋 JSON parsing error: {ex.Message}");
                throw new Exception($"Failed to parse response: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Unexpected error: {ex.Message}");
                Debug.WriteLine($"[CHAT SERVICE] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[CHAT SERVICE] StackTrace: {ex.StackTrace}");
                throw new Exception($"Error getting groups: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get messages for a specific group
        /// </summary>
        public async Task<List<GroupMessageItem>> GetGroupMessagesAsync(Guid groupId)
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] 📡 Fetching messages for group: {groupId}");

                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var response = await _httpClient.GetAsync($"/api/chat/groups/{groupId}/messages");

                Debug.WriteLine($"[CHAT SERVICE] Response status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ❌ Error: {response.StatusCode} - {errorContent}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        SecureStorage.Remove("auth_token");
                        throw new UnauthorizedAccessException("Session expired. Please login again.");
                    }

                    throw new HttpRequestException($"Failed to get messages: {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<MessagesResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                Debug.WriteLine($"[CHAT SERVICE] ✅ Fetched {result?.TotalMessages ?? 0} messages");
                return result?.Messages ?? new List<GroupMessageItem>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error getting messages: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Send a message to a group
        /// </summary>
        public async Task<bool> SendMessageAsync(Guid groupId, string message)
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] 📤 Sending message to group: {groupId}");

                if (!await SetAuthHeaderAsync())
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

                var payload = new { Message = message };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"/api/chat/groups/{groupId}/messages", content);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ❌ Error: {response.StatusCode} - {errorContent}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        SecureStorage.Remove("auth_token");
                        throw new UnauthorizedAccessException("Session expired. Please login again.");
                    }

                    return false;
                }

                Debug.WriteLine("[CHAT SERVICE] ✅ Message sent successfully");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error sending message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test endpoint to verify chat API is working
        /// </summary>
        public async Task<bool> TestChatApiAsync()
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] Testing connection to: {BaseUrl}/api/chat/test");

                var response = await _httpClient.GetAsync("/api/chat/test");

                Debug.WriteLine($"[CHAT SERVICE] Test response status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ✅ Test success! Response: {content}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ❌ Test failed! Error: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Test exception: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Test authenticated endpoint
        /// </summary>
        public async Task<bool> TestAuthAsync()
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] Testing authenticated endpoint...");

                if (!await SetAuthHeaderAsync())
                {
                    Debug.WriteLine("[CHAT SERVICE] ❌ No token available");
                    return false;
                }

                var response = await _httpClient.GetAsync("/api/chat/test-auth");

                Debug.WriteLine($"[CHAT SERVICE] Auth test response: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ✅ Auth test success! Response: {content}");
                    return true;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ❌ Auth test failed! Error: {errorContent}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Auth test exception: {ex.Message}");
                return false;
            }
        }
    }
}