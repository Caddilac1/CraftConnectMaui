using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.PageModels
{
    [QueryProperty(nameof(ConversationId), nameof(ConversationId))]
    [QueryProperty(nameof(OtherUserName), nameof(OtherUserName))]
    [QueryProperty(nameof(OtherUserId), nameof(OtherUserId))]
    // Set when arriving via "Reply Privately" from a group chat
    [QueryProperty(nameof(QuotedGroupSender), nameof(QuotedGroupSender))]
    [QueryProperty(nameof(QuotedGroupMessage), nameof(QuotedGroupMessage))]
    [QueryProperty(nameof(QuotedGroupMessageId), nameof(QuotedGroupMessageId))]
    // Source group info — needed so the quote banner can navigate back
    [QueryProperty(nameof(SourceGroupId), nameof(SourceGroupId))]
    [QueryProperty(nameof(SourceGroupName), nameof(SourceGroupName))]
    public class PrivateChatPageModel : INotifyPropertyChanged
    {
        private readonly IPrivateChatService _dmService;
        private readonly AuthService _authService;
        private readonly IChatSignalRService _signalR;

        private string _conversationId = string.Empty;
        private string _otherUserName = string.Empty;
        private string _otherUserId = string.Empty;
        private string _messageText = string.Empty;
        private string _currentUserId = string.Empty;
        private string _currentUserName = string.Empty;
        private bool _isBusy;

        // Context menu
        private bool _isContextMenuVisible;
        private PrivateMessageItemViewModel? _selectedMessage;

        // Reply
        private bool _isReplying;
        private string _replyingToSender = string.Empty;
        private string _replyingToMessage = string.Empty;
        private PrivateMessageItemViewModel? _replyTarget;

        // Temp message tracking
        private readonly HashSet<Guid> _tempIds = new();
        private HashSet<Guid> _deletedForMeIds = new();
        private const string DeletedPrefix = "dm_deleted_forme_";

        public ObservableCollection<PrivateMessageItemViewModel> Messages { get; } = new();

        public Command SendMessageCommand { get; }
        public Command LoadMessagesCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        public PrivateChatPageModel(
            IPrivateChatService dmService,
            AuthService authService,
            IChatSignalRService signalR)
        {
            _dmService = dmService;
            _authService = authService;
            _signalR = signalR;

            SendMessageCommand = new Command(
                async () => await SendMessageAsync(),
                () => !string.IsNullOrWhiteSpace(MessageText) && !IsBusy);

            LoadMessagesCommand = new Command(async () => await LoadMessages());

            _signalR.PrivateMessageReceived += OnPrivateMessageReceived;
            _signalR.PrivateMessageDeleted += OnPrivateMessageDeleted;
            _signalR.Reconnected += OnReconnected;
        }

        // ── Query properties ──────────────────────────────────────────────

        public string ConversationId
        {
            get => _conversationId;
            set { _conversationId = Uri.UnescapeDataString(value); OnPropertyChanged(); }
        }

        public string OtherUserName
        {
            get => _otherUserName;
            set { _otherUserName = Uri.UnescapeDataString(value); OnPropertyChanged(); }
        }

        public string OtherUserId
        {
            get => _otherUserId;
            set { _otherUserId = Uri.UnescapeDataString(value); OnPropertyChanged(); }
        }

        // Prequoted message from "Reply Privately" in group chat
        public string? QuotedGroupSender { get; set; }
        public string? QuotedGroupMessage { get; set; }

        /// <summary>Id of the specific group message that was quoted.</summary>
        public string? QuotedGroupMessageId { get; set; }

        /// <summary>GroupId of the group chat the quote came from.</summary>
        public string? SourceGroupId { get; set; }

        /// <summary>Display name of the source group.</summary>
        public string? SourceGroupName { get; set; }

        // ── Bindable properties ───────────────────────────────────────────

        public string MessageText
        {
            get => _messageText;
            set
            {
                _messageText = value;
                OnPropertyChanged();
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); ((Command)SendMessageCommand).ChangeCanExecute(); }
        }

        public bool IsContextMenuVisible
        {
            get => _isContextMenuVisible;
            private set { _isContextMenuVisible = value; OnPropertyChanged(); }
        }

        public PrivateMessageItemViewModel? SelectedMessage
        {
            get => _selectedMessage;
            private set { _selectedMessage = value; OnPropertyChanged(); }
        }

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

        // ── Context menu ──────────────────────────────────────────────────

        public void OpenContextMenu(PrivateMessageItemViewModel msg)
        {
            SelectedMessage = msg;
            IsContextMenuVisible = true;
        }

        public void CloseContextMenu() => IsContextMenuVisible = false;

        public void ReplyToSelected()
        {
            if (SelectedMessage == null) return;
            _replyTarget = SelectedMessage;
            ReplyingToSender = SelectedMessage.DisplayName;
            ReplyingToMessage = SelectedMessage.Message ?? "📎 Attachment";
            IsReplying = true;
        }

        public void CancelReply()
        {
            IsReplying = false;
            _replyTarget = null;
            ReplyingToSender = string.Empty;
            ReplyingToMessage = string.Empty;
        }

        // ── Init ──────────────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            await LoadCurrentUserAsync();
            await LoadDeletedIdsAsync();

            // PENDING_ prefix: no real conversation ID yet.
            if (ConversationId.StartsWith("PENDING_", StringComparison.Ordinal))
            {
                try
                {
                    var (realId, name) = await _dmService.OpenConversationAsync(OtherUserId);
                    ConversationId = realId;
                    if (string.IsNullOrEmpty(OtherUserName)) OtherUserName = name;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DM MODEL] OpenConversation failed: {ex.Message}");
                    await Application.Current!.MainPage!.DisplayAlert(
                        "Error", "Could not open private chat.", "OK");
                    await Shell.Current.GoToAsync("..");
                    return;
                }
            }

            await LoadCachedMessages();
            await ConnectAndJoinAsync();
            _ = Task.Run(async () => await LoadMessages());

            // Pre-fill reply bar if arriving via "Reply Privately"
            if (!string.IsNullOrEmpty(QuotedGroupSender) && !string.IsNullOrEmpty(QuotedGroupMessage))
            {
                ReplyingToSender = QuotedGroupSender;
                ReplyingToMessage = QuotedGroupMessage;
                IsReplying = true;
            }
        }

        private async Task LoadCurrentUserAsync()
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            _currentUserId = jwt.Claims
                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")
                ?.Value ?? string.Empty;
            var email = jwt.Claims
                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email || c.Type == "email")
                ?.Value ?? string.Empty;
            _currentUserName = email;
        }

        private async Task ConnectAndJoinAsync()
        {
            if (!_signalR.IsConnected)
                await _signalR.ConnectAsync();

            for (int i = 0; i < 5; i++)
            {
                if (_signalR.IsConnected)
                {
                    await _signalR.JoinPrivateConversationAsync(ConversationId);
                    return;
                }
                await Task.Delay(400);
            }
        }

        public async Task CleanupAsync()
        {
            _signalR.PrivateMessageReceived -= OnPrivateMessageReceived;
            _signalR.PrivateMessageDeleted -= OnPrivateMessageDeleted;
            _signalR.Reconnected -= OnReconnected;

            if (_signalR.IsConnected)
                await _signalR.LeavePrivateConversationAsync(ConversationId);
        }

        private async void OnReconnected(object? sender, string connectionId)
        {
            try { await _signalR.JoinPrivateConversationAsync(ConversationId); }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Re-join: {ex.Message}"); }
        }

        // ── Message loading ───────────────────────────────────────────────

        private async Task LoadCachedMessages()
        {
            try
            {
                var json = await SecureStorage.GetAsync($"dm_{ConversationId}");
                if (string.IsNullOrEmpty(json)) return;
                var cached = JsonSerializer.Deserialize<List<PrivateMessageItem>>(json);
                if (cached == null) return;
                Messages.Clear();
                foreach (var m in cached.OrderBy(m => m.SentAt))
                {
                    if (_deletedForMeIds.Contains(m.Id)) continue;
                    Messages.Add(new PrivateMessageItemViewModel(m, _currentUserId));
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Cache: {ex.Message}"); }
        }

        private async Task LoadMessages()
        {
            if (string.IsNullOrEmpty(ConversationId)) return;
            try
            {
                IsBusy = true;
                var messages = await _dmService.GetMessagesAsync(ConversationId);
                var filtered = messages
                    .Where(m => !_deletedForMeIds.Contains(m.Id))
                    .ToList();

                await SecureStorage.SetAsync($"dm_{ConversationId}", JsonSerializer.Serialize(filtered));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existing = Messages.Select(m => m.Id).ToHashSet();
                    foreach (var m in filtered
                        .Where(m => !existing.Contains(m.Id))
                        .OrderBy(m => m.SentAt))
                    {
                        Messages.Add(new PrivateMessageItemViewModel(m, _currentUserId));
                    }
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Load: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        // ── Send ──────────────────────────────────────────────────────────

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(MessageText) || IsBusy) return;

            var text = MessageText.Trim();
            var tempId = Guid.NewGuid();
            PrivateMessageItemViewModel? tempVm = null;

            // Capture the current reply/quote state before clearing it
            var quotedSender = IsReplying && _replyTarget == null ? QuotedGroupSender : null;
            var quotedMsg = IsReplying && _replyTarget == null ? QuotedGroupMessage : null;
            var quotedMsgId = IsReplying && _replyTarget == null
                ? (Guid.TryParse(QuotedGroupMessageId, out var qid) ? qid : (Guid?)null)
                : null;
            var replyTarget = _replyTarget;

            try
            {
                var tempItem = new PrivateMessageItem
                {
                    Id = tempId,
                    ConversationId = ConversationId,
                    SenderId = Guid.TryParse(_currentUserId, out var uid) ? uid : Guid.Empty,
                    SenderName = _currentUserName,
                    Message = text,
                    SentAt = DateTime.UtcNow,
                    IsPending = true,
                    QuotedGroupSender = quotedSender,
                    QuotedGroupMessage = quotedMsg,
                    QuotedGroupMessageId = quotedMsgId,
                    SourceGroupId = SourceGroupId,
                    ReplyToMessageId = replyTarget?.Id
                };

                _tempIds.Add(tempId);
                tempVm = new PrivateMessageItemViewModel(tempItem, _currentUserId);
                Messages.Add(tempVm);

                MessageText = string.Empty;
                CancelReply();
                // Clear the "Reply Privately" quote after first send
                QuotedGroupSender = null;
                QuotedGroupMessage = null;
                QuotedGroupMessageId = null;

                var (ok, realId) = await _dmService.SendMessageAsync(
                    ConversationId, text,
                    replyToMessageId: replyTarget?.Id.ToString(),
                    quotedGroupSender: quotedSender,
                    quotedGroupMessage: quotedMsg);

                if (!ok || realId == null)
                    throw new Exception("Server rejected the message.");

                await _signalR.SendPrivateMessageAsync(
                    ConversationId, realId, text,
                    quotedGroupSender: quotedSender,
                    quotedGroupMessage: quotedMsg,
                    replyToMessageId: replyTarget?.Id.ToString());

                _tempIds.Remove(tempId);
                Messages.Remove(tempVm);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DM MODEL] Send: {ex.Message}");
                _tempIds.Remove(tempId);
                if (tempVm != null) Messages.Remove(tempVm);
                MessageText = text;
                await Application.Current!.MainPage!.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // ── Receive ───────────────────────────────────────────────────────

        private void OnPrivateMessageReceived(object? sender, PrivateMessageReceivedEventArgs e)
        {
            if (e.ConversationId != ConversationId) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!Guid.TryParse(e.Id, out var id)) return;
                if (Messages.Any(m => m.Id == id)) return;
                if (_deletedForMeIds.Contains(id)) return;

                if (e.SenderId == _currentUserId)
                {
                    var temps = Messages
                        .Where(m => _tempIds.Contains(m.Id) && m.Message == e.Message)
                        .ToList();
                    foreach (var t in temps)
                    {
                        _tempIds.Remove(t.Id);
                        Messages.Remove(t);
                    }
                }

                Messages.Add(new PrivateMessageItemViewModel(new PrivateMessageItem
                {
                    Id = id,
                    ConversationId = e.ConversationId,
                    SenderId = Guid.TryParse(e.SenderId, out var sid) ? sid : Guid.Empty,
                    SenderName = e.SenderName,
                    Message = e.Message,
                    SentAt = e.SentAt,
                    HasAttachment = e.HasAttachment,
                    AttachmentUrl = e.AttachmentUrl,
                    AttachmentName = e.AttachmentName,
                    AttachmentType = e.AttachmentType,
                    MediaType = e.MediaType ?? "none",
                    QuotedGroupSender = e.QuotedGroupSender,
                    QuotedGroupMessage = e.QuotedGroupMessage,
                    // SourceGroupId comes from the model's current context if present
                    SourceGroupId = SourceGroupId,
                    IsSent = true,
                    IsDelivered = true
                }, _currentUserId));

                _ = Task.Run(PersistMessagesAsync);
            });
        }

        private void OnPrivateMessageDeleted(object? sender, string messageId)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!Guid.TryParse(messageId, out var id)) return;
                var msg = Messages.FirstOrDefault(m => m.Id == id);
                if (msg != null) await DeleteForSelfAsync(msg);
            });
        }

        // ── Delete ────────────────────────────────────────────────────────

        public async Task DeleteForSelfAsync(PrivateMessageItemViewModel msg)
        {
            _deletedForMeIds.Add(msg.Id);
            Messages.Remove(msg);
            await PersistDeletedIdsAsync();
            await PersistMessagesAsync();
        }

        public async Task DeleteForEveryoneAsync(PrivateMessageItemViewModel msg)
        {
            if (_signalR.IsConnected)
            {
                try { await _signalR.DeletePrivateMessageAsync(ConversationId, msg.Id.ToString()); }
                catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Delete broadcast: {ex.Message}"); }
            }

            await _dmService.DeleteForEveryoneAsync(msg.Id.ToString());
            await DeleteForSelfAsync(msg);
        }

        // ── Persistence ───────────────────────────────────────────────────

        private async Task PersistMessagesAsync()
        {
            try
            {
                var all = Messages
                    .Where(m => !_deletedForMeIds.Contains(m.Id))
                    .Select(m => new PrivateMessageItem
                    {
                        Id = m.Id,
                        ConversationId = ConversationId,
                        SenderId = m.SenderId,
                        SenderName = m.SenderName,
                        Message = m.Message,
                        SentAt = m.SentAt,
                        HasAttachment = m.HasAttachment,
                        AttachmentUrl = m.AttachmentUrl,
                        AttachmentName = m.AttachmentName,
                        AttachmentType = m.AttachmentType,
                        MediaType = m.MediaType,
                        QuotedGroupSender = m.QuotedGroupSender,
                        QuotedGroupMessage = m.QuotedGroupMessage,
                        QuotedGroupMessageId = m.QuotedGroupMessageId,
                        SourceGroupId = m.SourceGroupId,
                        IsSent = m.IsSent,
                        IsDelivered = m.IsDelivered
                    }).ToList();

                await SecureStorage.SetAsync($"dm_{ConversationId}", JsonSerializer.Serialize(all));
            }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Persist: {ex.Message}"); }
        }

        private async Task LoadDeletedIdsAsync()
        {
            try
            {
                var json = await SecureStorage.GetAsync($"{DeletedPrefix}{ConversationId}");
                if (string.IsNullOrEmpty(json)) return;
                var ids = JsonSerializer.Deserialize<List<string>>(json);
                if (ids == null) return;
                _deletedForMeIds = ids
                    .Where(s => Guid.TryParse(s, out _))
                    .Select(Guid.Parse)
                    .ToHashSet();
            }
            catch { }
        }

        private async Task PersistDeletedIdsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    _deletedForMeIds.Select(id => id.ToString()).ToList());
                await SecureStorage.SetAsync($"{DeletedPrefix}{ConversationId}", json);
            }
            catch { }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        ~PrivateChatPageModel()
        {
            _signalR.PrivateMessageReceived -= OnPrivateMessageReceived;
            _signalR.PrivateMessageDeleted -= OnPrivateMessageDeleted;
            _signalR.Reconnected -= OnReconnected;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PrivateMessageItemViewModel
    // ═══════════════════════════════════════════════════════════════════

    public class PrivateMessageItemViewModel : INotifyPropertyChanged
    {
        private readonly PrivateMessageItem _msg;
        private readonly string _currentUserId;

        private bool _isDownloaded;
        private bool _isDownloading;
        private string _localFilePath = string.Empty;
        private bool _isStarred;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PrivateMessageItemViewModel(PrivateMessageItem msg, string currentUserId)
        {
            _msg = msg;
            _currentUserId = currentUserId;
        }

        public Guid Id => _msg.Id;
        public Guid SenderId => _msg.SenderId;
        public string SenderName => _msg.SenderName;
        public string? Message => _msg.Message;
        public DateTime SentAt => _msg.SentAt;
        public bool IsPending => _msg.IsPending;
        public bool IsSent => _msg.IsSent;
        public bool IsDelivered => _msg.IsDelivered;

        public bool IsFromCurrentUser => SenderId.ToString() == _currentUserId;

        public string DisplayName =>
            IsFromCurrentUser ? "You"
            : string.IsNullOrWhiteSpace(SenderName) ? "Unknown"
            : SenderName;

        public string SenderInitial =>
            string.IsNullOrWhiteSpace(DisplayName) ? "?" : DisplayName[0].ToString().ToUpper();

        public bool HasMessageText => !string.IsNullOrWhiteSpace(_msg.Message);

        public string StatusIcon =>
            _msg.IsPending ? "⏳" : _msg.IsDelivered ? "✓✓" : _msg.IsSent ? "✓" : "";

        public string DisplayTime
        {
            get
            {
                var diff = DateTime.Now - SentAt;
                if (diff.TotalMinutes < 1) return "Just now";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}m ago";
                if (diff.TotalDays < 1) return SentAt.ToString("h:mm tt");
                if (diff.TotalDays < 2) return $"Yesterday {SentAt:h:mm tt}";
                return SentAt.ToString("MMM d, h:mm tt");
            }
        }

        // Attachment
        public bool HasAttachment => _msg.HasAttachment && !string.IsNullOrEmpty(_msg.AttachmentUrl);
        public string AttachmentUrl => _msg.AttachmentUrl ?? string.Empty;
        public string? AttachmentName => _msg.AttachmentName;
        public string? AttachmentSize => _msg.AttachmentSize;
        public string? AttachmentType => _msg.AttachmentType;
        public string MediaType => _msg.MediaType;
        public bool IsDownloaded => _isDownloaded;
        public bool IsDownloading => _isDownloading;
        public string LocalFilePath => _localFilePath;

        public bool IsImageAttachment =>
            HasAttachment && (MediaType == "image" ||
                new[] { "jpg", "jpeg", "png", "gif", "webp" }
                    .Contains(AttachmentType?.TrimStart('.').ToLower()));

        public bool IsDocumentAttachment =>
            HasAttachment && !IsImageAttachment;

        public void MarkAsDownloading()
        {
            _isDownloading = true;
            Notify(nameof(IsDownloading));
        }

        public void MarkAsDownloaded(string path)
        {
            _isDownloaded = true;
            _isDownloading = false;
            _localFilePath = path;
            Notify(nameof(IsDownloaded), nameof(IsDownloading), nameof(LocalFilePath));
        }

        public void CancelDownload()
        {
            _isDownloading = false;
            Notify(nameof(IsDownloading));
        }

        // Reply
        public bool HasReply => !string.IsNullOrEmpty(_msg.ReplyToMessageId?.ToString());
        public Guid? ReplyToId => _msg.ReplyToMessageId;

        // Group quote (Reply Privately)
        public bool HasGroupQuote =>
            !string.IsNullOrEmpty(_msg.QuotedGroupSender) ||
            !string.IsNullOrEmpty(_msg.QuotedGroupMessage);
        public string? QuotedGroupSender => _msg.QuotedGroupSender;
        public string? QuotedGroupMessage => _msg.QuotedGroupMessage;

        /// <summary>
        /// The specific group message ID that was quoted. Used by the page to pass
        /// ScrollToMessageId back to ChatPage when the user taps the quote banner.
        /// </summary>
        public Guid? QuotedGroupMessageId => _msg.QuotedGroupMessageId;

        /// <summary>
        /// GroupId of the source group chat. Stored so navigating back works even
        /// after the message has been cached and the query params are gone.
        /// </summary>
        public string? SourceGroupId => _msg.SourceGroupId;

        // Star
        public bool IsStarred => _isStarred;
        public string StarLabel => _isStarred ? "Unstar" : "Star";

        public void ToggleStar()
        {
            _isStarred = !_isStarred;
            Notify(nameof(IsStarred), nameof(StarLabel));
        }

        private void Notify(params string[] names)
        {
            foreach (var n in names)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }
    }
}