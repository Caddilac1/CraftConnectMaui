using Microsoft.AspNetCore.SignalR.Client;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Services
{
    public interface IChatSignalRService
    {
        event EventHandler<MessageReceivedEventArgs> MessageReceived;
        event EventHandler<TypingEventArgs> UserTyping;
        event EventHandler<string> UserStoppedTyping;

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
        private const string BaseUrl = "https://192.168.188.112:7023"; // Match ChatService URL

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<TypingEventArgs> UserTyping;
        public event EventHandler<string> UserStoppedTyping;

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public ChatSignalRService()
        {
            // Use same base URL as ChatService
            _hubUrl = BaseUrl + "/chathub";
            Debug.WriteLine($"[SignalR Service] Initialized with URL: {_hubUrl}");
            InitializeConnection();
        }

        public void UpdateHubUrl(string newBaseUrl)
        {
            // Remove /chathub if user accidentally includes it
            newBaseUrl = newBaseUrl.TrimEnd('/');
            if (newBaseUrl.EndsWith("/chathub", StringComparison.OrdinalIgnoreCase))
            {
                newBaseUrl = newBaseUrl.Substring(0, newBaseUrl.Length - 8);
            }

            _hubUrl = newBaseUrl + "/chathub";
            Debug.WriteLine($"[SignalR Service] Hub URL updated to: {_hubUrl}");

            // Reinitialize connection with new URL
            if (_hubConnection != null)
            {
                _ = DisconnectAsync();
            }
            InitializeConnection();
        }

        /// <summary>
        /// Get token from SecureStorage - matches ChatService pattern
        /// </summary>
        // Add this method to ChatSignalRService class to decode and inspect the JWT

        private void DebugToken(string token)
        {
            try
            {
                // JWT has 3 parts separated by dots: header.payload.signature
                var parts = token.Split('.');

                if (parts.Length != 3)
                {
                    Debug.WriteLine($"[SignalR Service] ⚠️ Invalid JWT format - expected 3 parts, got {parts.Length}");
                    return;
                }

                Debug.WriteLine($"[SignalR Service] 🔍 JWT Token Analysis:");
                Debug.WriteLine($"   Header length: {parts[0].Length}");
                Debug.WriteLine($"   Payload length: {parts[1].Length}");
                Debug.WriteLine($"   Signature length: {parts[2].Length}");

                // Decode payload (Base64Url)
                var payload = parts[1];

                // Add padding if needed for Base64 decoding
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                // Replace URL-safe characters
                payload = payload.Replace('-', '+').Replace('_', '/');

                var payloadBytes = Convert.FromBase64String(payload);
                var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);

                Debug.WriteLine($"   Payload JSON: {payloadJson}");

                // Parse to check expiration
                var payloadObj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);

                if (payloadObj != null)
                {
                    if (payloadObj.ContainsKey("exp"))
                    {
                        var exp = long.Parse(payloadObj["exp"].ToString());
                        var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
                        var now = DateTimeOffset.UtcNow;

                        Debug.WriteLine($"   Token Expiry: {expDate}");
                        Debug.WriteLine($"   Current Time: {now}");
                        Debug.WriteLine($"   Is Expired: {now > expDate}");
                        Debug.WriteLine($"   Time until expiry: {(expDate - now).TotalMinutes:F2} minutes");
                    }

                    if (payloadObj.ContainsKey("iss"))
                    {
                        Debug.WriteLine($"   Issuer (iss): {payloadObj["iss"]}");
                    }

                    if (payloadObj.ContainsKey("aud"))
                    {
                        Debug.WriteLine($"   Audience (aud): {payloadObj["aud"]}");
                    }

                    if (payloadObj.ContainsKey("sub") || payloadObj.ContainsKey("nameid"))
                    {
                        var userId = payloadObj.ContainsKey("sub") ? payloadObj["sub"] : payloadObj["nameid"];
                        Debug.WriteLine($"   User ID: {userId}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error decoding token: {ex.Message}");
            }
        }

        // Update GetAuthTokenAsync to use the debug method:
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
                Debug.WriteLine($"[SignalR Service] Token preview: {token.Substring(0, Math.Min(30, token.Length))}...");

                // Add detailed token analysis
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
                    // CRITICAL: Provide token via AccessTokenProvider for SignalR
                    options.AccessTokenProvider = async () =>
                    {
                        var token = await GetAuthTokenAsync();

                        if (string.IsNullOrEmpty(token))
                        {
                            Debug.WriteLine("[SignalR Service] ⚠️ WARNING: No token available for SignalR connection!");
                        }
                        else
                        {
                            Debug.WriteLine($"[SignalR Service] 🔐 Providing token for connection (length: {token.Length})");
                        }

                        return token;
                    };

#if DEBUG
                    // Allow self-signed certificates in development (matches ChatService)
                    options.HttpMessageHandlerFactory = (handler) =>
                    {
                        if (handler is HttpClientHandler clientHandler)
                        {
                            clientHandler.ServerCertificateCustomValidationCallback =
                                (message, cert, chain, errors) => true;
                            Debug.WriteLine("[SignalR Service] 🔓 Certificate validation disabled for DEBUG");
                        }
                        return handler;
                    };
#endif
                })
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            // Register event handlers
            _hubConnection.On<object>("ReceiveMessage", OnMessageReceived);
            _hubConnection.On<object>("UserTyping", OnUserTyping);
            _hubConnection.On<string>("UserStoppedTyping", OnUserStoppedTyping);

            // Connection state handlers
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

                // Verify token exists before connecting
                var token = await GetAuthTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    throw new UnauthorizedAccessException("Not authenticated. Please login first.");
                }

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

                // Check if it's a 401
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    Debug.WriteLine("[SignalR Service] 🔒 Token appears to be invalid or expired");
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
                Debug.WriteLine($"[SignalR Service] From: {senderFullName} ({senderName})");
                Debug.WriteLine($"[SignalR Service] Message: {message.Substring(0, Math.Min(50, message.Length))}...");

                await _hubConnection.InvokeAsync("SendMessage", groupId, message, senderName, senderFullName);

                Debug.WriteLine($"[SignalR Service] ✅ Message sent successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error sending message: {ex.Message}");
                Debug.WriteLine($"[SignalR Service] Exception type: {ex.GetType().Name}");
                throw;
            }
        }

        public async Task SendMessageWithAttachmentAsync(
            string groupId,
            string message,
            string senderName,
            string senderFullName,
            string attachmentUrl,
            string attachmentName,
            string attachmentSize,
            string attachmentType)
        {
            try
            {
                Debug.WriteLine($"[SignalR Service] 📤 Sending message with attachment to group: {groupId}");
                Debug.WriteLine($"[SignalR Service] Attachment: {attachmentName} ({attachmentSize})");

                await _hubConnection.InvokeAsync(
                    "SendMessageWithAttachment",
                    groupId,
                    message,
                    senderName,
                    senderFullName,
                    attachmentUrl,
                    attachmentName,
                    attachmentSize,
                    attachmentType);

                Debug.WriteLine($"[SignalR Service] ✅ Message with attachment sent");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error sending message with attachment: {ex.Message}");
                throw;
            }
        }

        public async Task NotifyTypingAsync(string groupId, string userName)
        {
            try
            {
                await _hubConnection.InvokeAsync("NotifyTyping", groupId, userName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error notifying typing: {ex.Message}");
            }
        }

        public async Task NotifyStoppedTypingAsync(string groupId)
        {
            try
            {
                await _hubConnection.InvokeAsync("NotifyStoppedTyping", groupId);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error notifying stopped typing: {ex.Message}");
            }
        }

        private void OnMessageReceived(object messageData)
        {
            try
            {
                Debug.WriteLine("[SignalR Service] 📨 Message received from hub");

                // Parse the dynamic object
                var json = System.Text.Json.JsonSerializer.Serialize(messageData);
                Debug.WriteLine($"[SignalR Service] Message JSON: {json.Substring(0, Math.Min(200, json.Length))}...");

                var data = System.Text.Json.JsonSerializer.Deserialize<MessageReceivedEventArgs>(json,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (data != null)
                {
                    Debug.WriteLine($"[SignalR Service] ✅ Message parsed successfully");
                    Debug.WriteLine($"[SignalR Service]    ID: {data.Id}");
                    Debug.WriteLine($"[SignalR Service]    Sender: {data.SenderFullName}");
                    Debug.WriteLine($"[SignalR Service]    Group: {data.GroupChatId}");

                    MessageReceived?.Invoke(this, data);
                }
                else
                {
                    Debug.WriteLine($"[SignalR Service] ⚠️ Failed to parse message data");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SignalR Service] ❌ Error processing received message: {ex.Message}");
                Debug.WriteLine($"[SignalR Service] Exception: {ex.GetType().Name}");
            }
        }

        private void OnUserTyping(object typingData)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(typingData);
                var data = System.Text.Json.JsonSerializer.Deserialize<TypingEventArgs>(json,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

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
            {
                Debug.WriteLine($"[SignalR Service] ❌ Connection closed with error: {exception.Message}");
            }
            else
            {
                Debug.WriteLine($"[SignalR Service] 👋 Connection closed normally");
            }
            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception exception)
        {
            Debug.WriteLine($"[SignalR Service] 🔄 Reconnecting...");
            if (exception != null)
            {
                Debug.WriteLine($"[SignalR Service] Reason: {exception.Message}");
            }
            return Task.CompletedTask;
        }



        private Task OnReconnected(string connectionId)
        {
            Debug.WriteLine($"[SignalR Service] ✅ Reconnected successfully");
            Debug.WriteLine($"[SignalR Service] New ConnectionId: {connectionId}");
            return Task.CompletedTask;
        }
    }
}