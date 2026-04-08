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
    // Populated when arriving via "Reply Privately" from a group chat
    [QueryProperty(nameof(QuotedGroupSender), nameof(QuotedGroupSender))]
    [QueryProperty(nameof(QuotedGroupMessage), nameof(QuotedGroupMessage))]
    [QueryProperty(nameof(QuotedGroupMessageId), nameof(QuotedGroupMessageId))]
    [QueryProperty(nameof(SourceGroupId), nameof(SourceGroupId))]
    [QueryProperty(nameof(SourceGroupName), nameof(SourceGroupName))]
    public class PrivateChatPageModel : INotifyPropertyChanged
    {
        // ── Dependencies ──────────────────────────────────────────────────────

        private readonly IPrivateChatService _dmService;
        private readonly AuthService _authService;
        private readonly IChatSignalRService _signalR;

        // ── Storage key prefixes ──────────────────────────────────────────────

        private const string PinnedKeyPrefix = "dm_pinned_";
        private const string DeletedKeyPrefix = "dm_deleted_forme_";
        private string MessagesKey => $"dm_{ConversationId}";

        // ── Private state ─────────────────────────────────────────────────────

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

        // Selection mode
        private bool _isSelectionMode;

        // Highlight
        private Guid? _highlightedMessageId;

        // Persisted sets
        private HashSet<Guid> _pinnedIds = new();
        private HashSet<Guid> _deletedForMeIds = new();

        // Temporary optimistic IDs awaiting server confirmation
        private readonly HashSet<Guid> _tempIds = new();

        // ── Public collections ────────────────────────────────────────────────

        public ObservableCollection<PrivateMessageItemViewModel> Messages { get; } = new();
        public ObservableCollection<PrivateMessageItemViewModel> SelectedMessages { get; } = new();

        // ── Commands ──────────────────────────────────────────────────────────

        public Command SendMessageCommand { get; }
        public Command LoadMessagesCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Constructor ───────────────────────────────────────────────────────

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

            LoadMessagesCommand = new Command(async () => await LoadMessagesAsync());

            SubscribeToSignalR();
        }

        // ── Query properties ──────────────────────────────────────────────────

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

        // Cross-chat quote query params (cleared after first send)
        public string? QuotedGroupSender { get; set; }
        public string? QuotedGroupMessage { get; set; }
        public string? QuotedGroupMessageId { get; set; }
        public string? SourceGroupId { get; set; }
        public string? SourceGroupName { get; set; }

        // ── Bindable properties ───────────────────────────────────────────────

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
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                ((Command)SendMessageCommand).ChangeCanExecute();
            }
        }

        // ── Context menu ──────────────────────────────────────────────────────

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

        public void OpenContextMenu(PrivateMessageItemViewModel msg)
        {
            SelectedMessage = msg;
            IsContextMenuVisible = true;
        }

        public void CloseContextMenu() => IsContextMenuVisible = false;

        // ── Reply ─────────────────────────────────────────────────────────────

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

        /// <summary>
        /// Activates the reply banner for the currently selected message.
        /// The selected message becomes the embedded reply reference on send.
        /// </summary>
        public void ReplyToSelected()
        {
            if (SelectedMessage is null) return;

            _replyTarget = SelectedMessage;
            ReplyingToSender = SelectedMessage.DisplayName;
            ReplyingToMessage = SelectedMessage.Message ?? "📎 Attachment";
            IsReplying = true;
        }

        public void CancelReply()
        {
            _replyTarget = null;
            ReplyingToSender = string.Empty;
            ReplyingToMessage = string.Empty;
            IsReplying = false;
        }

        // ── Pin ───────────────────────────────────────────────────────────────

        public void TogglePin(PrivateMessageItemViewModel msg)
        {
            if (msg.IsPinned)
            {
                _pinnedIds.Remove(msg.Id);
                msg.SetPinned(false);
            }
            else
            {
                _pinnedIds.Add(msg.Id);
                msg.SetPinned(true);
            }

            _ = Task.Run(PersistPinnedIdsAsync);
        }

        // ── Selection mode ────────────────────────────────────────────────────

        public bool IsSelectionMode
        {
            get => _isSelectionMode;
            private set { _isSelectionMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectionCountText)); }
        }

        public string SelectionCountText =>
            SelectedMessages.Count == 0 ? "Select messages" : $"{SelectedMessages.Count} selected";

        public void EnterSelectionMode(PrivateMessageItemViewModel firstMsg)
        {
            SelectedMessages.Clear();
            firstMsg.SetSelected(true);
            SelectedMessages.Add(firstMsg);
            IsSelectionMode = true;
            OnPropertyChanged(nameof(SelectionCountText));
        }

        public void ToggleMessageSelection(PrivateMessageItemViewModel msg)
        {
            if (SelectedMessages.Contains(msg))
            {
                msg.SetSelected(false);
                SelectedMessages.Remove(msg);
            }
            else
            {
                msg.SetSelected(true);
                SelectedMessages.Add(msg);
            }

            OnPropertyChanged(nameof(SelectionCountText));

            if (SelectedMessages.Count == 0)
                CancelSelectionMode();
        }

        public void CancelSelectionMode()
        {
            foreach (var m in SelectedMessages)
                m.SetSelected(false);

            SelectedMessages.Clear();
            IsSelectionMode = false;
        }

        public void StarSelectedMessages()
        {
            foreach (var m in SelectedMessages.Where(m => !m.IsStarred))
                m.ToggleStar();
        }

        // ── Highlight ─────────────────────────────────────────────────────────

        public void HighlightMessage(PrivateMessageItemViewModel msg)
        {
            // Clear previous highlight
            if (_highlightedMessageId.HasValue)
            {
                Messages.FirstOrDefault(m => m.Id == _highlightedMessageId.Value)
                        ?.SetHighlighted(false);
            }

            _highlightedMessageId = msg.Id;
            msg.SetHighlighted(true);

            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    msg.SetHighlighted(false);
                    _highlightedMessageId = null;
                });
            });
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            await LoadCurrentUserAsync();
            await LoadDeletedIdsAsync();
            await LoadPinnedIdsAsync();

            if (ConversationId.StartsWith("PENDING_", StringComparison.Ordinal))
                await ResolvePendingConversationAsync();

            await LoadCachedMessagesAsync();
            await ConnectAndJoinAsync();
            _ = Task.Run(LoadMessagesAsync);

            // Pre-fill reply banner when arriving via "Reply Privately" from a group chat
            if (!string.IsNullOrEmpty(QuotedGroupSender) && !string.IsNullOrEmpty(QuotedGroupMessage))
            {
                ReplyingToSender = QuotedGroupSender;
                ReplyingToMessage = QuotedGroupMessage;
                IsReplying = true;
                // _replyTarget intentionally stays null: this is a cross-chat quote,
                // not a reply to a message within this DM conversation.
            }
        }

        public async Task CleanupAsync()
        {
            UnsubscribeFromSignalR();

            if (_signalR.IsConnected)
                await _signalR.LeavePrivateConversationAsync(ConversationId);
        }

        // ── Delete ────────────────────────────────────────────────────────────

        public async Task DeleteForSelfAsync(PrivateMessageItemViewModel msg)
        {
            _deletedForMeIds.Add(msg.Id);
            _pinnedIds.Remove(msg.Id);
            Messages.Remove(msg);

            await Task.WhenAll(
                PersistDeletedIdsAsync(),
                PersistPinnedIdsAsync(),
                PersistMessagesAsync());
        }

        public async Task DeleteForEveryoneAsync(PrivateMessageItemViewModel msg)
        {
            if (_signalR.IsConnected)
            {
                try
                {
                    await _signalR.DeletePrivateMessageAsync(ConversationId, msg.Id.ToString());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DM MODEL] Delete broadcast: {ex.Message}");
                }
            }

            await _dmService.DeleteForEveryoneAsync(msg.Id.ToString());
            await DeleteForSelfAsync(msg);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void SubscribeToSignalR()
        {
            _signalR.PrivateMessageReceived += OnPrivateMessageReceived;
            _signalR.PrivateMessageDeleted += OnPrivateMessageDeleted;
            _signalR.Reconnected += OnReconnected;
        }

        private void UnsubscribeFromSignalR()
        {
            _signalR.PrivateMessageReceived -= OnPrivateMessageReceived;
            _signalR.PrivateMessageDeleted -= OnPrivateMessageDeleted;
            _signalR.Reconnected -= OnReconnected;
        }

        private async Task LoadCurrentUserAsync()
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return;

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            _currentUserId = jwt.Claims
                .FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Sub or "sub")
                ?.Value ?? string.Empty;

            _currentUserName = jwt.Claims
                .FirstOrDefault(c => c.Type is JwtRegisteredClaimNames.Email or "email")
                ?.Value ?? string.Empty;
        }

        private async Task ResolvePendingConversationAsync()
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
            }
        }

        private async Task ConnectAndJoinAsync()
        {
            if (!_signalR.IsConnected)
                await _signalR.ConnectAsync();

            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (_signalR.IsConnected)
                {
                    await _signalR.JoinPrivateConversationAsync(ConversationId);
                    return;
                }
                await Task.Delay(400);
            }
        }

        private async void OnReconnected(object? sender, string connectionId)
        {
            try { await _signalR.JoinPrivateConversationAsync(ConversationId); }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Re-join: {ex.Message}"); }
        }

        // ── Message loading ───────────────────────────────────────────────────

        private async Task LoadCachedMessagesAsync()
        {
            try
            {
                var json = await SecureStorage.GetAsync(MessagesKey);
                if (string.IsNullOrEmpty(json)) return;

                var cached = JsonSerializer.Deserialize<List<PrivateMessageItem>>(json);
                if (cached is null) return;

                Messages.Clear();
                foreach (var item in cached.OrderBy(m => m.SentAt))
                {
                    if (_deletedForMeIds.Contains(item.Id)) continue;
                    var vm = BuildViewModel(item);
                    Messages.Add(vm);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Cache load: {ex.Message}"); }
        }

        private async Task LoadMessagesAsync()
        {
            if (string.IsNullOrEmpty(ConversationId)) return;

            try
            {
                IsBusy = true;

                var messages = await _dmService.GetMessagesAsync(ConversationId);
                var filtered = messages.Where(m => !_deletedForMeIds.Contains(m.Id)).ToList();

                await SecureStorage.SetAsync(MessagesKey, JsonSerializer.Serialize(filtered));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var existingIds = Messages.Select(m => m.Id).ToHashSet();

                    foreach (var item in filtered
                        .Where(m => !existingIds.Contains(m.Id))
                        .OrderBy(m => m.SentAt))
                    {
                        Messages.Add(BuildViewModel(item));
                    }
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Load: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        // ── Send ──────────────────────────────────────────────────────────────

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(MessageText) || IsBusy) return;

            var text = MessageText.Trim();
            var tempId = Guid.NewGuid();
            var replyTarget = _replyTarget;

            // Cross-chat group quote applies only when there's no DM reply target
            var isCrossChatQuote = IsReplying && replyTarget is null;
            var quotedSender = isCrossChatQuote ? QuotedGroupSender : null;
            var quotedMsg = isCrossChatQuote ? QuotedGroupMessage : null;
            var quotedMsgId = isCrossChatQuote && Guid.TryParse(QuotedGroupMessageId, out var qid)
                                    ? qid : (Guid?)null;

            PrivateMessageItemViewModel? tempVm = null;

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
                    // DM reply fields
                    ReplyToMessageId = replyTarget?.Id,
                    ReplyToSenderName = replyTarget?.DisplayName,
                    ReplyToText = replyTarget?.Message,
                    // Cross-chat group quote fields
                    QuotedGroupSender = quotedSender,
                    QuotedGroupMessage = quotedMsg,
                    QuotedGroupMessageId = quotedMsgId,
                    SourceGroupId = SourceGroupId,
                };

                _tempIds.Add(tempId);
                tempVm = BuildViewModel(tempItem);
                Messages.Add(tempVm);

                // Clear input and reply state immediately for responsive UX
                MessageText = string.Empty;
                CancelReply();
                QuotedGroupSender = null;
                QuotedGroupMessage = null;
                QuotedGroupMessageId = null;

                var (ok, realId) = await _dmService.SendMessageAsync(
                    ConversationId, text,
                    replyToMessageId: replyTarget?.Id.ToString(),
                    quotedGroupSender: quotedSender,
                    quotedGroupMessage: quotedMsg);

                if (!ok || realId is null)
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
                if (tempVm is not null) Messages.Remove(tempVm);
                MessageText = text;

                await Application.Current!.MainPage!.DisplayAlert("Error", ex.Message, "OK");
            }
        }

        // ── SignalR handlers ──────────────────────────────────────────────────

        private void OnPrivateMessageReceived(object? sender, PrivateMessageReceivedEventArgs e)
        {
            if (e.ConversationId != ConversationId) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (!Guid.TryParse(e.Id, out var id)) return;
                if (Messages.Any(m => m.Id == id)) return;
                if (_deletedForMeIds.Contains(id)) return;

                // Remove the matching optimistic placeholder for own messages
                if (e.SenderId == _currentUserId)
                {
                    var stale = Messages
                        .Where(m => _tempIds.Contains(m.Id) && m.Message == e.Message)
                        .ToList();

                    foreach (var t in stale)
                    {
                        _tempIds.Remove(t.Id);
                        Messages.Remove(t);
                    }
                }

                var item = new PrivateMessageItem
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
                    SourceGroupId = SourceGroupId,
                    IsSent = true,
                    IsDelivered = true,
                };

                Messages.Add(BuildViewModel(item));
                _ = Task.Run(PersistMessagesAsync);
            });
        }

        private void OnPrivateMessageDeleted(object? sender, string messageId)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!Guid.TryParse(messageId, out var id)) return;

                var msg = Messages.FirstOrDefault(m => m.Id == id);
                if (msg is not null) await DeleteForSelfAsync(msg);
            });
        }

        // ── ViewModel factory ─────────────────────────────────────────────────

        private PrivateMessageItemViewModel BuildViewModel(PrivateMessageItem item)
        {
            var vm = new PrivateMessageItemViewModel(item, _currentUserId);
            if (_pinnedIds.Contains(item.Id)) vm.SetPinned(true);
            return vm;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        private async Task PersistMessagesAsync()
        {
            try
            {
                var snapshots = Messages
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
                        ReplyToMessageId = m.ReplyToId,
                        ReplyToSenderName = m.ReplyToSenderName,
                        ReplyToText = m.ReplyToText,
                        IsSent = m.IsSent,
                        IsDelivered = m.IsDelivered,
                    })
                    .ToList();

                await SecureStorage.SetAsync(MessagesKey, JsonSerializer.Serialize(snapshots));
            }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Persist messages: {ex.Message}"); }
        }

        private async Task LoadPinnedIdsAsync()
        {
            try
            {
                var json = await SecureStorage.GetAsync($"{PinnedKeyPrefix}{ConversationId}");
                if (string.IsNullOrEmpty(json)) return;

                var ids = JsonSerializer.Deserialize<List<string>>(json);
                if (ids is null) return;

                _pinnedIds = ids
                    .Where(s => Guid.TryParse(s, out _))
                    .Select(Guid.Parse)
                    .ToHashSet();
            }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Load pinned: {ex.Message}"); }
        }

        private async Task PersistPinnedIdsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_pinnedIds.Select(id => id.ToString()).ToList());
                await SecureStorage.SetAsync($"{PinnedKeyPrefix}{ConversationId}", json);
            }
            catch (Exception ex) { Debug.WriteLine($"[DM MODEL] Persist pinned: {ex.Message}"); }
        }

        private async Task LoadDeletedIdsAsync()
        {
            try
            {
                var json = await SecureStorage.GetAsync($"{DeletedKeyPrefix}{ConversationId}");
                if (string.IsNullOrEmpty(json)) return;

                var ids = JsonSerializer.Deserialize<List<string>>(json);
                if (ids is null) return;

                _deletedForMeIds = ids
                    .Where(s => Guid.TryParse(s, out _))
                    .Select(Guid.Parse)
                    .ToHashSet();
            }
            catch { /* non-critical – start with empty set */ }
        }

        private async Task PersistDeletedIdsAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(
                    _deletedForMeIds.Select(id => id.ToString()).ToList());
                await SecureStorage.SetAsync($"{DeletedKeyPrefix}{ConversationId}", json);
            }
            catch { /* non-critical */ }
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        ~PrivateChatPageModel() => UnsubscribeFromSignalR();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PrivateMessageItemViewModel
    // ═════════════════════════════════════════════════════════════════════════

    public class PrivateMessageItemViewModel : INotifyPropertyChanged
    {
        private readonly PrivateMessageItem _msg;
        private readonly string _currentUserId;

        private bool _isDownloaded;
        private bool _isDownloading;
        private string _localFilePath = string.Empty;
        private bool _isStarred;
        private bool _isPinned;
        private bool _isSelected;
        private bool _isHighlighted;

        public event PropertyChangedEventHandler? PropertyChanged;

        public PrivateMessageItemViewModel(PrivateMessageItem msg, string currentUserId)
        {
            _msg = msg;
            _currentUserId = currentUserId;
        }

        // ── Identity ──────────────────────────────────────────────────────────

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
            _msg.IsPending ? "⏳" :
            _msg.IsDelivered ? "✓✓" :
            _msg.IsSent ? "✓" : string.Empty;

        public string DisplayTime
        {
            get
            {
                var diff = DateTime.Now - SentAt;
                return diff switch
                {
                    { TotalMinutes: < 1 } => "Just now",
                    { TotalHours: < 1 } => $"{(int)diff.TotalMinutes}m ago",
                    { TotalDays: < 1 } => SentAt.ToString("h:mm tt"),
                    { TotalDays: < 2 } => $"Yesterday {SentAt:h:mm tt}",
                    _ => SentAt.ToString("MMM d, h:mm tt"),
                };
            }
        }

        // ── DM reply ──────────────────────────────────────────────────────────

        public bool HasReply => _msg.ReplyToMessageId.HasValue && !string.IsNullOrEmpty(_msg.ReplyToText);
        public Guid? ReplyToId => _msg.ReplyToMessageId;
        public string? ReplyToSenderName => _msg.ReplyToSenderName;
        public string? ReplyToText => _msg.ReplyToText;

        // ── Cross-chat group quote ────────────────────────────────────────────

        public bool HasGroupQuote => !string.IsNullOrEmpty(_msg.QuotedGroupSender) || !string.IsNullOrEmpty(_msg.QuotedGroupMessage);
        public string? QuotedGroupSender => _msg.QuotedGroupSender;
        public string? QuotedGroupMessage => _msg.QuotedGroupMessage;
        public Guid? QuotedGroupMessageId => _msg.QuotedGroupMessageId;
        public string? SourceGroupId => _msg.SourceGroupId;

        // ── Attachment ────────────────────────────────────────────────────────

        public bool HasAttachment => _msg.HasAttachment && !string.IsNullOrEmpty(_msg.AttachmentUrl);
        public string AttachmentUrl => _msg.AttachmentUrl ?? string.Empty;
        public string? AttachmentName => _msg.AttachmentName;
        public string? AttachmentSize => _msg.AttachmentSize;
        public string? AttachmentType => _msg.AttachmentType;
        public string MediaType => _msg.MediaType;

        public bool IsDownloaded => _isDownloaded;
        public bool IsDownloading => _isDownloading;
        public string LocalFilePath => _localFilePath;

        private static readonly HashSet<string> ImageExtensions =
            new(StringComparer.OrdinalIgnoreCase) { "jpg", "jpeg", "png", "gif", "webp" };

        public bool IsImageAttachment =>
            HasAttachment &&
            (MediaType == "image" || ImageExtensions.Contains(AttachmentType?.TrimStart('.') ?? string.Empty));

        public bool IsDocumentAttachment => HasAttachment && !IsImageAttachment;

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

        // ── Star ──────────────────────────────────────────────────────────────

        public bool IsStarred => _isStarred;
        public string StarIcon => _isStarred ? "\ue838" : "\ue83a";
        public string StarLabel => _isStarred ? "Unstar" : "Star";

        public void ToggleStar()
        {
            _isStarred = !_isStarred;
            Notify(nameof(IsStarred), nameof(StarIcon), nameof(StarLabel));
        }

        // ── Pin ───────────────────────────────────────────────────────────────

        public bool IsPinned => _isPinned;
        public string PinLabel => _isPinned ? "Unpin" : "Pin";

        public void SetPinned(bool pinned)
        {
            _isPinned = pinned;
            Notify(nameof(IsPinned), nameof(PinLabel));
        }

        // ── Selection ─────────────────────────────────────────────────────────

        public bool IsSelected => _isSelected;

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            Notify(nameof(IsSelected));
        }

        // ── Highlight ─────────────────────────────────────────────────────────

        public bool IsHighlighted => _isHighlighted;

        public void SetHighlighted(bool highlighted)
        {
            _isHighlighted = highlighted;
            Notify(nameof(IsHighlighted));
        }

        // ── INotifyPropertyChanged ────────────────────────────────────────────

        private void Notify(params string[] names)
        {
            foreach (var name in names)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}