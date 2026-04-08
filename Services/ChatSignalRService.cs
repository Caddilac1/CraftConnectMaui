using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;
using System.Text.Json;

namespace CraftConnect_Mobile_App.Services
{
    public class ChatSignalRService : IChatSignalRService, IAsyncDisposable
    {
        // ── Fields ────────────────────────────────────────────────────────

        private HubConnection? _hubConnection;
        private string _baseUrl;
        private string _hubUrl;

        // Prevents concurrent connect calls racing each other
        private readonly SemaphoreSlim _connectLock = new(1, 1);

        // Shared JSON options — created once, reused everywhere
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ── Events ────────────────────────────────────────────────────────

        public event EventHandler<bool>? ConnectionStateChanged;

        // Group chat
        public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<TypingEventArgs>? UserTyping;
        public event EventHandler<string>? UserStoppedTyping;
        public event EventHandler<string>? Reconnected;
        public event EventHandler<string>? MessageDeleted;

        // Private chat
        public event EventHandler<PrivateMessageReceivedEventArgs>? PrivateMessageReceived;
        public event EventHandler<string>? PrivateMessageDeleted;
        public event EventHandler<PrivateMessageNotificationEventArgs>? PrivateMessageNotification;

        // ── State ─────────────────────────────────────────────────────────

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public bool IsConnecting => _hubConnection?.State == HubConnectionState.Connecting
                                 || _hubConnection?.State == HubConnectionState.Reconnecting;

        // ── Constructor ───────────────────────────────────────────────────

        public ChatSignalRService(ApiConfig config)
        {
            _baseUrl = config.BaseUrl.TrimEnd('/');
            _hubUrl = _baseUrl + "/chathub";
            Debug.WriteLine($"[SignalR] Initialized. Hub URL: {_hubUrl}");
            BuildConnection();
        }

        // ── Build connection ──────────────────────────────────────────────

        private void BuildConnection()
        {
            // Dispose old connection safely before rebuilding
            if (_hubConnection != null)
            {
                _hubConnection.Closed -= OnClosed;
                _hubConnection.Reconnecting -= OnReconnecting;
                _hubConnection.Reconnected -= OnHubReconnected;
                _ = _hubConnection.DisposeAsync().AsTask();
            }

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    // Token is fetched fresh on every connection attempt
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await SecureStorage.GetAsync("auth_token");
                        if (string.IsNullOrWhiteSpace(token))
                        {
                            Debug.WriteLine("[SignalR] ⚠️ No auth token found.");
                            return null;
                        }
                        return token;
                    };

