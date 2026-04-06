using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public class PrivateChatService : IPrivateChatService
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions _json = new()
        { PropertyNameCaseInsensitive = true };

        public PrivateChatService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');

#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
            };
#else
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
            };
#endif
            _http = new HttpClient(handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        // ── Auth header ───────────────────────────────────────────────

        private async Task<bool> AuthAsync()
        {
            var token = await SecureStorage.GetAsync("auth_token");
            if (string.IsNullOrEmpty(token)) return false;
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            return true;
        }

        // ── Conversations ─────────────────────────────────────────────

        public async Task<List<PrivateConversationItem>> GetMyConversationsAsync()
        {
            try
            {
                if (!await AuthAsync()) throw new UnauthorizedAccessException();
                var resp = await _http.GetAsync("/api/dm/conversations");
                if (!resp.IsSuccessStatusCode) return new();

                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json).RootElement;
                var convs = doc.GetProperty("conversations");

                var result = new List<PrivateConversationItem>();
                foreach (var c in convs.EnumerateArray())
                {
                    result.Add(new PrivateConversationItem
                    {
                        Id = c.GetProperty("id").GetString() ?? "",
                        OtherUserId = c.GetProperty("otherUserId").GetString() ?? "",
                        OtherUserName = c.GetProperty("otherUserName").GetString() ?? "",
                        LastMessage = c.TryGetProperty("lastMessage", out var lm)
                            ? lm.GetString() : null,
                        LastMessageTime = c.TryGetProperty("lastMessageTime", out var lmt) &&
                            lmt.ValueKind != JsonValueKind.Null
                            ? lmt.GetDateTime() : default,
                        UnreadCount = c.TryGetProperty("unreadCount", out var uc)
                            ? uc.GetInt32() : 0
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DM SERVICE] GetMyConversations: {ex.Message}");
                return new();
            }
        }

        public async Task<(string conversationId, string otherUserName)> OpenConversationAsync(
            string otherUserId)
        {
            if (!await AuthAsync()) throw new UnauthorizedAccessException();

            var body = JsonSerializer.Serialize(new { OtherUserId = otherUserId });
            var resp = await _http.PostAsync("/api/dm/conversations/open",
                new StringContent(body, Encoding.UTF8, "application/json"));

            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json).RootElement;

            var convId = doc.GetProperty("conversationId").GetString() ?? "";
            var name = doc.GetProperty("otherUserName").GetString() ?? "";
            return (convId, name);
        }

        // ── Messages ──────────────────────────────────────────────────

        public async Task<List<PrivateMessageItem>> GetMessagesAsync(string conversationId)
        {
            try
            {
                if (!await AuthAsync()) throw new UnauthorizedAccessException();
                var resp = await _http.GetAsync($"/api/dm/conversations/{conversationId}/messages");
                if (!resp.IsSuccessStatusCode) return new();

                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json).RootElement;
                var msgs = doc.GetProperty("messages");

                var result = new List<PrivateMessageItem>();
                foreach (var m in msgs.EnumerateArray())
                {
                    result.Add(new PrivateMessageItem
                    {
                        Id = Guid.TryParse(m.GetProperty("id").GetString(), out var gid)
                            ? gid : Guid.Empty,
                        ConversationId = conversationId,
                        SenderId = Guid.TryParse(m.GetProperty("senderId").GetString(), out var sid)
                            ? sid : Guid.Empty,
                        SenderName = m.TryGetProperty("senderName", out var sn)
                            ? sn.GetString() ?? "" : "",
                        Message = m.TryGetProperty("message", out var msg)
                            ? msg.GetString() : null,
                        SentAt = m.GetProperty("sentAt").GetDateTime(),
                        HasAttachment = m.TryGetProperty("hasAttachment", out var ha)
                            && ha.GetBoolean(),
                        AttachmentUrl = m.TryGetProperty("attachmentUrl", out var au)
                            ? au.GetString() : null,
                        AttachmentName = m.TryGetProperty("attachmentName", out var an)
                            ? an.GetString() : null,
                        AttachmentType = m.TryGetProperty("attachmentType", out var at)
                            ? at.GetString() : null,
                        MediaType = m.TryGetProperty("mediaType", out var mt)
                            ? mt.GetString() ?? "none" : "none",
                        QuotedGroupSender = m.TryGetProperty("quotedGroupSender", out var qgs)
                            ? qgs.GetString() : null,
                        QuotedGroupMessage = m.TryGetProperty("quotedGroupMessage", out var qgm)
                            ? qgm.GetString() : null,
                        ReplyToMessageId = m.TryGetProperty("replyToMessageId", out var rtm) &&
                            rtm.ValueKind != JsonValueKind.Null &&
                            Guid.TryParse(rtm.GetString(), out var rtmId)
                            ? rtmId : null,
                        IsSent = true,
                        IsDelivered = true
                    });
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DM SERVICE] GetMessages: {ex.Message}");
                return new();
            }
        }

        public async Task<(bool success, string? messageId)> SendMessageAsync(
            string conversationId,
            string? message,
            string? attachmentUrl = null,
            string? replyToMessageId = null,
            string? quotedGroupSender = null,
            string? quotedGroupMessage = null)
        {
            try
            {
                if (!await AuthAsync()) throw new UnauthorizedAccessException();

                var payload = new
                {
                    Message = message,
                    AttachmentUrl = attachmentUrl,
                    ReplyToMessageId = replyToMessageId,
                    QuotedGroupSender = quotedGroupSender,
                    QuotedGroupMessage = quotedGroupMessage
                };

                var body = JsonSerializer.Serialize(payload);
                var resp = await _http.PostAsync(
                    $"/api/dm/conversations/{conversationId}/messages",
                    new StringContent(body, Encoding.UTF8, "application/json"));

                if (!resp.IsSuccessStatusCode) return (false, null);

                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json).RootElement;
                var msgId = doc.TryGetProperty("messageId", out var mid)
                    ? mid.GetString() : null;
                return (true, msgId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DM SERVICE] SendMessage: {ex.Message}");
                return (false, null);
            }
        }

        public async Task<bool> MarkAsReadAsync(string conversationId)
        {
            try
            {
                if (!await AuthAsync()) return false;
                var resp = await _http.PostAsync(
                    $"/api/dm/conversations/{conversationId}/mark-read",
                    new StringContent("{}", Encoding.UTF8, "application/json"));
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> DeleteForMeAsync(string messageId)
        {
            try
            {
                if (!await AuthAsync()) return false;
                var resp = await _http.DeleteAsync($"/api/dm/messages/{messageId}/for-me");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<bool> DeleteForEveryoneAsync(string messageId)
        {
            try
            {
                if (!await AuthAsync()) return false;
                var resp = await _http.DeleteAsync($"/api/dm/messages/{messageId}/for-everyone");
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }
    }
}
