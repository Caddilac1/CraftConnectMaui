using Microsoft.Maui.Controls;
using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class GroupChatListPage : ContentPage
    {
        private readonly GroupChatListPageModel _viewModel;

        public GroupChatListPage(GroupChatListPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Constructor - Page initialized");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("[GROUP CHAT LIST PAGE] OnAppearing - Page is now visible");

            try
            {
                await Task.Delay(150);
                Debug.WriteLine("[GROUP CHAT LIST PAGE] Executing LoadCommand to fetch groups");

                if (_viewModel?.LoadCommand?.CanExecute(null) == true)
                {
                    _viewModel.LoadCommand.Execute(null);
                    Debug.WriteLine("[GROUP CHAT LIST PAGE] ✅ LoadCommand executed");
                }
                else
                {
                    Debug.WriteLine("[GROUP CHAT LIST PAGE] ⚠️ LoadCommand cannot execute (might be busy)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GROUP CHAT LIST PAGE] ❌ Error in OnAppearing: {ex.Message}");
                Debug.WriteLine($"[GROUP CHAT LIST PAGE] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[GROUP CHAT LIST PAGE] StackTrace: {ex.StackTrace}");
                await DisplayAlert("Error", $"Failed to load groups: {ex.Message}", "OK");
            }
        }

        #region Top Bar Actions

        private void OnEditModeClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Edit mode clicked");
            // TODO: Implement edit mode functionality
        }

        /// <summary>
        /// Handles the add button click - Shows options for creating feed or group chat
        /// </summary>
        private async void OnAddGroupClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Add button clicked");

            try
            {
                var action = await DisplayActionSheet(
                    "What would you like to create?",
                    "Cancel",
                    null,
                    "📝 Create Feed Post (AI Assistant)",
                    "💬 Create Group Chat"
                );

                if (action == "📝 Create Feed Post (AI Assistant)")
                {
                    Debug.WriteLine("[GROUP CHAT LIST PAGE] Navigating to AI Feed Chat");
                    await Shell.Current.GoToAsync("aifeedchat");
                }
                else if (action == "💬 Create Group Chat")
                {
                    Debug.WriteLine("[GROUP CHAT LIST PAGE] Navigating to regular chat");
                    // Navigate to regular group chat creation or existing chat
                    await Shell.Current.GoToAsync("chat");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GROUP CHAT LIST PAGE] ❌ Navigation error: {ex.Message}");
                await DisplayAlert("Error", $"Could not open chat: {ex.Message}", "OK");
            }
        }

        #endregion

        #region Filter Actions

        private void OnFilterAllClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Filter: All");
            // TODO: Implement filter logic
        }

        private void OnFilterUnreadClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Filter: Unread");
            // TODO: Implement filter logic
        }

        private void OnFilterGroupsClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Filter: Groups");
            // TODO: Implement filter logic
        }

        private void OnFilterPersonalClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Filter: Personal");
            // TODO: Implement filter logic
        }

        #endregion

        #region Bottom Navigation

        private void OnUpdatesClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Updates clicked");
            // TODO: Navigate to updates page
        }

        private void OnContactsClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Contacts clicked");
            // TODO: Navigate to contacts page
        }

        private async void OnStoreClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Store clicked - Navigating to store");

            try
            {
                await Shell.Current.GoToAsync("store");
                Debug.WriteLine("[GROUP CHAT LIST PAGE] ✅ Navigation to store successful");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GROUP CHAT LIST PAGE] ❌ Navigation error: {ex.Message}");
                await DisplayAlert("Navigation Error", $"Could not open store: {ex.Message}", "OK");
            }
        }

        private void OnSettingsClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Settings clicked");
            // TODO: Navigate to settings page
        }

        #endregion
    }
}