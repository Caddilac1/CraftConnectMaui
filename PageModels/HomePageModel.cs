using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.PageModels
{
    /// <summary>
    /// ViewModel for the Agooha Home Page.
    /// Mirrors the web HomeController's Index action — loads promotions, categories,
    /// featured items, services, CraftConnect feeds, and a paginated product grid.
    /// </summary>
    public class HomePageModel : INotifyPropertyChanged
    {
        // ─── Dependencies ────────────────────────────────────────────────────────
        private readonly IApiService _api;

        // ─── Constructor ─────────────────────────────────────────────────────────
        public HomePageModel(IApiService api)
        {
            _api = api;

            // Initialise commands
            RefreshCommand = new Command(async () => await LoadAllAsync(forceRefresh: true));
            LoadMoreCommand = new Command(async () => await LoadMoreProductsAsync(), () => HasMorePages && !IsLoadingMore);
            SetFilterCommand = new Command<string>(async (f) => await SetFilterAsync(f));
            SetSortCommand = new Command<string>(async (s) => await SetSortAsync(s));
            SelectBroadCategoryCommand = new Command<int>(async (id) => await FilterByBroadCategoryAsync(id));
            SelectCategoryCommand = new Command<int>(async (id) => await FilterByCategoryAsync(id));
            OpenEcommerceItemCommand = new Command<EcommerceItemModel>(OpenEcommerceItem);
            OpenItemCommand = new Command<SpecialDealModel>(OpenSpecialDealItem);
            OpenServiceCommand = new Command<ServiceViewModel>(OpenService);
            BookServiceCommand = new Command<ServiceViewModel>(BookService);
            OpenFeedCommand = new Command<FeedViewModel>(OpenFeed);
            OpenPromotionCommand = new Command<string>(OpenPromotion);
            ToggleWishlistCommand = new Command<EcommerceItemModel>(async (item) => await ToggleWishlistAsync(item));
            AddToCartCommand = new Command<EcommerceItemModel>(async (item) => await AddToCartAsync(item));
            OpenCartCommand = new Command(async () => await Shell.Current.GoToAsync("cart"));
            OpenMessagesCommand = new Command(async () => await Shell.Current.GoToAsync("messages"));
            OpenNotificationsCommand = new Command(async () => await Shell.Current.GoToAsync("notifications"));
            OpenSearchCommand = new Command(async () => await Shell.Current.GoToAsync("search"));
            SeeAllDealsCommand = new Command(async () => await Shell.Current.GoToAsync("deals"));
            SeeAllFeaturedCommand = new Command(async () => await Shell.Current.GoToAsync("featured"));
            SeeAllServicesCommand = new Command(async () => await Shell.Current.GoToAsync("services"));
            SeeAllFeedsCommand = new Command(async () => await Shell.Current.GoToAsync("feeds"));
            SeeAllCategoriesCommand = new Command(async () => await Shell.Current.GoToAsync("categories"));
            SeeAllProductsCommand = new Command(async () => await Shell.Current.GoToAsync("products"));
            ClearRecentlyViewedCommand = new Command(async () => await ClearRecentlyViewedAsync());
        }

        // ─── Commands ────────────────────────────────────────────────────────────
        public ICommand RefreshCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand SetFilterCommand { get; }
        public ICommand SetSortCommand { get; }
        public ICommand SelectBroadCategoryCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand OpenEcommerceItemCommand { get; }
        public ICommand OpenItemCommand { get; }
        public ICommand OpenServiceCommand { get; }
        public ICommand BookServiceCommand { get; }
        public ICommand OpenFeedCommand { get; }
        public ICommand OpenPromotionCommand { get; }
        public ICommand ToggleWishlistCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand OpenCartCommand { get; }
        public ICommand OpenMessagesCommand { get; }
        public ICommand OpenNotificationsCommand { get; }
        public ICommand OpenSearchCommand { get; }
        public ICommand SeeAllDealsCommand { get; }
        public ICommand SeeAllFeaturedCommand { get; }
        public ICommand SeeAllServicesCommand { get; }
        public ICommand SeeAllFeedsCommand { get; }
        public ICommand SeeAllCategoriesCommand { get; }
        public ICommand SeeAllProductsCommand { get; }
        public ICommand ClearRecentlyViewedCommand { get; }

        // ─── Private state ───────────────────────────────────────────────────────
        private string _currentFilter = "";   // "", "popular", "promotions", "wishlist"
        private string _currentSortBy = "popular";
        private int _currentPage = 1;
        private const int PageSize = 24;
        private System.Timers.Timer? _countdownTimer;

        // ─── Observable properties ───────────────────────────────────────────────

        // Collections
        public ObservableCollection<PromotionCarouselModel> CurrentPromotions { get; } = new();
        public ObservableCollection<BroadCategoryModel> BroadCategories { get; } = new();
        public ObservableCollection<EcommerceItemModel> FeaturedItems { get; } = new();
        public ObservableCollection<SpecialDealModel> SpecialDeals { get; } = new();
        public ObservableCollection<ServiceViewModel> FeaturedServices { get; } = new();
        public ObservableCollection<FeedViewModel> FeaturedFeeds { get; } = new();
        public ObservableCollection<CategoryViewModel> TrendingCategories { get; } = new();
        public ObservableCollection<EcommerceItemModel> RecentlyViewed { get; } = new();
        public ObservableCollection<EcommerceItemModel> Commerce { get; } = new();

        // Loading flags
        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        private bool _isLoadingMore;
        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set { _isLoadingMore = value; OnPropertyChanged(); ((Command)LoadMoreCommand).ChangeCanExecute(); }
        }

        // Pagination
        private int _totalItems;
        private int _totalPages;

        public int TotalItems
        {
            get => _totalItems;
            set { _totalItems = value; OnPropertyChanged(); }
        }

        public int TotalPages
        {
            get => _totalPages;
            set { _totalPages = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasMorePages)); }
        }

        public bool HasMorePages => _currentPage < _totalPages && !IsLoadingMore;

        // Auth / user
        private bool _isAuthenticated;
        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set { _isAuthenticated = value; OnPropertyChanged(); }
        }

        private string _greetingText = "Good morning,";
        public string GreetingText
        {
            get => _greetingText;
            set { _greetingText = value; OnPropertyChanged(); }
        }

        private string _greetingName = "Welcome back!";
        public string GreetingName
        {
            get => _greetingName;
            set { _greetingName = value; OnPropertyChanged(); }
        }

        private string? _profileImageUrl;
        public string? ProfileImageUrl
        {
            get => _profileImageUrl;
            set { _profileImageUrl = value; OnPropertyChanged(); }
        }

        // Cart
        private int _cartCount;
        public int CartCount
        {
            get => _cartCount;
            set { _cartCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCartItems)); }
        }
        public bool HasCartItems => _cartCount > 0;

        // Countdown for deals
        private string _dealCountdownText = "";
        public string DealCountdownText
        {
            get => _dealCountdownText;
            set { _dealCountdownText = value; OnPropertyChanged(); }
        }

        // Section visibility helpers
        public bool HasSpecialDeals => SpecialDeals.Count > 0;
        public bool HasFeaturedItems => FeaturedItems.Count > 0;
        public bool HasTrendingCategories => TrendingCategories.Count > 0;
        public bool HasFeaturedServices => FeaturedServices.Count > 0;
        public bool HasFeaturedFeeds => FeaturedFeeds.Count > 0;
        public bool HasRecentlyViewed => RecentlyViewed.Count > 0;

        // ─── Page lifecycle ──────────────────────────────────────────────────────

        /// <summary>
        /// Called from OnAppearing in code-behind. Loads all home page data.
        /// </summary>
        public async Task InitialiseAsync()
        {
            await LoadAllAsync(forceRefresh: false);
            SetupDealCountdownTimer();
            SetGreeting();
        }

        // ─── Core data loading ───────────────────────────────────────────────────

        private async Task LoadAllAsync(bool forceRefresh)
        {
            if (IsRefreshing && !forceRefresh) return;

            try
            {
                IsRefreshing = true;
                _currentPage = 1;

                // Fire static data and paginated items concurrently — mirrors the
                // web controller's Task.WhenAll(staticDataTask, itemsDataTask) pattern.
                var staticTask = LoadStaticDataAsync();
                var itemsTask = LoadItemsAsync(page: 1, replaceExisting: true);
                var userTask = LoadUserDataAsync();

                await Task.WhenAll(staticTask, itemsTask, userTask);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Could not load home page: {ex.Message}", "OK");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// Loads cached/static content: promotions, categories, featured items,
        /// services, feeds.  Mirrors LoadOptimizedStaticDataAsync().
        /// </summary>
        private async Task LoadStaticDataAsync()
        {
            try
            {
                // Run all static data fetches in parallel
                var promotionsTask = _api.GetCurrentPromotionsAsync();
                var broadCatsTask = _api.GetBroadCategoriesAsync();
                var featuredTask = _api.GetFeaturedItemsAsync();
                var dealsTask = _api.GetSpecialDealsAsync();
                var servicesTask = _api.GetFeaturedServicesAsync();
                var feedsTask = _api.GetFeaturedFeedsAsync();
                var trendingTask = _api.GetTrendingCategoriesAsync();
                var recentlyTask = _api.GetRecentlyViewedAsync();

                await Task.WhenAll(promotionsTask, broadCatsTask, featuredTask,
                                   dealsTask, servicesTask, feedsTask,
                                   trendingTask, recentlyTask);

                // Update promotions carousel
                ReplaceCollection(CurrentPromotions, await promotionsTask);
                OnPropertyChanged(nameof(CurrentPromotions));

                // Update broad category pills
                ReplaceCollection(BroadCategories, await broadCatsTask);
                OnPropertyChanged(nameof(BroadCategories));

                // Update featured items
                ReplaceCollection(FeaturedItems, await featuredTask);
                OnPropertyChanged(nameof(HasFeaturedItems));
                OnPropertyChanged(nameof(FeaturedItems));

                // Update special deals
                ReplaceCollection(SpecialDeals, await dealsTask);
                OnPropertyChanged(nameof(HasSpecialDeals));
                OnPropertyChanged(nameof(SpecialDeals));

                // Update services
                ReplaceCollection(FeaturedServices, await servicesTask);
                OnPropertyChanged(nameof(HasFeaturedServices));
                OnPropertyChanged(nameof(FeaturedServices));

                // Update CraftConnect feeds
                ReplaceCollection(FeaturedFeeds, await feedsTask);
                OnPropertyChanged(nameof(HasFeaturedFeeds));
                OnPropertyChanged(nameof(FeaturedFeeds));

                // Update trending categories
                ReplaceCollection(TrendingCategories, await trendingTask);
                OnPropertyChanged(nameof(HasTrendingCategories));
                OnPropertyChanged(nameof(TrendingCategories));

                // Update recently viewed
                ReplaceCollection(RecentlyViewed, await recentlyTask);
                OnPropertyChanged(nameof(HasRecentlyViewed));
                OnPropertyChanged(nameof(RecentlyViewed));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HOME] LoadStaticDataAsync error: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads paginated product/service items.
        /// Mirrors LoadOptimizedFilteredItemsAsync() — supports the same filterType
        /// values: "", "popular", "promotions", "wishlist".
        /// </summary>
        private async Task LoadItemsAsync(int page, bool replaceExisting)
        {
            try
            {
                var result = await _api.GetHomeItemsAsync(new HomeItemsRequest
                {
                    FilterType = _currentFilter,
                    SortBy = _currentSortBy,
                    PageNumber = page,
                    PageSize = PageSize
                });

                if (replaceExisting)
                    Commerce.Clear();

                foreach (var item in result.Items)
                    Commerce.Add(item);

                TotalItems = result.TotalItems;
                TotalPages = result.TotalPages;
                _currentPage = page;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HOME] LoadItemsAsync error: {ex.Message}");
            }
        }

        private async Task LoadMoreProductsAsync()
        {
            if (IsLoadingMore || !HasMorePages) return;
            try
            {
                IsLoadingMore = true;
                await LoadItemsAsync(_currentPage + 1, replaceExisting: false);
            }
            finally
            {
                IsLoadingMore = false;
            }
        }

        private async Task LoadUserDataAsync()
        {
            try
            {
                var userInfo = await _api.GetCurrentUserInfoAsync();
                if (userInfo != null)
                {
                    IsAuthenticated = true;
                    GreetingName = userInfo.GreetingName ?? "Welcome back!";
                    ProfileImageUrl = userInfo.ProfileImageUrl;
                }
                else
                {
                    IsAuthenticated = false;
                }

                CartCount = await _api.GetCartCountAsync();
            }
            catch
            {
                IsAuthenticated = false;
            }
        }

        // ─── Filter & Sort ───────────────────────────────────────────────────────

        private async Task SetFilterAsync(string filterType)
        {
            if (_currentFilter == filterType) return;
            _currentFilter = filterType;
            await LoadItemsAsync(page: 1, replaceExisting: true);
        }

        private async Task SetSortAsync(string sortBy)
        {
            if (_currentSortBy == sortBy) return;
            _currentSortBy = sortBy;
            await LoadItemsAsync(page: 1, replaceExisting: true);
        }

        private async Task FilterByBroadCategoryAsync(int broadCategoryId)
        {
            try
            {
                var result = await _api.GetHomeItemsAsync(new HomeItemsRequest
                {
                    BroadCategoryId = broadCategoryId,
                    SortBy = _currentSortBy,
                    PageNumber = 1,
                    PageSize = PageSize
                });
                Commerce.Clear();
                foreach (var item in result.Items) Commerce.Add(item);
                TotalItems = result.TotalItems;
                TotalPages = result.TotalPages;
                _currentPage = 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HOME] FilterByBroadCategory error: {ex.Message}");
            }
        }

        private async Task FilterByCategoryAsync(int categoryId)
        {
            try
            {
                var result = await _api.GetHomeItemsAsync(new HomeItemsRequest
                {
                    CategoryId = categoryId,
                    SortBy = _currentSortBy,
                    PageNumber = 1,
                    PageSize = PageSize
                });
                Commerce.Clear();
                foreach (var item in result.Items) Commerce.Add(item);
                TotalItems = result.TotalItems;
                TotalPages = result.TotalPages;
                _currentPage = 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HOME] FilterByCategory error: {ex.Message}");
            }
        }

        // ─── Navigation ──────────────────────────────────────────────────────────

        private void OpenEcommerceItem(EcommerceItemModel item)
        {
            if (item == null) return;
            var route = item.Type switch
            {
                "Service" => $"servicedetail?id={item.Id}",
                "ComboProduct" => $"comboproductdetail?id={item.Id}",
                _ => $"productdetail?id={item.Id}"
            };
            Shell.Current.GoToAsync(route);
        }

        private void OpenSpecialDealItem(SpecialDealModel deal)
        {
            if (deal == null) return;
            var route = deal.Type switch
            {
                "Service" => $"servicedetail?id={deal.Id}",
                "ComboProduct" => $"comboproductdetail?id={deal.Id}",
                _ => $"productdetail?id={deal.Id}"
            };
            Shell.Current.GoToAsync(route);
        }

        private void OpenService(ServiceViewModel service)
        {
            if (service == null) return;
            Shell.Current.GoToAsync($"servicedetail?id={service.ServiceCompanyBusinessLocationId}");
        }

        private void BookService(ServiceViewModel service)
        {
            if (service == null) return;
            Shell.Current.GoToAsync($"servicebooking?id={service.ServiceCompanyBusinessLocationId}");
        }

        private void OpenFeed(FeedViewModel feed)
        {
            if (feed == null) return;
            Shell.Current.GoToAsync($"feeddetail?id={feed.Id}");
        }

        private void OpenPromotion(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            // If it's a relative path, apply filter; otherwise navigate to full URL
            if (url.Contains("filterType=promotions"))
                SetFilterAsync("promotions").ConfigureAwait(false);
            else
                Shell.Current.GoToAsync($"webview?url={Uri.EscapeDataString(url)}");
        }

        // ─── Wishlist & Cart ─────────────────────────────────────────────────────

        private async Task ToggleWishlistAsync(EcommerceItemModel item)
        {
            if (item == null) return;

            if (!IsAuthenticated)
            {
                await Shell.Current.DisplayAlert("Sign In Required",
                    "Please sign in to manage your wishlist.", "Sign In", "Cancel");
                return;
            }

            try
            {
                if (item.IsInWishlist)
                {
                    await _api.RemoveFromWishlistAsync(item.Id, item.Type);
                    item.IsInWishlist = false;
                }
                else
                {
                    await _api.AddToWishlistAsync(item.Id, item.Type);
                    item.IsInWishlist = true;
                }
                // Also sync in FeaturedItems if the same item appears there
                SyncWishlistState(item.Id, item.IsInWishlist);
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Could not update wishlist: {ex.Message}", "OK");
            }
        }

        private void SyncWishlistState(int itemId, bool isInWishlist)
        {
            foreach (var fi in FeaturedItems.Where(f => f.Id == itemId))
                fi.IsInWishlist = isInWishlist;
            foreach (var rv in RecentlyViewed.Where(r => r.Id == itemId))
                rv.IsInWishlist = isInWishlist;
        }

        private async Task AddToCartAsync(EcommerceItemModel item)
        {
            if (item == null) return;
            try
            {
                var result = await _api.AddToCartAsync(item.Id, item.Type);
                if (result.Success)
                {
                    CartCount = result.CartCount;
                    // Brief visual feedback — could also trigger a toast via a service
                    await Shell.Current.DisplayAlert("Added!", $"{item.Name} added to cart.", "OK");
                }
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Could not add to cart: {ex.Message}", "OK");
            }
        }

        // ─── Recently Viewed ─────────────────────────────────────────────────────

        private async Task ClearRecentlyViewedAsync()
        {
            var confirm = await Shell.Current.DisplayAlert(
                "Clear History", "Remove all recently viewed items?", "Clear", "Cancel");
            if (!confirm) return;

            try
            {
                await _api.ClearRecentlyViewedAsync();
                RecentlyViewed.Clear();
                OnPropertyChanged(nameof(HasRecentlyViewed));
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Error", $"Could not clear history: {ex.Message}", "OK");
            }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces all items in an ObservableCollection without losing the
        /// reference (so XAML bindings stay intact).
        /// </summary>
        private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T>? items)
        {
            collection.Clear();
            if (items == null) return;
            foreach (var item in items)
                collection.Add(item);
        }

        private void SetGreeting()
        {
            var hour = DateTime.Now.Hour;
            GreetingText = hour switch
            {
                < 12 => "Good morning,",
                < 17 => "Good afternoon,",
                _ => "Good evening,"
            };
        }

        /// <summary>
        /// Ticks every second to show a live countdown on the deals section.
        /// Mirrors the web template's countdown timer behaviour.
        /// </summary>
        private void SetupDealCountdownTimer()
        {
            var nearestDeal = SpecialDeals.FirstOrDefault(d => d.ExpiryDate.HasValue);
            if (nearestDeal?.ExpiryDate == null) return;

            _countdownTimer?.Dispose();
            _countdownTimer = new System.Timers.Timer(1000);
            _countdownTimer.Elapsed += (_, _) =>
            {
                var remaining = nearestDeal.ExpiryDate!.Value - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    DealCountdownText = "Deal expired";
                    _countdownTimer?.Stop();
                    return;
                }
                DealCountdownText = $"Ends in {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
            };
            _countdownTimer.Start();
        }

        // ─── INotifyPropertyChanged ──────────────────────────────────────────────
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ─── Request DTO ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Maps to the web controller's Index action parameters.
    /// </summary>
    public class HomeItemsRequest
    {
        public string FilterType { get; set; } = "";
        public int? CategoryId { get; set; }
        public int? BroadCategoryId { get; set; }
        public string SortBy { get; set; } = "popular";
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 24;
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public double? MinRating { get; set; }
    }
}