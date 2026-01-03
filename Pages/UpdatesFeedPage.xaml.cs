using CraftConnect_Mobile_App.PageModels;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class UpdatesFeedPage : ContentPage
    {
        private readonly UpdatesFeedPageModel _viewModel;
        private int _currentScrollIndex = 0;
        private readonly List<BoxView> _indicatorDots = new();

        public UpdatesFeedPage(UpdatesFeedPageModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.InitializeAsync();

            // Initialize scroll indicators after data loads
            await Task.Delay(300); // Small delay to ensure collection is rendered
            InitializeScrollIndicators();
        }

        private void InitializeScrollIndicators()
        {
            try
            {
                ScrollIndicators.Children.Clear();
                _indicatorDots.Clear();

                var feedCount = _viewModel.AllFeeds?.Count ?? 0;
                if (feedCount == 0) return;

                // Create dots (show max 5 at a time for better UX)
                var dotsToShow = Math.Min(feedCount, 5);

                for (int i = 0; i < dotsToShow; i++)
                {
                    var dot = new BoxView
                    {
                        WidthRequest = 6,
                        HeightRequest = 6,
                        CornerRadius = 3,
                        BackgroundColor = i == 0 ? Color.FromArgb("#5F67EA") : Color.FromArgb("#D1D5DB"),
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    };

                    _indicatorDots.Add(dot);
                    ScrollIndicators.Children.Add(dot);
                }

                // If more than 5 items, add ellipsis indicator
                if (feedCount > 5)
                {
                    var ellipsis = new Label
                    {
                        Text = "...",
                        FontSize = 10,
                        TextColor = Color.FromArgb("#9CA3AF"),
                        VerticalOptions = LayoutOptions.Center
                    };
                    ScrollIndicators.Children.Add(ellipsis);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Error initializing indicators: {ex.Message}");
            }
        }

        private void OnFeedsScrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            try
            {
                // Calculate current visible item index
                var scrollX = e.HorizontalOffset;
                var itemWidth = 336; // Card width (320) + spacing (16)
                var currentIndex = (int)Math.Round(scrollX / itemWidth);

                if (currentIndex == _currentScrollIndex) return;

                _currentScrollIndex = currentIndex;
                UpdateScrollIndicators(currentIndex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Error updating scroll: {ex.Message}");
            }
        }

        private void UpdateScrollIndicators(int activeIndex)
        {
            try
            {
                var feedCount = _viewModel.AllFeeds?.Count ?? 0;
                if (feedCount == 0 || _indicatorDots.Count == 0) return;

                // For 5 or fewer items, simple direct mapping
                if (feedCount <= 5)
                {
                    for (int i = 0; i < _indicatorDots.Count; i++)
                    {
                        _indicatorDots[i].BackgroundColor = i == activeIndex
                            ? Color.FromArgb("#5F67EA")
                            : Color.FromArgb("#D1D5DB");
                    }
                }
                else
                {
                    // For more items, use sliding window effect
                    var windowStart = Math.Max(0, Math.Min(activeIndex - 2, feedCount - 5));

                    for (int i = 0; i < _indicatorDots.Count; i++)
                    {
                        var itemIndex = windowStart + i;
                        _indicatorDots[i].BackgroundColor = itemIndex == activeIndex
                            ? Color.FromArgb("#5F67EA")
                            : Color.FromArgb("#D1D5DB");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Error updating indicators: {ex.Message}");
            }
        }

        private void OnSearchClicked(object sender, EventArgs e)
        {
            _viewModel.SearchCommand?.Execute(null);
        }

        private void OnFilterClicked(object sender, EventArgs e)
        {
            _viewModel.FilterCommand?.Execute(null);
        }
    }
}