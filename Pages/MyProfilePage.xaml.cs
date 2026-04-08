using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.Pages
{
    /// <summary>
    /// Read-only profile page.
    ///
    /// Role is decoded from the JWT via IUserService (UserService.GetUserClaimsFromTokenAsync).
    /// Data comes from IUserService.LoadUserProfileAsync(), which hits the correct API
    /// endpoint per role:
    ///   Artisan  → GET api/profilesapi/artisan/me   → returns ArtisanUser
    ///   Staff    → GET api/profilesapi/staff/me     → returns UserProfile (role = Staff/Admin)
    ///   Customer → GET api/profilesapi/customer/me  → returns UserProfile (role = Customer)
    ///
    /// Layout sections:
    ///   All roles  : ProfileCard (hero: avatar, name, badge, email, phone, bio, location)
    ///   Artisan    : ArtisanStatsCard + ArtisanBusinessCard + CredentialsCard
    ///   Customer/Admin/Staff : PersonalInfoCard
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

        // ── Lifecycle ─────────────────────────────────────────────────

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadProfileAsync();
        }

        // ── Load ──────────────────────────────────────────────────────

        private async Task LoadProfileAsync()
        {
            ShowLoading(true);
            HideAllContentCards();

            try
            {
                var profile = await _userService.LoadUserProfileAsync();

                if (profile == null)
                {
                    ShowError("Could not load your profile. Please try again.");
                    return;
                }

                var role = (profile.Role ?? "customer").Trim().ToLower();

                PopulateHeroCard(profile, role);

                if (role == "artisan")
                {
                    var artisan = profile as ArtisanUser;
                    PopulateArtisanStatsCard(artisan);
                    PopulateArtisanBusinessCard(artisan);
                    PopulateCredentialsCard(artisan);
                    ArtisanStatsCard.IsVisible = true;
                    ArtisanBusinessCard.IsVisible = true;
                    CredentialsCard.IsVisible = true;
                }
                else
                {
                    PopulatePersonalInfoCard(profile);
                    PersonalInfoCard.IsVisible = true;
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
                System.Diagnostics.Debug.WriteLine(
                    $"[MY PROFILE] Load error: {ex.Message}");
                ShowError("Failed to load profile. Please check your connection.");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        // ── Hero card — all roles ─────────────────────────────────────

        private void PopulateHeroCard(UserProfile profile, string role)
        {
            // Name
            var displayName = !string.IsNullOrWhiteSpace(profile.FullName)
                ? profile.FullName
                : profile.Email ?? "User";
            ProfileNameLabel.Text = displayName;
            ProfileInitialsLabel.Text = GetInitials(profile.FullName);

            // Email
            ProfileEmailLabel.Text = profile.Email ?? "";

            // Phone
            if (!string.IsNullOrWhiteSpace(profile.PhoneNumber))
            {
                ProfilePhoneLabel.Text = profile.PhoneNumber;
                PhoneRow.IsVisible = true;
            }

            // Role badge
            RoleBadgeLabel.Text = role switch
            {
                "artisan" => "Artisan",
                "admin" => "Admin",
                "staff" => "Staff",
                _ => "Customer"
            };
            RoleBadgeFrame.BackgroundColor = role switch
            {
                "artisan" => Color.FromArgb("#2DC98E"),
                "admin" => Color.FromArgb("#8B5CF6"),
                "staff" => Color.FromArgb("#F59E0B"),
                _ => Color.FromArgb("#2E6FD8")
            };

            // Bio
            if (!string.IsNullOrWhiteSpace(profile.Bio))
            {
                ProfileBioLabel.Text = profile.Bio;
                ProfileBioLabel.IsVisible = true;
            }

            // Location
            var locationParts = new[] { profile.City, profile.State, profile.Country }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var location = string.Join(", ", locationParts);
            if (!string.IsNullOrWhiteSpace(location))
            {
                ProfileLocationLabel.Text = location;
                LocationRow.IsVisible = true;
            }

            // Member since
            if (profile.DateJoined.HasValue)
            {
                MemberSinceLabel.Text =
                    $"Member since {profile.DateJoined.Value:MMMM yyyy}";
                MemberSinceLabel.IsVisible = true;
            }

            // Photo / initials
            if (!string.IsNullOrWhiteSpace(profile.ProfilePicture))
                TryLoadPhoto(profile.ProfilePicture);
            else
                ShowInitials();

            // Verified badge — artisan only
            if (role == "artisan" && profile is ArtisanUser au && au.IsVerified)
                VerifiedBadge.IsVisible = true;
        }

        // ── Artisan stats strip ───────────────────────────────────────

        private void PopulateArtisanStatsCard(ArtisanUser? au)
        {
            if (au == null) return;

            StatRatingLabel.Text = au.AverageRating > 0
                ? $"{au.AverageRating:F1} ★"
                : "—";

            StatProjectsLabel.Text = au.CompletedProjects > 0
                ? au.CompletedProjects.ToString()
                : "—";

            StatExperienceLabel.Text = au.YearsOfExperience > 0
                ? au.YearsOfExperience.ToString()
                : "—";
        }

        // ── Artisan business card ─────────────────────────────────────

        private void PopulateArtisanBusinessCard(ArtisanUser? au)
        {
            if (au == null) return;

            BusinessNameLabel.Text = au.BusinessName ?? "—";
            SpecializationLabel.Text = au.Specialization ?? "—";

            if (!string.IsNullOrWhiteSpace(au.ArtisanSpeciality) &&
                au.ArtisanSpeciality != au.Specialization)
            {
                ArtisanSpecialityLabel.Text = au.ArtisanSpeciality;
                ArtisanSpecialityRow.IsVisible = true;
            }

            if (au.HourlyRate.HasValue)
            {
                HourlyRateLabel.Text = $"GH₵ {au.HourlyRate.Value:F0} / hr";
                HourlyRateRow.IsVisible = true;
            }

            if (au.ServiceRadius.HasValue && au.ServiceRadius.Value > 0)
            {
                ServiceRadiusLabel.Text = $"{au.ServiceRadius} km service radius";
                ServiceRadiusRow.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(au.BusinessAddress))
            {
                BusinessAddressLabel.Text = au.BusinessAddress;
                BusinessAddressRow.IsVisible = true;
            }

            // Availability badge
            var status = (au.AvailabilityStatus ?? "").ToUpper();
            AvailabilityLabel.Text = status switch
            {
                "AVAILABLE" => "Available",
                "BUSY" => "Busy",
                "UNAVAILABLE" => "Unavailable",
                _ => au.AvailabilityStatus ?? "Unknown"
            };
            AvailabilityBadge.BackgroundColor = status switch
            {
                "AVAILABLE" => Color.FromArgb("#10B981"),
                "BUSY" => Color.FromArgb("#F59E0B"),
                _ => Color.FromArgb("#EF4444")
            };

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
                        Padding = new Thickness(10, 4),
                        HasShadow = false,
                        Margin = new Thickness(0, 0, 6, 6),
                        Content = new Label
                        {
                            Text = svc.Trim(),
                            FontSize = 12,
                            TextColor = Color.FromArgb("#2E6FD8")
                        }
                    });
                }
                ServicesSection.IsVisible = true;
            }
        }

        // ── Artisan credentials card ──────────────────────────────────

        private void PopulateCredentialsCard(ArtisanUser? au)
        {
            if (au == null) return;

            bool hasAny = false;

            if (!string.IsNullOrWhiteSpace(au.LicenseNumber))
            {
                LicenseLabel.Text = au.LicenseNumber;
                LicenseRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(au.Certification))
            {
                CertificationLabel.Text = au.Certification;
                CertificationRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(au.BusinessRegistration))
            {
                BusinessRegLabel.Text = au.BusinessRegistration;
                BusinessRegRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(au.TaxId))
            {
                TaxIdLabel.Text = au.TaxId;
                TaxIdRow.IsVisible = true;
                hasAny = true;
            }

            if (!string.IsNullOrWhiteSpace(au.InsuranceDetails))
            {
                InsuranceLabel.Text = au.InsuranceDetails;
                InsuranceRow.IsVisible = true;
                hasAny = true;
            }

            if (!hasAny)
                NoCredentialsLabel.IsVisible = true;
        }

        // ── Personal info card — Customer / Staff / Admin ─────────────

        private void PopulatePersonalInfoCard(UserProfile profile)
        {
            bool hasAny = false;

            if (!string.IsNullOrWhiteSpace(profile.Address))
            {
                AddressLabel.Text = profile.Address;
                AddressRow.IsVisible = true;
                hasAny = true;
            }

            var cityParts = new[] { profile.City, profile.State, profile.Country }
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var cityStr = string.Join(", ", cityParts);
            if (!string.IsNullOrWhiteSpace(cityStr))
            {
                CityCountryLabel.Text = cityStr;
                CityCountryRow.IsVisible = true;
                hasAny = true;
            }

            if (!hasAny)
                NoAddressLabel.IsVisible = true;
        }

        // ── Avatar helpers ────────────────────────────────────────────

        private void TryLoadPhoto(string path)
        {
            try
            {
                Uri uri;
                if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    uri = new Uri(path);
                }
                else
                {
                    var base_ = _apiConfig.BaseUrl.TrimEnd('/');
                    uri = new Uri($"{base_}/{path.TrimStart('/')}");
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
            var parts = fullName.Trim()
                                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpper()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        // ── UI state helpers ──────────────────────────────────────────

        private void ShowLoading(bool loading)
        {
            PageLoadingIndicator.IsRunning = loading;
            PageLoadingIndicator.IsVisible = loading;
        }

        private void HideAllContentCards()
        {
            ProfileCard.IsVisible = false;
            ArtisanStatsCard.IsVisible = false;
            ArtisanBusinessCard.IsVisible = false;
            CredentialsCard.IsVisible = false;
            PersonalInfoCard.IsVisible = false;
            EditProfileButton.IsVisible = false;
            ErrorState.IsVisible = false;
            VerifiedBadge.IsVisible = false;

            // Hero optional rows
            PhoneRow.IsVisible = false;
            ProfileBioLabel.IsVisible = false;
            LocationRow.IsVisible = false;
            MemberSinceLabel.IsVisible = false;

            // Artisan business rows
            ArtisanSpecialityRow.IsVisible = false;
            HourlyRateRow.IsVisible = false;
            ServiceRadiusRow.IsVisible = false;
            BusinessAddressRow.IsVisible = false;
            AboutSection.IsVisible = false;
            ProfBioSection.IsVisible = false;
            ServicesSection.IsVisible = false;

            // Credentials rows
            LicenseRow.IsVisible = false;
            CertificationRow.IsVisible = false;
            BusinessRegRow.IsVisible = false;
            TaxIdRow.IsVisible = false;
            InsuranceRow.IsVisible = false;
            NoCredentialsLabel.IsVisible = false;

            // Personal info rows
            AddressRow.IsVisible = false;
            CityCountryRow.IsVisible = false;
            NoAddressLabel.IsVisible = false;
        }

        private void ShowError(string message)
        {
            ErrorLabel.Text = message;
            ErrorState.IsVisible = true;
        }

        // ── Navigation ────────────────────────────────────────────────

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnEditClicked(object sender, EventArgs e)
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
                await DisplayAlert("Info",
                    "Edit Profile is not yet available.", "OK");
            }
        }

        private async void OnRetryClicked(object sender, EventArgs e)
        {
            await LoadProfileAsync();
        }
    }
}