using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CraftConnect_Mobile_App.Services;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.PageModels
{
    public class UpdatesFeedPageModel : INotifyPropertyChanged
    {
        #region Fields
        private readonly IUserFeedService _userFeedService;
        private bool _isRefreshing;
        private bool _isLoadingMore;
        private bool _isEmpty;
        private bool _hasInitialized;
        private string _feedCount;
        private ObservableCollection<FeedItemModel> _featuredFeeds;
        private ObservableCollection<FeedItemModel> _allFeeds;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private const int PAGE_SIZE = 20;
        #endregion

        #region Properties
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        public bool IsLoadingMore
        {
            get => _isLoadingMore;
            set { _isLoadingMore = value; OnPropertyChanged(); }
        }

        public bool IsEmpty
        {
            get => _isEmpty;
            set { _isEmpty = value; OnPropertyChanged(); }
        }

        public bool HasFeaturedFeeds => FeaturedFeeds?.Count > 0;

        public string FeedCount
        {
            get => _feedCount;
            set { _feedCount = value; OnPropertyChanged(); }
        }

        public ObservableCollection<FeedItemModel> FeaturedFeeds
        {
            get => _featuredFeeds;
            set
            {
                _featuredFeeds = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasFeaturedFeeds));
            }
        }

        public ObservableCollection<FeedItemModel> AllFeeds
        {
            get => _allFeeds;
            set
            {
                _allFeeds = value;
                OnPropertyChanged();
                UpdateFeedCount();
            }
        }
        #endregion

        #region Events

        /// <summary>
        /// Raised when the user taps "Send Proposal" on a feed card.
        /// The page subscribes to this and handles the actual navigation,
        /// because CreateProposalPage needs complex objects (feed list)
        /// that cannot travel through Shell query parameters.
        /// </summary>
        public event EventHandler<SendProposalNavigationArgs>? NavigateToCreateProposal;

        #endregion

        #region Commands
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand FeedTappedCommand { get; }
        public ICommand AddToFavoriteCommand { get; }
        public ICommand SendProposalCommand { get; }
        public ICommand LikeFeedCommand { get; }
        #endregion

        #region Constructor
        public UpdatesFeedPageModel(IUserFeedService userFeedService)
        {
            _userFeedService = userFeedService;

            FeaturedFeeds = new ObservableCollection<FeedItemModel>();
            AllFeeds = new ObservableCollection<FeedItemModel>();

            RefreshCommand = new Command(async () => await RefreshFeeds());
            SearchCommand = new Command(OnSearch);
            FilterCommand = new Command(OnFilter);
            LoadMoreCommand = new Command(async () => await LoadMoreFeeds());
            FeedTappedCommand = new Command<FeedItemModel>(OnFeedTapped);
            AddToFavoriteCommand = new Command<FeedItemModel>(async (feed) => await AddToFavorite(feed));
            LikeFeedCommand = new Command<FeedItemModel>(async (feed) => await LikeFeed(feed));

            // ── SendProposal fires event; navigation handled by the page ──
            SendProposalCommand = new Command<FeedItemModel>(OnSendProposal);
        }
        #endregion

        #region Public Methods
        public async Task InitializeAsync()
        {
            if (_hasInitialized) return;
            await LoadFeedsAsync();
            _hasInitialized = true;
        }

        public async Task LoadFeedsAsync()
        {
            if (IsRefreshing || IsLoadingMore) return;

            IsLoadingMore = true;
            IsEmpty = false;
            _currentPage = 1;

            try
            {
                System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Loading feeds from API...");

                var featuredTask = _userFeedService.GetFeaturedFeedsAsync(10);
                var allFeedsTask = _userFeedService.GetUserFeedsAsync(page: _currentPage, pageSize: PAGE_SIZE);

                await Task.WhenAll(featuredTask, allFeedsTask);

                var featuredData = await featuredTask;
                var (feeds, totalCount, totalPages) = await allFeedsTask;

                _totalPages = totalPages;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    FeaturedFeeds.Clear();
                    foreach (var feed in featuredData)
                        FeaturedFeeds.Add(MapToFeedItemModel(feed));

                    AllFeeds.Clear();
                    foreach (var feed in feeds)
                        AllFeeds.Add(MapToFeedItemModel(feed));

                    IsEmpty = AllFeeds.Count == 0;
                    UpdateFeedCount();
                });

                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] ✅ Loaded {AllFeeds.Count} feeds");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] ❌ Error loading feeds: {ex.Message}");

                MainThread.BeginInvokeOnMainThread(() => IsEmpty = true);

                if (Application.Current?.MainPage != null)
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Connection Error",
                        "Unable to load feeds. Please check your internet connection.",
                        "OK");
                }
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    IsLoadingMore = false;
                    IsRefreshing = false;
                });
            }
        }

        public async Task RefreshFeeds()
        {
            System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Refreshing feeds...");
            IsRefreshing = true;
            _currentPage = 1;
            await LoadFeedsAsync();
            IsRefreshing = false;
        }

        public async Task LoadMoreFeeds()
        {
            if (IsLoadingMore || IsRefreshing || _currentPage >= _totalPages) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Loading more feeds...");
                IsLoadingMore = true;
                _currentPage++;

                var (feeds, _, _) = await _userFeedService.GetUserFeedsAsync(
                    page: _currentPage,
                    pageSize: PAGE_SIZE);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    foreach (var feed in feeds)
                        AllFeeds.Add(MapToFeedItemModel(feed));
                    UpdateFeedCount();
                });

                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Loaded {feeds.Count} more feeds");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] ❌ Error loading more: {ex.Message}");
                _currentPage--;
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(() => IsLoadingMore = false);
            }
        }
        #endregion

        #region Private Methods

        /// <summary>
        /// Fires NavigateToCreateProposal so the page can push CreateProposalPage
        /// with the full feed list and this feed pre-selected.
        /// </summary>
        private void OnSendProposal(FeedItemModel feed)
        {
            if (feed == null)
            {
                System.Diagnostics.Debug.WriteLine("[UpdatesFeed] ❌ SendProposal called with null feed.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] 📨 Send Proposal — Id: {feed.Id}, Title: {feed.Title}");

            NavigateToCreateProposal?.Invoke(this, new SendProposalNavigationArgs
            {
                FeedId = feed.Id,
                FeedTitle = feed.Title ?? string.Empty
            });
        }

        private FeedItemModel MapToFeedItemModel(UserFeedDto dto)
        {
            var userName = dto.User?.FullName ?? dto.UserFullName ?? "Anonymous";
            var userPhone = dto.User?.PhoneNumber ?? dto.UserPhoneNumber;
            var userProfileImage = dto.User?.ProfilePicture ?? dto.UserProfileImage;

            return new FeedItemModel
            {
                Id = dto.Id.ToString(),
                Title = dto.Title,
                Description = dto.Description,
                ImageUrl = !string.IsNullOrEmpty(dto.InvoiceImage)
                                       ? dto.InvoiceImage
                                       : "https://via.placeholder.com/400x250?text=No+Image",
                Category = dto.JobCategory,
                PublishedDate = dto.CreatedAt,
                Author = userName,
                IsFeatured = dto.IsFeatured,
                LikesCount = dto.LikesCount,
                CommentsCount = dto.CommentsCount,
                Location = dto.Location,
                Status = dto.Status,
                Priority = dto.Priority,
                Deadline = dto.Deadline,
                UserId = dto.UserId.ToString(),
                UserPhone = userPhone,
                UserProfileImage = userProfileImage,
                ViewsCount = dto.ViewsCount
            };
        }

        private void UpdateFeedCount()
        {
            int count = AllFeeds?.Count ?? 0;
            FeedCount = count == 1 ? "1 feed" : $"{count} feeds";
        }

        private void OnSearch()
        {
            System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Search clicked");
            // TODO: Navigate to search page
        }

        private void OnFilter()
        {
            System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Filter clicked");
            // TODO: Show filter options
        }

        private void OnFeedTapped(FeedItemModel feed)
        {
            if (feed == null) return;
            System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Feed tapped: {feed.Title}");
            Shell.Current.GoToAsync($"feedDetail?id={feed.Id}");
        }

        private async Task AddToFavorite(FeedItemModel feed)
        {
            if (feed == null) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Toggling favorite: {feed.Title}");
                feed.IsFavorite = !feed.IsFavorite;
                // TODO: await _userFeedService.AddToFavoriteAsync(Guid.Parse(feed.Id));
                System.Diagnostics.Debug.WriteLine(feed.IsFavorite ? "Added to favorites ❤️" : "Removed from favorites");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] ❌ Favorite error: {ex.Message}");
                feed.IsFavorite = !feed.IsFavorite; // revert
            }
        }

        private async Task LikeFeed(FeedItemModel feed)
        {
            if (feed == null) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Liking feed: {feed.Title}");

                var wasLiked = feed.IsLiked;
                feed.IsLiked = !feed.IsLiked;
                feed.LikesCount += feed.IsLiked ? 1 : -1;

                var success = await _userFeedService.LikeFeedAsync(Guid.Parse(feed.Id));

                if (!success)
                {
                    feed.IsLiked = wasLiked;
                    feed.LikesCount += wasLiked ? 1 : -1;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] ❌ Like error: {ex.Message}");
            }
        }

        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
    }

    // ══════════════════════════════════════════════════════════════════
    // ▌ NAVIGATION ARGS
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Carried by the NavigateToCreateProposal event so the page knows
    /// which feed was tapped and can pre-select it on CreateProposalPage.
    /// </summary>
    public class SendProposalNavigationArgs : EventArgs
    {
        public string FeedId { get; init; } = string.Empty;
        public string FeedTitle { get; init; } = string.Empty;
    }

    // ══════════════════════════════════════════════════════════════════
    // ▌ FEED ITEM MODEL
    // ══════════════════════════════════════════════════════════════════

    public class FeedItemModel : INotifyPropertyChanged
    {
        private bool _isLiked;
        private bool _isFavorite;
        private int _likesCount;

        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public DateTime PublishedDate { get; set; }
        public string Author { get; set; }
        public bool IsFeatured { get; set; }
        public int CommentsCount { get; set; }
        public string Location { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public DateTime? Deadline { get; set; }
        public string UserId { get; set; }
        public string UserPhone { get; set; }
        public string UserProfileImage { get; set; }
        public int ViewsCount { get; set; }

        public int LikesCount
        {
            get => _likesCount;
            set { _likesCount = value; OnPropertyChanged(); }
        }

        public bool IsLiked
        {
            get => _isLiked;
            set { _isLiked = value; OnPropertyChanged(); }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                _isFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FavoriteIcon));
            }
        }

        public string FavoriteIcon => IsFavorite ? "❤️" : "🤍";

        public string TimeAgo
        {
            get
            {
                var ts = DateTime.Now - PublishedDate;
                if (ts.TotalMinutes < 60) return $"{(int)ts.TotalMinutes}m ago";
                if (ts.TotalHours < 24) return $"{(int)ts.TotalHours}h ago";
                if (ts.TotalDays < 7) return $"{(int)ts.TotalDays}d ago";
                return PublishedDate.ToString("MMM dd");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}