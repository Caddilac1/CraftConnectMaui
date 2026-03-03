namespace CraftConnect_Mobile_App.Pages
{
    public partial class BottomNavBar : ContentView
    {
        // ─── ActiveTab bindable property ─────────────────────────────────────────
        public static readonly BindableProperty ActiveTabProperty =
            BindableProperty.Create(
                propertyName: nameof(ActiveTab),
                returnType: typeof(string),
                declaringType: typeof(BottomNavBar),
                defaultValue: "Home",
                propertyChanged: OnActiveTabChanged);

        public string ActiveTab
        {
            get => (string)GetValue(ActiveTabProperty);
            set => SetValue(ActiveTabProperty, value);
        }

        private static void OnActiveTabChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is BottomNavBar bar)
                bar.ApplyActiveTab(newValue?.ToString() ?? "Home");
        }

        // ─── Constructor ─────────────────────────────────────────────────────────
        public BottomNavBar()
        {
            InitializeComponent();
            ApplyActiveTab("Home");
        }

        // ─── Tab activation ──────────────────────────────────────────────────────

        private void ApplyActiveTab(string tab)
        {
            // Reset all tabs to inactive state
            SetInactive(HomeIcon, HomeLabel, HomeDot);
            SetInactive(SearchIcon, SearchLabel, SearchDot);
            SetInactive(ChatsIcon, ChatsLabel, ChatsDot);
            SetInactive(ProfileIcon, ProfileLabel, ProfileDot);

            // Activate the selected tab
            switch (tab)
            {
                case "Home": SetActive(HomeIcon, HomeLabel, HomeDot); break;
                case "Search": SetActive(SearchIcon, SearchLabel, SearchDot); break;
                case "Chats": SetActive(ChatsIcon, ChatsLabel, ChatsDot); break;
                case "Profile": SetActive(ProfileIcon, ProfileLabel, ProfileDot); break;
                    // Orders has its own raised button style — handled in XAML
            }
        }

        private static void SetActive(Label icon, Label label, BoxView dot)
        {
            icon.Opacity = 1.0;
            label.TextColor = Color.FromArgb("#FF9900");
            label.FontAttributes = FontAttributes.Bold;
            dot.BackgroundColor = Color.FromArgb("#FF9900");
        }

        private static void SetInactive(Label icon, Label label, BoxView dot)
        {
            icon.Opacity = 0.55;
            label.TextColor = Color.FromArgb("#8A9BB0");
            label.FontAttributes = FontAttributes.None;
            dot.BackgroundColor = Colors.Transparent;
        }

        // ─── Tap handlers ────────────────────────────────────────────────────────

        private async void OnHomeTabTapped(object sender, TappedEventArgs e)
        {
            await AnimateTab(HomeTab);
            if (ActiveTab != "Home")
                await Shell.Current.GoToAsync("//home");
        }

        private async void OnSearchTabTapped(object sender, TappedEventArgs e)
        {
            await AnimateTab(SearchTab);
            await Shell.Current.GoToAsync("//search");
        }

        private async void OnOrdersTabTapped(object sender, TappedEventArgs e)
        {
            if (sender is View btn)
            {
                await btn.ScaleTo(0.9, 80);
                await btn.ScaleTo(1.0, 80);
            }
            await Shell.Current.GoToAsync("//orders");
        }

        private async void OnChatsTabTapped(object sender, TappedEventArgs e)
        {
            await AnimateTab(ChatsTab);
            await Shell.Current.GoToAsync("//chats");
        }

        private async void OnProfileTabTapped(object sender, TappedEventArgs e)
        {
            await AnimateTab(ProfileTab);
            await Shell.Current.GoToAsync("//profile");
        }

        // ─── Micro-animation ─────────────────────────────────────────────────────

        private static async Task AnimateTab(View tab)
        {
            await tab.ScaleTo(0.88, 60, Easing.CubicIn);
            await tab.ScaleTo(1.0, 80, Easing.SpringOut);
        }
    }
}