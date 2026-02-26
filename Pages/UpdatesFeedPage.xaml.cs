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

        public UpdatesFeedPage(UpdatesFeedPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Wire SendProposal navigation event
            _viewModel.NavigateToCreateProposal += OnNavigateToCreateProposal;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Re-subscribe if returning to this page (in case of OnDisappearing)
            _viewModel.NavigateToCreateProposal -= OnNavigateToCreateProposal;
            _viewModel.NavigateToCreateProposal += OnNavigateToCreateProposal;

            await _viewModel.InitializeAsync();
            await Task.Delay(300);
            InitializeScrollIndicators();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.NavigateToCreateProposal -= OnNavigateToCreateProposal;
        }

        // ══════════════════════════════════════════════════════════════
        // SEND PROPOSAL → navigate to CreateProposalPage
        // ══════════════════════════════════════════════════════════════

        private async void OnNavigateToCreateProposal(object? sender, SendProposalNavigationArgs args)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[UpdatesFeed] Navigating to CreateProposalPage — FeedId: {args.FeedId}, Title: {args.FeedTitle}");

                // Resolve from DI (must be registered as Transient)
                var createPage = Handler?.MauiContext?.Services.GetService<CreateProposalPage>();

                if (createPage == null)
                {
                    await DisplayAlert("Error", "Could not open the proposal page. Please try again.", "OK");
                    return;
                }

                // Pass the full feed list so the Picker is populated
                createPage.AvailableProjects = _viewModel.AllFeeds?
                    .Select(f => (f.Id, f.Title ?? "Untitled"))
                    .ToList()
                    ?? new List<(string, string)>();

                // Pre-select the feed the user tapped
                createPage.PreselectedFeedId = args.FeedId;

                await Navigation.PushAsync(createPage);

                System.Diagnostics.Debug.WriteLine(
                    $"[UpdatesFeed] Pushed CreateProposalPage. PreselectedFeedId: {args.FeedId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Navigation error: {ex.Message}");
                await DisplayAlert("Error", "Could not open the proposal page. Please try again.", "OK");
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

                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Initialized {dotsToShow} indicators");
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