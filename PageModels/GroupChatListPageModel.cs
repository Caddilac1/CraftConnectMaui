using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.PageModels
{
    public class GroupChatListPageModel : BasePageModel
    {
        private readonly IChatService _chatService;

        // ── Fix: full property with backing field so RefreshGroupsList()
        //    can reassign it and the CollectionView binding updates. ─────
        private ObservableCollection<GroupChatItem> _groups = new();
        public ObservableCollection<GroupChatItem> Groups
        {
            get => _groups;
            set
            {
                _groups = value;
                OnPropertyChanged();
            }
        }

        public Command LoadCommand { get; }
        public Command RefreshUnreadCommand { get; }
        public Command<GroupChatItem> GroupTappedCommand { get; }

        private GroupChatItem _selectedGroup;
        public GroupChatItem SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                _selectedGroup = value;
                OnPropertyChanged();

                if (value != null)
                {
                    _ = NavigateToChat(value);
                    _selectedGroup = null;
                    OnPropertyChanged(nameof(SelectedGroup));
                }
            }
        }

        // Total unread badge count — shown on the nav tab
        private int _totalUnreadCount;
        public int TotalUnreadCount
        {
            get => _totalUnreadCount;
            set
            {
                _totalUnreadCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasAnyUnread));
            }
        }

        public bool HasAnyUnread => TotalUnreadCount > 0;

        public GroupChatListPageModel(IChatService chatService)
        {
            _chatService = chatService;

            LoadCommand = new Command(async () => await LoadGroupsAsync());
            RefreshUnreadCommand = new Command(async () => await RefreshUnreadCountsAsync());
            GroupTappedCommand = new Command<GroupChatItem>(async (g) => await NavigateToChat(g));

            Debug.WriteLine("[GROUP CHAT LIST VM] Initialized");
        }

        // ═══════════════════════════════════════════════════════════════
        // LOAD GROUPS — fetches groups from API (includes UnreadCount
        // per group already set by ChatService from the API response)
        // ═══════════════════════════════════════════════════════════════

        private async Task LoadGroupsAsync()
        {
            if (IsBusy)
            {
                Debug.WriteLine("[GROUP CHAT LIST VM] Already loading, skipping...");
                return;
            }

            try
            {
                Debug.WriteLine("[GROUP CHAT LIST VM] Loading groups...");
                IsBusy = true;

                Groups.Clear();

                var list = await _chatService.GetMyGroupsAsync();

                foreach (var item in list)
                {
                    // Log each group's unread count so we can confirm API is sending it
                    Debug.WriteLine($"[GROUP CHAT LIST VM] Group: '{item.Name}' | UnreadCount={item.UnreadCount} | HasUnread={item.HasUnreadMessages} | IsGroup={item.IsGroup}");
                    Groups.Add(item);
                }

                // Cache the full list for filtering / searching
                CacheGroups();

                // Also fetch the overall total for the nav badge
                TotalUnreadCount = await _chatService.GetTotalUnreadCountAsync();

                Debug.WriteLine($"[GROUP CHAT LIST VM] ✅ Loaded {Groups.Count} groups. TotalUnread={TotalUnreadCount}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"[GROUP CHAT LIST VM] ❌ Unauthorized: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Session Expired",
                    "Your session has expired. Please login again.",
                    "OK");
                await Shell.Current.GoToAsync("//auth/LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GROUP CHAT LIST VM] ❌ Error loading groups: {ex.Message}");
                Debug.WriteLine($"[GROUP CHAT LIST VM] StackTrace: {ex.StackTrace}");

                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to load groups: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("[GROUP CHAT LIST VM] Loading complete");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // REFRESH UNREAD COUNTS — call this on a timer or after returning
        // from a chat page to refresh badges without reloading everything
        // ═══════════════════════════════════════════════════════════════

        private async Task RefreshUnreadCountsAsync()
        {
            try
            {
                Debug.WriteLine("[GROUP CHAT LIST VM] Refreshing unread counts...");

                TotalUnreadCount = await _chatService.GetTotalUnreadCountAsync();

                // Reload the full list so per-group badges update too
                var list = await _chatService.GetMyGroupsAsync();

                // Update in-place rather than clearing so the list doesn't flicker
                for (int i = 0; i < list.Count; i++)
                {
                    var updated = list[i];

                    if (i < Groups.Count)
                    {
                        // Update the existing item's unread count so the UI refreshes.
                        // NOTE: This only works because GroupChatItem implements
                        // INotifyPropertyChanged on UnreadCount.
                        Groups[i].UnreadCount = updated.UnreadCount;
                        Groups[i].LastMessageIsRead = updated.LastMessageIsRead;
                    }
                    else
                    {
                        Groups.Add(updated);
                    }
                }

                // Re-cache with fresh data so filters stay accurate
                CacheGroups();

                Debug.WriteLine($"[GROUP CHAT LIST VM] ✅ Unread refreshed. Total={TotalUnreadCount}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GROUP CHAT LIST VM] ❌ Error refreshing unread: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // FILTERING & SEARCH
        // ═══════════════════════════════════════════════════════════════

        // Keep a reference to the full unfiltered list
        private List<GroupChatItem> _allGroups = new();
        private string _currentSearch = "";
        private string _currentFilter = "All";

        /// <summary>
        /// Snapshots the current Groups list so filters/search work against
        /// the full dataset. Call after every load or refresh.
        /// </summary>
        private void CacheGroups()
        {
            _allGroups = Groups?.ToList() ?? new List<GroupChatItem>();
        }

        /// <summary>Called by the view when a filter chip is tapped.</summary>
        public void ApplyFilter(string filter)
        {
            _currentFilter = filter;
            RefreshGroupsList();
        }

        /// <summary>Called by the view when the search text changes.</summary>
        public void ApplySearch(string query)
        {
            _currentSearch = query ?? "";
            RefreshGroupsList();
        }

        private void RefreshGroupsList()
        {
            var filtered = _allGroups.AsEnumerable();

            // Filter chip
            filtered = _currentFilter switch
            {
                "Unread" => filtered.Where(g => g.HasUnreadMessages),
                "Groups" => filtered.Where(g => g.IsGroup),
                "Personal" => filtered.Where(g => !g.IsGroup),
                _ => filtered   // "All" — no filter
            };

            // Search query
            if (!string.IsNullOrWhiteSpace(_currentSearch))
            {
                var q = _currentSearch.ToLowerInvariant();
                filtered = filtered.Where(g =>
                    (g.Name?.ToLowerInvariant().Contains(q) ?? false) ||
                    (g.LastMessage?.ToLowerInvariant().Contains(q) ?? false));
            }

            // Fix CS0200: assign via the property setter, not the readonly field
            Groups = new ObservableCollection<GroupChatItem>(filtered);
        }

        // ═══════════════════════════════════════════════════════════════
        // NAVIGATE TO CHAT
        // ═══════════════════════════════════════════════════════════════

        private async Task NavigateToChat(GroupChatItem group)
        {
            if (group == null) return;

            Debug.WriteLine($"[GROUP CHAT LIST VM] Navigating to chat: {group.Name} (ID: {group.Id})");

            try
            {
                await Shell.Current.GoToAsync(
                    $"chat?GroupId={group.Id}&GroupName={Uri.EscapeDataString(group.Name)}");

                Debug.WriteLine("[GROUP CHAT LIST VM] ✅ Navigation successful");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GROUP CHAT LIST VM] ❌ Navigation error: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Navigation Error",
                    $"Could not open chat: {ex.Message}",
                    "OK");
            }
        }
    }
}