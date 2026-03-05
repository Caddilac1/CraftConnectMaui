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
    // PRIVATE API RESPONSE MODELS — match backend JSON shapes exactly
    // ═══════════════════════════════════════════════════════════════

    internal class GroupsResponse
    {
        public bool Success { get; set; }
        public List<GroupApiDto> Groups { get; set; }
        public int TotalGroups { get; set; }
        public int TotalUnread { get; set; }
    }

    internal class GroupApiDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string FeedId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string LastMessage { get; set; }
        public DateTime? LastMessageTime { get; set; }
        public int MemberCount { get; set; }
        public int UnreadCount { get; set; }
        public bool HasUnread { get; set; }
    }

    internal class MessagesResponse
    {
        public bool Success { get; set; }
        public List<MessageApiDto> Messages { get; set; }
        public int TotalMessages { get; set; }
    }

    internal class MessageApiDto
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderFullName { get; set; }
        public bool HasAttachment { get; set; }
        public string AttachmentUrl { get; set; }
        public string AttachmentName { get; set; }
        public string AttachmentSize { get; set; }
        public string AttachmentType { get; set; }
        public string MediaType { get; set; }
    }

    internal class MarkReadResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public DateTime? LastReadAt { get; set; }
    }

    internal class UnreadCountResponse
    {
        public bool Success { get; set; }
        public string GroupId { get; set; }
        public int UnreadCount { get; set; }
        public bool HasUnread { get; set; }
        public DateTime? LastReadAt { get; set; }
    }

    internal class UnreadTotalResponse
    {
        public bool Success { get; set; }
        public int TotalUnread { get; set; }
        public bool HasUnread { get; set; }
        public List<PerGroupUnread> PerGroup { get; set; }
    }

    internal class PerGroupUnread
    {
        public string GroupId { get; set; }
        public int UnreadCount { get; set; }
        public DateTime? LastReadAt { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // CHAT SERVICE
    // ═══════════════════════════════════════════════════════════════

    public class ChatService : IChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

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
        // Now includes UnreadCount and HasUnread per group
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
                    await HandleErrorResponseAsync(response, "GetMyGroups");
                    return new List<GroupChatItem>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GroupsResponse>(json, _jsonOptions);

                if (result?.Success != true)
                {
                    Debug.WriteLine("[CHAT SERVICE] ⚠️ API returned success=false");
                    return new List<GroupChatItem>();
                }

                // Map API DTOs → mobile GroupChatItem, populating unread fields
                var groups = (result.Groups ?? new List<GroupApiDto>()).Select(g => new GroupChatItem
                {
                    Id = g.Id,
                    Name = g.Name,
                    CreatedAt = g.CreatedAt,
                    LastMessage = g.LastMessage,
                    LastMessageTime = g.LastMessageTime ?? DateTime.MinValue,
                    // MemberCount is read-only (computed from Members list) — cannot assign
                    UnreadCount = g.UnreadCount,
                    LastMessageIsRead = !g.HasUnread,
                    LastMessageIsDelivered = true
                }).ToList();

                Debug.WriteLine($"[CHAT SERVICE] ✅ Fetched {groups.Count} groups. Total unread: {result.TotalUnread}");
                return groups;
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
        // Auto-marks the group as read on the server when called
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
                    await HandleErrorResponseAsync(response, "GetGroupMessages");
                    return new List<GroupMessageItem>();
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<MessagesResponse>(json, _jsonOptions);

                if (result?.Success != true || result.Messages == null)
                {
                    Debug.WriteLine("[CHAT SERVICE] ⚠️ No messages or invalid response");
                    return new List<GroupMessageItem>();
                }

                // The server auto-marks LastReadAt = now when GET messages is called,
                // so all these messages are considered read from this point forward.
                var messages = result.Messages.Select(m => new GroupMessageItem
                {
                    Id = Guid.TryParse(m.Id, out var msgId) ? msgId : Guid.Empty,
                    GroupChatId = groupId,
                    SenderId = Guid.TryParse(m.SenderId, out var sId) ? sId : Guid.Empty,
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
                    IsRead = true   // server marked as read when we fetched
                }).ToList();

                Debug.WriteLine($"[CHAT SERVICE] ✅ Parsed {messages.Count} messages (group marked as read)");
                return messages;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
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
                    await HandleErrorResponseAsync(response, "SendMessage");
                    return false;
                }

                Debug.WriteLine("[CHAT SERVICE] ✅ Message sent successfully");
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error sending message: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MARK GROUP AS READ  →  POST /api/chat/groups/{groupId}/mark-read
        // ═══════════════════════════════════════════════════════════════

        public async Task<bool> MarkGroupAsReadAsync(Guid groupId)
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] 📖 Marking group as read: {groupId}");

                if (!await SetAuthHeaderAsync())
                    return false;

                // Empty body — server only needs the groupId from the route
                var response = await _httpClient.PostAsync(
                    $"/api/chat/groups/{groupId}/mark-read",
                    new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[CHAT SERVICE] ❌ Mark-read failed: {response.StatusCode}");
                    return false;
                }

                Debug.WriteLine($"[CHAT SERVICE] ✅ Group {groupId} marked as read");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error marking as read: {ex.Message}");
                return false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET UNREAD COUNT FOR ONE GROUP
        //   GET /api/chat/groups/{groupId}/unread-count
        // ═══════════════════════════════════════════════════════════════

        public async Task<int> GetUnreadCountAsync(Guid groupId)
        {
            try
            {
                Debug.WriteLine($"[CHAT SERVICE] 🔢 Getting unread count for group: {groupId}");

                if (!await SetAuthHeaderAsync())
                    return 0;

                var response = await _httpClient.GetAsync($"/api/chat/groups/{groupId}/unread-count");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[CHAT SERVICE] ❌ Unread count failed: {response.StatusCode}");
                    return 0;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UnreadCountResponse>(json, _jsonOptions);

                var count = result?.UnreadCount ?? 0;
                Debug.WriteLine($"[CHAT SERVICE] ✅ Unread count for group {groupId}: {count}");
                return count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error getting unread count: {ex.Message}");
                return 0;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // GET TOTAL UNREAD ACROSS ALL GROUPS
        //   GET /api/chat/unread-total
        // ═══════════════════════════════════════════════════════════════

        public async Task<int> GetTotalUnreadCountAsync()
        {
            try
            {
                Debug.WriteLine("[CHAT SERVICE] 🔢 Getting total unread count...");

                if (!await SetAuthHeaderAsync())
                    return 0;

                var response = await _httpClient.GetAsync("/api/chat/unread-total");

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[CHAT SERVICE] ❌ Total unread failed: {response.StatusCode}");
                    return 0;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<UnreadTotalResponse>(json, _jsonOptions);

                var total = result?.TotalUnread ?? 0;
                Debug.WriteLine($"[CHAT SERVICE] ✅ Total unread across all groups: {total}");
                return total;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT SERVICE] ❌ Error getting total unread: {ex.Message}");
                return 0;
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

        /// <summary>
        /// Centralised error handler — logs details and throws for 401s.
        /// </summary>
        private async Task HandleErrorResponseAsync(HttpResponseMessage response, string caller)
        {
            var body = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"[CHAT SERVICE] ❌ {caller} error {(int)response.StatusCode}: {body}");

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                SecureStorage.Remove("auth_token");
                throw new UnauthorizedAccessException("Session expired. Please login again.");
            }
        }

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