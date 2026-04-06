using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.PageModels
{
    public class GroupChatListPageModel : INotifyPropertyChanged
    {
        private readonly IChatService _chatService;
        private readonly IPrivateChatService _dmService;
        private readonly AuthService _authService;
        private readonly IChatSignalRService _signalR;

        private string _currentUserId = string.Empty;
        private bool _isBusy;
        private string _activeFilter = "All";
        private string _searchText = string.Empty;

        // ── Raw data ───────────────────────────────────────────────
        private List<GroupChatItem> _allGroups = new();
        private List<PrivateConversationItem> _allConversations = new();

        // ── Displayed unified collection ───────────────────────────
        public ObservableCollection<ChatListItem> ChatItems { get; } = new();

        private ChatListItem? _selectedChatItem;

        public event PropertyChangedEventHandler? PropertyChanged;

        public Command LoadCommand { get; }
        public Command RefreshUnreadCommand { get; }

        public GroupChatListPageModel(
            IChatService chatService,
            IPrivateChatService dmService,
            AuthService authService,
            IChatSignalRService signalR)
        {
            _chatService = chatService;
            _dmService = dmService;
            _authService = authService;
            _signalR = signalR;

            LoadCommand = new Command(async () => await LoadAllAsync());
            RefreshUnreadCommand = new Command(async () => await RefreshUnreadAsync());

            _signalR.PrivateMessageNotification += OnPrivateNotification;
        }

        // ── Selection ──────────────────────────────────────────────

        public ChatListItem? SelectedChatItem
        {
            get => _selectedChatItem;
            set
            {
                _selectedChatItem = value;
                OnPropertyChanged();
                if (value != null) _ = NavigateAsync(value);
            }
        }

        private async Task NavigateAsync(ChatListItem item)
        {
            _selectedChatItem = null;
            OnPropertyChanged(nameof(SelectedChatItem));

            try
            {
                if (item.IsGroup)
                {
                    await Shell.Current.GoToAsync(
                        $"chat?GroupId={item.Id}&GroupName={Uri.EscapeDataString(item.DisplayName)}");
                }
                else
                {
                    await Shell.Current.GoToAsync(
                        $"{nameof(PrivateChatPage)}" +
                        $"?ConversationId={item.Id}" +
                        $"&OtherUserId={Uri.EscapeDataString(item.OtherUserId)}" +
                        $"&OtherUserName={Uri.EscapeDataString(item.OtherUserName)}");
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[LIST] Navigate: {ex.Message}"); }
        }

        // ── Open DM directly (called from GroupChatPage) ───────────

        public async Task OpenDmWithUserAsync(string otherUserId, string otherUserName)
        {
            try
            {
                var (convId, _) = await _dmService.OpenConversationAsync(otherUserId);
                await Shell.Current.GoToAsync(
                    $"{nameof(PrivateChatPage)}" +
                    $"?ConversationId={convId}" +
                    $"&OtherUserId={Uri.EscapeDataString(otherUserId)}" +
                    $"&OtherUserName={Uri.EscapeDataString(otherUserName)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LIST] OpenDmWithUser: {ex.Message}");
                await Application.Current!.MainPage!.DisplayAlert(
                    "Error", "Could not open private chat.", "OK");
            }
        }

        // ── Data loading ───────────────────────────────────────────

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private async Task LoadAllAsync()
        {
            try
            {
                IsBusy = true;
                await LoadCurrentUserAsync();
                await Task.WhenAll(LoadGroupsAsync(), LoadConversationsAsync());

                if (!_signalR.IsConnected)
                {
                    try { await _signalR.ConnectAsync(); }
                    catch (Exception ex) { Debug.WriteLine($"[LIST] SignalR: {ex.Message}"); }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"[LIST] LoadAll: {ex.Message}"); }
            finally { IsBusy = false; }
        }

        private async Task LoadGroupsAsync()
        {
            _allGroups = await _chatService.GetMyGroupsAsync();
            RebuildList();
        }

        private async Task LoadConversationsAsync()
        {
            _allConversations = await _dmService.GetMyConversationsAsync();
            RebuildList();
        }

        private async Task RefreshUnreadAsync()
        {
            try { await Task.WhenAll(LoadGroupsAsync(), LoadConversationsAsync()); }
            catch (Exception ex) { Debug.WriteLine($"[LIST] RefreshUnread: {ex.Message}"); }
        }

        private async Task LoadCurrentUserAsync()
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token)) return;
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            _currentUserId = jwt.Claims
                .FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")
                ?.Value ?? string.Empty;
        }

        // ── Filtering ──────────────────────────────────────────────

        /// <summary>
        /// Called by filter chips.
        /// All    → groups + DMs mixed, sorted by last message time
        /// Unread → same mix but only items with unread messages
        /// Groups → only group chats
        /// </summary>
        public void ApplyFilter(string filter)
        {
            _activeFilter = filter;
            RebuildList();
        }

        public void ApplySearch(string text)
        {
            _searchText = text;
            RebuildList();
        }

        private void RebuildList()
        {
            var search = _searchText.ToLower();

            // Start with all items merged
            IEnumerable<ChatListItem> items = _allGroups
                .Select(ChatListItem.FromGroup)
                .Concat(_allConversations.Select(ChatListItem.FromConversation));

            // Search across display name
            if (!string.IsNullOrWhiteSpace(search))
                items = items.Where(i => i.DisplayName.ToLower().Contains(search));

            // Filter chip logic
            items = _activeFilter switch
            {
                "Unread" => items.Where(i => i.HasUnreadMessages),
                "Groups" => items.Where(i => i.IsGroup),
                _ => items   // "All" — show everything
            };

            // Sort newest first
            items = items.OrderByDescending(i => i.LastMessageTime);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                ChatItems.Clear();
                foreach (var item in items)
                    ChatItems.Add(item);
            });
        }

        // ── Real-time DM notification ──────────────────────────────

        private void OnPrivateNotification(
            object? sender, PrivateMessageNotificationEventArgs e)
        {
            var conv = _allConversations.FirstOrDefault(c => c.Id == e.ConversationId);
            if (conv != null)
            {
                conv.UnreadCount++;
                RebuildList();
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        ~GroupChatListPageModel()
        {
            _signalR.PrivateMessageNotification -= OnPrivateNotification;
        }
    }
}