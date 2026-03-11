using Microsoft.Maui.Controls.Shapes;
using System.Diagnostics;
using MauiPath = Microsoft.Maui.Controls.Shapes.Path;

namespace CraftConnect_Mobile_App.Controls
{
    public partial class BottomNavBar : ContentView
    {
        public static readonly BindableProperty ActiveTabProperty =
            BindableProperty.Create(
                nameof(ActiveTab),
                typeof(string),
                typeof(BottomNavBar),
                "Chats",
                BindingMode.TwoWay,
                propertyChanged: OnActiveTabChanged);

        public string ActiveTab
        {
            get => (string)GetValue(ActiveTabProperty);
            set => SetValue(ActiveTabProperty, value);
        }

        private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is BottomNavBar nav)
                nav.ApplyActiveState(newValue as string);
        }

        public BottomNavBar()
        {
            InitializeComponent();
            Loaded += (_, _) => ApplyActiveState(ActiveTab);
        }

        // ── Visual state ──────────────────────────────────────────────────────

        private static readonly string[] TabKeys =
            ["Updates", "Contacts", "Chats", "Store", "Settings"];

        private static readonly Color ActiveColor = Color.FromArgb("#2563EB");
        private static readonly Color InactiveColor = Color.FromArgb("#6B7280");

        private (BoxView Pill, MauiPath Icon, Label Text)[] NavGroups() =>
        [
            (UpdatesPill,  UpdatesIcon,  UpdatesLabel),
            (ContactsPill, ContactsIcon, ContactsLabel),
            (ChatsPill,    ChatsIcon,    ChatsLabel),
            (StorePill,    StoreIcon,    StoreLabel),
            (ProfilePill,  ProfileIcon,  ProfileLabel),
        ];

        private void ApplyActiveState(string? tab)
        {
            var groups = NavGroups();
            for (int i = 0; i < groups.Length; i++)
            {
                bool active = TabKeys[i] == tab;
                var (pill, icon, label) = groups[i];

                pill.IsVisible = active;
                icon.Fill = active ? new SolidColorBrush(ActiveColor)
                                   : new SolidColorBrush(InactiveColor);
                label.TextColor = active ? ActiveColor : InactiveColor;
                label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
            }
        }

        // ── Tap handlers ─────────────────────────────────────────────────────

        private void OnUpdatesTapped(object sender, EventArgs e)
        {
            if (ActiveTab == "Updates") return;
            ActiveTab = "Updates";
            _ = NavigateSafe("//UpdatesFeedPage");
        }

        private void OnContactsTapped(object sender, EventArgs e)
        {
            if (ActiveTab == "Contacts") return;
            ActiveTab = "Contacts";
            _ = NavigateSafe("//ContactsPage");
        }

        private void OnChatsTapped(object sender, EventArgs e)
        {
            if (ActiveTab == "Chats") return;
            ActiveTab = "Chats";
            _ = NavigateSafe("//GroupChatListPage");
        }

        private void OnStoreTapped(object sender, EventArgs e)
        {
            if (ActiveTab == "Store") return;
            ActiveTab = "Store";
            _ = NavigateSafe("//StorePage");
        }

        private void OnProfileTapped(object sender, EventArgs e)
        {
            if (ActiveTab == "Settings") return;
            ActiveTab = "Settings";
            _ = NavigateSafe("//SettingsPage");
        }

        private static async Task NavigateSafe(string route)
        {
            try
            {
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NAV] ❌ {route}: {ex.Message}");
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void SyncTab(string tabKey) => SetValue(ActiveTabProperty, tabKey);

        public void SetBadgeVisible(string tabKey, bool visible)
        {
            Ellipse? badge = tabKey switch
            {
                "Updates" => UpdatesBadge,
                "Contacts" => ContactsBadge,
                "Chats" => ChatsBadge,
                "Settings" => ProfileBadge,
                _ => null
            };
            if (badge is not null) badge.IsVisible = visible;
        }
    }
}