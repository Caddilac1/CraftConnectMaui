using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.SignalR.Client;
using Path = System.IO.Path;

namespace CraftConnect_Mobile_App.PageModels
{
    [QueryProperty(nameof(GroupIdString), "GroupId")]
    [QueryProperty(nameof(GroupName), nameof(GroupName))]
    // Received from PrivateChatPage when user taps a quoted group message
    [QueryProperty(nameof(ScrollToMessageIdString), "ScrollToMessageId")]
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

        // ── Recording ─────────────────────────────────────────────
        private bool _isRecording;
        private string _recordingDuration = "0:00";
        private IDispatcherTimer? _recordingTimer;
        private int _recordingSeconds;

        // ── Context menu ──────────────────────────────────────────
        private bool _isContextMenuVisible;
        private GroupMessageItemViewModel? _selectedMessage;

        // ── Sender name popup ─────────────────────────────────────
        private bool _isSenderMenuVisible;
        private string _senderMenuName = string.Empty;

        // ── Reply ─────────────────────────────────────────────────
        private bool _isReplying;
        private string _replyingToSender = string.Empty;
        private string _replyingToMessage = string.Empty;
        private GroupMessageItemViewModel? _replyTargetMessage;

        // ── Highlight ─────────────────────────────────────────────
        private Guid? _highlightedMessageId;

