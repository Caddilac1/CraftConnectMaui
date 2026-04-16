using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class MyProfilePage : ContentPage
    {
        private readonly IUserService _userService;
        private readonly ApiConfig _apiConfig;
        private readonly IProfileApiService _profileApiService;

        private readonly SemaphoreSlim _loadLock = new(1, 1);
        private bool _isLoading;

        // JWT Cache
        private static string _cachedToken;
        private static JsonElement _cachedJwtPayload;
        private static bool _jwtParsed;

        // UI State
        private bool _historyExpanded;
        private bool _referralExpanded;

        // Company Data
        private int? _userCompanyId;
        private string _userCompanyName;
        private decimal? _userCompanyTrustScore;
        private string _userCompanyTrustBand;

        // Cached Data
        private IReadOnlyList<MobileTrustScoreHistoryItem> _cachedHistory;
        private IReadOnlyList<MobileWorkReferral> _cachedWorkReferrals;
        private IReadOnlyList<MobileVendorReferral> _cachedVendorReferrals;
        private IReadOnlyList<MobileColleagueReferral> _cachedColleagueReferrals;

        public MyProfilePage(IUserService userService, ApiConfig apiConfig, IProfileApiService profileApiService)
        {
            InitializeComponent();
            _userService = userService;
            _apiConfig = apiConfig;
            _profileApiService = profileApiService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadProfileSafeAsync();
        }

        #region Profile Loading

        private async Task LoadProfileSafeAsync()
        {
            if (_isLoading) return;

            await _loadLock.WaitAsync();
            try
            {
                if (_isLoading) return;
                _isLoading = true;
                await LoadProfileAsync();
            }
            finally
            {
                _isLoading = false;
                _loadLock.Release();
            }
        }

        private async Task LoadProfileAsync()
        {
            SetLoading(true);
            ResetUIBatch();

            try
            {
                await LoadCompanyFromJwtCachedAsync();

                var profile = await _userService.LoadUserProfileAsync();
                if (profile == null)
                {
                    ShowError("Failed to load profile.");
                    return;
                }

                PopulateHeader(profile);
                PopulateBio(profile);
                PopulatePersonal(profile);
                ApplyTrustUIFast();

                if (profile is ArtisanUser artisan)
                {
                    PopulateArtisan(artisan);
                }

                EditProfileButton.IsVisible = true;
                ErrorState.IsVisible = false;
            }
            catch (UnauthorizedAccessException)
            {
                await DisplayAlert("Session expired", "Please login again.", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                ShowError("Unexpected error loading profile.");
            }
            finally
            {
                SetLoading(false);
            }
        }

        #endregion

        #region JWT Handling

        private async Task LoadCompanyFromJwtCachedAsync()
        {
            var token = await SecureStorage.GetAsync("auth_token");
            if (string.IsNullOrWhiteSpace(token)) return;

            if (_jwtParsed && token == _cachedToken)
            {
                ApplyJwtCache();
                return;
            }

            try
            {
                var payload = token.Split('.')[1];
                payload = Base64Fix(payload);
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                var doc = JsonDocument.Parse(json);

                _cachedJwtPayload = doc.RootElement.Clone();
                _cachedToken = token;
                _jwtParsed = true;
                ExtractJwtClaims(_cachedJwtPayload);
            }
            catch
            {
                _jwtParsed = false;
            }
        }

        private void ExtractJwtClaims(JsonElement root)
        {
            _userCompanyId = TryGetInt(root, "CompanyId", "companyId", "company_id");
            _userCompanyName = TryGetString(root, "CompanyName", "company_name");
            _userCompanyTrustBand = TryGetString(root, "CompanyTrustBand");
            _userCompanyTrustScore = TryGetDecimal(root, "CompanyTrustScore");
        }

        private void ApplyJwtCache() => ExtractJwtClaims(_cachedJwtPayload);

        private static string Base64Fix(string input)
        {
            input = input.Replace('-', '+').Replace('_', '/');
            var padding = input.Length % 4;
            return padding switch
            {
                2 => input + "==",
                3 => input + "=",
                _ => input
            };
        }

        private static int? TryGetInt(JsonElement root, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (root.TryGetProperty(k, out var el))
                {
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var v))
                        return v;
                    if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var sv))
                        return sv;
                }
            }
            return null;
        }

        private static string TryGetString(JsonElement root, params string[] keys)
        {
            foreach (var k in keys)
            {
                if (root.TryGetProperty(k, out var el) && el.ValueKind == JsonValueKind.String)
                    return el.GetString();
            }
            return null;
        }

        private static decimal? TryGetDecimal(JsonElement root, string key)
        {
            if (!root.TryGetProperty(key, out var el)) return null;
            if (el.ValueKind == JsonValueKind.Number && decimal.TryParse(el.GetRawText(), out var d))
                return d;
            if (el.ValueKind == JsonValueKind.String && decimal.TryParse(el.GetString(), out var ds))
                return ds;
            return null;
        }

        #endregion

        #region UI Population

        private void PopulateHeader(UserProfile profile)
        {
            var name = profile.DisplayName;
            if (Guid.TryParse(name, out _))
                name = profile.Email;

            ProfileNameLabel.Text = name;
            ProfileEmailLabel.Text = profile.Email;
            ProfileInitialsLabel.Text = GetInitials(name);
        }

        private void PopulateBio(UserProfile profile)
        {
            BioCard.IsVisible = !string.IsNullOrWhiteSpace(profile.Bio);
            BioLabel.Text = profile.Bio;
        }

        private void PopulatePersonal(UserProfile profile)
        {
            PersonalInfoCard.IsVisible = true;
            PersonalEmailLabel.Text = profile.Email;
            PersonalPhoneLabel.Text = profile.PhoneNumber;
        }

        private void PopulateArtisan(ArtisanUser artisan)
        {
            ArtisanBusinessCard.IsVisible = true;
            BusinessNameLabel.Text = artisan.BusinessName;
            StatRatingLabel.Text = $"{artisan.AverageRating:F1}";
        }

        private void ApplyTrustUIFast()
        {
            if (!_userCompanyTrustScore.HasValue)
            {
                TrustScoreCard.IsVisible = false;
                return;
            }

            TrustScoreCard.IsVisible = true;
            TrustScoreValueLabel.Text = _userCompanyTrustScore.Value.ToString("F0");
            TrustBandLabel.Text = (_userCompanyTrustBand ?? "—").ToUpper();
            TrustScoreValueLabel.TextColor = _userCompanyTrustScore >= 700 ? Colors.LightGreen :
                                              _userCompanyTrustScore >= 550 ? Colors.Orange :
                                              Colors.IndianRed;
        }

        private void ResetUIBatch()
        {
            BioCard.IsVisible = false;
            PersonalInfoCard.IsVisible = false;
            ArtisanBusinessCard.IsVisible = false;
            TrustScoreCard.IsVisible = false;
            EditProfileButton.IsVisible = false;
            ErrorState.IsVisible = false;

            TrustHistoryLayout.Children.Clear();
            WorkReferralLayout.Children.Clear();
            VendorReferralLayout.Children.Clear();
            ColleagueReferralLayout.Children.Clear();

            _cachedHistory = null;
            _cachedWorkReferrals = null;
            _cachedVendorReferrals = null;
            _cachedColleagueReferrals = null;
            _historyExpanded = false;
            _referralExpanded = false;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpper()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        #endregion

        #region Trust History & Referrals

        private async Task LoadTrustHistoryAsync()
        {
            try
            {
                ShowTrustHistoryLoading(true);

                if (_profileApiService != null && _userCompanyId.HasValue)
                {
                    _cachedHistory = await _profileApiService.GetTrustScoreHistoryAsync(_userCompanyId.Value);
                }

                PopulateTrustHistory(_cachedHistory);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading trust history: {ex}");
                ShowTrustHistoryError("Failed to load history");
            }
            finally
            {
                ShowTrustHistoryLoading(false);
            }
        }

        private async Task LoadReferralDetailsAsync()
        {
            try
            {
                ShowTrustHistoryLoading(true);

                if (_profileApiService != null && _userCompanyId.HasValue)
                {
                    _cachedWorkReferrals = await _profileApiService.GetWorkReferralsAsync(_userCompanyId.Value);
                    _cachedVendorReferrals = await _profileApiService.GetVendorReferralsAsync(_userCompanyId.Value);
                    _cachedColleagueReferrals = await _profileApiService.GetColleagueReferralsAsync(_userCompanyId.Value);
                }

                UpdateReferralSummaryCounts(
                    _cachedWorkReferrals?.Count ?? 0,
                    _cachedVendorReferrals?.Count ?? 0,
                    _cachedColleagueReferrals?.Count ?? 0);

                PopulateReferralDetails();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading referral details: {ex}");
                ShowTrustHistoryError("Failed to load referral details");
            }
            finally
            {
                ShowTrustHistoryLoading(false);
            }
        }

        private void PopulateTrustHistory(IReadOnlyList<MobileTrustScoreHistoryItem> history)
        {
            TrustHistoryLayout?.Children.Clear();

            if (history == null || history.Count == 0)
            {
                if (NoHistoryLabel != null)
                    NoHistoryLabel.IsVisible = true;
                return;
            }

            if (NoHistoryLabel != null)
                NoHistoryLabel.IsVisible = false;

            for (int i = 0; i < history.Count; i++)
            {
                var historyItem = CreateHistoryItem(history[i]);
                TrustHistoryLayout.Children.Add(historyItem);

                if (i < history.Count - 1)
                {
                    TrustHistoryLayout.Children.Add(CreateSeparator());
                }
            }
        }

        private void PopulateReferralDetails()
        {
            PopulateWorkReferrals();
            PopulateVendorReferrals();
            PopulateColleagueReferrals();

            NoReferralsLabel.IsVisible =
                (_cachedWorkReferrals == null || _cachedWorkReferrals.Count == 0) &&
                (_cachedVendorReferrals == null || _cachedVendorReferrals.Count == 0) &&
                (_cachedColleagueReferrals == null || _cachedColleagueReferrals.Count == 0);
        }

        #endregion

        #region UI Component Creators

        private Grid CreateHistoryItem(MobileTrustScoreHistoryItem item)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Padding = new Thickness(0, 8),
                ColumnSpacing = 8
            };

            // Date - using RecordedAt (correct property name)
            var dateLabel = new Label
            {
                Text = item.RecordedAt.ToString("MMM dd, yyyy"),
                FontSize = 12,
                TextColor = Color.FromArgb("#8BAFC9"),
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(dateLabel, 0);

            // Score
            var scoreLabel = new Label
            {
                Text = item.Score.ToString("F0"),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FFFFFF"),
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(scoreLabel, 1);

            // Band instead of Change (since Change doesn't exist)
            var bandLabel = new Label
            {
                Text = item.Band ?? "—",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = GetBandColor(item.Band),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End
            };
            Grid.SetColumn(bandLabel, 2);

            grid.Children.Add(dateLabel);
            grid.Children.Add(scoreLabel);
            grid.Children.Add(bandLabel);

            return grid;
        }

        private Color GetBandColor(string band)
        {
            if (string.IsNullOrEmpty(band)) return Color.FromArgb("#8BAFC9");

            return band.ToLower() switch
            {
                "gold" => Color.FromArgb("#FBBF24"),
                "silver" => Color.FromArgb("#9CA3AF"),
                "bronze" => Color.FromArgb("#CD7F32"),
                _ => Color.FromArgb("#8BAFC9")
            };
        }

        private BoxView CreateSeparator() => new()
        {
            HeightRequest = 0.5,
            Color = Color.FromArgb("#243B52"),
            Margin = new Thickness(0, 4)
        };

        private void PopulateWorkReferrals()
        {
            WorkReferralLayout.Children.Clear();

            if (_cachedWorkReferrals != null && _cachedWorkReferrals.Count > 0)
            {
                WorkReferralSection.IsVisible = true;
                foreach (var referral in _cachedWorkReferrals)
                {
                    WorkReferralLayout.Children.Add(CreateWorkReferralItem(referral));
                }
            }
            else
            {
                WorkReferralSection.IsVisible = false;
            }
        }

        private void PopulateVendorReferrals()
        {
            VendorReferralLayout.Children.Clear();

            if (_cachedVendorReferrals != null && _cachedVendorReferrals.Count > 0)
            {
                VendorReferralSection.IsVisible = true;
                foreach (var referral in _cachedVendorReferrals)
                {
                    VendorReferralLayout.Children.Add(CreateVendorReferralItem(referral));
                }
            }
            else
            {
                VendorReferralSection.IsVisible = false;
            }
        }

        private void PopulateColleagueReferrals()
        {
            ColleagueReferralLayout.Children.Clear();

            if (_cachedColleagueReferrals != null && _cachedColleagueReferrals.Count > 0)
            {
                ColleagueReferralSection.IsVisible = true;
                foreach (var referral in _cachedColleagueReferrals)
                {
                    ColleagueReferralLayout.Children.Add(CreateColleagueReferralItem(referral));
                }
            }
            else
            {
                ColleagueReferralSection.IsVisible = false;
            }
        }

        private Grid CreateWorkReferralItem(MobileWorkReferral referral)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Padding = new Thickness(0, 6),
                ColumnSpacing = 8
            };

            var infoStack = new VerticalStackLayout { Spacing = 2 };

            infoStack.Children.Add(new Label
            {
                Text = referral.ReferrerName ?? "Anonymous",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FFFFFF")
            });

            if (!string.IsNullOrWhiteSpace(referral.ProjectTitle))
            {
                infoStack.Children.Add(new Label
                {
                    Text = referral.ProjectTitle,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#8BAFC9"),
                    FontAttributes = FontAttributes.Italic
                });
            }

            if (!string.IsNullOrWhiteSpace(referral.Comment))
            {
                infoStack.Children.Add(new Label
                {
                    Text = referral.Comment,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#8BAFC9"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    MaxLines = 2
                });
            }

            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            var rightColumn = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };

            rightColumn.Children.Add(new Label
            {
                Text = referral.DisplayRating,
                FontSize = 11,
                TextColor = Color.FromArgb("#FBBF24"),
                HorizontalOptions = LayoutOptions.End
            });

            rightColumn.Children.Add(new Label
            {
                Text = referral.SubmittedAt.ToString("MMM dd, yyyy"),
                FontSize = 10,
                TextColor = Color.FromArgb("#4A7A9B"),
                HorizontalOptions = LayoutOptions.End
            });

            Grid.SetColumn(rightColumn, 1);
            grid.Children.Add(rightColumn);

            return grid;
        }

        private Grid CreateVendorReferralItem(MobileVendorReferral referral)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Padding = new Thickness(0, 6),
                ColumnSpacing = 8
            };

            var infoStack = new VerticalStackLayout { Spacing = 2 };

            infoStack.Children.Add(new Label
            {
                Text = referral.VendorName ?? "Anonymous Vendor",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FFFFFF")
            });

            if (!string.IsNullOrWhiteSpace(referral.Category))
            {
                infoStack.Children.Add(new Label
                {
                    Text = referral.Category,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#8BAFC9")
                });
            }

            if (!string.IsNullOrWhiteSpace(referral.Comment))
            {
                infoStack.Children.Add(new Label
                {
                    Text = referral.Comment,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#8BAFC9"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    MaxLines = 2
                });
            }

            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            var rightColumn = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };

            rightColumn.Children.Add(new Label
            {
                Text = referral.DisplayRating,
                FontSize = 11,
                TextColor = Color.FromArgb("#FBBF24"),
                HorizontalOptions = LayoutOptions.End
            });

            rightColumn.Children.Add(new Label
            {
                Text = referral.SubmittedAt.ToString("MMM dd, yyyy"),
                FontSize = 10,
                TextColor = Color.FromArgb("#4A7A9B"),
                HorizontalOptions = LayoutOptions.End
            });

            Grid.SetColumn(rightColumn, 1);
            grid.Children.Add(rightColumn);

            return grid;
        }

        private Grid CreateColleagueReferralItem(MobileColleagueReferral referral)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                Padding = new Thickness(0, 6),
                ColumnSpacing = 8
            };

            var infoStack = new VerticalStackLayout { Spacing = 2 };

            infoStack.Children.Add(new Label
            {
                Text = referral.ColleagueName ?? "Anonymous Colleague",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FFFFFF")
            });

            if (!string.IsNullOrWhiteSpace(referral.Relationship))
            {
                infoStack.Children.Add(new Label
                {
                    Text = referral.Relationship,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#8BAFC9")
                });
            }

            if (!string.IsNullOrWhiteSpace(referral.Comment))
            {
                infoStack.Children.Add(new Label
                {
                    Text = referral.Comment,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#8BAFC9"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    MaxLines = 2
                });
            }

            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            var rightColumn = new VerticalStackLayout { Spacing = 4, HorizontalOptions = LayoutOptions.End };

            rightColumn.Children.Add(new Label
            {
                Text = referral.DisplayRating,
                FontSize = 11,
                TextColor = Color.FromArgb("#FBBF24"),
                HorizontalOptions = LayoutOptions.End
            });

            rightColumn.Children.Add(new Label
            {
                Text = referral.SubmittedAt.ToString("MMM dd, yyyy"),
                FontSize = 10,
                TextColor = Color.FromArgb("#4A7A9B"),
                HorizontalOptions = LayoutOptions.End
            });

            Grid.SetColumn(rightColumn, 1);
            grid.Children.Add(rightColumn);

            return grid;
        }

        private void UpdateReferralSummaryCounts(int workCount, int vendorCount, int colleagueCount)
        {
            if (WorkReferralCountLabel != null)
                WorkReferralCountLabel.Text = workCount.ToString();

            if (VendorReferralCountLabel != null)
                VendorReferralCountLabel.Text = vendorCount.ToString();

            if (ColleagueReferralCountLabel != null)
                ColleagueReferralCountLabel.Text = colleagueCount.ToString();

            if (ReferralSummaryStrip != null)
                ReferralSummaryStrip.IsVisible = (workCount + vendorCount + colleagueCount) > 0;
        }

        #endregion

        #region Event Handlers

        private async void OnBackClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");

        private async void OnEditClicked(object sender, EventArgs e)
            => await NavigateEdit();

        private async void OnEditTapped(object sender, EventArgs e)
            => await NavigateEdit();

        private async void OnRetryClicked(object sender, TappedEventArgs e)
            => await LoadProfileSafeAsync();

        private async void OnTrustHistoryToggled(object sender, EventArgs e)
        {
            _historyExpanded = !_historyExpanded;
            TrustHistoryPanel.IsVisible = _historyExpanded;

            if (HistoryChevron != null)
            {
                HistoryChevron.Source = _historyExpanded ? "chevron_up.svg" : "chevron_down.svg";
            }

            if (_historyExpanded && _cachedHistory == null)
            {
                await LoadTrustHistoryAsync();
            }
        }

        private async void OnReferralDetailsToggled(object sender, EventArgs e)
        {
            _referralExpanded = !_referralExpanded;
            ReferralDetailsPanel.IsVisible = _referralExpanded;

            if (ReferralChevron != null)
            {
                ReferralChevron.Source = _referralExpanded ? "chevron_up.svg" : "chevron_down.svg";
            }

            if (_referralExpanded && _cachedWorkReferrals == null)
            {
                await LoadReferralDetailsAsync();
            }
        }

        #endregion

        #region Helper Methods

        private async Task NavigateEdit()
        {
            var role = _userService.GetCurrentUser()?.Role ?? "Customer";
            await Shell.Current.GoToAsync(nameof(EditProfilePage),
                new Dictionary<string, object> { { "Role", role } });
        }

        private void SetLoading(bool state)
        {
            if (PageLoadingIndicator != null)
            {
                PageLoadingIndicator.IsRunning = state;
                PageLoadingIndicator.IsVisible = state;
            }
        }

        private void ShowError(string msg)
        {
            if (ErrorLabel != null)
                ErrorLabel.Text = msg;
            if (ErrorState != null)
                ErrorState.IsVisible = true;
        }

        private void ShowTrustHistoryLoading(bool show)
        {
            if (TrustScoreLoading != null)
            {
                TrustScoreLoading.IsVisible = show;
                TrustScoreLoading.IsRunning = show;
            }

            if (TrustScoreErrorLabel != null)
                TrustScoreErrorLabel.IsVisible = false;
        }

        private void ShowTrustHistoryError(string message)
        {
            if (TrustScoreErrorLabel != null)
            {
                TrustScoreErrorLabel.Text = message;
                TrustScoreErrorLabel.IsVisible = true;
            }
        }

        private void TryLoadPhoto(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                ShowInitials();
                return;
            }

            try
            {
                var uri = path.StartsWith("http")
                    ? new Uri(path)
                    : new Uri($"{_apiConfig.BaseUrl}/{path.TrimStart('/')}");

                if (ProfilePhoto != null)
                    ProfilePhoto.Source = ImageSource.FromUri(uri);
                if (ProfilePhotoFrame != null)
                    ProfilePhotoFrame.IsVisible = true;
                if (ProfileInitialsFrame != null)
                    ProfileInitialsFrame.IsVisible = false;
            }
            catch
            {
                ShowInitials();
            }
        }

        private void ShowInitials()
        {
            if (ProfilePhotoFrame != null)
                ProfilePhotoFrame.IsVisible = false;
            if (ProfileInitialsFrame != null)
                ProfileInitialsFrame.IsVisible = true;
        }

        #endregion
    }
}