using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CraftConnect_Mobile_App.PageModels
{
    public class UpdatesFeedPageModel : INotifyPropertyChanged
    {
        #region Fields
        private bool _isLoading;
        private bool _isEmpty;
        private string _feedCount;
        private ObservableCollection<FeedItemModel> _featuredFeeds;
        private ObservableCollection<FeedItemModel> _allFeeds;
        #endregion

        #region Properties
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public bool IsEmpty
        {
            get => _isEmpty;
            set
            {
                _isEmpty = value;
                OnPropertyChanged();
            }
        }

        public string FeedCount
        {
            get => _feedCount;
            set
            {
                _feedCount = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FeedItemModel> FeaturedFeeds
        {
            get => _featuredFeeds;
            set
            {
                _featuredFeeds = value;
                OnPropertyChanged();
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

        #region Commands
        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand FilterCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand FeedTappedCommand { get; }
        #endregion

        #region Constructor
        public UpdatesFeedPageModel()
        {
            // Initialize collections
            FeaturedFeeds = new ObservableCollection<FeedItemModel>();
            AllFeeds = new ObservableCollection<FeedItemModel>();

            // Initialize commands
            RefreshCommand = new Command(async () => await RefreshFeeds());
            SearchCommand = new Command(OnSearch);
            FilterCommand = new Command(OnFilter);
            LoadMoreCommand = new Command(async () => await LoadMoreFeeds());
            FeedTappedCommand = new Command<FeedItemModel>(OnFeedTapped);

            // DON'T load initial data in constructor - let the page do it
            // This prevents the DI error during navigation
        }
        #endregion

        #region Public Methods
        public async Task InitializeAsync()
        {
            // Call this from the page's OnAppearing instead of constructor
            await LoadFeedsAsync();
        }

        public async Task LoadFeedsAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            IsEmpty = false;

            try
            {
                System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Loading feeds...");

                // Simulate API call - Replace with actual service call
                await Task.Delay(800);

                // Load featured feeds
                var featuredData = await GetFeaturedFeedsFromService();
                FeaturedFeeds.Clear();
                foreach (var feed in featuredData)
                {
                    FeaturedFeeds.Add(feed);
                }

                // Load all feeds
                var allFeedsData = await GetAllFeedsFromService();
                AllFeeds.Clear();
                foreach (var feed in allFeedsData)
                {
                    AllFeeds.Add(feed);
                }

                // Update empty state
                IsEmpty = AllFeeds.Count == 0;
                UpdateFeedCount();

                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Loaded {AllFeeds.Count} feeds successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] ❌ Error loading feeds: {ex.Message}");
                IsEmpty = true;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task RefreshFeeds()
        {
            System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Refreshing feeds...");
            await LoadFeedsAsync();
        }

        public async Task LoadMoreFeeds()
        {
            if (IsLoading) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Loading more feeds...");

                // Simulate loading more feeds
                var moreFeeds = await GetMoreFeedsFromService();
                foreach (var feed in moreFeeds)
                {
                    AllFeeds.Add(feed);
                }
                UpdateFeedCount();

                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Loaded {moreFeeds.Count} more feeds");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] ❌ Error loading more feeds: {ex.Message}");
            }
        }
        #endregion

        #region Private Methods
        private void UpdateFeedCount()
        {
            int count = AllFeeds?.Count ?? 0;
            FeedCount = count == 1 ? "1 feed" : $"{count} feeds";
        }

        private void OnSearch()
        {
            System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Search clicked");
            // Implement search functionality
            // Navigate to search page or show search overlay
        }

        private void OnFilter()
        {
            System.Diagnostics.Debug.WriteLine("[UpdatesFeed] Filter clicked");
            // Implement filter functionality
            // Show filter options (e.g., by category, date, etc.)
        }

        private void OnFeedTapped(FeedItemModel feed)
        {
            if (feed == null) return;

            System.Diagnostics.Debug.WriteLine($"[UpdatesFeed] Feed tapped: {feed.Title}");
            // Navigate to feed detail page
            // Shell.Current.GoToAsync($"feedDetail?id={feed.Id}");
        }

        #region Mock Data Service Methods
        // Replace these with actual API service calls
        private async Task<List<FeedItemModel>> GetFeaturedFeedsFromService()
        {
            await Task.Delay(300); // Simulate network delay

            return new List<FeedItemModel>
            {
                new FeedItemModel
                {
                    Id = "f1",
                    Title = "New Partnership Announcement",
                    Description = "We're excited to announce our partnership with local artisans across Ghana...",
                    ImageUrl = "https://picsum.photos/300/200?random=1",
                    Category = "Announcement",
                    PublishedDate = DateTime.Now.AddHours(-2),
                    IsFeatured = true,
                    Author = "CraftConnect Team",
                    LikesCount = 245,
                    CommentsCount = 38
                },
                new FeedItemModel
                {
                    Id = "f2",
                    Title = "Community Spotlight: Master Craftspeople",
                    Description = "Meet the talented craftspeople making waves in our community...",
                    ImageUrl = "https://picsum.photos/300/200?random=2",
                    Category = "Community",
                    PublishedDate = DateTime.Now.AddHours(-5),
                    IsFeatured = true,
                    Author = "Editorial Team",
                    LikesCount = 189,
                    CommentsCount = 42
                },
                new FeedItemModel
                {
                    Id = "f3",
                    Title = "Weekend Workshop Series",
                    Description = "Join our intensive workshop covering essential crafting skills...",
                    ImageUrl = "https://picsum.photos/300/200?random=3",
                    Category = "Events",
                    PublishedDate = DateTime.Now.AddHours(-8),
                    IsFeatured = true,
                    Author = "Workshop Team",
                    LikesCount = 312,
                    CommentsCount = 67
                }
            };
        }

        private async Task<List<FeedItemModel>> GetAllFeedsFromService()
        {
            await Task.Delay(500); // Simulate network delay

            return new List<FeedItemModel>
            {
                new FeedItemModel
                {
                    Id = "1",
                    Title = "5 Tips for Improving Your Craft",
                    Description = "Learn essential techniques to take your craftsmanship to the next level with these proven methods...",
                    ImageUrl = "https://picsum.photos/400/250?random=4",
                    Category = "Tips & Tricks",
                    PublishedDate = DateTime.Now.AddDays(-1),
                    Author = "Jane Smith",
                    LikesCount = 124,
                    CommentsCount = 32
                },
                new FeedItemModel
                {
                    Id = "2",
                    Title = "Upcoming Workshop: Woodworking Basics",
                    Description = "Join us for an intensive weekend workshop covering fundamental woodworking skills and techniques...",
                    ImageUrl = "https://picsum.photos/400/250?random=5",
                    Category = "Events",
                    PublishedDate = DateTime.Now.AddDays(-2),
                    Author = "Workshop Coordinator",
                    LikesCount = 89,
                    CommentsCount = 15
                },
                new FeedItemModel
                {
                    Id = "3",
                    Title = "Material Spotlight: Sustainable Woods",
                    Description = "Discover eco-friendly wood options for your next project and learn about sustainable sourcing...",
                    ImageUrl = "https://picsum.photos/400/250?random=6",
                    Category = "Materials",
                    PublishedDate = DateTime.Now.AddDays(-3),
                    Author = "Environmental Team",
                    LikesCount = 203,
                    CommentsCount = 47
                },
                new FeedItemModel
                {
                    Id = "4",
                    Title = "Success Story: From Hobby to Business",
                    Description = "Read how one artisan turned their passion into a thriving business with dedication and strategy...",
                    ImageUrl = "https://picsum.photos/400/250?random=7",
                    Category = "Success Stories",
                    PublishedDate = DateTime.Now.AddDays(-4),
                    Author = "Business Development",
                    LikesCount = 156,
                    CommentsCount = 28
                },
                new FeedItemModel
                {
                    Id = "5",
                    Title = "Crafting Trends in 2024",
                    Description = "Explore the latest trends in handcrafted goods and what customers are looking for this year...",
                    ImageUrl = "https://picsum.photos/400/250?random=8",
                    Category = "Trends",
                    PublishedDate = DateTime.Now.AddDays(-5),
                    Author = "Market Insights",
                    LikesCount = 178,
                    CommentsCount = 34
                },
                new FeedItemModel
                {
                    Id = "6",
                    Title = "Tool Maintenance 101",
                    Description = "Keep your tools in top condition with these essential maintenance tips and best practices...",
                    ImageUrl = "https://picsum.photos/400/250?random=9",
                    Category = "Tips & Tricks",
                    PublishedDate = DateTime.Now.AddDays(-6),
                    Author = "Technical Team",
                    LikesCount = 95,
                    CommentsCount = 18
                }
            };
        }

        private async Task<List<FeedItemModel>> GetMoreFeedsFromService()
        {
            await Task.Delay(400); // Simulate network delay

            return new List<FeedItemModel>
            {
                new FeedItemModel
                {
                    Id = "7",
                    Title = "Market Trends in Handcrafted Goods",
                    Description = "Analysis of current market trends and customer preferences in the crafts industry...",
                    ImageUrl = "https://picsum.photos/400/250?random=10",
                    Category = "Market Insights",
                    PublishedDate = DateTime.Now.AddDays(-7),
                    Author = "Market Research",
                    LikesCount = 76,
                    CommentsCount = 12
                },
                new FeedItemModel
                {
                    Id = "8",
                    Title = "Collaboration Opportunities",
                    Description = "Connect with other artisans for exciting collaborative projects and partnerships...",
                    ImageUrl = "https://picsum.photos/400/250?random=11",
                    Category = "Community",
                    PublishedDate = DateTime.Now.AddDays(-8),
                    Author = "Community Manager",
                    LikesCount = 134,
                    CommentsCount = 29
                }
            };
        }
        #endregion
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }

    #region Feed Item Model
    public class FeedItemModel : INotifyPropertyChanged
    {
        private bool _isLiked;
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

        public int LikesCount
        {
            get => _likesCount;
            set
            {
                _likesCount = value;
                OnPropertyChanged();
            }
        }

        public bool IsLiked
        {
            get => _isLiked;
            set
            {
                _isLiked = value;
                OnPropertyChanged();
            }
        }

        public string TimeAgo
        {
            get
            {
                var timeSpan = DateTime.Now - PublishedDate;
                if (timeSpan.TotalMinutes < 60)
                    return $"{(int)timeSpan.TotalMinutes}m ago";
                if (timeSpan.TotalHours < 24)
                    return $"{(int)timeSpan.TotalHours}h ago";
                if (timeSpan.TotalDays < 7)
                    return $"{(int)timeSpan.TotalDays}d ago";
                return PublishedDate.ToString("MMM dd");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    #endregion
}