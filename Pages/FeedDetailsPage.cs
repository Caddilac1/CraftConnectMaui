using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class FeedDetailsPage : ContentPage
    {
        private readonly UserFeedDto _feed;
        private readonly IUserFeedService _userFeedService;
        private bool _isLiked = false;

        public FeedDetailsPage(UserFeedDto feed, IUserFeedService userFeedService)
        {
            _feed = feed;
            _userFeedService = userFeedService;

            InitializeComponent();
            LoadFeedDetails();
        }

        private void InitializeComponent()
        {
            BackgroundColor = Color.FromArgb("#F0F2F5");

            var scrollView = new ScrollView();
            var mainLayout = new VerticalStackLayout { Spacing = 0 };

            // Header with back button
            var header = CreateHeader();
            mainLayout.Add(header);

            // User Info Section
            var userSection = CreateUserSection();
            mainLayout.Add(userSection);

            // Feed Content Section
            var contentSection = CreateContentSection();
            mainLayout.Add(contentSection);

            // Image Section (if available)
            if (!string.IsNullOrEmpty(_feed.InvoiceImage))
            {
                var imageSection = CreateImageSection();
                mainLayout.Add(imageSection);
            }

            // Details Section
            var detailsSection = CreateDetailsSection();
            mainLayout.Add(detailsSection);

            // Stats Section
            var statsSection = CreateStatsSection();
            mainLayout.Add(statsSection);

            // Action Buttons
            var actionSection = CreateActionSection();
            mainLayout.Add(actionSection);

            scrollView.Content = mainLayout;
            Content = scrollView;
        }

        private Grid CreateHeader()
        {
            var header = new Grid
            {
                BackgroundColor = Colors.White,
                Padding = 16,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var backButton = new Button
            {
                Text = "←",
                FontSize = 24,
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.Black,
                WidthRequest = 44,
                HeightRequest = 44
            };
            backButton.Clicked += async (s, e) => await Navigation.PopAsync();

            var titleLabel = new Label
            {
                Text = "Feed Details",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            };

            var shareButton = new Button
            {
                Text = "⤴",
                FontSize = 20,
                BackgroundColor = Colors.Transparent,
                TextColor = Colors.Black,
                WidthRequest = 44,
                HeightRequest = 44
            };
            shareButton.Clicked += OnShareClicked;

            header.Add(backButton, 0, 0);
            header.Add(titleLabel, 1, 0);
            header.Add(shareButton, 2, 0);

            return header;
        }

        private Frame CreateUserSection()
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 0,
                Padding = 16,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var grid = new HorizontalStackLayout { Spacing = 12 };

            var profileBorder = new Border
            {
                WidthRequest = 60,
                HeightRequest = 60,
                StrokeShape = new RoundRectangle { CornerRadius = 30 },
                Stroke = _feed.IsFeatured ? Color.FromArgb("#25D366") : Colors.Transparent,
                StrokeThickness = 3,
                Content = new Image
                {
                    Source = string.IsNullOrEmpty(_feed.UserProfileImage)
                        ? "default_profile.png"
                        : _feed.UserProfileImage,
                    Aspect = Aspect.AspectFill
                }
            };

            var userInfo = new VerticalStackLayout
            {
                Spacing = 4,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.StartAndExpand
            };

            userInfo.Add(new Label
            {
                Text = _feed.UserFullName,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            });

            userInfo.Add(new Label
            {
                Text = _feed.UserPhoneNumber,
                FontSize = 14,
                TextColor = Color.FromArgb("#667781")
            });

            userInfo.Add(new Label
            {
                Text = $"Posted {GetTimeAgo(_feed.CreatedAt)}",
                FontSize = 12,
                TextColor = Color.FromArgb("#999999")
            });

            grid.Add(profileBorder);
            grid.Add(userInfo);

            if (_feed.IsFeatured)
            {
                grid.Add(new Label
                {
                    Text = "⭐ Featured",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#25D366"),
                    FontAttributes = FontAttributes.Bold,
                    VerticalOptions = LayoutOptions.Center
                });
            }

            frame.Content = grid;
            return frame;
        }

        private Frame CreateContentSection()
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 0,
                Padding = 16,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var content = new VerticalStackLayout { Spacing = 12 };

            content.Add(new Label
            {
                Text = _feed.Title,
                FontSize = 22,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black
            });

            content.Add(new Frame
            {
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                BorderColor = Colors.Transparent,
                CornerRadius = 8,
                Padding = new Thickness(12, 6),
                HasShadow = false,
                HorizontalOptions = LayoutOptions.Start,
                Content = new Label
                {
                    Text = _feed.JobCategory,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#25D366")
                }
            });

            content.Add(new Label
            {
                Text = _feed.Description,
                FontSize = 16,
                TextColor = Color.FromArgb("#333333"),
                LineBreakMode = LineBreakMode.WordWrap
            });

            frame.Content = content;
            return frame;
        }

        private Frame CreateImageSection()
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 0,
                Padding = 16,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var image = new Image
            {
                Source = _feed.InvoiceImage,
                Aspect = Aspect.AspectFit,
                HeightRequest = 300
            };

            frame.Content = image;
            return frame;
        }

        private Frame CreateDetailsSection()
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 0,
                Padding = 16,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var content = new VerticalStackLayout { Spacing = 16 };

            content.Add(CreateDetailRow("📍", "Location", _feed.Location));

            if (_feed.PreferredStartDate.HasValue)
                content.Add(CreateDetailRow("📅", "Start Date",
                    _feed.PreferredStartDate.Value.ToString("MMMM dd, yyyy")));

            if (_feed.Deadline.HasValue)
                content.Add(CreateDetailRow("⏰", "Deadline",
                    _feed.Deadline.Value.ToString("MMMM dd, yyyy")));

            content.Add(CreateDetailRow("⚡", "Priority", _feed.PriorityDisplay));
            content.Add(CreateDetailRow("📊", "Status", _feed.StatusDisplay));

            frame.Content = content;
            return frame;
        }

        private Grid CreateDetailRow(string emoji, string label, string value)
        {
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(40) },
                    new ColumnDefinition { Width = new GridLength(100) },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            grid.Add(new Label
            {
                Text = emoji,
                FontSize = 20,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            grid.Add(new Label
            {
                Text = label,
                FontSize = 14,
                TextColor = Color.FromArgb("#667781"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);

            grid.Add(new Label
            {
                Text = value,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.Black,
                VerticalOptions = LayoutOptions.Center
            }, 2, 0);

            return grid;
        }

        private Frame CreateStatsSection()
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 0,
                Padding = 16,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            grid.Add(CreateStatColumn("👁", _feed.ViewsCount.ToString(), "Views"), 0, 0);
            grid.Add(CreateStatColumn("❤️", _feed.LikesCount.ToString(), "Likes"), 1, 0);
            grid.Add(CreateStatColumn("💬", _feed.CommentsCount.ToString(), "Comments"), 2, 0);
            grid.Add(CreateStatColumn("👎", _feed.DislikesCount.ToString(), "Dislikes"), 3, 0);

            frame.Content = grid;
            return frame;
        }

        private VerticalStackLayout CreateStatColumn(string emoji, string count, string label)
        {
            return new VerticalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = emoji, FontSize = 24, HorizontalOptions = LayoutOptions.Center },
                    new Label
                    {
                        Text = count,
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Colors.Black,
                        HorizontalOptions = LayoutOptions.Center
                    },
                    new Label
                    {
                        Text = label,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#667781"),
                        HorizontalOptions = LayoutOptions.Center
                    }
                }
            };
        }

        private Frame CreateActionSection()
        {
            var frame = new Frame
            {
                BackgroundColor = Colors.White,
                BorderColor = Colors.Transparent,
                CornerRadius = 0,
                Padding = 16,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 12
            };

            var likeButton = new Button
            {
                Text = _isLiked ? "❤️" : "🤍",
                FontSize = 24,
                BackgroundColor = Color.FromArgb("#F0F2F5"),
                WidthRequest = 60,
                HeightRequest = 56,
                CornerRadius = 12
            };
            likeButton.Clicked += OnLikeClicked;

            var proposalButton = new Button
            {
                Text = "Send Proposal",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Color.FromArgb("#25D366"),
                TextColor = Colors.White,
                HeightRequest = 56,
                CornerRadius = 12
            };
            proposalButton.Clicked += OnSendProposalClicked;

            grid.Add(likeButton, 0, 0);
            grid.Add(proposalButton, 1, 0);

            frame.Content = grid;
            return frame;
        }

        private void LoadFeedDetails()
        {
            // Additional loading logic if needed
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 1) return "just now";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} minutes ago";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} hours ago";
            if (timeSpan.TotalDays < 7) return $"{(int)timeSpan.TotalDays} days ago";

            return dateTime.ToString("MMM dd, yyyy");
        }

        private async void OnLikeClicked(object sender, EventArgs e)
        {
            try
            {
                var success = await _userFeedService.LikeFeedAsync(_feed.Id);
                if (success)
                {
                    _isLiked = !_isLiked;
                    ((Button)sender).Text = _isLiked ? "❤️" : "🤍";
                    _feed.LikesCount += _isLiked ? 1 : -1;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to like feed: {ex.Message}", "OK");
            }
        }

        private async void OnSendProposalClicked(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new SendProposalPage(_feed));
        }

        private async void OnShareClicked(object sender, EventArgs e)
        {
            await Share.RequestAsync(new ShareTextRequest
            {
                Title = _feed.Title,
                Text = $"Check out this feed: {_feed.Title}\n\n{_feed.Description}"
            });
        }
    }
}