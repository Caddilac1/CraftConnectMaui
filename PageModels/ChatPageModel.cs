using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR.Client;

namespace CraftConnect_Mobile_App.PageModels
{
    [QueryProperty(nameof(GroupIdString), "GroupId")]
    [QueryProperty(nameof(GroupName), nameof(GroupName))]
    public class ChatPageModel : BasePageModel
    {
        private readonly IChatService _chatService;
        private readonly AuthService _authService;
        private readonly IChatSignalRService _signalRService;

        private Guid _groupId;
        private string _groupName = string.Empty;
        private string _messageText = string.Empty;
        private string _currentUserId = string.Empty;
        private string _currentUserName = string.Empty;
        private string _currentUserFullName = string.Empty;

        // Track temporary message IDs to prevent duplicates
        private readonly HashSet<Guid> _tempMessageIds = new();

        public ObservableCollection<GroupMessageItemViewModel> Messages { get; } = new();

        public Command LoadMessagesCommand { get; }
        public Command SendMessageCommand { get; }

        public ChatPageModel(IChatService chatService, AuthService authService, IChatSignalRService signalRService)
        {
            _chatService = chatService;
            _authService = authService;
            _signalRService = signalRService;

            LoadMessagesCommand = new Command(async () => await LoadMessages());
            SendMessageCommand = new Command(
                async () => await SendMessage(),
                () => !string.IsNullOrWhiteSpace(MessageText) && !IsBusy);

            _signalRService.MessageReceived += OnMessageReceived;

            // ✅ FIX: Re-join group after automatic reconnect
            _signalRService.Reconnected += OnSignalRReconnected;

            Debug.WriteLine("[CHAT PAGE MODEL] Initialized");
        }

        // ═══════════════════════════════════════════════════════════════
        // PROPERTIES
        // ═══════════════════════════════════════════════════════════════

        public string GroupIdString
        {
            set
            {
                if (Guid.TryParse(value, out var guidValue))
                {
                    GroupId = guidValue;
                    Debug.WriteLine($"[CHAT PAGE MODEL] GroupIdString parsed: {value} → {guidValue}");
                }
                else
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Failed to parse GroupId: {value}");
                }
            }
        }

        public Guid GroupId
        {
            get => _groupId;
            set
            {
                _groupId = value;
                OnPropertyChanged();
                Debug.WriteLine($"[CHAT PAGE MODEL] GroupId set to: {value}");
            }
        }

