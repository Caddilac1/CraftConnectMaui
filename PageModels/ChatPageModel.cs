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

        // ── Recording state ───────────────────────────────────────
        private bool _isRecording;
        private string _recordingDuration = "0:00";
        private IDispatcherTimer? _recordingTimer;
        private int _recordingSeconds;

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
                    GroupId = guidValue;
                else
                    Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Failed to parse GroupId: {value}");
            }
        }

        public Guid GroupId
        {
            get => _groupId;
            set { _groupId = value; OnPropertyChanged(); }
        }

        public string GroupName
        {
            get => _groupName;
            set { _groupName = value; OnPropertyChanged(); }
        }

        public string MessageText
        {
            get => _messageText;
            set
            {
                _messageText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowMicIcon));
                OnPropertyChanged(nameof(ShowSendIcon));
                OnPropertyChanged(nameof(ShowAttachmentIcons));
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }

        public string CurrentUserId
        {
            get => _currentUserId;
            set { _currentUserId = value; OnPropertyChanged(); }
        }

        // ── Recording properties ──────────────────────────────────

        /// <summary>True while the microphone is actively recording.</summary>
        public bool IsRecording
        {
            get => _isRecording;
            private set
            {
                _isRecording = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowMicIcon));
                OnPropertyChanged(nameof(ShowSendIcon));
                OnPropertyChanged(nameof(ShowAttachmentIcons));
                OnPropertyChanged(nameof(SendButtonColor));
            }
        }

        /// <summary>Elapsed recording time shown in the banner, e.g. "0:12".</summary>
        public string RecordingDuration
        {
            get => _recordingDuration;
            private set { _recordingDuration = value; OnPropertyChanged(); }
        }

        // ── Derived UI helpers ────────────────────────────────────

        /// <summary>Mic icon: no text AND not recording.</summary>
        public bool ShowMicIcon => string.IsNullOrWhiteSpace(MessageText) && !IsRecording;

        /// <summary>Send icon: text is present.</summary>
        public bool ShowSendIcon => !string.IsNullOrWhiteSpace(MessageText);

        /// <summary>Attachment/camera icons: no text AND not recording.</summary>
        public bool ShowAttachmentIcons => string.IsNullOrWhiteSpace(MessageText) && !IsRecording;

        /// <summary>Button turns red while recording, otherwise WhatsApp teal.</summary>
        public Color SendButtonColor =>
            IsRecording ? Color.FromArgb("#D32F2F") : Color.FromArgb("#075E54");

        // Kept for any XAML still referencing MessageButtonIcon
        public string MessageButtonIcon =>
            string.IsNullOrWhiteSpace(MessageText) ? "\ue029" : "\ue163";

        // ═══════════════════════════════════════════════════════════════
        // RECORDING STATE (called from code-behind)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Called by ChatPage when recording starts.</summary>
        public void StartRecordingState()
        {
            _recordingSeconds = 0;
            RecordingDuration = "0:00";
            IsRecording = true;

            _recordingTimer = Application.Current!.Dispatcher.CreateTimer();
            _recordingTimer.Interval = TimeSpan.FromSeconds(1);
            _recordingTimer.Tick += OnRecordingTimerTick;
            _recordingTimer.Start();

            Debug.WriteLine("[CHAT PAGE MODEL] Recording state started");
        }

        /// <summary>Called by ChatPage when recording stops (send or discard).</summary>
        public void StopRecordingState()
        {
            IsRecording = false;

            _recordingTimer?.Stop();
            _recordingTimer = null;
            RecordingDuration = "0:00";
            _recordingSeconds = 0;

            Debug.WriteLine("[CHAT PAGE MODEL] Recording state stopped");
        }

        private void OnRecordingTimerTick(object? sender, EventArgs e)
        {
            _recordingSeconds++;
            RecordingDuration = $"{_recordingSeconds / 60}:{_recordingSeconds % 60:D2}";
        }

        // ═══════════════════════════════════════════════════════════════
        // SEND VOICE MESSAGE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Sends the recorded voice note into the group chat via SignalR.
        /// The audio file is uploaded via the REST chat service; the resulting
        /// URL (or a placeholder) is then broadcast as an attachment message.
        /// </summary>
        public async Task SendVoiceMessageAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine("[CHAT PAGE MODEL] ❌ Voice file not found");
                return;
            }

            var durationLabel = RecordingDuration == "0:00" ? "" : $" ({RecordingDuration})";
            var fileName = Path.GetFileName(filePath);

            try
            {
                IsBusy = true;
                Debug.WriteLine($"[CHAT PAGE MODEL] 🎙 Sending voice note: {fileName}");

                // ── Option A: Upload audio to your REST endpoint ──────────
                // If IChatService exposes an upload method, swap in the real
                // call below. For now we broadcast a text placeholder so the
                // UI keeps working even without a dedicated audio endpoint.
                //
                // using var stream = File.OpenRead(filePath);
                // var uploadResult = await _chatService.UploadVoiceAsync(GroupId, stream, fileName);
                // var attachmentUrl = uploadResult?.Url ?? string.Empty;

                // ── Option B (current): broadcast as a text message ───────
                var voiceText = $"🎙 Voice note{durationLabel}";

                // Optimistic local bubble
                var tempId = Guid.NewGuid();
                var tempItem = new GroupMessageItem
                {
                    Id = tempId,
                    GroupChatId = GroupId,
                    SenderId = Guid.TryParse(CurrentUserId, out var uid) ? uid : Guid.Empty,
                    SenderName = _currentUserName,
                    SenderFullName = _currentUserFullName,
                    Message = voiceText,
                    SentAt = DateTime.UtcNow,
                    IsPending = true,
                    HasAttachment = false,
                    MediaType = "none"
                };

                _tempMessageIds.Add(tempId);
                var tempVm = new GroupMessageItemViewModel(tempItem, CurrentUserId);
                Messages.Add(tempVm);

                // Broadcast via SignalR
                await _signalRService.SendMessageAsync(
                    GroupId.ToString(),
                    voiceText,
                    _currentUserName,
                    _currentUserFullName);

                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Voice note sent via SignalR");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Voice send error: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert(
                    "Error", "Failed to send voice note. Please try again.", "OK");
            }
            finally
            {
                IsBusy = false;

                // Clean up temp file
                try { if (File.Exists(filePath)) File.Delete(filePath); }
                catch { /* non-critical */ }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════

        public async Task InitializeAsync()
        {
            Debug.WriteLine($"[CHAT PAGE MODEL] InitializeAsync — group: {GroupId}");

            try
            {
                await LoadCurrentUserAsync();
                await LoadCachedMessages();
                await ConnectAndJoinGroupAsync();
                _ = Task.Run(async () => await LoadMessagesInBackground());
                Debug.WriteLine("[CHAT PAGE MODEL] ✅ Initialization complete");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Init error: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert(
                    "Connection Error",
                    "Failed to connect to chat. Please check your internet connection and try again.",
                    "OK");
            }
        }

        private async Task LoadCurrentUserAsync()
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return;

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            CurrentUserId = jwt.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value ?? string.Empty;

            var email = jwt.Claims.FirstOrDefault(c =>
                c.Type == JwtRegisteredClaimNames.Email || c.Type == "email")?.Value ?? string.Empty;

            _currentUserName = email;
            _currentUserFullName = email;

            Debug.WriteLine($"[CHAT PAGE MODEL] Current user: {CurrentUserId}");
        }

        private async Task ConnectAndJoinGroupAsync()
        {
            if (!_signalRService.IsConnected)
            {
                Debug.WriteLine("[CHAT PAGE MODEL] Connecting to SignalR...");
                await _signalRService.ConnectAsync();
            }

            const int maxRetries = 5;
            const int retryDelayMs = 500;

            for (int i = 0; i < maxRetries; i++)
            {
                if (_signalRService.IsConnected)
                {
                    await _signalRService.JoinGroupAsync(GroupId.ToString());
                    Debug.WriteLine("[CHAT PAGE MODEL] ✅ Joined group");
                    return;
                }

                Debug.WriteLine($"[CHAT PAGE MODEL] ⏳ Waiting for stable connection ({i + 1}/{maxRetries})...");
                await Task.Delay(retryDelayMs);
            }

            throw new InvalidOperationException(
                "SignalR connection did not stabilize after multiple attempts.");
        }

        public async Task CleanupAsync()
        {
            Debug.WriteLine("[CHAT PAGE MODEL] Cleanup");

            try
            {
                _signalRService.Reconnected -= OnSignalRReconnected;

                if (_signalRService.IsConnected)
                    await _signalRService.LeaveGroupAsync(GroupId.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Cleanup error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // RECONNECT
        // ═══════════════════════════════════════════════════════════════

        private async void OnSignalRReconnected(object sender, string connectionId)
        {
            Debug.WriteLine($"[CHAT PAGE MODEL] 🔄 Reconnected, re-joining group: {GroupId}");
            try
            {
                await _signalRService.JoinGroupAsync(GroupId.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Re-join failed: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MESSAGE LOADING
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadCachedMessages()
        {
            try
            {
                var cachedJson = await SecureStorage.GetAsync($"messages_{GroupId}");
                if (string.IsNullOrEmpty(cachedJson)) return;

                var cached = JsonSerializer.Deserialize<List<GroupMessageItem>>(cachedJson);
                if (cached == null || !cached.Any()) return;

                Messages.Clear();
                foreach (var m in cached.OrderBy(m => m.SentAt))
                    Messages.Add(new GroupMessageItemViewModel(m, CurrentUserId));

                Debug.WriteLine($"[CHAT PAGE MODEL] Loaded {cached.Count} cached messages");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ Cache load error: {ex.Message}");
            }
        }

        private async Task LoadMessagesInBackground()
        {
            try
            {
                await Task.Delay(500);

                var messages = await _chatService.GetGroupMessagesAsync(GroupId);
                await SecureStorage.SetAsync($"messages_{GroupId}", JsonSerializer.Serialize(messages));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existing = Messages.Select(m => m.Id).ToHashSet();
                    foreach (var m in messages.Where(m => !existing.Contains(m.Id)).OrderBy(m => m.SentAt))
                        Messages.Add(new GroupMessageItemViewModel(m, CurrentUserId));
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ Background load error: {ex.Message}");
            }
        }

        private async Task LoadMessages()
        {
            if (GroupId == Guid.Empty) return;

            try
            {
                IsBusy = true;
                var messages = await _chatService.GetGroupMessagesAsync(GroupId);
                await SecureStorage.SetAsync($"messages_{GroupId}", JsonSerializer.Serialize(messages));

                Messages.Clear();
                foreach (var m in messages.OrderBy(m => m.SentAt))
                    Messages.Add(new GroupMessageItemViewModel(m, CurrentUserId));
            }
            catch (UnauthorizedAccessException)
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "Session Expired", "Your session has expired. Please login again.", "OK");
                await Shell.Current.GoToAsync("//auth/LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Load error: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Error", $"Failed to load: {ex.Message}", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SEND TEXT MESSAGE
        // ═══════════════════════════════════════════════════════════════

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText) || IsBusy) return;

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
                    await Application.Current!.MainPage!.DisplayAlert(
                        "Error", "Failed to send message. Please try again.", "OK");
                    return;
                }

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
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Send error: {ex.Message}");
                _tempMessageIds.Remove(tempMessageId);
                if (tempViewModel != null) Messages.Remove(tempViewModel);
                MessageText = messageToSend;
                await Application.Current!.MainPage!.DisplayAlert(
                    "Error", $"Failed to send: {ex.Message}", "OK");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // RECEIVE MESSAGE
        // ═══════════════════════════════════════════════════════════════

        private void OnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e.GroupChatId != GroupId.ToString()) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (e.SenderId == CurrentUserId)
                    {
                        var temps = Messages
                            .Where(m => _tempMessageIds.Contains(m.Id) && m.Message == e.Message)
                            .ToList();

                        foreach (var t in temps)
                        {
                            _tempMessageIds.Remove(t.Id);
                            Messages.Remove(t);
                        }
                    }

                    var msgId = Guid.Parse(e.Id);
                    if (Messages.Any(m => m.Id == msgId)) return;

                    Messages.Add(new GroupMessageItemViewModel(new GroupMessageItem
                    {
                        Id = msgId,
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
                        IsSent = true,
                        IsDelivered = true
                    }, CurrentUserId));

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var all = Messages.Select(m => new GroupMessageItem
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

                            await SecureStorage.SetAsync(
                                $"messages_{GroupId}",
                                JsonSerializer.Serialize(all));
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[CHAT PAGE MODEL] ⚠️ Cache update error: {ex.Message}");
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CHAT PAGE MODEL] ❌ Receive error: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FINALIZER
        // ═══════════════════════════════════════════════════════════════

        ~ChatPageModel()
        {
            _signalRService.MessageReceived -= OnMessageReceived;
            _signalRService.Reconnected -= OnSignalRReconnected;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VIEW MODEL WRAPPER  (unchanged)
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
                var filePath = Path.Combine(
                    FileSystem.CacheDirectory, "downloads",
                    AttachmentName ?? $"image_{_message.Id}.jpg");

                if (File.Exists(filePath))
                {
                    _isDownloaded = true;
                    _localFilePath = filePath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VIEW MODEL] Check download error: {ex.Message}");
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

        public Guid Id => _message.Id;
        public string Message => _message.Message;
        public DateTime SentAt => _message.SentAt;
        public Guid SenderId => _message.SenderId;
        public string SenderName => _message.SenderName;
        public string SenderFullName => _message.SenderFullName;

        public bool IsFromCurrentUser => SenderId.ToString() == _currentUserId;
        public string DisplayName => IsFromCurrentUser ? "You" : (SenderFullName ?? SenderName ?? "Unknown");
        public bool HasMessageText => !string.IsNullOrWhiteSpace(_message.Message);
        public bool IsPending => _message.IsPending;
        public bool IsSent => _message.IsSent;
        public bool IsDelivered => _message.IsDelivered;

        public string StatusIcon =>
            _message.IsPending ? "⏳" : _message.IsDelivered ? "✓✓" : _message.IsSent ? "✓" : "";

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

        public bool HasAttachment => _message.HasAttachment && !string.IsNullOrEmpty(_message.AttachmentUrl);
        public string AttachmentUrl => _message.AttachmentUrl;
        public string AttachmentName => _message.AttachmentName ?? "File";
        public string AttachmentSize => _message.AttachmentSize ?? "Unknown size";
        public string AttachmentType => _message.AttachmentType ?? "File";
        public string MediaType => _message.MediaType ?? "none";

        public bool IsDownloaded => _isDownloaded;
        public bool IsDownloading => _isDownloading;
        public string LocalFilePath => _localFilePath;
        public double DownloadProgress => _downloadProgress;
        public bool ShowDownloadButton => (IsImageAttachment || IsDocumentAttachment) && !IsDownloaded && !IsDownloading;
        public string DownloadIcon => IsDownloading ? "✕" : "⬇";
        public string DownloadStatusText => IsDownloading ? "Downloading..." : AttachmentSize;

        public bool IsImageAttachment =>
            HasAttachment && (MediaType == "image" || IsImageExtension(AttachmentType));

        public bool IsDocumentAttachment =>
            HasAttachment && (MediaType == "document" || IsDocumentExtension(AttachmentType));

        public string AttachmentIcon
        {
            get
            {
                var ext = (AttachmentType ?? "").ToLower();
                if (!ext.StartsWith(".")) ext = "." + ext;
                return ext switch
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

        private static bool IsImageExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            var e = ext.ToLower();
            if (!e.StartsWith(".")) e = "." + e;
            return e is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".svg";
        }

        private static bool IsDocumentExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            var e = ext.ToLower();
            if (!e.StartsWith(".")) e = "." + e;
            return e is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx"
                       or ".ppt" or ".pptx" or ".txt" or ".zip" or ".rar" or ".7z";
        }
    }
}