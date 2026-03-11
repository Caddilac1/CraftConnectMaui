using System.Diagnostics;

namespace CraftConnect_Mobile_App.Controls
{
    public partial class BottomNavBar : ContentView
    {
        // Bindable property to track active tab
        public static readonly BindableProperty ActiveTabProperty =
            BindableProperty.Create(
                nameof(ActiveTab),
                typeof(string),
                typeof(BottomNavBar),
                "Chats",
                BindingMode.TwoWay);

        public string ActiveTab
        {
            get => (string)GetValue(ActiveTabProperty);
            set => SetValue(ActiveTabProperty, value);
        }

        public BottomNavBar()
        {
            InitializeComponent();
        }

        private async void OnUpdatesTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[BOTTOM NAV] Updates tapped - Navigating to updates feed");

            // Don't navigate if already on this page
            if (ActiveTab == "Updates")
            {
                Debug.WriteLine("[BOTTOM NAV] Already on Updates page, skipping navigation");
                return;
            }

            ActiveTab = "Updates";

            try
            {
                // Navigate to the UpdatesFeedPage using Shell navigation
                await Shell.Current.GoToAsync("//main/UpdatesFeedPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BOTTOM NAV] ❌ Navigation error: {ex.Message}");
                await Shell.Current.DisplayAlert("Navigation Error", "Could not navigate to Updates page", "OK");
            }
        }

        private async void OnContactsTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[BOTTOM NAV] Contacts tapped");

            // Don't navigate if already on this page
            if (ActiveTab == "Contacts")
            {
                Debug.WriteLine("[BOTTOM NAV] Already on Contacts page, skipping navigation");
                return;
            }

            ActiveTab = "Contacts";

            try
            {
                // TODO: Update route when ContactsPage is created
                // await Shell.Current.GoToAsync("//main/ContactsPage");
                await Shell.Current.DisplayAlert("Coming Soon", "Contacts feature coming soon!", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BOTTOM NAV] ❌ Navigation error: {ex.Message}");
            }
        }

        private async void OnChatsTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[BOTTOM NAV] Chats tapped - Navigating to chat list");

            // Don't navigate if already on this page
            if (ActiveTab == "Chats")
            {
                Debug.WriteLine("[BOTTOM NAV] Already on Chats page, skipping navigation");
                return;
            }

            ActiveTab = "Chats";

            try
            {
                // Navigate back to the main chat list page
                await Shell.Current.GoToAsync("//main/GroupChatListPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BOTTOM NAV] ❌ Navigation error: {ex.Message}");
            }
        }

        private async void OnStoreTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[BOTTOM NAV] Store tapped - Navigating to store");

            // Don't navigate if already on this page
            if (ActiveTab == "Store")
            {
                Debug.WriteLine("[BOTTOM NAV] Already on Store page, skipping navigation");
                return;
            }

            ActiveTab = "Store";

            try
            {
                await Shell.Current.GoToAsync("store");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BOTTOM NAV] ❌ Navigation error: {ex.Message}");
            }
        }

        private async void OnSettingsTapped(object sender, EventArgs e)
        {
            Debug.WriteLine("[BOTTOM NAV] Settings tapped - Navigating to settings");

            // Don't navigate if already on this page
            if (ActiveTab == "Settings")
            {
                Debug.WriteLine("[BOTTOM NAV] Already on Settings page, skipping navigation");
                return;
            }

            ActiveTab = "Settings";

            try
            {
                await Shell.Current.GoToAsync("//main/SettingsPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BOTTOM NAV] ❌ Navigation error: {ex.Message}");
                await Shell.Current.DisplayAlert("Navigation Error", "Could not navigate to Settings page", "OK");
            }
        }
    }
}