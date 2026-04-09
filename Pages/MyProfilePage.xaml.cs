using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Pages
{
    /// <summary>
    /// MyProfilePage — redesigned to match Settings page aesthetic.
    ///
    /// Color scheme:
    ///   • Header  : #1B2B3A (dark navy, same as Settings)
    ///   • Cards   : White with #F0F4F8 dividers
    ///   • Accents : #2563EB blue / #4DA6E8 light blue
    ///   • Text    : #1B2B3A primary / #8A9BAD muted
    ///   • Page bg : #F0F4F8
    ///
    /// Layout:
    ///   1. Dark header  — avatar, name, role badge, email, quick stats row, bio
    ///   2. Artisan Stats strip  (artisan only)
    ///   3. Personal Info card   (all roles)
    ///   4. Artisan Business card (artisan only)
    ///   5. Artisan Credentials card (artisan only)
    ///   6. Edit Profile button
    /// </summary>
    public partial class MyProfilePage : ContentPage
    {
        private readonly IUserService _userService;
        private readonly ApiConfig _apiConfig;

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
                PopulatePersonalInfoCard(profile);
                PersonalInfoCard.IsVisible = true;

                if (profile is ArtisanUser artisan)
                {
                    PopulateArtisanStatsCard(artisan);
                    PopulateArtisanBusinessCard(artisan);
                    PopulateCredentialsCard(artisan);
                    ArtisanStatsCard.IsVisible = true;
                    ArtisanBusinessCard.IsVisible = true;
                    CredentialsCard.IsVisible = true;
                }

                EditProfileButton.IsVisible = true;
                ErrorState.IsVisible = false;
            }
            catch (UnauthorizedAccessException)
            {
                await DisplayAlert("Session Expired",
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

            // Role badge
            RoleBadgeLabel.Text = role switch
            {
                "artisan" => "◆  ARTISAN",
                "admin" => "◆  ADMIN",
                "staff" => "◆  STAFF",
                _ => "◆  CUSTOMER"
            };

            (RoleBadgeFrame.BackgroundColor, RoleBadgeFrame.BorderColor, RoleBadgeLabel.TextColor) = role switch
            {
                "artisan" => (Color.FromArgb("#1E3A5F"), Color.FromArgb("#2563EB"), Color.FromArgb("#4DA6E8")),
                "admin" => (Color.FromArgb("#2D1B69"), Color.FromArgb("#7C3AED"), Color.FromArgb("#A78BFA")),
                "staff" => (Color.FromArgb("#14532D"), Color.FromArgb("#16A34A"), Color.FromArgb("#2DC98E")),
                _ => (Color.FromArgb("#243447"), Color.FromArgb("#2563EB"), Color.FromArgb("#4DA6E8"))
            };

            // Bio
            if (!string.IsNullOrWhiteSpace(profile.Bio))
            {
                ProfileBioLabel.Text = profile.Bio;
                BioBubble.IsVisible = true;
            }

            // Quick stats row — phone
            if (!string.IsNullOrWhiteSpace(profile.PhoneNumber))
            {
                ProfilePhoneLabel.Text = profile.PhoneNumber;
                PhoneColumn.IsVisible = true;
            }

            // Quick stats row — location
            var location = profile.LocationDisplay;
            if (!string.IsNullOrWhiteSpace(location))
            {
                ProfileLocationLabel.Text = location;
                LocationColumn.IsVisible = true;
            }

            // Quick stats row — member since
            var joinedDate = profile.DateJoined;
            if (profile is ArtisanUser au && !joinedDate.HasValue)
                joinedDate = au.CreatedAt;

            if (joinedDate.HasValue)
            {
                MemberSinceLabel.Text = $"{joinedDate.Value:MMM yyyy}";
                MemberColumn.IsVisible = true;
            }

            // Avatar
            if (!string.IsNullOrWhiteSpace(profile.ProfilePicture))
                TryLoadPhoto(profile.ProfilePicture);
            else
                ShowInitials();

            // Verified badge
            if (profile is ArtisanUser artisan && artisan.IsVerified)
                VerifiedBadge.IsVisible = true;
        }

        // ── Personal Info card ─────────────────────────────────────────

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

        // ── Artisan stats strip ────────────────────────────────────────

        private void PopulateArtisanStatsCard(ArtisanUser au)
        {
            StatRatingLabel.Text = au.AverageRating > 0 ? $"{au.AverageRating:F1} ★" : "—";
            StatProjectsLabel.Text = au.CompletedProjects > 0 ? au.CompletedProjects.ToString() : "—";
            StatReviewsLabel.Text = au.TotalReviews > 0 ? au.TotalReviews.ToString() : "—";
            StatExperienceLabel.Text = au.YearsOfExperience > 0 ? au.YearsOfExperience.ToString() : "—";
        }

        // ── Artisan business card ──────────────────────────────────────

        private void PopulateArtisanBusinessCard(ArtisanUser au)
        {
            BusinessNameLabel.Text = au.BusinessName ?? "—";
            SpecializationLabel.Text = au.Specialization ?? "—";

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

            if (!string.IsNullOrWhiteSpace(au.BusinessAddress))
            {
                BusinessAddressLabel.Text = au.BusinessAddress;
                BusinessAddressRow.IsVisible = true;
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
                        CornerRadius = 8,
                        Padding = new Thickness(11, 5),
                        HasShadow = false,
                        Margin = new Thickness(0, 0, 6, 6),
                        BorderColor = Color.FromArgb("#BFDBFE"),
                        Content = new Label
                        {
                            Text = svc.Trim(),
                            FontSize = 12,
                            TextColor = Color.FromArgb("#1D4ED8")
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
                {
                    uri = new Uri(path);
                }
                else
                {
                    var baseUrl = _apiConfig.BaseUrl?.TrimEnd('/');
                    var relative = path.TrimStart('/');
                    uri = new Uri($"{baseUrl}/{relative}");
                }

                System.Diagnostics.Debug.WriteLine($"[MY PROFILE] Avatar URL: {uri}");

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
            PersonalInfoCard.IsVisible = false;
            ArtisanStatsCard.IsVisible = false;
            ArtisanBusinessCard.IsVisible = false;
            CredentialsCard.IsVisible = false;
            EditProfileButton.IsVisible = false;
            ErrorState.IsVisible = false;

            // Header reset
            ProfilePhotoFrame.IsVisible = false;
            ProfileInitialsFrame.IsVisible = true;
            VerifiedBadge.IsVisible = false;
            BioBubble.IsVisible = false;
            PhoneColumn.IsVisible = false;
            LocationColumn.IsVisible = false;
            MemberColumn.IsVisible = false;

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
                await DisplayAlert("Info", "Edit Profile is not yet available.", "OK");
            }
        }

        private async void OnRetryClicked(object sender, TappedEventArgs e)
            => await LoadProfileAsync();
    }
}