                    // SSL handler — bypasses cert check in DEBUG only
                    options.HttpMessageHandlerFactory = _ =>
                    {
#if DEBUG && ANDROID
                        return new Xamarin.Android.Net.AndroidMessageHandler
                        {
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                        };
#elif DEBUG
                        return new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                        };
#else
                        // PRODUCTION: enforce SSL validation
                        return new HttpClientHandler();
#endif
                    };
                })
                // Exponential-style backoff: 0s, 2s, 5s, 10s, 30s
                .WithAutomaticReconnect([
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)
                ])
                .Build();

            RegisterHandlers();

            _hubConnection.Closed += OnClosed;
            _hubConnection.Reconnecting += OnReconnecting;
            _hubConnection.Reconnected += OnHubReconnected;
        }

        // ── Register hub handlers ─────────────────────────────────────────

        private void RegisterHandlers()
        {
            if (_hubConnection == null) return;

            _hubConnection.On<object>("ReceiveMessage", OnGroupMessageReceived);
            _hubConnection.On<object>("UserTyping", OnUserTypingReceived);
            _hubConnection.On<string>("UserStoppedTyping", OnUserStoppedTypingReceived);
            _hubConnection.On<string, string>("MessageDeleted", OnGroupMessageDeleted);

            _hubConnection.On<object>("ReceivePrivateMessage", OnPrivateMessageReceived);
            _hubConnection.On<string, string>("PrivateMessageDeleted", OnPrivateMessageDeleted);
            _hubConnection.On<object>("PrivateMessageNotification", OnPrivateNotification);
        }

        // ── Connect / Disconnect ──────────────────────────────────────────

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_hubConnection == null) return;

            // Already up — nothing to do
            if (IsConnected || IsConnecting) return;

            // Prevent concurrent connect calls
            if (!await _connectLock.WaitAsync(0)) return;

            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrWhiteSpace(token))
                    throw new UnauthorizedAccessException("No auth token — user must log in first.");

                // Only start if truly disconnected
                if (_hubConnection.State != HubConnectionState.Disconnected)
                {
                    Debug.WriteLine($"[SignalR] ⚠️ Cannot start — state is {_hubConnection.State}");
                    return;
                }

                await _hubConnection.StartAsync(cancellationToken);
                Debug.WriteLine("[SignalR] ✅ Connected");
                ConnectionStateChanged?.Invoke(this, true);
            }
            catch (UnauthorizedAccessException)
            {
                SecureStorage.Remove("auth_token");
                Debug.WriteLine("[SignalR] ❌ Unauthorized — token cleared.");
                throw;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[SignalR] ⚠️ Connect cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR] ❌ Connect failed: {ex.Message}");
                throw;
            }
            finally
            {
                _connectLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection == null) return;
            if (_hubConnection.State == HubConnectionState.Disconnected) return;

            try
            {
                await _hubConnection.StopAsync();
                Debug.WriteLine("[SignalR] ✅ Disconnected cleanly.");
                ConnectionStateChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR] ❌ Disconnect error: {ex.Message}");
            }
        }

        // ── Safe invoke helper ────────────────────────────────────────────

        /// <summary>
        /// Invokes a hub method only when connected. Silently skips if not.
        /// </summary>
        private async Task SafeInvokeAsync(string method, params object?[] args)
        {
            if (_hubConnection == null || !IsConnected)
            {
                Debug.WriteLine($"[SignalR] ⚠️ Skipped '{method}' — not connected.");
                return;
            }

            try
            {
                await _hubConnection.SendCoreAsync(method, args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR] ❌ '{method}' failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Invokes a hub method and returns a result. Throws on failure.
        /// </summary>
        private async Task RequiredInvokeAsync(string method, params object?[] args)
        {
            if (_hubConnection == null || !IsConnected)
                throw new InvalidOperationException($"[SignalR] Cannot invoke '{method}' — not connected.");

            await _hubConnection.SendCoreAsync(method, args);
        }

        // ── Group chat ────────────────────────────────────────────────────

        public Task JoinGroupAsync(string groupId) =>
            RequiredInvokeAsync("JoinGroup", groupId);

        public Task LeaveGroupAsync(string groupId) =>
            SafeInvokeAsync("LeaveGroup", groupId);

        public Task SendMessageAsync(
            string groupId, string message, string senderName, string senderFullName) =>
            RequiredInvokeAsync("SendMessage", groupId, message, senderName, senderFullName);

        public Task SendMessageWithAttachmentAsync(
            string groupId, string message, string senderName, string senderFullName,
            string attachmentUrl, string attachmentName, string attachmentSize, string attachmentType) =>
            RequiredInvokeAsync("SendMessageWithAttachment",
                groupId, message, senderName, senderFullName,
                attachmentUrl, attachmentName, attachmentSize, attachmentType);

        public Task NotifyTypingAsync(string groupId, string userName) =>
            SafeInvokeAsync("NotifyTyping", groupId, userName);

        public Task NotifyStoppedTypingAsync(string groupId) =>
            SafeInvokeAsync("NotifyStoppedTyping", groupId);

        public Task DeleteMessageAsync(string groupId, string messageId) =>
            RequiredInvokeAsync("DeleteMessage", groupId, messageId);

        // ── Private chat ──────────────────────────────────────────────────

        public Task JoinPrivateConversationAsync(string conversationId) =>
            RequiredInvokeAsync("JoinPrivateConversation", conversationId);

        public Task LeavePrivateConversationAsync(string conversationId) =>
            SafeInvokeAsync("LeavePrivateConversation", conversationId);

        public Task SendPrivateMessageAsync(
            string conversationId, string messageId, string message,
            string? attachmentUrl = null, string? quotedGroupSender = null,
            string? quotedGroupMessage = null, string? replyToMessageId = null) =>
            RequiredInvokeAsync("SendPrivateMessage",
                conversationId, messageId, message,
                attachmentUrl, quotedGroupSender, quotedGroupMessage, replyToMessageId);

        public Task DeletePrivateMessageAsync(string conversationId, string messageId) =>
            RequiredInvokeAsync("DeletePrivateMessage", conversationId, messageId);

        public Task PrivateTypingAsync(string conversationId, string userName) =>
            SafeInvokeAsync("PrivateTyping", conversationId, userName);

        public Task PrivateStoppedTypingAsync(string conversationId) =>
            SafeInvokeAsync("PrivateStoppedTyping", conversationId);

        // ── URL update ────────────────────────────────────────────────────

        public void UpdateHubUrl(string newBaseUrl)
        {
            newBaseUrl = newBaseUrl.TrimEnd('/');
            if (newBaseUrl.EndsWith("/chathub", StringComparison.OrdinalIgnoreCase))
                newBaseUrl = newBaseUrl[..^8];

            if (_baseUrl == newBaseUrl) return; // no change

            _baseUrl = newBaseUrl;
            _hubUrl = newBaseUrl + "/chathub";
            Debug.WriteLine($"[SignalR] URL updated to: {_hubUrl}");

            _ = DisconnectAsync().ContinueWith(_ => BuildConnection());
        }

        // ── Hub event handlers ────────────────────────────────────────────

        private void OnGroupMessageReceived(object data) =>
            DeserializeAndRaise<MessageReceivedEventArgs>(data, MessageReceived, "GroupMessage");

        private void OnPrivateMessageReceived(object data) =>
            DeserializeAndRaise<PrivateMessageReceivedEventArgs>(data, PrivateMessageReceived, "PrivateMessage");

        private void OnPrivateNotification(object data) =>
            DeserializeAndRaise<PrivateMessageNotificationEventArgs>(data, PrivateMessageNotification, "PrivateNotif");

        private void OnUserTypingReceived(object data) =>
            DeserializeAndRaise<TypingEventArgs>(data, UserTyping, "UserTyping");

        private void OnUserStoppedTypingReceived(string userId) =>
            UserStoppedTyping?.Invoke(this, userId);

        private void OnGroupMessageDeleted(string groupId, string messageId) =>
            MessageDeleted?.Invoke(this, messageId);

        private void OnPrivateMessageDeleted(string conversationId, string messageId) =>
            PrivateMessageDeleted?.Invoke(this, messageId);

        // ── Deserialize helper ────────────────────────────────────────────

        /// <summary>
        /// Deserializes a raw SignalR object payload and raises the matching event.
        /// Uses a single shared JsonSerializerOptions instance for performance.
        /// </summary>
        private void DeserializeAndRaise<T>(
            object data,
            EventHandler<T>? eventHandler,
            string label) where T : class
        {
            try
            {
                var json = JsonSerializer.Serialize(data, _jsonOpts);
                var result = JsonSerializer.Deserialize<T>(json, _jsonOpts);
                if (result != null)
                    eventHandler?.Invoke(this, result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR] ❌ Deserialize '{label}': {ex.Message}");
            }
        }

        // ── Connection lifecycle ──────────────────────────────────────────

        private Task OnClosed(Exception? ex)
        {
            Debug.WriteLine($"[SignalR] Connection closed: {ex?.Message ?? "clean shutdown"}");
            ConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception? ex)
        {
            Debug.WriteLine($"[SignalR] Reconnecting... reason: {ex?.Message}");
            ConnectionStateChanged?.Invoke(this, false);
            return Task.CompletedTask;
        }

        private Task OnHubReconnected(string? connectionId)
        {
            Debug.WriteLine($"[SignalR] ✅ Reconnected. ConnectionId: {connectionId}");
            ConnectionStateChanged?.Invoke(this, true);
            Reconnected?.Invoke(this, connectionId ?? string.Empty);
            return Task.CompletedTask;
        }

        // ── Disposal ──────────────────────────────────────────────────────

        public async ValueTask DisposeAsync()
        {
            _connectLock.Dispose();
            if (_hubConnection != null)
                await _hubConnection.DisposeAsync();
        }
    }
}