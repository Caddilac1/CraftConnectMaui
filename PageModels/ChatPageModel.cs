using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

        // ✅ Track temporary message IDs to prevent duplicates
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
            SendMessageCommand = new Command(async () => await SendMessage(), () => !string.IsNullOrWhiteSpace(MessageText) && !IsBusy);

            // ✅ Subscribe to SignalR events
            _signalRService.MessageReceived += OnMessageReceived;

            Debug.WriteLine("[CHAT PAGE MODEL] Initialized");
        }

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

        public async Task InitializeAsync()
        {
            Debug.WriteLine($"[CHAT PAGE MODEL] InitializeAsync called for group: {GroupId}");

            try
            {
                // Get current user info
                var userInfo = await _authService.GetCurrentUserAsync();
                CurrentUserId = userInfo.UserId;
                _currentUserName = userInfo.FullName ?? "User";
                _currentUserFullName = userInfo.FullName ?? userInfo.FullName ?? "User";
                Debug.WriteLine($"[CHAT PAGE MODEL] Current user: {CurrentUserId} - {_currentUserFullName}");

                // ✅ STEP 1: Load cached messages IMMEDIATELY (instant display!)
                await LoadCachedMessages();

                // ✅ STEP 2: Connect to SignalR and WAIT for it to connect
                if (!_signalRService.IsConnected)
                {
                    Debug.WriteLine("[CHAT PAGE MODEL] Connecting to SignalR...");
                    await _signalRService.ConnectAsync(); // ✅ AWAIT this!
                    Debug.WriteLine("[CHAT PAGE MODEL] ✅ SignalR connected");
                }

                // ✅ STEP 3: Join the group chat (NOW that we're connected)
                Debug.WriteLine($"[CHAT PAGE MODEL] Joining group: {GroupId}");
                await _signalRService.JoinGroupAsync(GroupId.ToString());
                Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Joined group successfully");

                // ✅ STEP 4: Load fresh messages from API in background (update cache)
                _ = Task.Run(async () => await LoadMessagesInBackground());

                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Initialization error: {ex.Message}");

                // Show user-friendly error
                await Application.Current.MainPage.DisplayAlert(
                    "Connection Error",
                    "Failed to connect to chat. Please check your internet connection and try again.",
                    "OK");
            }
        }

        public async Task CleanupAsync()
        {
            Debug.WriteLine("[CHAT PAGE MODEL] Cleanup called");

            try
            {
                await _signalRService.LeaveGroupAsync(GroupId.ToString());
                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Left group");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ Load cached messages INSTANTLY (from device storage)
        /// </summary>
        private async Task LoadCachedMessages()
        {
            try
            {
                var cacheKey = $"messages_{GroupId}";
                var cachedJson = await SecureStorage.GetAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedJson))
                {
                    var cachedMessages = JsonSerializer.Deserialize<List<GroupMessageItem>>(cachedJson);

                    if (cachedMessages != null && cachedMessages.Any())
                    {
                        Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Loaded {cachedMessages.Count} cached messages");

                        Messages.Clear();
                        foreach (var message in cachedMessages.OrderBy(m => m.SentAt))
                        {
                            var viewModel = new GroupMessageItemViewModel(message, CurrentUserId);
                            Messages.Add(viewModel);
                        }

                        Debug.WriteLine($"[CHAT PAGE MODEL] 🚀 Messages displayed from cache instantly!");
                    }
                }
                else
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] No cached messages found");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ Error loading cached messages: {ex.Message}");
                // Continue anyway - will load from API
            }
        }

        /// <summary>
        /// ✅ Load messages from API in background and update cache
        /// </summary>
        private async Task LoadMessagesInBackground()
        {
            try
            {
                await Task.Delay(500); // Small delay to let cached messages display first

                var messages = await _chatService.GetGroupMessagesAsync(GroupId);

                Debug.WriteLine($"[CHAT PAGE MODEL] Received {messages.Count} messages from API");

                // ✅ Save to cache for next time
                var cacheKey = $"messages_{GroupId}";
                var json = JsonSerializer.Serialize(messages);
                await SecureStorage.SetAsync(cacheKey, json);
                Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Messages cached for instant load next time");

                // ✅ Update UI only if messages are different from cache
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existingIds = Messages.Select(m => m.Id).ToHashSet();
                    var newMessages = messages.Where(m => !existingIds.Contains(m.Id)).ToList();

                    if (newMessages.Any())
                    {
                        Debug.WriteLine($"[CHAT PAGE MODEL] Adding {newMessages.Count} new messages from API");
                        foreach (var message in newMessages.OrderBy(m => m.SentAt))
                        {
                            var viewModel = new GroupMessageItemViewModel(message, CurrentUserId);
                            Messages.Add(viewModel);
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ Error loading messages in background: {ex.Message}");
            }
        }

        /// <summary>
        /// ✅ Load messages from API (called when user manually refreshes)
        /// </summary>
        private async Task LoadMessages()
        {
            if (GroupId == Guid.Empty)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] LoadMessages skipped - Invalid GroupId");
                return;
            }

            try
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] Loading messages for group: {GroupId}");
                IsBusy = true;

                var messages = await _chatService.GetGroupMessagesAsync(GroupId);

                Debug.WriteLine($"[CHAT PAGE MODEL] Received {messages.Count} messages from API");

                // Save to cache
                var cacheKey = $"messages_{GroupId}";
                var json = JsonSerializer.Serialize(messages);
                await SecureStorage.SetAsync(cacheKey, json);

                // Clear and reload messages
                Messages.Clear();
                foreach (var message in messages.OrderBy(m => m.SentAt))
                {
                    var viewModel = new GroupMessageItemViewModel(message, CurrentUserId);
                    Messages.Add(viewModel);
                }

                Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Messages loaded. Total: {Messages.Count}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Unauthorized: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Session Expired",
                    "Your session has expired. Please login again.",
                    "OK");
                await Shell.Current.GoToAsync("//auth/LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Error loading messages: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to load messages: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText))
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] SendMessage skipped - Empty message");
                return;
            }

            if (IsBusy)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] SendMessage skipped - Already busy");
                return;
            }

            var messageToSend = MessageText.Trim();
            var tempMessageId = Guid.NewGuid();

            try
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] Sending message to group: {GroupId}");
                Debug.WriteLine($"[CHAT PAGE MODEL] Message: {messageToSend}");

                // ✅ 1. Create temporary message (OPTIMISTIC UI - WhatsApp style)
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

                // ✅ Track this as a temporary message
                _tempMessageIds.Add(tempMessageId);

                // ✅ 2. Show message immediately in UI
                var tempViewModel = new GroupMessageItemViewModel(tempMessage, CurrentUserId);
                Messages.Add(tempViewModel);

                // Clear input immediately for better UX
                MessageText = string.Empty;

                // ✅ 3. Send via SignalR (real-time delivery)
                try
                {
                    await _signalRService.SendMessageAsync(
                        GroupId.ToString(),
                        messageToSend,
                        _currentUserName,
                        _currentUserFullName
                    );

                    Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Message sent via SignalR");
                }
                catch (Exception signalREx)
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ SignalR send failed: {signalREx.Message}");

                    // Remove temp message and restore text
                    _tempMessageIds.Remove(tempMessageId);
                    Messages.Remove(tempViewModel);
                    MessageText = messageToSend;

                    await Application.Current.MainPage.DisplayAlert(
                        "Error",
                        "Failed to send message. Please try again.",
                        "OK");
                }

                // ✅ 4. Save to API in background (for persistence)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _chatService.SendMessageAsync(GroupId, messageToSend);
                        Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Message saved to API");
                    }
                    catch (Exception apiEx)
                    {
                        Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ API save failed: {apiEx.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Error sending message: {ex.Message}");

                // Remove the failed message
                _tempMessageIds.Remove(tempMessageId);
                var failedMessage = Messages.FirstOrDefault(m => m.Id == tempMessageId);
                if (failedMessage != null)
                {
                    Messages.Remove(failedMessage);
                }

                // Restore message text
                MessageText = messageToSend;

                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to send message: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// ✅ Handle incoming SignalR messages (real-time updates)
        /// </summary>
        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] 📨 SignalR message received");
                Debug.WriteLine($"[CHAT PAGE MODEL]    Group: {e.GroupChatId}");
                Debug.WriteLine($"[CHAT PAGE MODEL]    Sender: {e.SenderFullName}");
                Debug.WriteLine($"[CHAT PAGE MODEL]    Message: {e.Message}");

                // Only process if it's for this group
                if (e.GroupChatId != GroupId.ToString())
                {
                    Debug.WriteLine($"[CHAT PAGE MODEL] Message not for this group, ignoring");
                    return;
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // ✅ If this is OUR message coming back from SignalR, REPLACE the temp one
                    if (e.SenderId == CurrentUserId)
                    {
                        Debug.WriteLine($"[CHAT PAGE MODEL] This is our message coming back from SignalR");

                        // Find and remove ALL temporary messages with same content
                        var tempMessages = Messages
                            .Where(m => _tempMessageIds.Contains(m.Id) &&
                                       m.Message == e.Message)
                            .ToList();

                        foreach (var tempMsg in tempMessages)
                        {
                            Debug.WriteLine($"[CHAT PAGE MODEL] Removing temp message: {tempMsg.Id}");
                            _tempMessageIds.Remove(tempMsg.Id);
                            Messages.Remove(tempMsg);
                        }
                    }

                    // ✅ Check if this exact message ID already exists
                    var messageId = Guid.Parse(e.Id);
                    var existingMessage = Messages.FirstOrDefault(m => m.Id == messageId);
                    if (existingMessage != null)
                    {
                        Debug.WriteLine($"[CHAT PAGE MODEL] Message {messageId} already exists, skipping");
                        return;
                    }

                    // ✅ Add the real message from server
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

                    var viewModel = new GroupMessageItemViewModel(message, CurrentUserId);
                    Messages.Add(viewModel);
                    Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Message added to UI. Total: {Messages.Count}");

                    // ✅ Update cache in background
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
                            Debug.WriteLine($"[CHAT PAGE MODEL] ✅ Cache updated with new message");
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

        ~ChatPageModel()
        {
            _signalRService.MessageReceived -= OnMessageReceived;
        }
    }

    /// <summary>
    /// ViewModel wrapper for GroupMessageItem with UI-specific properties
    /// </summary>
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

            // Check if file is already downloaded
            if (HasAttachment && IsImageAttachment)
            {
                CheckIfDownloaded();
            }
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
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Guid Id => _message.Id;
        public string Message => _message.Message;
        public DateTime SentAt => _message.SentAt;
        public Guid SenderId => _message.SenderId;
        public string SenderName => _message.SenderName;
        public string SenderFullName => _message.SenderFullName;

        public bool IsFromCurrentUser => SenderId.ToString() == _currentUserId;

        public string DisplayName => IsFromCurrentUser ? "You" : (SenderFullName ?? SenderName ?? "Unknown");

        // ✅ Check if there's text content
        public bool HasMessageText => !string.IsNullOrWhiteSpace(_message.Message);

        // Message status properties
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
                var now = DateTime.Now;
                var diff = now - SentAt;

                if (diff.TotalMinutes < 1)
                    return "Just now";
                else if (diff.TotalHours < 1)
                    return $"{(int)diff.TotalMinutes}m ago";
                else if (diff.TotalDays < 1)
                    return SentAt.ToString("h:mm tt");
                else if (diff.TotalDays < 2)
                    return $"Yesterday {SentAt:h:mm tt}";
                else if (diff.TotalDays < 7)
                    return SentAt.ToString("ddd h:mm tt");
                else
                    return SentAt.ToString("MMM d, h:mm tt");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // ATTACHMENT PROPERTIES (WhatsApp style with download state)
        // ═══════════════════════════════════════════════════════════════

        public bool HasAttachment => _message.HasAttachment && !string.IsNullOrEmpty(_message.AttachmentUrl);
        public string AttachmentUrl => _message.AttachmentUrl;
        public string AttachmentName => _message.AttachmentName ?? "File";
        public string AttachmentSize => _message.AttachmentSize ?? "Unknown size";
        public string AttachmentType => _message.AttachmentType ?? "File";
        public string MediaType => _message.MediaType ?? "none";

        // ✅ Download state
        public bool IsDownloaded => _isDownloaded;
        public bool IsDownloading => _isDownloading;
        public string LocalFilePath => _localFilePath;
        public double DownloadProgress => _downloadProgress;
        public bool ShowDownloadButton => (IsImageAttachment || IsDocumentAttachment) && !IsDownloaded && !IsDownloading;

        // ✅ Download icon (changes based on state)
        public string DownloadIcon => IsDownloading ? "✕" : "⬇";

        // ✅ Status text (file size or downloading status)
        public string DownloadStatusText => IsDownloading ? "Downloading..." : AttachmentSize;

        // ✅ Check if attachment is an image (for inline preview)
        public bool IsImageAttachment
        {
            get
            {
                if (!HasAttachment) return false;
                return MediaType == "image" || IsImageExtension(AttachmentType);
            }
        }

        // ✅ Check if attachment is a document (for download card)
        public bool IsDocumentAttachment
        {
            get
            {
                if (!HasAttachment) return false;
                return MediaType == "document" || IsDocumentExtension(AttachmentType);
            }
        }

        // ✅ Get appropriate icon for file type
        public string AttachmentIcon
        {
            get
            {
                if (string.IsNullOrEmpty(AttachmentType))
                    return "\ue24d"; // insert_drive_file

                var extension = AttachmentType.ToLower();
                if (!extension.StartsWith("."))
                    extension = "." + extension;

                return extension switch
                {
                    ".pdf" => "\ue415", // picture_as_pdf
                    ".doc" or ".docx" => "\ue873", // description
                    ".xls" or ".xlsx" => "\ue24d", // insert_drive_file
                    ".ppt" or ".pptx" => "\ue24d", // insert_drive_file
                    ".txt" => "\ue873", // description
                    ".zip" or ".rar" or ".7z" => "\ue2c6", // folder_zip
                    ".jpg" or ".jpeg" or ".png" or ".gif" => "\ue3f4", // image
                    ".mp4" or ".mov" or ".avi" => "\ue04b", // videocam
                    ".mp3" or ".wav" => "\ue310", // audiotrack
                    _ => "\ue24d" // insert_drive_file
                };
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════

        private bool IsImageExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;

            var ext = extension.ToLower();
            if (!ext.StartsWith(".")) ext = "." + ext;

            return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg";
        }

        private bool IsDocumentExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;

            var ext = extension.ToLower();
            if (!ext.StartsWith(".")) ext = "." + ext;

            return ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx"
                       or ".ppt" or ".pptx" or ".txt" or ".zip" or ".rar" or ".7z";
        }
    }
}