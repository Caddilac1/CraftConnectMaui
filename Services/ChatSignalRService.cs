using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Services
{
    public interface IChatSignalRService
    {
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
        event EventHandler<TypingEventArgs> UserTyping;
        event EventHandler<string> UserStoppedTyping;
        event EventHandler<string> Reconnected; // ✅ ADDED

        Task ConnectAsync();
        Task DisconnectAsync();
        Task JoinGroupAsync(string groupId);
        Task LeaveGroupAsync(string groupId);
        Task SendMessageAsync(string groupId, string message, string senderName, string senderFullName);
        Task SendMessageWithAttachmentAsync(string groupId, string message, string senderName, string senderFullName, string attachmentUrl, string attachmentName, string attachmentSize, string attachmentType);
        Task NotifyTypingAsync(string groupId, string userName);
        Task NotifyStoppedTypingAsync(string groupId);
        bool IsConnected { get; }
        void UpdateHubUrl(string newBaseUrl);
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public string Id { get; set; }
        public string GroupChatId { get; set; }
        public string SenderId { get; set; }
        public string SenderName { get; set; }
        public string SenderFullName { get; set; }
        public string Message { get; set; }
        public DateTime SentAt { get; set; }
        public bool HasAttachment { get; set; }
        public string AttachmentUrl { get; set; }
        public string AttachmentName { get; set; }
        public string AttachmentSize { get; set; }
        public string AttachmentType { get; set; }
        public string MediaType { get; set; }
    }

    public class TypingEventArgs : EventArgs
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ChatSignalRService : IChatSignalRService
    {
        private HubConnection _hubConnection;
        private string _hubUrl;

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<TypingEventArgs> UserTyping;
        public event EventHandler<string> UserStoppedTyping;
        public event EventHandler<string> Reconnected; // ✅ ADDED

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public ChatSignalRService(ApiConfig config)
        {
            _hubUrl = config.BaseUrl.TrimEnd('/') + "/chathub";
            Debug.WriteLine($"[SignalR Service] Initialized with URL: {_hubUrl}");
            InitializeConnection();
        }

        public void UpdateHubUrl(string newBaseUrl)
        {
            newBaseUrl = newBaseUrl.TrimEnd('/');
            if (newBaseUrl.EndsWith("/chathub", StringComparison.OrdinalIgnoreCase))
                newBaseUrl = newBaseUrl.Substring(0, newBaseUrl.Length - 8);

            _hubUrl = newBaseUrl + "/chathub";
            Debug.WriteLine($"[SignalR Service] Hub URL updated to: {_hubUrl}");

            if (_hubConnection != null)
                _ = DisconnectAsync();

            InitializeConnection();
        }

        private void DebugToken(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length != 3)
                {
                    Debug.WriteLine($"[SignalR Service] ⚠️ Invalid JWT format - expected 3 parts, got {parts.Length}");
                    return;
                }

                var payload = parts[1];
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                payload = payload.Replace('-', '+').Replace('_', '/');
                var payloadBytes = Convert.FromBase64String(payload);
                var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);

                Debug.WriteLine($"[SignalR Service] 🔍 JWT Payload: {payloadJson}");

                var payloadObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
                if (payloadObj != null && payloadObj.ContainsKey("exp"))
                {
                    var exp = long.Parse(payloadObj["exp"].ToString());
                    var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
                    var now = DateTimeOffset.UtcNow;
                    Debug.WriteLine($"[SignalR Service]    Expires: {expDate}, Is expired: {now > expDate}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error decoding token: {ex.Message}");
            }
        }

        private async Task<string> GetAuthTokenAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");

                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("[SignalR Service] ❌ No auth token found in SecureStorage");
                    return null;
                }

                Debug.WriteLine($"[SignalR Service] ✅ Auth token retrieved. Length: {token.Length}");
                DebugToken(token);
                return token;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error getting auth token: {ex.Message}");
                return null;
            }
        }

        private void InitializeConnection()
        {
            Debug.WriteLine("[SignalR Service] Initializing connection...");

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(_hubUrl, options =>
                {
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await GetAuthTokenAsync();
                        if (string.IsNullOrEmpty(token))
                            Debug.WriteLine("[SignalR Service] ⚠️ WARNING: No token available for SignalR connection!");
                        else
                            Debug.WriteLine($"[SignalR Service] 🔐 Providing token for connection (length: {token.Length})");
                        return token;
                    };

                    options.HttpMessageHandlerFactory = _ =>
                    {
#if ANDROID
                        return new Xamarin.Android.Net.AndroidMessageHandler
                        {
                            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                            {
                                Debug.WriteLine($"[SignalR SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                                return true;
                            }
                        };
#else
                        return new HttpClientHandler
                        {
                            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                            {
                                Debug.WriteLine($"[SignalR SSL] Host: {message.RequestUri.Host}, Errors: {errors}");
                                return true;
                            }
                        };
#endif
                    };
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            _hubConnection.On<object>("ReceiveMessage", OnMessageReceived);
            _hubConnection.On<object>("UserTyping", OnUserTyping);
            _hubConnection.On<string>("UserStoppedTyping", OnUserStoppedTyping);

            _hubConnection.Closed += OnConnectionClosed;
            _hubConnection.Reconnecting += OnReconnecting;
            _hubConnection.Reconnected += OnReconnected;

            Debug.WriteLine("[SignalR Service] ✅ Connection initialized");
        }

        public async Task ConnectAsync()
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                Debug.WriteLine("[SignalR Service] Already connected");
                return;
            }

            try
            {
                Debug.WriteLine($"[SignalR Service] 🔌 Connecting to {_hubUrl}...");

                var token = await GetAuthTokenAsync();
                if (string.IsNullOrEmpty(token))
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");

                await _hubConnection.StartAsync();
                Debug.WriteLine("[SignalR Service] ✅ Connected successfully");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[SignalR Service] 🔒 Unauthorized: {ex.Message}");
                throw;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Connection error (HTTP): {ex.Message}");
                Debug.WriteLine($"[SignalR Service] Failed URL: {_hubUrl}");

                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    SecureStorage.Remove("auth_token");
                    throw new UnauthorizedAccessException("Token expired or invalid. Please login again.", ex);
                }

                throw new Exception($"Failed to connect to SignalR: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Connection error: {ex.Message}");
                Debug.WriteLine($"[SignalR Service] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[SignalR Service] Failed URL: {_hubUrl}");
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                Debug.WriteLine("[SignalR Service] Already disconnected");
                return;
            }

            try
            {
                Debug.WriteLine("[SignalR Service] 🔌 Disconnecting...");
                await _hubConnection.StopAsync();
                Debug.WriteLine("[SignalR Service] ✅ Disconnected");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Disconnect error: {ex.Message}");
            }
        }

        public async Task JoinGroupAsync(string groupId)
        {
            try
            {
                Debug.WriteLine($"[SignalR Service] 🚪 Joining group: {groupId}");
                await _hubConnection.InvokeAsync("JoinGroup", groupId);
                Debug.WriteLine($"[SignalR Service] ✅ Joined group: {groupId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error joining group: {ex.Message}");
                throw;
            }
        }

        public async Task LeaveGroupAsync(string groupId)
        {
            try
            {
                Debug.WriteLine($"[SignalR Service] 🚪 Leaving group: {groupId}");
                await _hubConnection.InvokeAsync("LeaveGroup", groupId);
                Debug.WriteLine($"[SignalR Service] ✅ Left group: {groupId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error leaving group: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(string groupId, string message, string senderName, string senderFullName)
        {
            try
            {
                Debug.WriteLine($"[SignalR Service] 📤 Sending message to group: {groupId}");
                await _hubConnection.InvokeAsync("SendMessage", groupId, message, senderName, senderFullName);
                Debug.WriteLine("[SignalR Service] ✅ Message sent successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error sending message: {ex.Message}");
                throw;
            }
        }

        public async Task SendMessageWithAttachmentAsync(
            string groupId, string message, string senderName, string senderFullName,
            string attachmentUrl, string attachmentName, string attachmentSize, string attachmentType)
        {
            try
            {
                Debug.WriteLine($"[SignalR Service] 📤 Sending message with attachment to group: {groupId}");
                await _hubConnection.InvokeAsync(
                    "SendMessageWithAttachment",
                    groupId, message, senderName, senderFullName,
                    attachmentUrl, attachmentName, attachmentSize, attachmentType);
                Debug.WriteLine("[SignalR Service] ✅ Message with attachment sent");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error sending message with attachment: {ex.Message}");
                throw;
            }
        }

        public async Task NotifyTypingAsync(string groupId, string userName)
        {
            try { await _hubConnection.InvokeAsync("NotifyTyping", groupId, userName); }
            catch (Exception ex) { Debug.WriteLine($"[SignalR Service] ❌ Error notifying typing: {ex.Message}"); }
        }

        public async Task NotifyStoppedTypingAsync(string groupId)
        {
            try { await _hubConnection.InvokeAsync("NotifyStoppedTyping", groupId); }
            catch (Exception ex) { Debug.WriteLine($"[SignalR Service] ❌ Error notifying stopped typing: {ex.Message}"); }
        }

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE EVENT HANDLERS
        // ═══════════════════════════════════════════════════════════════

        private void OnMessageReceived(object messageData)
        {
            try
            {
                Debug.WriteLine("[SignalR Service] 📨 Message received from hub");
                var json = System.Text.Json.JsonSerializer.Serialize(messageData);
                var data = System.Text.Json.JsonSerializer.Deserialize<MessageReceivedEventArgs>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null)
                {
                    Debug.WriteLine($"[SignalR Service] ✅ Message from: {data.SenderFullName}, Group: {data.GroupChatId}");
                    MessageReceived?.Invoke(this, data);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error processing received message: {ex.Message}");
            }
        }

        private void OnUserTyping(object typingData)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(typingData);
                var data = System.Text.Json.JsonSerializer.Deserialize<TypingEventArgs>(json,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null)
                {
                    Debug.WriteLine($"[SignalR Service] 👤 User typing: {data.UserName}");
                    UserTyping?.Invoke(this, data);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error processing typing notification: {ex.Message}");
            }
        }

        private void OnUserStoppedTyping(string userId)
        {
            Debug.WriteLine($"[SignalR Service] 👤 User stopped typing: {userId}");
            UserStoppedTyping?.Invoke(this, userId);
        }

        private Task OnConnectionClosed(Exception exception)
        {
            if (exception != null)
                Debug.WriteLine($"[SignalR Service] ❌ Connection closed with error: {exception.Message}");
            else
                Debug.WriteLine("[SignalR Service] 👋 Connection closed normally");
            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception exception)
        {
            Debug.WriteLine($"[SignalR Service] 🔄 Reconnecting... {exception?.Message}");
            return Task.CompletedTask;
        }

        private Task OnReconnected(string connectionId)
        {
            Debug.WriteLine($"[SignalR Service] ✅ Reconnected. ConnectionId: {connectionId}");
            Reconnected?.Invoke(this, connectionId); // ✅ ADDED — fires event so ChatPageModel can re-join the group
            return Task.CompletedTask;
        }
    }
}