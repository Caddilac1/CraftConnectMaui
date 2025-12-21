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

        public ObservableCollection<GroupChatItem> Groups { get; } = new();

        public Command LoadCommand { get; }
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
                    // Navigate to chat when item is selected
                    _ = NavigateToChat(value);

                    // Reset selection so user can tap the same item again
                    _selectedGroup = null;
                    OnPropertyChanged(nameof(SelectedGroup));
                }
            }
        }

        public GroupChatListPageModel(IChatService chatService)
        {
            _chatService = chatService;

            LoadCommand = new Command(async () => await LoadGroups());
            GroupTappedCommand = new Command<GroupChatItem>(async (group) => await NavigateToChat(group));

            Debug.WriteLine("[GROUP CHAT LIST VM] Initialized");
        }

        /// <summary>
        /// Load all groups from the server
        /// </summary>
        private async Task LoadGroups()
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
                    Groups.Add(item);
                }

                Debug.WriteLine($"[GROUP CHAT LIST VM] ✅ Loaded {Groups.Count} groups successfully");
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

        /// <summary>
        /// Navigate to the chat page for the selected group
        /// </summary>
        private async Task NavigateToChat(GroupChatItem group)
        {
            if (group == null)
            {
                Debug.WriteLine("[GROUP CHAT LIST VM] ⚠️ Cannot navigate - group is null");
                return;
            }

            Debug.WriteLine($"[GROUP CHAT LIST VM] Navigating to chat: {group.Name} (ID: {group.Id})");

            try
            {
                // Navigate to chat page with group parameters
                // Route "chat" is registered in AppShell.xaml.cs
                await Shell.Current.GoToAsync(
                    $"chat?GroupId={group.Id}&GroupName={Uri.EscapeDataString(group.Name)}");

                Debug.WriteLine("[GROUP CHAT LIST VM] ✅ Navigation successful");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GROUP CHAT LIST VM] ❌ Navigation error: {ex.Message}");
                Debug.WriteLine($"[GROUP CHAT LIST VM] StackTrace: {ex.StackTrace}");

                await Application.Current.MainPage.DisplayAlert(
                    "Navigation Error",
                    $"Could not open chat: {ex.Message}",
                    "OK");
            }
        }
    }
}