using CraftConnect_Mobile_App.PageModels;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class GroupChatListPage : ContentPage
    {
        private readonly GroupChatListPageModel _viewModel;
        private bool _initialized = false;
        private string _activeFilter = "All";

        private static readonly Color ActiveBg = Color.FromArgb("#2563EB");
        private static readonly Color ActiveText = Colors.White;
        private static readonly Color InactiveBg = Color.FromArgb("#FFFFFF");
        private static readonly Color InactiveText = Color.FromArgb("#555555");

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

        // ══════════════════════════════════════════════════════════════
        // FILTER CHIPS
        // ══════════════════════════════════════════════════════════════

        private void SetActiveChip(string filter)
        {
            if (_activeFilter == filter) return;
            _activeFilter = filter;

            SetChipStyle(ChipAll, ChipAllLabel, false);
            SetChipStyle(ChipUnread, ChipUnreadLabel, false);
            SetChipStyle(ChipGroups, ChipGroupsLabel, false);

            switch (filter)
            {
                case "All": SetChipStyle(ChipAll, ChipAllLabel, true); break;
                case "Unread": SetChipStyle(ChipUnread, ChipUnreadLabel, true); break;
                case "Groups": SetChipStyle(ChipGroups, ChipGroupsLabel, true); break;
            }

            _viewModel.ApplyFilter(filter);
        }

        private static void SetChipStyle(Border chip, Label label, bool active)
        {
            chip.BackgroundColor = active ? ActiveBg : InactiveBg;
            label.TextColor = active ? ActiveText : InactiveText;
            label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        }

        private void OnFilterAllClicked(object sender, EventArgs e) => SetActiveChip("All");
        private void OnFilterUnreadClicked(object sender, EventArgs e) => SetActiveChip("Unread");
        private void OnFilterGroupsClicked(object sender, EventArgs e) => SetActiveChip("Groups");

        // ══════════════════════════════════════════════════════════════
        // SEARCH
        // ══════════════════════════════════════════════════════════════

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e) =>
            _viewModel.ApplySearch(e.NewTextValue);

        // ══════════════════════════════════════════════════════════════
        // HEADER BUTTONS
        // ══════════════════════════════════════════════════════════════

        private void OnEditModeClicked(object sender, EventArgs e) =>
            Debug.WriteLine("[ChatList] Edit mode tapped");

        private async void OnAddGroupClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("addgroup"); }
            catch (Exception ex) { Debug.WriteLine($"[ChatList] AddGroup: {ex.Message}"); }
        }
    }
}