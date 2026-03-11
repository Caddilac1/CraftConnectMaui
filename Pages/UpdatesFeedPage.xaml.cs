using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Pages;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class UpdatesFeedPage : ContentPage
    {
        private readonly UpdatesFeedPageModel _viewModel;
        private int _currentScrollIndex = 0;
        private readonly List<BoxView> _indicatorDots = new();
        private bool _isScrolling = false;
        private bool _initialized = false;

        public UpdatesFeedPage(UpdatesFeedPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            _viewModel.NavigateToCreateProposal += OnNavigateToCreateProposal;
            _viewModel.NavigateToEditProfile += OnNavigateToEditProfile;
        }

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            BottomNav.SyncTab("Updates");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Re-subscribe each time (guarded against double subscription)
            _viewModel.NavigateToCreateProposal -= OnNavigateToCreateProposal;
            _viewModel.NavigateToCreateProposal += OnNavigateToCreateProposal;
            _viewModel.NavigateToEditProfile -= OnNavigateToEditProfile;
            _viewModel.NavigateToEditProfile += OnNavigateToEditProfile;

            // SPEED FIX: page renders immediately, data loads in background.
            if (!_initialized)
            {
                _initialized = true;
                _ = LoadDataAsync();
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.NavigateToCreateProposal -= OnNavigateToCreateProposal;
            _viewModel.NavigateToEditProfile -= OnNavigateToEditProfile;
        }

        private async Task LoadDataAsync()
        {
            try
            {
                await _viewModel.InitializeAsync();
                await Task.Delay(300);
                InitializeScrollIndicators();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] ❌ Load error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SEND PROPOSAL → user HAS a profile
        // ══════════════════════════════════════════════════════════════

        private async void OnNavigateToCreateProposal(object? sender, SendProposalNavigationArgs args)
        {
            try
            {
                var createPage = Handler?.MauiContext?.Services.GetService<CreateProposalPage>();

                if (createPage == null)
                {
                    await DisplayAlert("Error", "Could not open the proposal page. Please try again.", "OK");
                    return;
                }

                createPage.AvailableProjects = _viewModel.AllFeeds?
                    .Select(f => (f.Id, f.Title ?? "Untitled"))
                    .ToList()
                    ?? new List<(string, string)>();

                createPage.PreselectedFeedId = args.FeedId;

                await Navigation.PushAsync(createPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Navigation error: {ex.Message}");
                await DisplayAlert("Error", "Could not open the proposal page. Please try again.", "OK");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SEND PROPOSAL → user has NO profile
        // ══════════════════════════════════════════════════════════════

        private async void OnNavigateToEditProfile(object? sender, NoProfileNavigationArgs args)
        {
            try
            {
                bool proceed = await DisplayAlert(
                    "Artisan Profile Required",
                    "Sending a proposal is only available to users with an artisan profile. " +
                    "Would you like to set up your artisan profile now? " +
                    "Tap \"Set Up Profile\" to complete your profile and continue, " +
                    "or \"Not Now\" to cancel.",
                    "Set Up Profile",
                    "Not Now");

                if (!proceed) return;

                var editPage = Handler?.MauiContext?.Services.GetService<EditArtisanProfilePage>();

                if (editPage == null)
                {
                    await DisplayAlert("Error", "Could not open the profile setup page. Please try again.", "OK");
                    return;
                }

                editPage.ReturnFeedId = args.FeedId;
                editPage.ReturnFeedTitle = args.FeedTitle;
                editPage.AllFeedsSnapshot = _viewModel.AllFeeds?
                    .Select(f => (f.Id, f.Title ?? "Untitled"))
                    .ToList()
                    ?? new List<(string, string)>();

                await Navigation.PushAsync(editPage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] EditProfile navigation error: {ex.Message}");
                await DisplayAlert("Error", "Could not open the profile setup page. Please try again.", "OK");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SCROLL INDICATORS
        // ══════════════════════════════════════════════════════════════

        private void InitializeScrollIndicators()
        {
            try
            {
                ScrollIndicators.Children.Clear();
                _indicatorDots.Clear();

                var feedCount = _viewModel.AllFeeds?.Count ?? 0;
                if (feedCount == 0) return;

                var dotsToShow = Math.Min(feedCount, 5);

                for (int i = 0; i < dotsToShow; i++)
                {
                    var dot = new BoxView
                    {
                        WidthRequest = 8,
                        HeightRequest = 8,
                        CornerRadius = 4,
                        BackgroundColor = i == 0
                            ? Color.FromArgb("#5F67EA")
                            : Color.FromArgb("#D1D5DB"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(2, 0)
                    };
                    _indicatorDots.Add(dot);
                    ScrollIndicators.Children.Add(dot);
                }

                if (feedCount > 5)
                {
                    ScrollIndicators.Children.Add(new Label
                    {
                        Text = "...",
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#9CA3AF"),
                        VerticalOptions = LayoutOptions.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Error initializing indicators: {ex.Message}");
            }
        }

        private void OnFeedsScrolled(object sender, ScrolledEventArgs e)
        {
            if (_isScrolling) return;

            try
            {
                _isScrolling = true;

                var itemWidth = 336;
                var currentIndex = (int)Math.Round(e.ScrollX / itemWidth);

                if (currentIndex == _currentScrollIndex) return;

                _currentScrollIndex = currentIndex;
                UpdateScrollIndicators(currentIndex);

                var feedCount = _viewModel.AllFeeds?.Count ?? 0;
                if (feedCount > 0 && currentIndex >= feedCount - 3)
                    _ = _viewModel.LoadMoreFeeds();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Scroll error: {ex.Message}");
            }
            finally
            {
                _isScrolling = false;
            }
        }

        private void UpdateScrollIndicators(int activeIndex)
        {
            try
            {
                var feedCount = _viewModel.AllFeeds?.Count ?? 0;
                if (feedCount == 0 || _indicatorDots.Count == 0) return;

                if (feedCount <= 5)
                {
                    for (int i = 0; i < _indicatorDots.Count; i++)
                        _indicatorDots[i].BackgroundColor = i == activeIndex
                            ? Color.FromArgb("#5F67EA")
                            : Color.FromArgb("#D1D5DB");
                }
                else
                {
                    var windowStart = Math.Max(0, Math.Min(activeIndex - 2, feedCount - 5));
                    for (int i = 0; i < _indicatorDots.Count; i++)
                        _indicatorDots[i].BackgroundColor = (windowStart + i) == activeIndex
                            ? Color.FromArgb("#5F67EA")
                            : Color.FromArgb("#D1D5DB");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Indicator error: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        // HEADER BUTTONS
        // ══════════════════════════════════════════════════════════════

        private void OnSearchClicked(object sender, EventArgs e) =>
            _viewModel.SearchCommand?.Execute(null);

        private void OnFilterClicked(object sender, EventArgs e) =>
            _viewModel.FilterCommand?.Execute(null);
    }
}