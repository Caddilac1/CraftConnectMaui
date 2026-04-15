using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class MyProfilePage : ContentPage
    {
        private readonly IUserService _userService;
        private readonly ApiConfig _apiConfig;
        private IProfileApiService _profileApiService;

        // Track accordion state
        private bool _historyExpanded = false;
        private bool _referralExpanded = false;

        // Cached company data from token
        private int? _userCompanyId;
        private string? _userCompanyName;
        private decimal? _userCompanyTrustScore;
        private string? _userCompanyTrustBand;

        public MyProfilePage(IUserService userService, ApiConfig apiConfig)
        {
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[MY PROFILE] Constructor: InitializeComponent completed");
            _userService = userService;
            _apiConfig = apiConfig;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            System.Diagnostics.Debug.WriteLine("[MY PROFILE] OnAppearing start");
            await LoadProfileAsync();
            System.Diagnostics.Debug.WriteLine("[MY PROFILE] OnAppearing end");
        }

        // ── Load Profile ─────────────────────────────────────────────────────

        private async Task LoadProfileAsync()
        {
            ShowLoading(true);
            ResetAllSections();

            try
            {
                // First, extract company details from JWT token
                await LoadCompanyDetailsFromTokenAsync();

                System.Diagnostics.Debug.WriteLine($"[MY PROFILE] Company from token: ID={_userCompanyId}, Name={_userCompanyName}, TrustScore={_userCompanyTrustScore}, Band={_userCompanyTrustBand}");

                // Then load user profile
                System.Diagnostics.Debug.WriteLine("[MY PROFILE] LoadProfileAsync: calling LoadUserProfileAsync");
                var profile = await _userService.LoadUserProfileAsync();

                System.Diagnostics.Debug.WriteLine(profile == null
                    ? "[MY PROFILE] LoadProfileAsync: profile == null"
                    : $"[MY PROFILE] LoadProfileAsync: profile loaded Role={profile.Role} Email={profile.Email} DisplayName={profile.DisplayName}");

                if (profile == null)
                {
                    ShowError("Could not load your profile. Please try again.");
                    return;
                }

                PopulateHeader(profile);
                PopulateBioCard(profile);
                PopulatePersonalInfoCard(profile);
                PersonalInfoCard.IsVisible = true;

                // Check if user has company association (from token)
                if (_userCompanyId.HasValue && _userCompanyId.Value > 0)
                {
                    // Show Trust Score card with data from token
                    TrustScoreCard.IsVisible = true;

                    // If trust score is in token, display it immediately
                    if (_userCompanyTrustScore.HasValue)
                    {
                        DisplayTrustScoreFromToken();
                    }

                    // Load full trust score details from API (history, referrals, etc.)
                    _ = LoadFullTrustScoreDetailsAsync(_userCompanyId.Value);
                    System.Diagnostics.Debug.WriteLine($"[MY PROFILE] Loading full trust score details for CompanyId={_userCompanyId}");
                }
                else
                {
                    TrustScoreCard.IsVisible = false;
                    System.Diagnostics.Debug.WriteLine("[MY PROFILE] No company association found, hiding trust score card");
                }

                // Show artisan-specific sections if user is artisan
                if (profile is ArtisanUser artisan)
                {
                    System.Diagnostics.Debug.WriteLine($"[MY PROFILE] Profile is ArtisanUser - CompanyId={artisan.CompanyId}");
                    PopulateArtisanStatsStrip(artisan);
                    PopulateArtisanBusinessCard(artisan);
                    PopulateCredentialsCard(artisan);
                    ArtisanBusinessCard.IsVisible = true;
                    CredentialsCard.IsVisible = true;
                }

                EditProfileButton.IsVisible = true;
                ErrorState.IsVisible = false;
            }
            catch (UnauthorizedAccessException)
            {
                await DisplayAlert("Session expired", "Your session has expired. Please log in again.", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MY PROFILE] Load error: {ex}");
                ShowError("Failed to load profile. Please check your connection.");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        // ── Load Company Details from JWT Token ──────────────────────────────

        /// <summary>
        /// Extracts company information directly from the JWT token
        /// </summary>
        private async Task LoadCompanyDetailsFromTokenAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrWhiteSpace(token))
                {
                    System.Diagnostics.Debug.WriteLine("[JWT] No token found");
                    return;
                }

                var parts = token.Split('.');
                if (parts.Length != 3)
                {
                    System.Diagnostics.Debug.WriteLine("[JWT] Invalid token format");
                    return;
                }

                var payload = parts[1];
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                System.Diagnostics.Debug.WriteLine($"[JWT] Payload: {json}");

                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Extract CompanyId
                foreach (var claimName in new[] { "CompanyId", "company_id", "companyId" })
                {
                    if (root.TryGetProperty(claimName, out var el))
                    {
                        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var id))
                            _userCompanyId = id;
                        else if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var sid))
                            _userCompanyId = sid;

                        if (_userCompanyId.HasValue) break;
                    }
                }

                // Extract CompanyName
                foreach (var claimName in new[] { "CompanyName", "company_name" })
                {
                    if (root.TryGetProperty(claimName, out var el) && el.ValueKind == JsonValueKind.String)
                    {
                        _userCompanyName = el.GetString();
                        break;
                    }
                }

                // Extract Trust Score - Fixed: Use TryGetProperty and parse the value
                if (root.TryGetProperty("CompanyTrustScore", out var scoreEl))
                {
                    if (scoreEl.ValueKind == JsonValueKind.Number)
                    {
                        // For number type, get the raw text and parse
                        var scoreStr = scoreEl.GetRawText();
                        if (decimal.TryParse(scoreStr, out var scoreValue))
                            _userCompanyTrustScore = scoreValue;
                    }
                    else if (scoreEl.ValueKind == JsonValueKind.String)
                    {
                        var scoreStr = scoreEl.GetString();
                        if (!string.IsNullOrEmpty(scoreStr) && decimal.TryParse(scoreStr, out var scoreValue))
                            _userCompanyTrustScore = scoreValue;
                    }
                }

                // Extract Trust Band
                if (root.TryGetProperty("CompanyTrustBand", out var bandEl) && bandEl.ValueKind == JsonValueKind.String)
                {
                    _userCompanyTrustBand = bandEl.GetString();
                }

                System.Diagnostics.Debug.WriteLine($"[JWT] Company details - ID:{_userCompanyId}, Name:{_userCompanyName}, Score:{_userCompanyTrustScore}, Band:{_userCompanyTrustBand}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JWT] Error decoding token: {ex.Message}");
            }
        }

        /// <summary>
        /// Displays trust score using data from JWT token (fast, no API call)
        /// </summary>
        private void DisplayTrustScoreFromToken()
        {
            if (!_userCompanyTrustScore.HasValue)
            {
                TrustScoreValueLabel.Text = "—";
                TrustBandLabel.Text = "NOT RATED";
                return;
            }

            // Display score (already on 0-1000 scale from backend)
            TrustScoreValueLabel.Text = _userCompanyTrustScore.Value.ToString("F0");
            TrustBandLabel.Text = (_userCompanyTrustBand ?? "Not Rated").ToUpper();
            TrustCalcLabel.Text = "From your company profile";

            // Band colour scheme
            (Color pillBg, Color pillText) = (_userCompanyTrustBand?.ToLower()) switch
            {
                "elite" => (Color.FromArgb("#F59E0B"), Color.FromArgb("#1B2D3E")),
                "strong" => (Color.FromArgb("#94A3B8"), Color.FromArgb("#1B2D3E")),
                "moderate" => (Color.FromArgb("#B45309"), Color.FromArgb("#FFFFFF")),
                "risky" => (Color.FromArgb("#DC2626"), Color.FromArgb("#FFFFFF")),
                "high risk" => (Color.FromArgb("#991B1B"), Color.FromArgb("#FFFFFF")),
                _ => (Color.FromArgb("#4A7A9B"), Color.FromArgb("#FFFFFF"))
            };

            TrustBandPill.BackgroundColor = pillBg;
            TrustBandLabel.TextColor = pillText;

            // Score value colour based on 0-1000 scale
            TrustScoreValueLabel.TextColor = _userCompanyTrustScore.Value >= 700
                ? Color.FromArgb("#4ADE80")
                : _userCompanyTrustScore.Value >= 550
                    ? Color.FromArgb("#F59E0B")
                    : Color.FromArgb("#F87171");
        }

        // ── Load Full Trust Score Details from API ───────────────────────────

        /// <summary>
        /// Loads full trust score details (breakdown, history, referrals) from API
        /// </summary>
        private async Task LoadFullTrustScoreDetailsAsync(int companyId)
        {
            System.Diagnostics.Debug.WriteLine($"[TRUST SCORE] Loading full details for companyId={companyId}");

            TrustScoreLoading.IsVisible = true;
            TrustScoreLoading.IsRunning = true;

            try
            {
                _profileApiService ??= new ProfileApiService(_apiConfig);

                // Get full snapshot from API
                var snapshot = await _profileApiService.GetTrustScoreSnapshotAsync(companyId);

                System.Diagnostics.Debug.WriteLine(snapshot == null
                    ? "[TRUST SCORE] snapshot == null"
                    : $"[TRUST SCORE] snapshot received - TotalReferrals={snapshot.TotalReferrals}");

                if (snapshot?.CurrentScore != null)
                {
                    // Update hero section with full data (overrides token display)
                    PopulateTrustScoreHero(snapshot.CurrentScore);
                }

                if (snapshot != null)
                {
                    PopulateReferralSummary(snapshot);
                    TrustScoreErrorLabel.IsVisible = false;
                }

                // Preload history for accordion
                _ = PreloadTrustHistoryAsync(companyId);
            }
            catch (UnauthorizedAccessException)
            {
                TrustScoreErrorLabel.Text = "You don't have access to this trust score.";
                TrustScoreErrorLabel.IsVisible = true;
                System.Diagnostics.Debug.WriteLine("[TRUST SCORE] UnauthorizedAccessException");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRUST SCORE] exception: {ex}");
                TrustScoreErrorLabel.Text = "Could not load full trust score details. Tap to retry.";
                TrustScoreErrorLabel.IsVisible = true;
            }
            finally
            {
                TrustScoreLoading.IsRunning = false;
                TrustScoreLoading.IsVisible = false;
            }
        }

        // ── Trust Score Hero Population ─────────────────────────────────────

        private void PopulateTrustScoreHero(MobileTrustScore score)
        {
            System.Diagnostics.Debug.WriteLine(score == null
                ? "[TRUST SCORE] PopulateTrustScoreHero called with null"
                : $"[TRUST SCORE] PopulateTrustScoreHero score={score.Score} band={score.Band}");

            TrustScoreValueLabel.Text = score.DisplayScore;
            TrustBandLabel.Text = score.DisplayBand.ToUpper();
            TrustCalcLabel.Text = $"Calculated {score.CalculatedAt:dd MMM yyyy}";

            // Band colour scheme
            (Color pillBg, Color pillText, Color ringAccent) = score.Band?.ToLower() switch
            {
                "elite" => (Color.FromArgb("#F59E0B"), Color.FromArgb("#1B2D3E"), Color.FromArgb("#F59E0B")),
                "strong" => (Color.FromArgb("#94A3B8"), Color.FromArgb("#1B2D3E"), Color.FromArgb("#94A3B8")),
                "moderate" => (Color.FromArgb("#B45309"), Color.FromArgb("#FFFFFF"), Color.FromArgb("#B45309")),
                "risky" => (Color.FromArgb("#DC2626"), Color.FromArgb("#FFFFFF"), Color.FromArgb("#DC2626")),
                "high risk" => (Color.FromArgb("#991B1B"), Color.FromArgb("#FFFFFF"), Color.FromArgb("#991B1B")),
                _ => (Color.FromArgb("#4A7A9B"), Color.FromArgb("#FFFFFF"), Color.FromArgb("#4A7A9B"))
            };

            TrustBandPill.BackgroundColor = pillBg;
            TrustBandLabel.TextColor = pillText;

            // Score value colour based on 0-1000 scale
            TrustScoreValueLabel.TextColor = score.Score >= 700
                ? Color.FromArgb("#4ADE80")
                : score.Score >= 550
                    ? Color.FromArgb("#F59E0B")
                    : Color.FromArgb("#F87171");

            // Breakdown bars (score is 0-1000, convert to percentage)
            if (score.Breakdown?.Count > 0)
            {
                TrustBreakdownLayout.Children.Clear();
                foreach (var kv in score.Breakdown)
                {
                    // Convert from 0-1000 to percentage for display
                    var pct = Math.Clamp((double)kv.Value / 10, 0, 100);
                    TrustBreakdownLayout.Children.Add(BuildBreakdownRow(kv.Key, pct));
                }
                TrustBreakdownSection.IsVisible = true;
            }
        }

        private View BuildBreakdownRow(string label, double pct)
        {
            var barColor = pct >= 70 ? "#4ADE80" : pct >= 40 ? "#F59E0B" : "#F87171";

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(120) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = new GridLength(40) }
                },
                ColumnSpacing = 10
            };

            grid.Add(new Label
            {
                Text = label,
                FontSize = 11,
                TextColor = Color.FromArgb("#8BAFC9"),
                VerticalOptions = LayoutOptions.Center
            }, 0);

            var track = new Grid { HeightRequest = 5, VerticalOptions = LayoutOptions.Center };
            track.Add(new Frame
            {
                BackgroundColor = Color.FromArgb("#243B52"),
                CornerRadius = 3,
                HasShadow = false,
                Padding = 0,
                BorderColor = Colors.Transparent
            });
            track.Add(new Frame
            {
                BackgroundColor = Color.FromArgb(barColor),
                CornerRadius = 3,
                HasShadow = false,
                Padding = 0,
                BorderColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Start,
                WidthRequest = 0
            });
            grid.Add(track, 1);

            grid.Add(new Label
            {
                Text = $"{pct:F0}%",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(barColor),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            }, 2);

            track.SizeChanged += (s, e) =>
            {
                if (track.Width <= 0) return;
                var fill = track.Children[1] as Frame;
                if (fill != null)
                    fill.WidthRequest = track.Width * (pct / 100.0);
            };

            return grid;
        }

        // ── Referral Summary ───────────────────────────────────────────────

        private void PopulateReferralSummary(MobileTrustScoreSnapshot snapshot)
        {
            WorkReferralCountLabel.Text = (snapshot.WorkReferrals?.Count ?? 0).ToString();
            VendorReferralCountLabel.Text = (snapshot.VendorReferrals?.Count ?? 0).ToString();
            ColleagueReferralCountLabel.Text = (snapshot.ColleagueReferrals?.Count ?? 0).ToString();
            ReferralSummaryStrip.IsVisible = true;

            PopulateWorkReferralCards(snapshot.WorkReferrals);
            PopulateVendorReferralCards(snapshot.VendorReferrals);
            PopulateColleagueReferralCards(snapshot.ColleagueReferrals);

            bool anyReferrals = snapshot.TotalReferrals > 0;
            NoReferralsLabel.IsVisible = !anyReferrals;
        }

        // ── History Preload ────────────────────────────────────────────────

        private IReadOnlyList<MobileTrustScoreHistoryItem> _cachedHistory;

        private async Task PreloadTrustHistoryAsync(int companyId)
        {
            try
            {
                _profileApiService ??= new ProfileApiService(_apiConfig);
                _cachedHistory = await _profileApiService.GetTrustScoreHistoryAsync(companyId);
                System.Diagnostics.Debug.WriteLine(_cachedHistory == null
                    ? "[TRUST HISTORY] preload: history == null"
                    : $"[TRUST HISTORY] preload: items={_cachedHistory.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRUST HISTORY] preload exception: {ex}");
            }
        }

        // ── Accordion Toggles ──────────────────────────────────────────────

        private async void OnTrustHistoryToggled(object sender, TappedEventArgs e)
        {
            _historyExpanded = !_historyExpanded;
            TrustHistoryPanel.IsVisible = _historyExpanded;
            await HistoryChevron.RotateTo(_historyExpanded ? 180 : 0, 200, Easing.CubicOut);

            if (_historyExpanded)
            {
                PopulateHistoryPanel();
            }
        }

        private async void OnReferralDetailsToggled(object sender, TappedEventArgs e)
        {
            _referralExpanded = !_referralExpanded;
            ReferralDetailsPanel.IsVisible = _referralExpanded;
            await ReferralChevron.RotateTo(_referralExpanded ? 180 : 0, 200, Easing.CubicOut);
        }

        // ── History Panel ─────────────────────────────────────────────────

        private void PopulateHistoryPanel()
        {
            TrustHistoryLayout.Children.Clear();

            if (_cachedHistory == null || _cachedHistory.Count == 0)
            {
                NoHistoryLabel.IsVisible = true;
                return;
            }

            NoHistoryLabel.IsVisible = false;

            for (int i = 0; i < _cachedHistory.Count; i++)
            {
                var item = _cachedHistory[i];
                bool isLast = i == _cachedHistory.Count - 1;
                TrustHistoryLayout.Children.Add(BuildHistoryRow(item, isLast));
            }
        }

        private View BuildHistoryRow(MobileTrustScoreHistoryItem item, bool isLast)
        {
            // Score is 0-1000 from backend, convert to 0-100 for display
            var displayScore = item.Score / 10;
            var scoreColor = displayScore >= 70 ? "#4ADE80" : displayScore >= 55 ? "#F59E0B" : "#F87171";
            var bandColor = GetBandAccentHex(item.Band);

            var outerGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(28) },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 12,
                Margin = new Thickness(0, 0, 0, 0)
            };

            var timelineStack = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.Center };

            timelineStack.Add(new Frame
            {
                WidthRequest = 10,
                HeightRequest = 10,
                CornerRadius = 5,
                Padding = 0,
                HasShadow = false,
                BackgroundColor = Color.FromArgb(bandColor),
                BorderColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });

            if (!isLast)
            {
                timelineStack.Add(new BoxView
                {
                    WidthRequest = 1,
                    HeightRequest = 40,
                    Color = Color.FromArgb("#243B52"),
                    HorizontalOptions = LayoutOptions.Center
                });
            }

            outerGrid.Add(timelineStack, 0);

            var contentStack = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(0, 0, 0, isLast ? 6 : 14) };

            var dateRow = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } } };
            dateRow.Add(new Label
            {
                Text = item.RecordedAt.ToString("dd MMM yyyy"),
                FontSize = 11,
                TextColor = Color.FromArgb("#4A7A9B"),
                VerticalOptions = LayoutOptions.Center
            }, 0);

            if (!string.IsNullOrWhiteSpace(item.Band))
            {
                dateRow.Add(new Frame
                {
                    BackgroundColor = Color.FromArgb(bandColor + "33"),
                    CornerRadius = 8,
                    Padding = new Thickness(7, 2),
                    HasShadow = false,
                    BorderColor = Colors.Transparent,
                    Content = new Label
                    {
                        Text = item.Band.ToUpper(),
                        FontSize = 9,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb(bandColor)
                    }
                }, 1);
            }
            contentStack.Add(dateRow);

            var scoreRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 6
            };

            scoreRow.Add(new Label
            {
                Text = $"{displayScore:F0}",
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(scoreColor),
                VerticalOptions = LayoutOptions.Center
            }, 0);

            scoreRow.Add(new Label
            {
                Text = "/ 100",
                FontSize = 11,
                TextColor = Color.FromArgb("#4A7A9B"),
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, 3)
            }, 1);

            contentStack.Add(scoreRow);

            if (!string.IsNullOrWhiteSpace(item.ChangeReason))
            {
                contentStack.Add(new Label
                {
                    Text = item.ChangeReason,
                    FontSize = 11,
                    TextColor = Color.FromArgb("#8BAFC9"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    LineHeight = 1.4
                });
            }

            outerGrid.Add(contentStack, 1);
            return outerGrid;
        }

        // ── Referral Card Builders ─────────────────────────────────────────

        private void PopulateWorkReferralCards(IReadOnlyList<MobileWorkReferral> referrals)
        {
            WorkReferralLayout.Children.Clear();
            if (referrals == null || referrals.Count == 0) return;
            foreach (var r in referrals)
                WorkReferralLayout.Children.Add(BuildReferralCard(r.ReferrerName, r.ProjectTitle, r.DisplayRating, r.Comment, r.SubmittedAt, "#7DB8E0"));
            WorkReferralSection.IsVisible = true;
        }

        private void PopulateVendorReferralCards(IReadOnlyList<MobileVendorReferral> referrals)
        {
            VendorReferralLayout.Children.Clear();
            if (referrals == null || referrals.Count == 0) return;
            foreach (var r in referrals)
                VendorReferralLayout.Children.Add(BuildReferralCard(r.VendorName, r.Category, r.DisplayRating, r.Comment, r.SubmittedAt, "#A78BFA"));
            VendorReferralSection.IsVisible = true;
        }

        private void PopulateColleagueReferralCards(IReadOnlyList<MobileColleagueReferral> referrals)
        {
            ColleagueReferralLayout.Children.Clear();
            if (referrals == null || referrals.Count == 0) return;
            foreach (var r in referrals)
                ColleagueReferralLayout.Children.Add(BuildReferralCard(r.ColleagueName, r.Relationship, r.DisplayRating, r.Comment, r.SubmittedAt, "#34D399"));
            ColleagueReferralSection.IsVisible = true;
        }

        private View BuildReferralCard(string name, string subtitle, string rating,
                                       string comment, DateTime submittedAt, string accentHex)
        {
            var card = new Frame
            {
                BackgroundColor = Color.FromArgb("#243B52"),
                CornerRadius = 12,
                Padding = new Thickness(14, 12),
                HasShadow = false,
                BorderColor = Color.FromArgb("#2E4D6A"),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new VerticalStackLayout { Spacing = 6 };

            var header = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var nameStack = new VerticalStackLayout { Spacing = 1 };
            nameStack.Add(new Label
            {
                Text = name ?? "—",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FFFFFF"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                nameStack.Add(new Label
                {
                    Text = subtitle,
                    FontSize = 11,
                    TextColor = Color.FromArgb(accentHex),
                    LineBreakMode = LineBreakMode.TailTruncation
                });
            }
            header.Add(nameStack, 0);

            header.Add(new Label
            {
                Text = rating,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#F59E0B"),
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End
            }, 1);

            stack.Add(header);

            if (!string.IsNullOrWhiteSpace(comment))
            {
                stack.Add(new Label
                {
                    Text = $"\"{comment}\"",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#8BAFC9"),
                    LineBreakMode = LineBreakMode.WordWrap,
                    LineHeight = 1.5
                });
            }

            stack.Add(new Label
            {
                Text = submittedAt.ToString("dd MMM yyyy"),
                FontSize = 10,
                TextColor = Color.FromArgb("#4A7A9B"),
                HorizontalOptions = LayoutOptions.End
            });

            card.Content = stack;
            return card;
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static string GetBandAccentHex(string band) => band?.ToLower() switch
        {
            "elite" => "#F59E0B",
            "strong" => "#94A3B8",
            "moderate" => "#B45309",
            "risky" => "#DC2626",
            "high risk" => "#991B1B",
            _ => "#4A7A9B"
        };

        // ── Header Population ───────────────────────────────────────────────

        private void PopulateHeader(UserProfile profile)
        {
            var role = (profile.Role ?? "customer").Trim().ToLower();

            var displayName = profile.DisplayName;
            if (IsLikelyGuid(displayName))
                displayName = profile.Email ?? "User";

            ProfileNameLabel.Text = displayName;
            ProfileInitialsLabel.Text = GetInitials(displayName);
            ProfileEmailLabel.Text = profile.Email ?? "";

            RoleBadgeLabel.Text = role switch
            {
                "artisan" => "◆  Artisan",
                "admin" => "◆  Admin",
                "staff" => "◆  Staff",
                _ => "◆  Customer"
            };

            (RoleBadgeFrame.BackgroundColor, RoleBadgeFrame.BorderColor, RoleBadgeLabel.TextColor) = role switch
            {
                "artisan" => (Color.FromArgb("#243B52"), Color.FromArgb("#3A5A78"), Color.FromArgb("#7DB8E0")),
                "admin" => (Color.FromArgb("#2D2050"), Color.FromArgb("#4C3499"), Color.FromArgb("#C4B5FD")),
                "staff" => (Color.FromArgb("#0F2E25"), Color.FromArgb("#1D5C45"), Color.FromArgb("#4ADE80")),
                _ => (Color.FromArgb("#243B52"), Color.FromArgb("#3A5A78"), Color.FromArgb("#7DB8E0"))
            };

            if (!string.IsNullOrWhiteSpace(profile.ProfilePicture))
                TryLoadPhoto(profile.ProfilePicture);
            else
                ShowInitials();

            if (profile is ArtisanUser artisan && artisan.IsVerified)
                VerifiedBadge.IsVisible = true;
        }

        private void PopulateArtisanStatsStrip(ArtisanUser au)
        {
            StatRatingLabel.Text = au.AverageRating > 0 ? $"{au.AverageRating:F1} ★" : "—";
            StatProjectsLabel.Text = au.CompletedProjects > 0 ? au.CompletedProjects.ToString() : "—";
            StatReviewsLabel.Text = au.TotalReviews > 0 ? au.TotalReviews.ToString() : "—";
            StatExperienceLabel.Text = au.YearsOfExperience > 0 ? $"{au.YearsOfExperience} yrs" : "—";
        }

        private void PopulateBioCard(UserProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.Bio))
            {
                BioLabel.Text = profile.Bio;
                BioCard.IsVisible = true;
            }
        }

        private void PopulatePersonalInfoCard(UserProfile profile)
        {
            bool hasAny = false;

            if (!string.IsNullOrWhiteSpace(profile.Bio))
            {
                PersonalBioLabel.Text = profile.Bio;
                PersonalBioSection.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(profile.Email))
            {
                PersonalEmailLabel.Text = profile.Email;
                PersonalEmailRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(profile.PhoneNumber))
            {
                PersonalPhoneLabel.Text = profile.PhoneNumber;
                PersonalPhoneRow.IsVisible = true;
                hasAny = true;
            }

            if (profile.DateOfBirth.HasValue)
            {
                DateOfBirthLabel.Text = profile.DateOfBirth.Value.ToString("dd MMM yyyy");
                DateOfBirthRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(profile.EmergencyContact))
            {
                EmergencyContactLabel.Text = profile.EmergencyContact;
                EmergencyContactRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(profile.StaffTypeName))
            {
                StaffTypeLabel.Text = profile.StaffTypeName;
                StaffTypeRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(profile.Address))
            {
                var addr = profile.Address;
                if (!string.IsNullOrWhiteSpace(profile.AddressLine2))
                    addr += $"\n{profile.AddressLine2}";
                AddressLabel.Text = addr;
                AddressRow.IsVisible = true;
                hasAny = true;
            }

            var cityStr = profile.LocationDisplay;
            if (!string.IsNullOrWhiteSpace(cityStr))
            {
                CityCountryLabel.Text = cityStr;
                CityCountryRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(profile.PostalCode))
            {
                PostalCodeLabel.Text = profile.PostalCode;
                PostalCodeRow.IsVisible = true;
                hasAny = true;
            }

            bool hasLanguage = !string.IsNullOrWhiteSpace(profile.PreferredLanguage);
            bool hasTimezone = !string.IsNullOrWhiteSpace(profile.Timezone);
            if (hasLanguage || hasTimezone)
            {
                LanguageLabel.Text = hasLanguage ? profile.PreferredLanguage : "—";
                TimezoneLabel.Text = hasTimezone ? profile.Timezone : "—";
                LanguageTimezoneRow.IsVisible = true;
                hasAny = true;
            }

            if (profile.DateJoined.HasValue)
            {
                DateJoinedLabel.Text = profile.DateJoined.Value.ToString("dd MMM yyyy");
                DateJoinedRow.IsVisible = true;
                hasAny = true;
            }

            if (!hasAny)
                NoPersonalInfoLabel.IsVisible = true;
        }

        private void PopulateArtisanBusinessCard(ArtisanUser au)
        {
            BusinessNameLabel.Text = au.BusinessName ?? "—";
            SpecializationLabel.Text = au.Specialization ?? "";

            if (!string.IsNullOrWhiteSpace(au.ArtisanSpeciality) &&
                au.ArtisanSpeciality != au.Specialization)
            {
                ArtisanSpecialityLabel.Text = au.ArtisanSpeciality;
                ArtisanSpecialityRow.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(au.ExperienceLevel))
            {
                ExperienceLevelLabel.Text = au.ExperienceLevel;
                ExperienceLevelRow.IsVisible = true;
            }

            if (au.HourlyRate.HasValue)
            {
                HourlyRateLabel.Text = $"GH₵ {au.HourlyRate.Value:F0}/hr";
                HourlyRateBox.IsVisible = true;
            }

            if (au.ServiceRadius.HasValue && au.ServiceRadius.Value > 0)
            {
                ServiceRadiusLabel.Text = $"{au.ServiceRadius:F0} km";
                ServiceRadiusBox.IsVisible = true;
            }

            var status = au.AvailabilityStatusUpper;
            AvailabilityLabel.Text = status switch
            {
                "AVAILABLE" => "● Available",
                "BUSY" => "● Busy",
                "UNAVAILABLE" => "● Unavailable",
                _ => au.AvailabilityStatus ?? "Unknown"
            };

            (AvailabilityBadge.BackgroundColor, AvailabilityLabel.TextColor) = status switch
            {
                "AVAILABLE" => (Color.FromArgb("#DCFCE7"), Color.FromArgb("#16A34A")),
                "BUSY" => (Color.FromArgb("#FEF3C7"), Color.FromArgb("#D97706")),
                _ => (Color.FromArgb("#FEF2F2"), Color.FromArgb("#DC2626"))
            };

            if (!string.IsNullOrWhiteSpace(au.BusinessAddress))
            {
                BusinessAddressLabel.Text = au.BusinessAddress;
                BusinessAddressRow.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(au.Slug))
            {
                SlugLabel.Text = au.Slug;
                SlugRow.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(au.About))
            {
                AboutLabel.Text = au.About;
                AboutSection.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(au.ProfessionalBio))
            {
                ProfBioLabel.Text = au.ProfessionalBio;
                ProfBioSection.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(au.ServicesOffered))
            {
                ServicesLayout.Children.Clear();
                foreach (var svc in au.ServicesOffered.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    ServicesLayout.Children.Add(new Frame
                    {
                        BackgroundColor = Color.FromArgb("#EBF3FB"),
                        CornerRadius = 20,
                        Padding = new Thickness(12, 5),
                        HasShadow = false,
                        Margin = new Thickness(0, 0, 6, 6),
                        BorderColor = Color.FromArgb("#B5D4F4"),
                        Content = new Label
                        {
                            Text = svc.Trim(),
                            FontSize = 12,
                            TextColor = Color.FromArgb("#185FA5")
                        }
                    });
                }
                ServicesSection.IsVisible = true;
            }

            if (au.UpdatedAt.HasValue)
            {
                UpdatedAtLabel.Text = au.UpdatedAt.Value.ToString("dd MMM yyyy, HH:mm");
                UpdatedAtRow.IsVisible = true;
            }
        }

        private void PopulateCredentialsCard(ArtisanUser au)
        {
            bool hasAny = false;

            if (!string.IsNullOrWhiteSpace(au.LicenseNumber))
            { LicenseLabel.Text = au.LicenseNumber; LicenseRow.IsVisible = true; hasAny = true; }

            if (!string.IsNullOrWhiteSpace(au.Certification))
            { CertificationLabel.Text = au.Certification; CertificationRow.IsVisible = true; hasAny = true; }

            if (!string.IsNullOrWhiteSpace(au.BusinessRegistration))
            { BusinessRegLabel.Text = au.BusinessRegistration; BusinessRegRow.IsVisible = true; hasAny = true; }

            if (!string.IsNullOrWhiteSpace(au.TaxId))
            { TaxIdLabel.Text = au.TaxId; TaxIdRow.IsVisible = true; hasAny = true; }

            if (!string.IsNullOrWhiteSpace(au.InsuranceDetails))
            { InsuranceLabel.Text = au.InsuranceDetails; InsuranceRow.IsVisible = true; hasAny = true; }

            if (au.IsVerified && au.VerifiedDate.HasValue)
            {
                VerifiedDateLabel.Text = au.VerifiedDate.Value.ToString("dd MMM yyyy");
                VerifiedDateRow.IsVisible = true;
                hasAny = true;
            }

            if (!hasAny)
                NoCredentialsLabel.IsVisible = true;
        }

        // ── Avatar Helpers ─────────────────────────────────────────────────

        private void TryLoadPhoto(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) { ShowInitials(); return; }

                Uri uri;
                if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    uri = new Uri(path);
                else
                {
                    var baseUrl = _apiConfig.BaseUrl?.TrimEnd('/');
                    var relative = path.TrimStart('/');
                    uri = new Uri($"{baseUrl}/{relative}");
                }

                ProfilePhoto.Source = ImageSource.FromUri(uri);
                ProfilePhotoFrame.IsVisible = true;
                ProfileInitialsFrame.IsVisible = false;

                Device.StartTimer(TimeSpan.FromSeconds(3), () =>
                {
                    if (ProfilePhoto.Source == null) ShowInitials();
                    return false;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MY PROFILE] Avatar error: {ex.Message}");
                ShowInitials();
            }
        }

        private void ShowInitials()
        {
            ProfilePhotoFrame.IsVisible = false;
            ProfileInitialsFrame.IsVisible = true;
        }

        // ── General Helpers ────────────────────────────────────────────────

        private static bool IsLikelyGuid(string value) =>
            !string.IsNullOrWhiteSpace(value) && Guid.TryParse(value, out _);

        private static string GetInitials(string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName)) return "?";

            var atIdx = displayName.IndexOf('@');
            if (atIdx > 0) return displayName[0].ToString().ToUpper();

            var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpper()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        // ── UI State Helpers ───────────────────────────────────────────────

        private void ShowLoading(bool loading)
        {
            PageLoadingIndicator.IsRunning = loading;
            PageLoadingIndicator.IsVisible = loading;
        }

        private void ResetAllSections()
        {
            BioCard.IsVisible = false;
            PersonalInfoCard.IsVisible = false;
            ArtisanBusinessCard.IsVisible = false;
            CredentialsCard.IsVisible = false;
            TrustScoreCard.IsVisible = false;
            EditProfileButton.IsVisible = false;
            ErrorState.IsVisible = false;

            ProfilePhotoFrame.IsVisible = false;
            ProfileInitialsFrame.IsVisible = true;
            VerifiedBadge.IsVisible = false;

            StatRatingLabel.Text = "—";
            StatProjectsLabel.Text = "—";
            StatReviewsLabel.Text = "—";
            StatExperienceLabel.Text = "—";

            TrustScoreValueLabel.Text = "—";
            TrustBandLabel.Text = "—";
            TrustCalcLabel.Text = "";
            TrustBreakdownSection.IsVisible = false;
            TrustBreakdownLayout.Children.Clear();
            ReferralSummaryStrip.IsVisible = false;
            TrustHistoryPanel.IsVisible = false;
            ReferralDetailsPanel.IsVisible = false;
            TrustScoreErrorLabel.IsVisible = false;
            TrustScoreLoading.IsVisible = false;
            NoHistoryLabel.IsVisible = false;
            NoReferralsLabel.IsVisible = false;
            WorkReferralSection.IsVisible = false;
            VendorReferralSection.IsVisible = false;
            ColleagueReferralSection.IsVisible = false;
            WorkReferralLayout.Children.Clear();
            VendorReferralLayout.Children.Clear();
            ColleagueReferralLayout.Children.Clear();
            TrustHistoryLayout.Children.Clear();
            _historyExpanded = false;
            _referralExpanded = false;
            _cachedHistory = null;
            _userCompanyId = null;
            _userCompanyName = null;
            _userCompanyTrustScore = null;
            _userCompanyTrustBand = null;

            PersonalBioSection.IsVisible = false;
            PersonalEmailRow.IsVisible = false;
            PersonalPhoneRow.IsVisible = false;
            DateOfBirthRow.IsVisible = false;
            EmergencyContactRow.IsVisible = false;
            StaffTypeRow.IsVisible = false;
            AddressRow.IsVisible = false;
            CityCountryRow.IsVisible = false;
            PostalCodeRow.IsVisible = false;
            LanguageTimezoneRow.IsVisible = false;
            DateJoinedRow.IsVisible = false;
            NoPersonalInfoLabel.IsVisible = false;

            ArtisanSpecialityRow.IsVisible = false;
            ExperienceLevelRow.IsVisible = false;
            HourlyRateBox.IsVisible = false;
            ServiceRadiusBox.IsVisible = false;
            BusinessAddressRow.IsVisible = false;
            SlugRow.IsVisible = false;
            AboutSection.IsVisible = false;
            ProfBioSection.IsVisible = false;
            ServicesSection.IsVisible = false;
            UpdatedAtRow.IsVisible = false;

            LicenseRow.IsVisible = false;
            CertificationRow.IsVisible = false;
            BusinessRegRow.IsVisible = false;
            TaxIdRow.IsVisible = false;
            InsuranceRow.IsVisible = false;
            VerifiedDateRow.IsVisible = false;
            NoCredentialsLabel.IsVisible = false;
        }

        private void ShowError(string message)
        {
            ErrorLabel.Text = message;
            ErrorState.IsVisible = true;
        }

        // ── Navigation ─────────────────────────────────────────────────────

        private async void OnBackClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");

        private async void OnEditTapped(object sender, TappedEventArgs e)
            => await NavigateToEditAsync();

        private async void OnEditClicked(object sender, EventArgs e)
            => await NavigateToEditAsync();

        private async Task NavigateToEditAsync()
        {
            try
            {
                var profile = _userService.GetCurrentUser();
                var role = profile?.Role ?? "Customer";
                await Shell.Current.GoToAsync(nameof(EditProfilePage),
                    new Dictionary<string, object> { { "Role", role } });
            }
            catch
            {
                await DisplayAlert("Info", "Edit profile is not yet available.", "OK");
            }
        }

        private async void OnRetryClicked(object sender, TappedEventArgs e)
            => await LoadProfileAsync();
    }
}