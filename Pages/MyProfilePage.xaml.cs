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
    /// MyProfilePage — redesigned.
    ///
    /// Layout order:
    ///   1. Hero card (avatar, name, role badge, contact, location, member since)
    ///   2. Personal Info card  ← always shown for every role
    ///   3. Artisan Stats strip ← shown when profile has ArtisanUser data
    ///   4. Artisan Business card
    ///   5. Artisan Credentials card
    ///   6. Edit Profile button
    ///
    /// A user can be both Customer/Staff/Admin AND an artisan simultaneously.
    /// Artisan sections appear whenever the loaded profile is an ArtisanUser,
    /// regardless of the Role claim string.
    ///
    /// Data source: single GET api/profilesapi/MyProfile endpoint (one round-trip).
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

                // 1. Hero card — always
                PopulateHeroCard(profile);

                // 2. Personal info — always, for all roles
                PopulatePersonalInfoCard(profile);
                PersonalInfoCard.IsVisible = true;

                // 3-5. Artisan sections — only when the profile has artisan data
                if (profile is ArtisanUser artisan)
                {
                    PopulateArtisanStatsCard(artisan);
                    PopulateArtisanBusinessCard(artisan);
                    PopulateCredentialsCard(artisan);
                    ArtisanStatsCard.IsVisible = true;
                    ArtisanBusinessCard.IsVisible = true;
                    CredentialsCard.IsVisible = true;
                }

                ProfileCard.IsVisible = true;
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

        // ── Hero card ──────────────────────────────────────────────────

        private void PopulateHeroCard(UserProfile profile)
        {
            var role = (profile.Role ?? "customer").Trim().ToLower();

            // Name
            ProfileNameLabel.Text = profile.DisplayName;
            ProfileInitialsLabel.Text = GetInitials(profile.FullName);

            // Email
            ProfileEmailLabel.Text = profile.Email ?? "";

            // Phone vs Member-Since in contact row
            if (!string.IsNullOrWhiteSpace(profile.PhoneNumber))
            {
                ProfilePhoneLabel.Text = profile.PhoneNumber;
                PhoneColumn.IsVisible = true;
                MemberColumn.IsVisible = false;
            }
            else
            {
                PhoneColumn.IsVisible = false;
                MemberColumn.IsVisible = true;
            }

            // Role badge
            RoleBadgeLabel.Text = role switch
            {
                "artisan" => "◆  ARTISAN",
                "admin" => "◆  ADMIN",
                "staff" => "◆  STAFF",
                _ => "◆  CUSTOMER"
            };

            // Role badge colour
            switch (role)
            {
                case "artisan":
                    RoleBadgeFrame.BackgroundColor = Color.FromArgb("#1E3A5F");
                    RoleBadgeFrame.BorderColor = Color.FromArgb("#2563EB");
                    RoleBadgeLabel.TextColor = Color.FromArgb("#60A5FA");
                    break;
                case "admin":
                    RoleBadgeFrame.BackgroundColor = Color.FromArgb("#2D1B69");
                    RoleBadgeFrame.BorderColor = Color.FromArgb("#7C3AED");
                    RoleBadgeLabel.TextColor = Color.FromArgb("#A78BFA");
                    break;
                case "staff":
                    RoleBadgeFrame.BackgroundColor = Color.FromArgb("#1C2B1A");
                    RoleBadgeFrame.BorderColor = Color.FromArgb("#16A34A");
                    RoleBadgeLabel.TextColor = Color.FromArgb("#4ADE80");
                    break;
                default:
                    RoleBadgeFrame.BackgroundColor = Color.FromArgb("#1E2A38");
                    RoleBadgeFrame.BorderColor = Color.FromArgb("#2563EB");
                    RoleBadgeLabel.TextColor = Color.FromArgb("#60A5FA");
                    break;
            }

            // Bio bubble in hero (brief preview)
            if (!string.IsNullOrWhiteSpace(profile.Bio))
            {
                ProfileBioLabel.Text = profile.Bio;
                BioBubble.IsVisible = true;
            }

            // Location pill
            var location = profile.LocationDisplay;
            if (!string.IsNullOrWhiteSpace(location))
            {
                ProfileLocationLabel.Text = location;
                LocationPill.IsVisible = true;
            }

            // Member since
            var joinedDate = profile.DateJoined;
            if (profile is ArtisanUser au && !joinedDate.HasValue)
                joinedDate = au.CreatedAt;

            if (joinedDate.HasValue)
            {
                MemberSinceLabel.Text = $"{joinedDate.Value:MMM yyyy}";
                MemberSinceShortLabel.Text = $"{joinedDate.Value:MMM yyyy}";
                MemberPill.IsVisible = true;
                MemberSinceShortLabel.IsVisible = !PhoneColumn.IsVisible;
            }

            // Avatar
            if (!string.IsNullOrWhiteSpace(profile.ProfilePicture))
                TryLoadPhoto(profile.ProfilePicture);
            else
                ShowInitials();

            // Verified badge (artisan only)
            if (profile is ArtisanUser artisan && artisan.IsVerified)
                VerifiedBadge.IsVisible = true;
        }

        // ── Personal Info card (ALL roles) ────────────────────────────

        private void PopulatePersonalInfoCard(UserProfile profile)
        {
            bool hasAny = false;

            // Bio — only show here if NOT already shown in hero bubble
            // (hero shows it for brevity; here we always show the full text)
            if (!string.IsNullOrWhiteSpace(profile.Bio))
            {
                PersonalBioLabel.Text = profile.Bio;
                PersonalBioSection.IsVisible = true;
                hasAny = true;
            }

            // Email
            if (!string.IsNullOrWhiteSpace(profile.Email))
            {
                PersonalEmailLabel.Text = profile.Email;
                PersonalEmailRow.IsVisible = true;
                hasAny = true;
            }

            // Phone
            if (!string.IsNullOrWhiteSpace(profile.PhoneNumber))
            {
                PersonalPhoneLabel.Text = profile.PhoneNumber;
                PersonalPhoneRow.IsVisible = true;
                hasAny = true;
            }

            // Date of birth (staff/admin)
            if (profile.DateOfBirth.HasValue)
            {
                DateOfBirthLabel.Text = profile.DateOfBirth.Value.ToString("dd MMM yyyy");
                DateOfBirthRow.IsVisible = true;
                hasAny = true;
            }

            // Emergency contact (staff/admin)
            if (!string.IsNullOrWhiteSpace(profile.EmergencyContact))
            {
                EmergencyContactLabel.Text = profile.EmergencyContact;
                EmergencyContactRow.IsVisible = true;
                hasAny = true;
            }

            // Staff type (staff/admin)
            if (!string.IsNullOrWhiteSpace(profile.StaffTypeName))
            {
                StaffTypeLabel.Text = profile.StaffTypeName;
                StaffTypeRow.IsVisible = true;
                hasAny = true;
            }

            // Address
            if (!string.IsNullOrWhiteSpace(profile.Address))
            {
                var addr = profile.Address;
                if (!string.IsNullOrWhiteSpace(profile.AddressLine2))
                    addr += $"\n{profile.AddressLine2}";
                AddressLabel.Text = addr;
                AddressRow.IsVisible = true;
                hasAny = true;
            }

            // City / State / Country
            var cityStr = profile.LocationDisplay;
            if (!string.IsNullOrWhiteSpace(cityStr))
            {
                CityCountryLabel.Text = cityStr;
                CityCountryRow.IsVisible = true;
                hasAny = true;
            }

            // Postal code
            if (!string.IsNullOrWhiteSpace(profile.PostalCode))
            {
                PostalCodeLabel.Text = profile.PostalCode;
                PostalCodeRow.IsVisible = true;
                hasAny = true;
            }

            // Language + Timezone — show both in the same row if either exists
            bool hasLanguage = !string.IsNullOrWhiteSpace(profile.PreferredLanguage);
            bool hasTimezone = !string.IsNullOrWhiteSpace(profile.Timezone);
            if (hasLanguage || hasTimezone)
            {
                LanguageLabel.Text = hasLanguage ? profile.PreferredLanguage : "—";
                TimezoneLabel.Text = hasTimezone ? profile.Timezone : "—";
                LanguageTimezoneRow.IsVisible = true;
                hasAny = true;
            }

            // Date joined
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

            // Artisan speciality (different from Specialization)
            if (!string.IsNullOrWhiteSpace(au.ArtisanSpeciality) &&
                au.ArtisanSpeciality != au.Specialization)
            {
                ArtisanSpecialityLabel.Text = au.ArtisanSpeciality;
                ArtisanSpecialityRow.IsVisible = true;
            }

            // Experience level
            if (!string.IsNullOrWhiteSpace(au.ExperienceLevel))
            {
                ExperienceLevelLabel.Text = au.ExperienceLevel;
                ExperienceLevelRow.IsVisible = true;
            }

            // Rate
            if (au.HourlyRate.HasValue)
            {
                HourlyRateLabel.Text = $"GH₵ {au.HourlyRate.Value:F0}/hr";
                HourlyRateBox.IsVisible = true;
            }

            // Service radius
            if (au.ServiceRadius.HasValue && au.ServiceRadius.Value > 0)
            {
                ServiceRadiusLabel.Text = $"{au.ServiceRadius:F0} km";
                ServiceRadiusBox.IsVisible = true;
            }

            // Business address
            if (!string.IsNullOrWhiteSpace(au.BusinessAddress))
            {
                BusinessAddressLabel.Text = au.BusinessAddress;
                BusinessAddressRow.IsVisible = true;
            }

            // Availability badge
            var status = au.AvailabilityStatusUpper;
            AvailabilityLabel.Text = status switch
            {
                "AVAILABLE" => "● Available",
                "BUSY" => "● Busy",
                "UNAVAILABLE" => "● Unavailable",
                _ => au.AvailabilityStatus ?? "Unknown"
            };
            AvailabilityBadge.BackgroundColor = status switch
            {
                "AVAILABLE" => Color.FromArgb("#14532D"),
                "BUSY" => Color.FromArgb("#451A03"),
                _ => Color.FromArgb("#450A0A")
            };

            // Slug
            if (!string.IsNullOrWhiteSpace(au.Slug))
            {
                SlugLabel.Text = au.Slug;
                SlugRow.IsVisible = true;
            }

            // About
            if (!string.IsNullOrWhiteSpace(au.About))
            {
                AboutLabel.Text = au.About;
                AboutSection.IsVisible = true;
            }

            // Professional Bio
            if (!string.IsNullOrWhiteSpace(au.ProfessionalBio))
            {
                ProfBioLabel.Text = au.ProfessionalBio;
                ProfBioSection.IsVisible = true;
            }

            // Services
            if (!string.IsNullOrWhiteSpace(au.ServicesOffered))
            {
                ServicesLayout.Children.Clear();
                foreach (var svc in au.ServicesOffered
                             .Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    ServicesLayout.Children.Add(new Frame
                    {
                        BackgroundColor = Color.FromArgb("#1C2B3A"),
                        CornerRadius = 9,
                        Padding = new Thickness(11, 5),
                        HasShadow = false,
                        Margin = new Thickness(0, 0, 6, 6),
                        BorderColor = Color.FromArgb("#2563EB"),
                        Content = new Label
                        {
                            Text = svc.Trim(),
                            FontSize = 12,
                            TextColor = Color.FromArgb("#60A5FA")
                        }
                    });
                }
                ServicesSection.IsVisible = true;
            }

            // Last updated
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
                Uri uri;
                if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    uri = new Uri(path);
                else
                {
                    var baseUrl = _apiConfig.BaseUrl.TrimEnd('/');
                    uri = new Uri($"{baseUrl}/{path.TrimStart('/')}");
                }

                ProfilePhoto.Source = ImageSource.FromUri(uri);
                ProfilePhotoFrame.IsVisible = true;
                ProfileInitialsFrame.IsVisible = false;
            }
            catch
            {
                ShowInitials();
            }
        }

        private void ShowInitials()
        {
            ProfilePhotoFrame.IsVisible = false;
            ProfileInitialsFrame.IsVisible = true;
        }

        private static string GetInitials(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
            // Cards
            ProfileCard.IsVisible = false;
            PersonalInfoCard.IsVisible = false;
            ArtisanStatsCard.IsVisible = false;
            ArtisanBusinessCard.IsVisible = false;
            CredentialsCard.IsVisible = false;
            EditProfileButton.IsVisible = false;
            ErrorState.IsVisible = false;

            // Avatar
            ProfilePhotoFrame.IsVisible = false;
            ProfileInitialsFrame.IsVisible = true;
            VerifiedBadge.IsVisible = false;

            // Hero optional
            PhoneColumn.IsVisible = false;
            MemberColumn.IsVisible = true;
            BioBubble.IsVisible = false;
            LocationPill.IsVisible = false;
            MemberPill.IsVisible = false;

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

            // Artisan business rows
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