        public string GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                OnPropertyChanged();
                Debug.WriteLine($"[CHAT PAGE MODEL] GroupName set to: {value}");
            }
        }

        public string MessageText
        {
            get => _messageText;
            set
            {
                _messageText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MessageButtonIcon));
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }

        public string CurrentUserId
        {
            get => _currentUserId;
            set
            {
                _currentUserId = value;
                OnPropertyChanged();
            }
        }

        public string MessageButtonIcon => string.IsNullOrWhiteSpace(MessageText) ? "\ue029" : "\ue163";

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        public async Task InitializeAsync()
        {
            Debug.WriteLine($"[CHAT PAGE MODEL] InitializeAsync called for group: {GroupId}");

            try
            {
                // STEP 1: Decode JWT to get current user info
                await LoadCurrentUserAsync();

                // STEP 2: Show cached messages immediately for instant UI
                await LoadCachedMessages();

                // STEP 3: Connect to SignalR with retry logic
                await ConnectAndJoinGroupAsync();

                // STEP 4: Load fresh messages from API in background
                _ = Task.Run(async () => await LoadMessagesInBackground());

                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Initialization error: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Connection Error",
                    "Failed to connect to chat. Please check your internet connection and try again.",
                    "OK");
            }
        }

        private async Task LoadCurrentUserAsync()
        {
            var token = await _authService.GetTokenAsync();

            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("[CHAT PAGE MODEL] ⚠️ No token found — user not authenticated");
                return;
            }

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            CurrentUserId = jwt.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Sub ||
                c.Type == "sub")?.Value ?? string.Empty;

            var email = jwt.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Email ||
                c.Type == "email")?.Value ?? string.Empty;

            _currentUserName = email;
            _currentUserFullName = email;

            Debug.WriteLine($"[CHAT PAGE MODEL] Current user: {CurrentUserId} - {_currentUserFullName}");
        }

        /// <summary>
        /// ✅ FIX: Connects to SignalR then waits for a stable connection before joining.
        /// Retries up to 5 times with 500ms delay to handle brief post-connect drops.
        /// </summary>
        private async Task ConnectAndJoinGroupAsync()
        {
            if (!_signalRService.IsConnected)
            {
                Debug.WriteLine("[CHAT PAGE MODEL] Connecting to SignalR...");
                await _signalRService.ConnectAsync();
                Debug.WriteLine("[CHAT PAGE MODEL] ✅ SignalR connected");
            }

            // ✅ FIX: Wait for a stable connection state before invoking JoinGroup.
            // After StartAsync() returns, the connection can briefly drop if the server
            // rejects it (e.g. auth failure). We poll here to catch that window.
            const int maxRetries = 5;
            const int retryDelayMs = 500;

            for (int i = 0; i < maxRetries; i++)
            {
                if (_signalRService.IsConnected)
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] Joining group: {GroupId}");
                    await _signalRService.JoinGroupAsync(GroupId.ToString());
                    Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Joined group successfully");
                    return;
                }

                Debug.WriteLine($"[CHAT PAGE MODEL] ⏳ Waiting for stable connection ({i + 1}/{maxRetries})...");
                await Task.Delay(retryDelayMs);
            }

            // If we still can't connect after retries, throw so the caller shows an error
            throw new InvalidOperationException(
                "SignalR connection did not stabilize after multiple attempts. " +
                "This may be an authentication issue — check that the server's JWT bearer " +
                "events are configured to extract the token from the query string for SignalR.");
        }

        public async Task CleanupAsync()
        {
            Debug.WriteLine("[CHAT PAGE MODEL] Cleanup called");

            try
            {
                _signalRService.Reconnected -= OnSignalRReconnected;

                if (_signalRService.IsConnected)
                    await _signalRService.LeaveGroupAsync(GroupId.ToString());

                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Left group");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Cleanup error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // RECONNECT HANDLER
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// ✅ FIX: After automatic reconnect, re-join the group.
        /// Without this, the user silently stops receiving messages after a reconnect.
        /// </summary>
        private async void OnSignalRReconnected(object sender, string connectionId)
        {
            Debug.WriteLine($"[CHAT PAGE MODEL] 🔄 Reconnected ({connectionId}), re-joining group: {GroupId}");

            try
            {
                await _signalRService.JoinGroupAsync(GroupId.ToString());
                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Re-joined group after reconnect");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Failed to re-join group after reconnect: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MESSAGE LOADING
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadCachedMessages()
        {
            try
            {
                var cacheKey = $"messages_{GroupId}";
                var cachedJson = await SecureStorage.GetAsync(cacheKey);

                if (string.IsNullOrEmpty(cachedJson))
                {
                    Debug.WriteLine("[CHAT PAGE MODEL] No cached messages found");
                    return;
                }

                var cachedMessages = JsonSerializer.Deserialize<List<GroupMessageItem>>(cachedJson);

                if (cachedMessages == null || !cachedMessages.Any())
                    return;

                Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Loaded {cachedMessages.Count} cached messages");

                Messages.Clear();
                foreach (var message in cachedMessages.OrderBy(m => m.SentAt))
                    Messages.Add(new GroupMessageItemViewModel(message, CurrentUserId));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ Error loading cached messages: {ex.Message}");
            }
        }

        private async Task LoadMessagesInBackground()
        {
            try
            {
                await Task.Delay(500);

                var messages = await _chatService.GetGroupMessagesAsync(GroupId);
                Debug.WriteLine($"[CHAT PAGE MODEL] Received {messages.Count} messages from API");

                var cacheKey = $"messages_{GroupId}";
                var json = JsonSerializer.Serialize(messages);
                await SecureStorage.SetAsync(cacheKey, json);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existingIds = Messages.Select(m => m.Id).ToHashSet();
                    var newMessages = messages.Where(m => !existingIds.Contains(m.Id)).ToList();

                    foreach (var message in newMessages.OrderBy(m => m.SentAt))
                        Messages.Add(new GroupMessageItemViewModel(message, CurrentUserId));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ Error loading messages in background: {ex.Message}");
            }
        }

        private async Task LoadMessages()
        {
            if (GroupId == Guid.Empty)
                return;

            try
            {
                IsBusy = true;

                var messages = await _chatService.GetGroupMessagesAsync(GroupId);

                var cacheKey = $"messages_{GroupId}";
                var json = JsonSerializer.Serialize(messages);
                await SecureStorage.SetAsync(cacheKey, json);

                Messages.Clear();
                foreach (var message in messages.OrderBy(m => m.SentAt))
                    Messages.Add(new GroupMessageItemViewModel(message, CurrentUserId));
            }
            catch (UnauthorizedAccessException)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "Session Expired", "Your session has expired. Please login again.", "OK");
                await Shell.Current.GoToAsync("//auth/LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Error loading messages: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Error", $"Failed to load messages: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SEND MESSAGE
        // ═══════════════════════════════════════════════════════════════

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText) || IsBusy)
                return;

            var messageToSend = MessageText.Trim();
            var tempMessageId = Guid.NewGuid();
            GroupMessageItemViewModel tempViewModel = null;

            try
            {
                var tempMessage = new GroupMessageItem
                {
                    Id = tempMessageId,
                    GroupChatId = GroupId,
                    SenderId = Guid.Parse(CurrentUserId),
                    SenderName = _currentUserName,
                    SenderFullName = _currentUserFullName,
                    Message = messageToSend,
                    SentAt = DateTime.UtcNow,
                    IsPending = true,
                    IsSent = false,
                    IsDelivered = false,
                    HasAttachment = false,
                    MediaType = "none"
                };

                _tempMessageIds.Add(tempMessageId);
                tempViewModel = new GroupMessageItemViewModel(tempMessage, CurrentUserId);
                Messages.Add(tempViewModel);
                MessageText = string.Empty;

                // Send via SignalR (real-time broadcast to group)
                try
                {
                    await _signalRService.SendMessageAsync(
                        GroupId.ToString(),
                        messageToSend,
                        _currentUserName,
                        _currentUserFullName);
                }
                catch (Exception signalREx)
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ SignalR send failed: {signalREx.Message}");
                    _tempMessageIds.Remove(tempMessageId);
                    Messages.Remove(tempViewModel);
                    MessageText = messageToSend;
                    await Application.Current.MainPage.DisplayAlert(
                        "Error", "Failed to send message. Please try again.", "OK");
                    return;
                }

                // Also persist via REST API as a fallback/backup
                _ = Task.Run(async () =>
                {
                    try { await _chatService.SendMessageAsync(GroupId, messageToSend); }
                    catch (Exception apiEx)
                    {
                        Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ API save failed: {apiEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Error sending message: {ex.Message}");
                _tempMessageIds.Remove(tempMessageId);

                if (tempViewModel != null)
                    Messages.Remove(tempViewModel);

                MessageText = messageToSend;
                await Application.Current.MainPage.DisplayAlert(
                    "Error", $"Failed to send message: {ex.Message}", "OK");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // RECEIVE MESSAGE (SignalR event)
        // ═══════════════════════════════════════════════════════════════

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                // Ignore messages for other groups
                if (e.GroupChatId != GroupId.ToString())
                    return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Remove matching temp message for sender's own messages
                    if (e.SenderId == CurrentUserId)
                    {
                        var tempMessages = Messages
                            .Where(m => _tempMessageIds.Contains(m.Id) && m.Message == e.Message)
                            .ToList();

                        foreach (var tempMsg in tempMessages)
                        {
                            _tempMessageIds.Remove(tempMsg.Id);
                            Messages.Remove(tempMsg);
                        }
                    }

                    // Avoid duplicates
                    var messageId = Guid.Parse(e.Id);
                    if (Messages.Any(m => m.Id == messageId))
                        return;

                    var message = new GroupMessageItem
                    {
                        Id = messageId,
                        GroupChatId = Guid.Parse(e.GroupChatId),
                        SenderId = Guid.Parse(e.SenderId),
                        SenderName = e.SenderName,
                        SenderFullName = e.SenderFullName,
                        Message = e.Message,
                        SentAt = e.SentAt,
                        HasAttachment = e.HasAttachment,
                        AttachmentUrl = e.AttachmentUrl,
                        AttachmentName = e.AttachmentName,
                        AttachmentSize = e.AttachmentSize,
                        AttachmentType = e.AttachmentType,
                        MediaType = e.MediaType ?? "none",
                        IsPending = false,
                        IsSent = true,
                        IsDelivered = true
                    };

                    Messages.Add(new GroupMessageItemViewModel(message, CurrentUserId));

                    // Update cache asynchronously
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var allMessages = Messages.Select(m => new GroupMessageItem
                            {
                                Id = m.Id,
                                GroupChatId = GroupId,
                                SenderId = m.SenderId,
                                SenderName = m.SenderName,
                                SenderFullName = m.SenderFullName,
                                Message = m.Message,
                                SentAt = m.SentAt,
                                HasAttachment = m.HasAttachment,
                                AttachmentUrl = m.AttachmentUrl,
                                AttachmentName = m.AttachmentName,
                                AttachmentSize = m.AttachmentSize,
                                AttachmentType = m.AttachmentType,
                                MediaType = m.MediaType
                            }).ToList();

                            var cacheKey = $"messages_{GroupId}";
                            var json = JsonSerializer.Serialize(allMessages);
                            await SecureStorage.SetAsync(cacheKey, json);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ Error updating cache: {ex.Message}");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Error handling received message: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════════════

        ~ChatPageModel()
        {
            _signalRService.MessageReceived -= OnMessageReceived;
            _signalRService.Reconnected -= OnSignalRReconnected;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIEW MODEL WRAPPER
    // ═══════════════════════════════════════════════════════════════════════

    public class GroupMessageItemViewModel : INotifyPropertyChanged
    {
        private readonly GroupMessageItem _message;
        private readonly string _currentUserId;
        private bool _isDownloaded;
        private bool _isDownloading;
        private string _localFilePath;
        private double _downloadProgress;

        public event PropertyChangedEventHandler PropertyChanged;

        public GroupMessageItemViewModel(GroupMessageItem message, string currentUserId)
        {
            _message = message;
            _currentUserId = currentUserId;

            if (HasAttachment && IsImageAttachment)
                CheckIfDownloaded();
        }

        private void CheckIfDownloaded()
        {
            try
            {
                var fileName = AttachmentName ?? $"image_{_message.Id}.jpg";
                var filePath = Path.Combine(FileSystem.CacheDirectory, "downloads", fileName);

                if (File.Exists(filePath))
                {
                    _isDownloaded = true;
                    _localFilePath = filePath;
                    OnPropertyChanged(nameof(IsDownloaded));
                    OnPropertyChanged(nameof(LocalFilePath));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VIEW MODEL] Error checking download: {ex.Message}");
            }
        }

        public void MarkAsDownloading()
        {
            _isDownloading = true;
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(ShowDownloadButton));
            OnPropertyChanged(nameof(DownloadIcon));
        }

        public void UpdateDownloadProgress(double progress)
        {
            _downloadProgress = progress;
            OnPropertyChanged(nameof(DownloadProgress));
        }

        public void MarkAsDownloaded(string filePath)
        {
            _isDownloaded = true;
            _isDownloading = false;
            _localFilePath = filePath;
            _downloadProgress = 100;
            OnPropertyChanged(nameof(IsDownloaded));
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(LocalFilePath));
            OnPropertyChanged(nameof(ShowDownloadButton));
            OnPropertyChanged(nameof(DownloadIcon));
        }

        public void CancelDownload()
        {
            _isDownloading = false;
            _downloadProgress = 0;
            OnPropertyChanged(nameof(IsDownloading));
            OnPropertyChanged(nameof(ShowDownloadButton));
            OnPropertyChanged(nameof(DownloadProgress));
            OnPropertyChanged(nameof(DownloadIcon));
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Core message data
        public Guid Id => _message.Id;
        public string Message => _message.Message;
        public DateTime SentAt => _message.SentAt;
        public Guid SenderId => _message.SenderId;
        public string SenderName => _message.SenderName;
        public string SenderFullName => _message.SenderFullName;

        // Display helpers
        public bool IsFromCurrentUser => SenderId.ToString() == _currentUserId;
        public string DisplayName => IsFromCurrentUser ? "You" : (SenderFullName ?? SenderName ?? "Unknown");
        public bool HasMessageText => !string.IsNullOrWhiteSpace(_message.Message);
        public bool IsPending => _message.IsPending;
        public bool IsSent => _message.IsSent;
        public bool IsDelivered => _message.IsDelivered;

        public string StatusIcon
        {
            get
            {
                if (_message.IsPending) return "⏳";
                if (_message.IsDelivered) return "✓✓";
                if (_message.IsSent) return "✓";
                return "";
            }
        }

        public string DisplayTime
        {
            get
            {
                var diff = DateTime.Now - SentAt;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalDays < 1) return SentAt.ToString("h:mm tt");
                if (diff.TotalDays < 2) return $"Yesterday {SentAt:h:mm tt}";
                if (diff.TotalDays < 7) return SentAt.ToString("ddd h:mm tt");
                return SentAt.ToString("MMM d, h:mm tt");
            }
        }

        // Attachment properties
        public bool HasAttachment => _message.HasAttachment && !string.IsNullOrEmpty(_message.AttachmentUrl);
        public string AttachmentUrl => _message.AttachmentUrl;
        public string AttachmentName => _message.AttachmentName ?? "File";
        public string AttachmentSize => _message.AttachmentSize ?? "Unknown size";
        public string AttachmentType => _message.AttachmentType ?? "File";
        public string MediaType => _message.MediaType ?? "none";

        // Download state
        public bool IsDownloaded => _isDownloaded;
        public bool IsDownloading => _isDownloading;
        public string LocalFilePath => _localFilePath;
        public double DownloadProgress => _downloadProgress;
        public bool ShowDownloadButton => (IsImageAttachment || IsDocumentAttachment) && !IsDownloaded && !IsDownloading;
        public string DownloadIcon => IsDownloading ? "✕" : "⬇";
        public string DownloadStatusText => IsDownloading ? "Downloading..." : AttachmentSize;

        public bool IsImageAttachment
        {
            get
            {
                if (!HasAttachment) return false;
                return MediaType == "image" || IsImageExtension(AttachmentType);
            }
        }

        public bool IsDocumentAttachment
        {
            get
            {
                if (!HasAttachment) return false;
                return MediaType == "document" || IsDocumentExtension(AttachmentType);
            }
        }

        public string AttachmentIcon
        {
            get
            {
                var extension = (AttachmentType ?? "").ToLower();
                if (!extension.StartsWith(".")) extension = "." + extension;

                return extension switch
                {
                    ".pdf" => "\ue415",
                    ".doc" or ".docx" => "\ue873",
                    ".xls" or ".xlsx" => "\ue24d",
                    ".ppt" or ".pptx" => "\ue24d",
                    ".txt" => "\ue873",
                    ".zip" or ".rar" or ".7z" => "\ue2c6",
                    ".jpg" or ".jpeg" or ".png" or ".gif" => "\ue3f4",
                    ".mp4" or ".mov" or ".avi" => "\ue04b",
                    ".mp3" or ".wav" => "\ue310",
                    _ => "\ue24d"
                };
            }
        }

        private static bool IsImageExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            var ext = extension.ToLower();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg";
        }

        private static bool IsDocumentExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            var ext = extension.ToLower();
            if (!ext.StartsWith(".")) ext = "." + ext;
            return ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx"
                       or ".ppt" or ".pptx" or ".txt" or ".zip" or ".rar" or ".7z";
        }
    }
}