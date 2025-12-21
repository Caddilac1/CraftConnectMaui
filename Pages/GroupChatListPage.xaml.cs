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
        }

        private async void OnAddGroupClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Add group button clicked");
            await Shell.Current.GoToAsync("chat");
        }

        #endregion

        #region Filter Actions

        private void OnFilterAllClicked(object sender, EventArgs e) { }
        private void OnFilterUnreadClicked(object sender, EventArgs e) { }
        private void OnFilterGroupsClicked(object sender, EventArgs e) { }
        private void OnFilterPersonalClicked(object sender, EventArgs e) { }

        #endregion

        #region Bottom Navigation

        private void OnUpdatesClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Updates clicked");
        }

        private void OnContactsClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("[GROUP CHAT LIST PAGE] Contacts clicked");
        }

        // ✅ UPDATED - Navigate to Store
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
        }

        #endregion
    }
}