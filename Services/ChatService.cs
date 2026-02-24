using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    // ═══════════════════════════════════════════════════════════════
    // API RESPONSE MODELS (matches backend structure)
    // ═══════════════════════════════════════════════════════════════

    public class GroupsResponse
    {
        public bool Success { get; set; }
        public List<GroupChatItem> Groups { get; set; }
        public int TotalGroups { get; set; }
    }

    public class MessagesResponse
    {
        public bool Success { get; set; }
        public List<MessageDto> Messages { get; set; }
        public int TotalMessages { get; set; }
    }

    public class MessageDto
    {
        public Guid Id { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }
        public Guid SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderFullName { get; set; }
        public bool HasAttachment { get; set; }
        public string AttachmentUrl { get; set; }
        public string AttachmentName { get; set; }
        public string AttachmentSize { get; set; }
        public string AttachmentType { get; set; }
        public string MediaType { get; set; }
        public FileInfoDto FileInfo { get; set; }
    }

    public class FileInfoDto
    {
        public string Name { get; set; }
        public string Extension { get; set; }
        public string Type { get; set; }
        public string DisplayIcon { get; set; }
    }

    public class ChatService : IChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ChatService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');

#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[CHAT SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    Debug.WriteLine($"[CHAT SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                    return true;
                }
            };
#endif

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            Debug.WriteLine($"[CHAT SERVICE] Initialized with BaseUrl: {_baseUrl}");
        }

        // ═══════════════════════════════════════════════════════════════
        // AUTH HEADER
        // ═══════════════════════════════════════════════════════════════

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
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error setting auth header: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET MY GROUPS  →  GET /api/chat/my-groups
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<GroupChatItem>> GetMyGroupsAsync()
        {
            try
            {
                Debug.WriteLine("[CHAT SERVICE] 📡 Fetching user groups...");

                if (!await SetAuthHeaderAsync())
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync("/api/chat/my-groups");
                sw.Stop();

                Debug.WriteLine($"[CHAT SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ❌ Error: {errorContent}");

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        SecureStorage.Remove("auth_token");
                        throw new UnauthorizedAccessException("Token expired or invalid. Please login again.");
                    }

                    throw new HttpRequestException($"API Error: {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[CHAT SERVICE] 📄 Response JSON length: {json.Length} chars");

                var result = JsonSerializer.Deserialize<GroupsResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Success == true)
                {
                    Debug.WriteLine($"[CHAT SERVICE] ✅ Fetched {result.TotalGroups} groups");
                    return result.Groups ?? new List<GroupChatItem>();
                }

                Debug.WriteLine("[CHAT SERVICE] ⚠️ API returned success=false");
                return new List<GroupChatItem>();
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error getting groups: {ex.Message}");
                throw new Exception($"Error getting groups: {ex.Message}", ex);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET MESSAGES  →  GET /api/chat/groups/{groupId}/messages
        // ═══════════════════════════════════════════════════════════════

        public async Task<List<GroupMessageItem>> GetGroupMessagesAsync(Guid groupId)
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] 📡 Fetching messages for group: {groupId}");

                if (!await SetAuthHeaderAsync())
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.GetAsync($"/api/chat/groups/{groupId}/messages");
                sw.Stop();

                Debug.WriteLine($"[CHAT SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

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
                Debug.WriteLine($"[CHAT SERVICE] 📄 Response JSON length: {json.Length} chars");

                var result = JsonSerializer.Deserialize<MessagesResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result?.Success != true || result.Messages == null)
                {
                    Debug.WriteLine("[CHAT SERVICE] ⚠️ No messages or invalid response");
                    return new List<GroupMessageItem>();
                }

                var messages = result.Messages.Select(m => new GroupMessageItem
                {
                    Id = m.Id,
                    GroupChatId = groupId,
                    SenderId = m.SenderId,
                    SenderName = m.SenderName,
                    SenderFullName = m.SenderFullName,
                    Message = m.Message,
                    SentAt = m.SentAt,
                    HasAttachment = m.HasAttachment,
                    AttachmentUrl = m.AttachmentUrl,
                    AttachmentName = m.AttachmentName ?? ExtractFileNameFromUrl(m.AttachmentUrl),
                    AttachmentSize = m.AttachmentSize ?? "Unknown size",
                    AttachmentType = m.AttachmentType ?? GetFileExtension(m.AttachmentUrl),
                    MediaType = m.MediaType ?? "none",
                    IsPending = false,
                    IsSent = true,
                    IsDelivered = true,
                    IsRead = false
                }).ToList();

                Debug.WriteLine($"[CHAT SERVICE] ✅ Parsed {messages.Count} messages");
                return messages;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error getting messages: {ex.Message}");
                throw;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SEND MESSAGE  →  POST /api/chat/groups/{groupId}/messages
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> SendMessageAsync(Guid groupId, string message)
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] 📤 Sending message to group: {groupId}");

                if (!await SetAuthHeaderAsync())
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

                var payload = new { Message = message, AttachmentUrl = (string)null };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var sw = Stopwatch.StartNew();
                var response = await _httpClient.PostAsync($"/api/chat/groups/{groupId}/messages", content);
                sw.Stop();

                Debug.WriteLine($"[CHAT SERVICE] 📥 Response: {(int)response.StatusCode} in {sw.ElapsedMilliseconds}ms");

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

        // ═══════════════════════════════════════════════════════════════
        // TEST METHODS
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> TestChatApiAsync()
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] Testing: {_baseUrl}/api/chat/test");
                var response = await _httpClient.GetAsync("/api/chat/test");
                Debug.WriteLine($"[CHAT SERVICE] Test response: {(int)response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ✅ Test success: {content}");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Test exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TestAuthAsync()
        {
            try
            {
                Debug.WriteLine("[CHAT SERVICE] Testing authenticated endpoint...");

                if (!await SetAuthHeaderAsync())
                {
                    Debug.WriteLine("[CHAT SERVICE] ❌ No token available");
                    return false;
                }

                var response = await _httpClient.GetAsync("/api/chat/test-auth");
                Debug.WriteLine($"[CHAT SERVICE] Auth test response: {(int)response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[CHAT SERVICE] ✅ Auth test success: {content}");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[CHAT SERVICE] ❌ Auth test failed: {errorContent}");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Auth test exception: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════

        private static string ExtractFileNameFromUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            try
            {
                var lastSlash = url.LastIndexOf('/');
                var fileName = lastSlash >= 0 ? url.Substring(lastSlash + 1) : url;
                var queryIndex = fileName.IndexOf('?');
                if (queryIndex >= 0) fileName = fileName.Substring(0, queryIndex);
                return Uri.UnescapeDataString(fileName);
            }
            catch { return "File"; }
        }

        private static string GetFileExtension(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            try
            {
                var ext = System.IO.Path.GetExtension(url);
                var queryIndex = ext.IndexOf('?');
                if (queryIndex >= 0) ext = ext.Substring(0, queryIndex);
                return ext.ToLowerInvariant();
            }
            catch { return ""; }
        }
    }
}