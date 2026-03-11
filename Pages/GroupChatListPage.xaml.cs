using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class GroupChatListPage : ContentPage
    {
        private readonly GroupChatListPageModel _viewModel;
        private bool _initialized = false;

        public GroupChatListPage(GroupChatListPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            BottomNav.SyncTab("Chats");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (!_initialized)
            {
                _initialized = true;
                _viewModel.LoadCommand?.Execute(null);
            }
            else
            {
                _viewModel.RefreshUnreadCommand?.Execute(null);
            }
        }

        private void OnEditModeClicked(object sender, EventArgs e) =>
            Debug.WriteLine("[ChatList] Edit mode tapped");

        private async void OnAddGroupClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("addgroup"); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatList] AddGroup nav error: {ex.Message}");
            }
        }

        private void OnFilterAllClicked(object sender, EventArgs e) =>
            Debug.WriteLine("[ChatList] Filter: All");

        private void OnFilterUnreadClicked(object sender, EventArgs e) =>
            Debug.WriteLine("[ChatList] Filter: Unread");

        private void OnFilterGroupsClicked(object sender, EventArgs e) =>
            Debug.WriteLine("[ChatList] Filter: Groups");

        private void OnFilterPersonalClicked(object sender, EventArgs e) =>
            Debug.WriteLine("[ChatList] Filter: Personal");
    }
}