        // ── Deleted message IDs (for-me, persisted per group) ─────
        private HashSet<Guid> _deletedForMeIds = new();
        private const string DeletedForMePrefix = "deleted_forme_";

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
            _signalRService.MessageDeleted += OnMessageDeleted;
        }

        // ═══════════════════════════════════════════════════════════
        // QUERY PROPERTIES
        // ═══════════════════════════════════════════════════════════

        public string GroupIdString
        {
            set
            {
                if (Guid.TryParse(value, out var g)) GroupId = g;
                else Debug.WriteLine($"[MODEL] Bad GroupId: {value}");
            }
        }

        /// <summary>
        /// Set by PrivateChatPage when user taps a quoted group message banner.
        /// Triggers ScrollToMessage on the page after initialization.
        /// </summary>
        public string? ScrollToMessageIdString
        {
            set
            {
                if (!string.IsNullOrEmpty(value) && Guid.TryParse(value, out var id))
                    _pendingScrollToMessageId = id;
            }
        }
        private Guid? _pendingScrollToMessageId;

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

        // ── Recording ─────────────────────────────────────────────

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

        public string RecordingDuration
        {
            get => _recordingDuration;
            private set { _recordingDuration = value; OnPropertyChanged(); }
        }

        // ── Derived UI ────────────────────────────────────────────

        public bool ShowMicIcon => string.IsNullOrWhiteSpace(MessageText) && !IsRecording;
        public bool ShowSendIcon => !string.IsNullOrWhiteSpace(MessageText);
        public bool ShowAttachmentIcons => string.IsNullOrWhiteSpace(MessageText) && !IsRecording;

        public Color SendButtonColor =>
            IsRecording ? Color.FromArgb("#D32F2F") : Color.FromArgb("#075E54");

        // ── Context menu ──────────────────────────────────────────

        public bool IsContextMenuVisible
        {
            get => _isContextMenuVisible;
            private set { _isContextMenuVisible = value; OnPropertyChanged(); }
        }

        public GroupMessageItemViewModel? SelectedMessage
        {
            get => _selectedMessage;
            private set { _selectedMessage = value; OnPropertyChanged(); }
        }

        public void OpenContextMenu(GroupMessageItemViewModel msg)
        {
            SelectedMessage = msg;
            IsContextMenuVisible = true;
        }

        public void CloseContextMenu()
        {
            IsContextMenuVisible = false;
            // SelectedMessage intentionally kept so handlers can still read it
        }

        // ── Sender name popup ─────────────────────────────────────

        public bool IsSenderMenuVisible
        {
            get => _isSenderMenuVisible;
            private set { _isSenderMenuVisible = value; OnPropertyChanged(); }
        }

        public string SenderMenuName
        {
            get => _senderMenuName;
            private set { _senderMenuName = value; OnPropertyChanged(); }
        }

        public void ShowSenderMenu(string displayName)
        {
            SenderMenuName = displayName;
            IsSenderMenuVisible = true;
        }

        public void HideSenderMenu()
        {
            IsSenderMenuVisible = false;
        }

        // ── Reply ─────────────────────────────────────────────────

        public bool IsReplying
        {
            get => _isReplying;
            private set { _isReplying = value; OnPropertyChanged(); }
        }

        public string ReplyingToSender
        {
            get => _replyingToSender;
            private set { _replyingToSender = value; OnPropertyChanged(); }
        }

        public string ReplyingToMessage
        {
            get => _replyingToMessage;
            private set { _replyingToMessage = value; OnPropertyChanged(); }
        }

        public void ReplyToSelected()
        {
            if (SelectedMessage == null) return;

            _replyTargetMessage = SelectedMessage;
            ReplyingToSender = SelectedMessage.DisplayName;
            ReplyingToMessage = SelectedMessage.IsVoiceMessage
                ? "🎙 Voice note"
                : SelectedMessage.Message;
            IsReplying = true;

            Debug.WriteLine($"[MODEL] 💬 Replying to: {ReplyingToSender} — \"{ReplyingToMessage}\"");
        }

        public void CancelReply()
        {
            IsReplying = false;
            ReplyingToSender = string.Empty;
            ReplyingToMessage = string.Empty;
            _replyTargetMessage = null;
        }

        // ── Highlight (flash a message after scroll-to) ───────────

        public void HighlightMessage(GroupMessageItemViewModel msg)
        {
            // Clear previous highlight
            if (_highlightedMessageId.HasValue)
            {
                var prev = Messages.FirstOrDefault(m => m.Id == _highlightedMessageId.Value);
                prev?.SetHighlighted(false);
            }

            _highlightedMessageId = msg.Id;
            msg.SetHighlighted(true);

            // Auto-clear after 1.5 s
            Task.Run(async () =>
            {
                await Task.Delay(1500);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    msg.SetHighlighted(false);
                    _highlightedMessageId = null;
                });
            });
        }

        // ═══════════════════════════════════════════════════════════
        // DELETE FOR SELF
        // ═══════════════════════════════════════════════════════════

        public async Task DeleteMessageForSelfAsync(GroupMessageItemViewModel msg)
        {
            try
            {
                _deletedForMeIds.Add(msg.Id);
                Messages.Remove(msg);
                await PersistDeletedIdsAsync();
                await PersistMessagesPublicAsync();
                Debug.WriteLine($"[MODEL] 🗑 Deleted for self: {msg.Id}");
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ❌ DeleteForSelf: {ex.Message}"); }
        }

        // ═══════════════════════════════════════════════════════════
        // DELETE FOR EVERYONE
        // ═══════════════════════════════════════════════════════════

        public async Task DeleteMessageForEveryoneAsync(GroupMessageItemViewModel msg)
        {
            try
            {
                if (_signalRService.IsConnected)
                {
                    try
                    {
                        await _signalRService.DeleteMessageAsync(GroupId.ToString(), msg.Id.ToString());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MODEL] ⚠️ SignalR delete broadcast failed: {ex.Message}");
                    }
                }

                await DeleteMessageForSelfAsync(msg);
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ❌ DeleteForEveryone: {ex.Message}"); }
        }

        private void OnMessageDeleted(object? sender, string messageId)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!Guid.TryParse(messageId, out var id)) return;
                var msg = Messages.FirstOrDefault(m => m.Id == id);
                if (msg != null)
                    await DeleteMessageForSelfAsync(msg);
            });
        }

        // ═══════════════════════════════════════════════════════════
        // RECORDING STATE
        // ═══════════════════════════════════════════════════════════

        public void StartRecordingState()
        {
            _recordingSeconds = 0;
            RecordingDuration = "0:00";
            IsRecording = true;

            _recordingTimer = Application.Current!.Dispatcher.CreateTimer();
            _recordingTimer.Interval = TimeSpan.FromSeconds(1);
            _recordingTimer.Tick += OnRecordingTimerTick;
            _recordingTimer.Start();
        }

        public void StopRecordingState()
        {
            IsRecording = false;
            _recordingTimer?.Stop();
            _recordingTimer = null;
            RecordingDuration = "0:00";
            _recordingSeconds = 0;
        }

        private void OnRecordingTimerTick(object? sender, EventArgs e)
        {
            _recordingSeconds++;
            RecordingDuration = $"{_recordingSeconds / 60}:{_recordingSeconds % 60:D2}";
        }

        // ═══════════════════════════════════════════════════════════
        // SEND VOICE MESSAGE
        // ═══════════════════════════════════════════════════════════

        public async Task SendVoiceMessageAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.WriteLine("[MODEL] ❌ Voice file not found");
                return;
            }

            var duration = RecordingDuration == "0:00" ? "0:01" : RecordingDuration;
            var fileName = Path.GetFileName(filePath);

            try
            {
                IsBusy = true;

                var tempId = Guid.NewGuid();
                var tempItem = new GroupMessageItem
                {
                    Id = tempId,
                    GroupChatId = GroupId,
                    SenderId = Guid.TryParse(CurrentUserId, out var uid) ? uid : Guid.Empty,
                    SenderName = _currentUserName,
                    SenderFullName = _currentUserFullName,
                    Message = string.Empty,
                    SentAt = DateTime.UtcNow,
                    IsPending = true,
                    HasAttachment = true,
                    AttachmentUrl = filePath,
                    AttachmentName = fileName,
                    AttachmentType = ".m4a",
                    MediaType = "audio",
                    VoiceDuration = duration
                };

                _tempMessageIds.Add(tempId);
                var tempVm = new GroupMessageItemViewModel(tempItem, CurrentUserId);
                tempVm.MarkAsDownloaded(filePath);
                Messages.Add(tempVm);

                var voiceText = $"🎙 Voice note ({duration})";
                await _signalRService.SendMessageAsync(
                    GroupId.ToString(), voiceText,
                    _currentUserName, _currentUserFullName);

                Debug.WriteLine("[MODEL] ✅ Voice note sent");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MODEL] ❌ Voice send: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Error", "Failed to send voice note.", "OK");
            }
            finally
            {
                IsBusy = false;
                try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
            }
        }

        // ═══════════════════════════════════════════════════════════
        // INIT
        // ═══════════════════════════════════════════════════════════

        public async Task InitializeAsync()
        {
            try
            {
                await LoadCurrentUserAsync();
                await LoadDeletedIdsAsync();
                await LoadCachedMessages();
                await ConnectAndJoinGroupAsync();
                _ = Task.Run(async () => await LoadMessagesInBackground());

                // If we arrived here via a "scroll to message" navigation from PrivateChatPage
                if (_pendingScrollToMessageId.HasValue)
                {
                    var scrollId = _pendingScrollToMessageId.Value;
                    _pendingScrollToMessageId = null;
                    // Small delay to let the UI render before scrolling
                    await Task.Delay(400);
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Raise event so the page code-behind can perform the scroll
                        ScrollToMessageRequested?.Invoke(this, scrollId);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MODEL] ❌ Init: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert(
                    "Connection Error",
                    "Failed to connect to chat. Check your connection and try again.", "OK");
            }
        }

        /// <summary>
        /// Raised after initialization when a ScrollToMessageId was passed in as a query param.
        /// ChatPage subscribes to this and calls MessagesCollectionView.ScrollTo().
        /// </summary>
        public event EventHandler<Guid>? ScrollToMessageRequested;

        private async Task LoadCurrentUserAsync()
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return;

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            CurrentUserId = jwt.Claims
                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")
                ?.Value ?? string.Empty;

            var email = jwt.Claims
                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email || c.Type == "email")
                ?.Value ?? string.Empty;

            _currentUserName = email;
            _currentUserFullName = email;
        }

        private async Task LoadDeletedIdsAsync()
        {
            try
            {
                var key = $"{DeletedForMePrefix}{GroupId}";
                var json = await SecureStorage.GetAsync(key);
                if (string.IsNullOrEmpty(json)) return;

                var ids = JsonSerializer.Deserialize<List<string>>(json);
                if (ids == null) return;

                _deletedForMeIds = ids
                    .Where(s => Guid.TryParse(s, out _))
                    .Select(Guid.Parse)
                    .ToHashSet();

                Debug.WriteLine($"[MODEL] 📋 Loaded {_deletedForMeIds.Count} deleted-for-me IDs");
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ⚠️ LoadDeletedIds: {ex.Message}"); }
        }

        private async Task PersistDeletedIdsAsync()
        {
            try
            {
                var key = $"{DeletedForMePrefix}{GroupId}";
                var json = JsonSerializer.Serialize(_deletedForMeIds.Select(id => id.ToString()).ToList());
                await SecureStorage.SetAsync(key, json);
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ⚠️ PersistDeletedIds: {ex.Message}"); }
        }

        private async Task ConnectAndJoinGroupAsync()
        {
            if (!_signalRService.IsConnected)
                await _signalRService.ConnectAsync();

            for (int i = 0; i < 5; i++)
            {
                if (_signalRService.IsConnected)
                {
                    await _signalRService.JoinGroupAsync(GroupId.ToString());
                    return;
                }
                await Task.Delay(500);
            }
            throw new InvalidOperationException("SignalR connection did not stabilize.");
        }

        public async Task CleanupAsync()
        {
            try
            {
                _signalRService.MessageReceived -= OnMessageReceived;
                _signalRService.Reconnected -= OnSignalRReconnected;
                _signalRService.MessageDeleted -= OnMessageDeleted;

                if (_signalRService.IsConnected)
                    await _signalRService.LeaveGroupAsync(GroupId.ToString());
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ❌ Cleanup: {ex.Message}"); }
        }

        private async void OnSignalRReconnected(object? sender, string connectionId)
        {
            try { await _signalRService.JoinGroupAsync(GroupId.ToString()); }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ❌ Re-join: {ex.Message}"); }
        }

        // ═══════════════════════════════════════════════════════════
        // MESSAGE LOADING
        // ═══════════════════════════════════════════════════════════

        private async Task LoadCachedMessages()
        {
            try
            {
                var json = await SecureStorage.GetAsync($"messages_{GroupId}");
                if (string.IsNullOrEmpty(json)) return;

                var cached = JsonSerializer.Deserialize<List<GroupMessageItem>>(json);
                if (cached == null || !cached.Any()) return;

                Messages.Clear();
                foreach (var m in cached.OrderBy(m => m.SentAt))
                {
                    if (_deletedForMeIds.Contains(m.Id)) continue;
                    Messages.Add(new GroupMessageItemViewModel(m, CurrentUserId));
                }

                Debug.WriteLine($"[MODEL] 📦 Loaded {Messages.Count} messages from cache");
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ⚠️ Cache: {ex.Message}"); }
        }

        private async Task LoadMessagesInBackground()
        {
            try
            {
                await Task.Delay(500);
                var messages = await _chatService.GetGroupMessagesAsync(GroupId);

                var filtered = messages
                    .Where(m => !_deletedForMeIds.Contains(m.Id))
                    .ToList();

                await SecureStorage.SetAsync(
                    $"messages_{GroupId}",
                    JsonSerializer.Serialize(filtered));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existing = Messages.Select(m => m.Id).ToHashSet();
                    foreach (var m in filtered
                        .Where(m => !existing.Contains(m.Id))
                        .OrderBy(m => m.SentAt))
                    {
                        Messages.Add(new GroupMessageItemViewModel(m, CurrentUserId));
                    }
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ⚠️ BgLoad: {ex.Message}"); }
        }

        private async Task LoadMessages()
        {
            if (GroupId == Guid.Empty) return;
            try
            {
                IsBusy = true;
                var messages = await _chatService.GetGroupMessagesAsync(GroupId);

                var filtered = messages
                    .Where(m => !_deletedForMeIds.Contains(m.Id))
                    .ToList();

                await SecureStorage.SetAsync(
                    $"messages_{GroupId}",
                    JsonSerializer.Serialize(filtered));

                Messages.Clear();
                foreach (var m in filtered.OrderBy(m => m.SentAt))
                    Messages.Add(new GroupMessageItemViewModel(m, CurrentUserId));
            }
            catch (UnauthorizedAccessException)
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "Session Expired", "Please login again.", "OK");
                await Shell.Current.GoToAsync("//auth/LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MODEL] ❌ Load: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert("Error", ex.Message, "OK");
            }
            finally { IsBusy = false; }
        }

        // ═══════════════════════════════════════════════════════════
        // SEND TEXT MESSAGE
        // ═══════════════════════════════════════════════════════════

        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(MessageText) || IsBusy) return;

            var text = MessageText.Trim();
            var tempId = Guid.NewGuid();
            GroupMessageItemViewModel? tempVm = null;

            var replyTarget = _replyTargetMessage;

            try
            {
                var tempItem = new GroupMessageItem
                {
                    Id = tempId,
                    GroupChatId = GroupId,
                    SenderId = Guid.Parse(CurrentUserId),
                    SenderName = _currentUserName,
                    SenderFullName = _currentUserFullName,
                    Message = text,
                    SentAt = DateTime.UtcNow,
                    IsPending = true,
                    HasAttachment = false,
                    MediaType = "none",
                    ReplyToMessageId = replyTarget?.Id,
                    ReplyToSender = replyTarget?.DisplayName,
                    ReplyToMessage = replyTarget?.IsVoiceMessage == true
                        ? "🎙 Voice note"
                        : replyTarget?.Message
                };

                _tempMessageIds.Add(tempId);
                tempVm = new GroupMessageItemViewModel(tempItem, CurrentUserId);
                Messages.Add(tempVm);

                MessageText = string.Empty;
                CancelReply();

                string wireText = text;
                if (replyTarget != null)
                {
                    var replyMsg = replyTarget.IsVoiceMessage
                        ? "🎙 Voice note"
                        : EscapePipeInReplyText(replyTarget.Message);
                    var replyName = EscapePipeInReplyText(replyTarget.DisplayName);
                    wireText = $"«REPLY|{replyTarget.Id}|{replyName}|{replyMsg}»{text}";
                }

                Debug.WriteLine($"[MODEL] 📤 Wire: {wireText}");

                await _signalRService.SendMessageAsync(
                    GroupId.ToString(), wireText,
                    _currentUserName, _currentUserFullName);

                _ = Task.Run(async () =>
                {
                    try { await _chatService.SendMessageAsync(GroupId, text); }
                    catch (Exception ex) { Debug.WriteLine($"[MODEL] ⚠️ API save: {ex.Message}"); }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MODEL] ❌ Send: {ex.Message}");
                _tempMessageIds.Remove(tempId);
                if (tempVm != null) Messages.Remove(tempVm);
                MessageText = text;
                await Application.Current!.MainPage!.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // ═══════════════════════════════════════════════════════════
        // RECEIVE MESSAGE FROM SIGNALR
        // ═══════════════════════════════════════════════════════════

        private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
        {
            try
            {
                if (e.GroupChatId != GroupId.ToString()) return;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ParseReplyPrefix(e.Message,
                        out var replyId, out var replyToSender, out var replyToMsg, out var plainMessage);

                    if (e.SenderId == CurrentUserId)
                    {
                        var temps = Messages
                            .Where(m => _tempMessageIds.Contains(m.Id) && m.Message == plainMessage)
                            .ToList();

                        foreach (var t in temps)
                        {
                            _tempMessageIds.Remove(t.Id);
                            Messages.Remove(t);
                        }
                    }

                    var msgId = Guid.Parse(e.Id);
                    if (Messages.Any(m => m.Id == msgId)) return;
                    if (_deletedForMeIds.Contains(msgId)) return;

                    Messages.Add(new GroupMessageItemViewModel(new GroupMessageItem
                    {
                        Id = msgId,
                        GroupChatId = Guid.Parse(e.GroupChatId),
                        SenderId = Guid.Parse(e.SenderId),
                        SenderName = e.SenderName,
                        SenderFullName = e.SenderFullName,
                        Message = plainMessage,
                        SentAt = e.SentAt,
                        HasAttachment = e.HasAttachment,
                        AttachmentUrl = e.AttachmentUrl,
                        AttachmentName = e.AttachmentName,
                        AttachmentSize = e.AttachmentSize,
                        AttachmentType = e.AttachmentType,
                        MediaType = e.MediaType ?? "none",
                        IsSent = true,
                        IsDelivered = true,
                        ReplyToMessageId = replyId,
                        ReplyToSender = replyToSender,
                        ReplyToMessage = replyToMsg
                    }, CurrentUserId));

                    _ = Task.Run(PersistMessagesPublicAsync);
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ❌ Receive: {ex.Message}"); }
        }

        // ═══════════════════════════════════════════════════════════
        // PERSISTENCE
        // ═══════════════════════════════════════════════════════════

        public async Task PersistMessagesPublicAsync()
        {
            try
            {
                var all = Messages
                    .Where(m => !_deletedForMeIds.Contains(m.Id))
                    .Select(m => new GroupMessageItem
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
                        MediaType = m.MediaType,
                        IsSent = m.IsSent,
                        IsDelivered = m.IsDelivered,
                        IsRead = m.IsRead,
                        ReplyToMessageId = m.ReplyToId,
                        ReplyToSender = m.ReplyToSender,
                        ReplyToMessage = m.ReplyToMessage
                    }).ToList();

                await SecureStorage.SetAsync($"messages_{GroupId}", JsonSerializer.Serialize(all));
            }
            catch (Exception ex) { Debug.WriteLine($"[MODEL] ⚠️ Persist: {ex.Message}"); }
        }

        // ═══════════════════════════════════════════════════════════
        // REPLY WIRE-FORMAT HELPERS
        // ═══════════════════════════════════════════════════════════

        private static string EscapePipeInReplyText(string? s) =>
            s?.Replace("|", "\u2016") ?? string.Empty;

        private static string UnescapePipe(string? s) =>
            s?.Replace("\u2016", "|") ?? string.Empty;

        private static void ParseReplyPrefix(
            string wireText,
            out Guid? replyId,
            out string? replyToSender,
            out string? replyToMsg,
            out string plainMessage)
        {
            replyId = null;
            replyToSender = null;
            replyToMsg = null;
            plainMessage = wireText;

            if (string.IsNullOrEmpty(wireText) || !wireText.StartsWith("«REPLY|"))
                return;

            var closeBracket = wireText.IndexOf('»');
            if (closeBracket < 0) return;

            var header = wireText[7..closeBracket];
            var parts = header.Split('|', 3);
            if (parts.Length < 3) return;

            if (Guid.TryParse(parts[0], out var id)) replyId = id;
            replyToSender = UnescapePipe(parts[1]);
            replyToMsg = UnescapePipe(parts[2]);
            plainMessage = wireText[(closeBracket + 1)..];

            Debug.WriteLine($"[MODEL] 🔗 Reply — to: {replyToSender}, msg: {replyToMsg}, body: {plainMessage}");
        }

        ~ChatPageModel()
        {
            _signalRService.MessageReceived -= OnMessageReceived;
            _signalRService.Reconnected -= OnSignalRReconnected;
            _signalRService.MessageDeleted -= OnMessageDeleted;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // GROUP MESSAGE ITEM VIEW MODEL
    // ═══════════════════════════════════════════════════════════════

    public class GroupMessageItemViewModel : INotifyPropertyChanged
    {
        private readonly GroupMessageItem _message;
        private readonly string _currentUserId;

        private bool _isDownloaded;
        private bool _isDownloading;
        private string _localFilePath = string.Empty;
        private double _downloadProgress;
        private bool _isPlaying;
        private bool _isStarred;
        private bool _isHighlighted;

        public event PropertyChangedEventHandler? PropertyChanged;

        public GroupMessageItemViewModel(GroupMessageItem message, string currentUserId)
        {
            _message = message;
            _currentUserId = currentUserId;

            if (HasAttachment && (IsImageAttachment || IsDocumentAttachment || IsVoiceMessage))
                CheckIfDownloaded();
        }

        private void CheckIfDownloaded()
        {
            try
            {
                var name = AttachmentName ?? $"file_{_message.Id}{AttachmentType}";
                var path = Path.Combine(FileSystem.CacheDirectory, "downloads", name);
                if (File.Exists(path))
                {
                    _isDownloaded = true;
                    _localFilePath = path;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[VM] CheckDownload: {ex.Message}"); }
        }

        // ── Download state ────────────────────────────────────────

        public void MarkAsDownloading()
        {
            _isDownloading = true;
            Notify(nameof(IsDownloading), nameof(ShowDownloadButton), nameof(DownloadIcon));
        }

        public void UpdateDownloadProgress(double progress)
        {
            _downloadProgress = progress;
            Notify(nameof(DownloadProgress));
        }

        public void MarkAsDownloaded(string filePath)
        {
            _isDownloaded = true;
            _isDownloading = false;
            _localFilePath = filePath;
            _downloadProgress = 100;
            Notify(nameof(IsDownloaded), nameof(IsDownloading), nameof(LocalFilePath),
                   nameof(ShowDownloadButton), nameof(DownloadIcon),
                   nameof(IsDocumentNotDownloaded), nameof(IsDocumentDownloaded));
        }

        public void CancelDownload()
        {
            _isDownloading = false;
            _downloadProgress = 0;
            Notify(nameof(IsDownloading), nameof(ShowDownloadButton),
                   nameof(DownloadProgress), nameof(DownloadIcon));
        }

        // ── Playback ──────────────────────────────────────────────

        public void SetPlaying(bool playing)
        {
            _isPlaying = playing;
            Notify(nameof(IsPlaying), nameof(VoicePlayIcon));
        }

        // ── Highlight (flash when scrolled-to) ────────────────────

        public bool IsHighlighted => _isHighlighted;

        public void SetHighlighted(bool highlighted)
        {
            _isHighlighted = highlighted;
            Notify(nameof(IsHighlighted));
        }

        // ── Star ──────────────────────────────────────────────────

        public bool IsStarred => _isStarred;
        public string StarIcon => _isStarred ? "\ue838" : "\ue83a";
        public string StarLabel => _isStarred ? "Unstar" : "Star";

        public void ToggleStar()
        {
            _isStarred = !_isStarred;
            Notify(nameof(IsStarred), nameof(StarIcon), nameof(StarLabel));
        }

        // ── Identity ──────────────────────────────────────────────

        public Guid Id => _message.Id;
        public string Message => _message.Message;
        public DateTime SentAt => _message.SentAt;
        public Guid SenderId => _message.SenderId;
        public string SenderName => _message.SenderName;
        public string SenderFullName => _message.SenderFullName;

        public bool IsFromCurrentUser => SenderId.ToString() == _currentUserId;

        public string DisplayName =>
            IsFromCurrentUser
                ? "You"
                : (!string.IsNullOrWhiteSpace(SenderFullName) ? SenderFullName : SenderName) ?? "Unknown";

        public string SenderInitial =>
            string.IsNullOrWhiteSpace(DisplayName) ? "?" : DisplayName[0].ToString().ToUpper();

        public bool HasMessageText => !string.IsNullOrWhiteSpace(_message.Message) && !IsVoiceMessage;
        public bool IsPending => _message.IsPending;
        public bool IsSent => _message.IsSent;
        public bool IsDelivered => _message.IsDelivered;
        public bool IsRead => _message.IsRead;

        public string StatusIcon =>
            _message.IsPending ? "⏳" :
            _message.IsDelivered ? "✓✓" :
            _message.IsSent ? "✓" : "";

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

        // ── Attachment ────────────────────────────────────────────

        public bool HasAttachment => _message.HasAttachment && !string.IsNullOrEmpty(_message.AttachmentUrl);
        public string AttachmentUrl => _message.AttachmentUrl ?? string.Empty;
        public string AttachmentName => _message.AttachmentName ?? "File";
        public string AttachmentSize => _message.AttachmentSize ?? "Unknown size";
        public string AttachmentType => _message.AttachmentType ?? string.Empty;
        public string MediaType => _message.MediaType ?? "none";

        public string AttachmentSizeAndType
        {
            get
            {
                var ext = AttachmentType.TrimStart('.').ToUpper();
                return string.IsNullOrEmpty(ext) ? AttachmentSize : $"{AttachmentSize} • {ext}";
            }
        }

        public bool IsDownloaded => _isDownloaded;
        public bool IsDownloading => _isDownloading;
        public string LocalFilePath => _localFilePath;
        public double DownloadProgress => _downloadProgress;

        public bool ShowDownloadButton =>
            (IsImageAttachment || IsDocumentAttachment || IsVoiceMessage)
            && !IsDownloaded && !IsDownloading;

        public string DownloadIcon => IsDownloading ? "✕" : "\ue2c4";
        public string DownloadStatusText => IsDownloading ? "Downloading..." : AttachmentSize;

        // ── Type helpers ──────────────────────────────────────────

        public bool IsImageAttachment =>
            HasAttachment && (MediaType == "image" || IsImageExt(AttachmentType));

        public bool IsDocumentAttachment =>
            HasAttachment && (MediaType == "document" || IsDocExt(AttachmentType));

        public bool IsVoiceMessage =>
            HasAttachment && (MediaType == "audio" || IsAudioExt(AttachmentType));

        public bool IsDocumentNotDownloaded => IsDocumentAttachment && !IsDownloaded;
        public bool IsDocumentDownloaded => IsDocumentAttachment && IsDownloaded;

        // ── Voice ─────────────────────────────────────────────────

        public bool IsPlaying => _isPlaying;
        public string VoicePlayIcon => _isPlaying ? "\ue034" : "\ue037";
        public string VoiceDuration =>
            string.IsNullOrEmpty(_message.VoiceDuration) ? "0:00" : _message.VoiceDuration;

        // ── Reply ─────────────────────────────────────────────────

        public bool HasReply => !string.IsNullOrEmpty(_message.ReplyToMessage);
        public Guid? ReplyToId => _message.ReplyToMessageId;
        public string ReplyToSender => _message.ReplyToSender ?? string.Empty;
        public string ReplyToMessage => _message.ReplyToMessage ?? string.Empty;

        // ── Attachment icon ───────────────────────────────────────

        public string AttachmentIcon
        {
            get
            {
                var ext = AttachmentType.ToLower();
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
                    ".mp3" or ".wav" or ".m4a" => "\ue310",
                    _ => "\ue24d"
                };
            }
        }

        // ── Static extension helpers ──────────────────────────────

        private static bool IsImageExt(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            return ext.ToLower().TrimStart('.') is "jpg" or "jpeg" or "png" or "gif" or "bmp" or "webp" or "svg";
        }

        private static bool IsDocExt(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            return ext.ToLower().TrimStart('.') is
                "pdf" or "doc" or "docx" or "xls" or "xlsx"
                or "ppt" or "pptx" or "txt" or "zip" or "rar" or "7z";
        }

        private static bool IsAudioExt(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            return ext.ToLower().TrimStart('.') is "mp3" or "wav" or "ogg" or "m4a" or "aac" or "flac";
        }

        private void Notify(params string[] names)
        {
            foreach (var n in names)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}