using Microsoft.Maui.Controls.Shapes;
using System.Diagnostics;
using MauiPath = Microsoft.Maui.Controls.Shapes.Path;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class BottomNavBar : ContentView
    {
        // ── Bindable property ────────────────────────────────────────────────
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
            if (bindable is BottomNavBar nav && newValue is string tab)
                nav.ApplyActiveState(tab);
        }

        // ── Route map ────────────────────────────────────────────────────────
        private static readonly IReadOnlyDictionary<string, string> RouteMap =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "Updates",  "//UpdatesFeedPage"  },
                { "Contacts", "//ContactsPage"     },
                { "Chats",    "//GroupChatListPage" },
                { "Store",    "//StorePage"         },
                { "Settings", "//SettingsPage"      },
            };

        private static readonly string[] TabKeys =
            ["Updates", "Contacts", "Chats", "Store", "Settings"];

        private static readonly Color ActiveColor = Color.FromArgb("#2563EB");
        private static readonly Color InactiveColor = Color.FromArgb("#6B7280");

        private volatile bool _isNavigating;

        // ── Constructor ──────────────────────────────────────────────────────
        public BottomNavBar()
        {
            InitializeComponent();
            Loaded += (_, _) => ApplyActiveState(ActiveTab);
        }

        // ── Visual state ─────────────────────────────────────────────────────
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
        private void OnUpdatesTapped(object sender, EventArgs e) => TryNavigate("Updates");
        private void OnContactsTapped(object sender, EventArgs e) => TryNavigate("Contacts");
        private void OnChatsTapped(object sender, EventArgs e) => TryNavigate("Chats");
        private void OnStoreTapped(object sender, EventArgs e) => TryNavigate("Store");
        private void OnProfileTapped(object sender, EventArgs e) => TryNavigate("Settings");

        // ── Core navigation ──────────────────────────────────────────────────
        private async void TryNavigate(string tab)
        {
            if (ActiveTab == tab) return;
            if (_isNavigating) return;
            _isNavigating = true;

            try
            {
                ActiveTab = tab;

                if (!RouteMap.TryGetValue(tab, out var route))
                {
                    Debug.WriteLine($"[NAV] Unknown tab: {tab}");
                    return;
                }

                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NAV] ❌ {tab}: {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────
        public void SyncTab(string tabKey)
        {
            if (string.IsNullOrWhiteSpace(tabKey)) return;
            if (ActiveTab == tabKey) return;
            SetValue(ActiveTabProperty, tabKey);
        }

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