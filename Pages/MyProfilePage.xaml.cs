using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Pages
{
    /// <summary>
    /// MyProfilePage — dark navy header redesign with integrated Trust Score.
    ///
    /// Header (non-scrolling):
    ///   - Top bar: back | "My Profile" | edit
    ///   - Profile row: avatar + name / email / role badge
    ///   - Stats bar: Rating · Projects · Reviews · Exp
    ///
    /// Scrollable body (light grey #EFF3F8):
    ///   1. Trust Score card  (dark navy, artisan only)
    ///        · Big score number + band pill + ring visual
    ///        · Score breakdown bars
    ///        · Referral count summary strip
    ///        · "Score History" accordion  → timeline rows
    ///        · "Referral Details" accordion → Work / Vendor / Colleague cards
    ///   2. Bio card
    ///   3. Business card     (artisan only)
    ///   4. Personal info card
    ///   5. Credentials card  (artisan only)
    ///   6. Edit profile button
    /// </summary>
    public partial class MyProfilePage : ContentPage
    {
        private readonly IUserService _userService;
        private readonly ApiConfig _apiConfig;
        private IProfileApiService _profileApiService;

        // Track accordion state
        private bool _historyExpanded = false;
        private bool _referralExpanded = false;

        // Cached artisan company id (resolved from artisan profile id)
        private int? _artisanCompanyId;

        public MyProfilePage(IUserService userService, ApiConfig apiConfig)
        {
            InitializeComponent();
            _userService = userService;
            _apiConfig = apiConfig;
        }

        // ── Lifecycle ──────────────────────────────────────────────────

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadProfileAsync();
        }

        // ── Load ───────────────────────────────────────────────────────

        private async Task LoadProfileAsync()
        {
            ShowLoading(true);
            ResetAllSections();

            try
            {
                var profile = await _userService.LoadUserProfileAsync();

                if (profile == null)
                {
                    ShowError("Could not load your profile. Please try again.");
                    return;
                }

                PopulateHeader(profile);
                PopulateBioCard(profile);
                PopulatePersonalInfoCard(profile);
                PersonalInfoCard.IsVisible = true;

                if (profile is ArtisanUser artisan)
                {
                    PopulateArtisanStatsStrip(artisan);
                    PopulateArtisanBusinessCard(artisan);
                    PopulateCredentialsCard(artisan);
                    ArtisanBusinessCard.IsVisible = true;
                    CredentialsCard.IsVisible = true;

                    // Show trust score card and kick off async load
                    TrustScoreCard.IsVisible = true;
                    _ = LoadTrustScoreAsync(artisan);
                }

                EditProfileButton.IsVisible = true;
                ErrorState.IsVisible = false;
            }
            catch (UnauthorizedAccessException)
            {
                await DisplayAlert("Session expired",
                    "Your session has expired. Please log in again.", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MY PROFILE] Load error: {ex.Message}");
                ShowError("Failed to load profile. Please check your connection.");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        // ── Trust Score ────────────────────────────────────────────────

        private async Task LoadTrustScoreAsync(ArtisanUser artisan)
        {
            // Try artisan.CompanyId first (mapped from profile response)
            int companyId = artisan.CompanyId;

            // Fallback: decode CompanyId from the JWT claim directly
            if (companyId <= 0)
                companyId = await GetCompanyIdFromTokenAsync();

            System.Diagnostics.Debug.WriteLine($"[TRUST SCORE] Resolved companyId={companyId}");

            if (companyId <= 0)
            {
                TrustScoreErrorLabel.Text = "Trust score not available — company ID missing.";
                TrustScoreErrorLabel.IsVisible = true;
                return;
            }

            _artisanCompanyId = companyId;
            TrustScoreLoading.IsVisible = true;
            TrustScoreLoading.IsRunning = true;

            try
            {
                _profileApiService ??= new ProfileApiService(_apiConfig);
                var snapshot = await _profileApiService.GetTrustScoreSnapshotAsync(companyId);

                if (snapshot?.CurrentScore != null)
                    PopulateTrustScoreHero(snapshot.CurrentScore);

                if (snapshot != null)
                    PopulateReferralSummary(snapshot);

                TrustScoreErrorLabel.IsVisible = false;
                _ = PreloadTrustHistoryAsync(companyId);
            }
            catch (UnauthorizedAccessException)
            {
                TrustScoreErrorLabel.Text = "You don't have access to this trust score.";
                TrustScoreErrorLabel.IsVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRUST SCORE] {ex.Message}");
                TrustScoreErrorLabel.Text = "Could not load trust score. Tap to retry.";
                TrustScoreErrorLabel.IsVisible = true;
            }
            finally
            {
                TrustScoreLoading.IsRunning = false;
                TrustScoreLoading.IsVisible = false;
            }
        }

        /// <summary>
        /// Decodes the JWT from SecureStorage and reads the CompanyId claim.
        /// The backend sets either "CompanyId" or "company_id" — we check both.
        /// </summary>
        private static async Task<int> GetCompanyIdFromTokenAsync()
        {
            try
            {
                var token = await SecureStorage.GetAsync("auth_token");
                if (string.IsNullOrWhiteSpace(token)) return 0;

                // JWT = header.payload.signature — decode the payload (middle segment)
                var parts = token.Split('.');
                if (parts.Length != 3) return 0;

                // Base64url → Base64 standard padding
                var payload = parts[1];
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }

                var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Try both claim name variants the backend uses
                foreach (var claimName in new[] { "CompanyId", "company_id", "companyId" })
                {
                    if (root.TryGetProperty(claimName, out var el))
                    {
                        if (el.ValueKind == System.Text.Json.JsonValueKind.Number &&
                            el.TryGetInt32(out var id))
                            return id;

                        if (el.ValueKind == System.Text.Json.JsonValueKind.String &&
                            int.TryParse(el.GetString(), out var sid))
                            return sid;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[JWT DECODE] {ex.Message}");
            }
            return 0;
        }
        

        private void PopulateTrustScoreHero(MobileTrustScore score)
        {
            TrustScoreValueLabel.Text = score.DisplayScore;
            TrustBandLabel.Text = score.DisplayBand.ToUpper();
            TrustCalcLabel.Text = $"Calculated {score.CalculatedAt:dd MMM yyyy}";

            // Band colour scheme
            (Color pillBg, Color pillText, Color ringAccent) = score.Band?.ToLower() switch
            {
                "gold" => (Color.FromArgb("#F59E0B"), Color.FromArgb("#1B2D3E"), Color.FromArgb("#F59E0B")),
                "silver" => (Color.FromArgb("#94A3B8"), Color.FromArgb("#1B2D3E"), Color.FromArgb("#94A3B8")),
                "bronze" => (Color.FromArgb("#B45309"), Color.FromArgb("#FFFFFF"), Color.FromArgb("#B45309")),
                "platinum" => (Color.FromArgb("#7DB8E0"), Color.FromArgb("#1B2D3E"), Color.FromArgb("#7DB8E0")),
                _ => (Color.FromArgb("#4A7A9B"), Color.FromArgb("#FFFFFF"), Color.FromArgb("#4A7A9B"))
            };

            TrustBandPill.BackgroundColor = pillBg;
            TrustBandLabel.TextColor = pillText;

            // Score value colour — green for good, amber for mid, red for low
            TrustScoreValueLabel.TextColor = score.Score >= 70
                ? Color.FromArgb("#4ADE80")
                : score.Score >= 40
                    ? Color.FromArgb("#F59E0B")
                    : Color.FromArgb("#F87171");

            // Breakdown bars
            if (score.Breakdown?.Count > 0)
            {
                TrustBreakdownLayout.Children.Clear();
                foreach (var kv in score.Breakdown)
                {
                    var pct = Math.Clamp((double)kv.Value, 0, 100);
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
                    new ColumnDefinition { Width = new GridLength(100) },
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

            // Track bar
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
                WidthRequest = 0  // animated below
            });
            grid.Add(track, 1);

            grid.Add(new Label
            {
                Text = $"{pct:F0}",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(barColor),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            }, 2);

            // Animate bar width after layout
            track.SizeChanged += (s, e) =>
            {
                if (track.Width <= 0) return;
                var fill = track.Children[1] as Frame;
                if (fill != null)
                    fill.WidthRequest = track.Width * (pct / 100.0);
            };

            return grid;
        }

        private void PopulateReferralSummary(MobileTrustScoreSnapshot snapshot)
        {
            WorkReferralCountLabel.Text = (snapshot.WorkReferrals?.Count ?? 0).ToString();
            VendorReferralCountLabel.Text = (snapshot.VendorReferrals?.Count ?? 0).ToString();
            ColleagueReferralCountLabel.Text = (snapshot.ColleagueReferrals?.Count ?? 0).ToString();
            ReferralSummaryStrip.IsVisible = true;

            // Pre-populate detail panels while we have the data
            PopulateWorkReferralCards(snapshot.WorkReferrals);
            PopulateVendorReferralCards(snapshot.VendorReferrals);
            PopulateColleagueReferralCards(snapshot.ColleagueReferrals);

            bool anyReferrals = snapshot.TotalReferrals > 0;
            NoReferralsLabel.IsVisible = !anyReferrals;
        }

        // ── History pre-load ───────────────────────────────────────────

        private IReadOnlyList<MobileTrustScoreHistoryItem> _cachedHistory;

        private async Task PreloadTrustHistoryAsync(int companyId)
        {
            try
            {
                _profileApiService ??= new ProfileApiService(_apiConfig);
                _cachedHistory = await _profileApiService.GetTrustScoreHistoryAsync(companyId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TRUST HISTORY] preload: {ex.Message}");
            }
        }

        // ── Accordion toggles ──────────────────────────────────────────

        private async void OnTrustHistoryToggled(object sender, TappedEventArgs e)
        {
            _historyExpanded = !_historyExpanded;
            TrustHistoryPanel.IsVisible = _historyExpanded;

            // Animate chevron
            await HistoryChevron.RotateTo(_historyExpanded ? 180 : 0, 200, Easing.CubicOut);

            if (_historyExpanded)
                PopulateHistoryPanel();
        }

        private async void OnReferralDetailsToggled(object sender, TappedEventArgs e)
        {
            _referralExpanded = !_referralExpanded;
            ReferralDetailsPanel.IsVisible = _referralExpanded;

            await ReferralChevron.RotateTo(_referralExpanded ? 180 : 0, 200, Easing.CubicOut);
        }

        // ── History panel population ───────────────────────────────────

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
            var scoreColor = item.Score >= 70 ? "#4ADE80" : item.Score >= 40 ? "#F59E0B" : "#F87171";
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

            // Timeline column: dot + vertical line
            var timelineStack = new VerticalStackLayout { Spacing = 0, HorizontalOptions = LayoutOptions.Center };

            // Dot
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

            // Vertical connector line (hidden for last item)
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

            // Content column
            var contentStack = new VerticalStackLayout { Spacing = 3, Padding = new Thickness(0, 0, 0, isLast ? 6 : 14) };

            // Date row
            var dateRow = new Grid { ColumnDefinitions = { new ColumnDefinition { Width = GridLength.Star }, new ColumnDefinition { Width = GridLength.Auto } } };
            dateRow.Add(new Label
            {
                Text = item.RecordedAt.ToString("dd MMM yyyy"),
                FontSize = 11,
                TextColor = Color.FromArgb("#4A7A9B"),
                VerticalOptions = LayoutOptions.Center
            }, 0);

            // Band pill (small)
            if (!string.IsNullOrWhiteSpace(item.Band))
            {
                dateRow.Add(new Frame
                {
                    BackgroundColor = Color.FromArgb(bandColor + "33"), // 20% opacity
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

            // Score line
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
                Text = $"{item.Score:F1}",
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

            // Change reason
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

        // ── Referral card builders ─────────────────────────────────────

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

            // Header: name + rating
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

            // Comment
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

            // Date footer
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

        // ── Band accent helper ─────────────────────────────────────────

        private static string GetBandAccentHex(string band) => band?.ToLower() switch
        {
            "gold" => "#F59E0B",
            "silver" => "#94A3B8",
            "bronze" => "#B45309",
            "platinum" => "#7DB8E0",
            _ => "#4A7A9B"
        };

        // ── Header ─────────────────────────────────────────────────────

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

        // ── Stats strip (artisan only) ─────────────────────────────────

        private void PopulateArtisanStatsStrip(ArtisanUser au)
        {
            StatRatingLabel.Text = au.AverageRating > 0 ? $"{au.AverageRating:F1} ★" : "—";
            StatProjectsLabel.Text = au.CompletedProjects > 0 ? au.CompletedProjects.ToString() : "—";
            StatReviewsLabel.Text = au.TotalReviews > 0 ? au.TotalReviews.ToString() : "—";
            StatExperienceLabel.Text = au.YearsOfExperience > 0 ? $"{au.YearsOfExperience} yrs" : "—";
        }

        // ── Bio card ───────────────────────────────────────────────────

        private void PopulateBioCard(UserProfile profile)
        {
            if (!string.IsNullOrWhiteSpace(profile.Bio))
            {
                BioLabel.Text = profile.Bio;
                BioCard.IsVisible = true;
            }
        }

        // ── Personal info card ─────────────────────────────────────────

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

        // ── Artisan business card ──────────────────────────────────────

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
                foreach (var svc in au.ServicesOffered
                             .Split(',', StringSplitOptions.RemoveEmptyEntries))
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

        // ── Credentials card ───────────────────────────────────────────

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

        // ── Avatar helpers ─────────────────────────────────────────────

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

        // ── Helpers ────────────────────────────────────────────────────

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

        // ── UI state helpers ───────────────────────────────────────────

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

            // Header reset
            ProfilePhotoFrame.IsVisible = false;
            ProfileInitialsFrame.IsVisible = true;
            VerifiedBadge.IsVisible = false;

            // Stats
            StatRatingLabel.Text = "—";
            StatProjectsLabel.Text = "—";
            StatReviewsLabel.Text = "—";
            StatExperienceLabel.Text = "—";

            // Trust score
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
            _artisanCompanyId = null;

            // Personal info rows
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

            // Business rows
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

            // Credentials rows
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

        // ── Navigation ─────────────────────────────────────────────────

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