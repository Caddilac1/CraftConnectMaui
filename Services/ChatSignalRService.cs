using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Services
{
    public class ChatSignalRService : IChatSignalRService
    {
        private HubConnection _hubConnection;
        private string _hubUrl;

        // Group events
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<TypingEventArgs>? UserTyping;
        public event EventHandler<string>? UserStoppedTyping;
        public event EventHandler<string>? Reconnected;
        public event EventHandler<string>? MessageDeleted;

        // Private events
        public event EventHandler<PrivateMessageReceivedEventArgs>? PrivateMessageReceived;
        public event EventHandler<string>? PrivateMessageDeleted;
        public event EventHandler<PrivateMessageNotificationEventArgs>? PrivateMessageNotification;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public ChatSignalRService(ApiConfig config)
        {
            _hubUrl = config.BaseUrl.TrimEnd('/') + "/chathub";
            Debug.WriteLine($"[SignalR] Initialized with URL: {_hubUrl}");
            InitializeConnection();
        }

        public void UpdateHubUrl(string newBaseUrl)
        {
            newBaseUrl = newBaseUrl.TrimEnd('/');
            if (newBaseUrl.EndsWith("/chathub", StringComparison.OrdinalIgnoreCase))
                newBaseUrl = newBaseUrl[..^8];

            _hubUrl = newBaseUrl + "/chathub";
            if (_hubConnection != null) _ = DisconnectAsync();
            InitializeConnection();
        }

        // ── Init ──────────────────────────────────────────────────────────

        private void InitializeConnection()
        {
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                        await SecureStorage.GetAsync("auth_token");

                    options.HttpMessageHandlerFactory = _ =>
                    {
#if ANDROID
                        return new Xamarin.Android.Net.AndroidMessageHandler
                        {
                            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
                        };
#else
                        return new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
                        };
#endif
                    };
                })
                .WithAutomaticReconnect([
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                ])
                .Build();

            // Group handlers
            _hubConnection.On<object>("ReceiveMessage", OnHubGroupMessageReceived);
            _hubConnection.On<object>("UserTyping", OnHubUserTyping);
            _hubConnection.On<string>("UserStoppedTyping", OnHubUserStoppedTyping);
            _hubConnection.On<string, string>("MessageDeleted", OnHubGroupMessageDeleted);

            // Private handlers
            _hubConnection.On<object>("ReceivePrivateMessage", OnHubPrivateMessageReceived);
            _hubConnection.On<string, string>("PrivateMessageDeleted", OnHubPrivateMessageDeleted);
            _hubConnection.On<object>("PrivateMessageNotification", OnHubPrivateNotification);

            _hubConnection.Closed += OnConnectionClosed;
            _hubConnection.Reconnecting += OnReconnecting;
            _hubConnection.Reconnected += OnHubReconnected;
        }

        // ── Connection ────────────────────────────────────────────────────

        public async Task ConnectAsync()
        {
            if (_hubConnection.State == HubConnectionState.Connected) return;
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrEmpty(token))
                    throw new UnauthorizedAccessException("Not authenticated.");
                await _hubConnection.StartAsync();
                Debug.WriteLine("[SignalR] ✅ Connected");
            }
            catch (System.Net.Http.HttpRequestException ex)
                when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
            {
                SecureStorage.Remove("auth_token");
                throw new UnauthorizedAccessException("Token expired. Please login again.", ex);
            }
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection.State == HubConnectionState.Disconnected) return;
            try { await _hubConnection.StopAsync(); }
            catch (Exception ex) { Debug.WriteLine($"[SignalR] ❌ Disconnect: {ex.Message}"); }
        }

        // ── Group chat ────────────────────────────────────────────────────

        public async Task JoinGroupAsync(string groupId) =>
            await _hubConnection.InvokeAsync("JoinGroup", groupId);

        public async Task LeaveGroupAsync(string groupId)
        {
            try { await _hubConnection.InvokeAsync("LeaveGroup", groupId); } catch { }
        }

        public async Task SendMessageAsync(
            string groupId, string message, string senderName, string senderFullName) =>
            await _hubConnection.InvokeAsync("SendMessage", groupId, message, senderName, senderFullName);

        public async Task SendMessageWithAttachmentAsync(
            string groupId, string message, string senderName, string senderFullName,
            string attachmentUrl, string attachmentName, string attachmentSize, string attachmentType) =>
            await _hubConnection.InvokeAsync("SendMessageWithAttachment",
                groupId, message, senderName, senderFullName,
                attachmentUrl, attachmentName, attachmentSize, attachmentType);

        public async Task NotifyTypingAsync(string groupId, string userName)
        {
            try { await _hubConnection.InvokeAsync("NotifyTyping", groupId, userName); } catch { }
        }

        public async Task NotifyStoppedTypingAsync(string groupId)
        {
            try { await _hubConnection.InvokeAsync("NotifyStoppedTyping", groupId); } catch { }
        }

        public async Task DeleteMessageAsync(string groupId, string messageId) =>
            await _hubConnection.InvokeAsync("DeleteMessage", groupId, messageId);

        // ── Private chat ──────────────────────────────────────────────────

        public async Task JoinPrivateConversationAsync(string conversationId) =>
            await _hubConnection.InvokeAsync("JoinPrivateConversation", conversationId);

        public async Task LeavePrivateConversationAsync(string conversationId)
        {
            try { await _hubConnection.InvokeAsync("LeavePrivateConversation", conversationId); } catch { }
        }

        public async Task SendPrivateMessageAsync(
            string conversationId, string messageId, string message,
            string? attachmentUrl = null, string? quotedGroupSender = null,
            string? quotedGroupMessage = null, string? replyToMessageId = null) =>
            await _hubConnection.InvokeAsync("SendPrivateMessage",
                conversationId, messageId, message,
                attachmentUrl, quotedGroupSender, quotedGroupMessage, replyToMessageId);

        public async Task DeletePrivateMessageAsync(string conversationId, string messageId) =>
            await _hubConnection.InvokeAsync("DeletePrivateMessage", conversationId, messageId);

        public async Task PrivateTypingAsync(string conversationId, string userName)
        {
            try { await _hubConnection.InvokeAsync("PrivateTyping", conversationId, userName); } catch { }
        }

        public async Task PrivateStoppedTypingAsync(string conversationId)
        {
            try { await _hubConnection.InvokeAsync("PrivateStoppedTyping", conversationId); } catch { }
        }

        // ── Hub event handlers ────────────────────────────────────────────

        private void OnHubGroupMessageReceived(object data)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                var msg = System.Text.Json.JsonSerializer.Deserialize<MessageReceivedEventArgs>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (msg != null) MessageReceived?.Invoke(this, msg);
            }
            catch (Exception ex) { Debug.WriteLine($"[SignalR] OnGroupMessage: {ex.Message}"); }
        }

        private void OnHubPrivateMessageReceived(object data)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                var msg = System.Text.Json.JsonSerializer.Deserialize<PrivateMessageReceivedEventArgs>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (msg != null) PrivateMessageReceived?.Invoke(this, msg);
            }
            catch (Exception ex) { Debug.WriteLine($"[SignalR] OnPrivateMessage: {ex.Message}"); }
        }

        private void OnHubPrivateNotification(object data)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                var notif = System.Text.Json.JsonSerializer.Deserialize<PrivateMessageNotificationEventArgs>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (notif != null) PrivateMessageNotification?.Invoke(this, notif);
            }
            catch { }
        }

        private void OnHubUserTyping(object data)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(data);
                var args = System.Text.Json.JsonSerializer.Deserialize<TypingEventArgs>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (args != null) UserTyping?.Invoke(this, args);
            }
            catch { }
        }

        private void OnHubUserStoppedTyping(string userId) =>
            UserStoppedTyping?.Invoke(this, userId);

        private void OnHubGroupMessageDeleted(string groupId, string messageId) =>
            MessageDeleted?.Invoke(this, messageId);

        private void OnHubPrivateMessageDeleted(string conversationId, string messageId) =>
            PrivateMessageDeleted?.Invoke(this, messageId);

        private Task OnConnectionClosed(Exception? ex)
        {
            Debug.WriteLine($"[SignalR] Connection closed: {ex?.Message ?? "normal"}");
            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception? ex)
        {
            Debug.WriteLine($"[SignalR] Reconnecting... {ex?.Message}");
            return Task.CompletedTask;
        }

        private Task OnHubReconnected(string? connectionId)
        {
            Debug.WriteLine($"[SignalR] Reconnected: {connectionId}");
            Reconnected?.Invoke(this, connectionId ?? string.Empty);
            return Task.CompletedTask;
        }
    }
}