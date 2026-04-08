using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.Pages
{
    /// <summary>
    /// Read-only profile card. Uses IProfileApiService to fetch data from
    /// GET api/ProfilesApi/MyProfile. Displays role-specific sections:
    ///   - All roles  : hero card (photo, name, role badge, email, bio, location)
    ///   - Artisan    : stats strip + business card
    ///   - Customer / Admin : personal info card (address, city/country)
    /// </summary>
    [QueryProperty(nameof(Role), "Role")]
    public partial class MyProfilePage : ContentPage
    {
        private readonly IProfileApiService _profileService;
        private readonly ApiConfig _apiConfig;

        public string Role { get; set; } = "Customer";

        public MyProfilePage(IProfileApiService profileService, ApiConfig apiConfig)
        {
            InitializeComponent();
            _profileService = profileService;
            _apiConfig = apiConfig;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadProfileAsync();
        }

        // ── Data ─────────────────────────────────────────────────────

        private async Task LoadProfileAsync()
        {
            try
            {
                PageLoadingIndicator.IsRunning = true;
                PageLoadingIndicator.IsVisible = true;
                ProfileCard.IsVisible = false;
                EditProfileButton.IsVisible = false;

                var profile = await _profileService.GetMyProfileAsync();

                if (profile == null)
                {
                    await DisplayAlert("Error", "Could not load profile.", "OK");
                    return;
                }

                PopulateHeroCard(profile);

                var role = (Role ?? "customer").ToLower();

                if (role == "artisan")
                    PopulateArtisanCards(profile);
                else
                    PopulatePersonalInfoCard(profile);

                ProfileCard.IsVisible = true;
                EditProfileButton.IsVisible = true;
            }
            catch (UnauthorizedAccessException)
            {
                await DisplayAlert("Session Expired", "Please log in again.", "OK");
                await Shell.Current.GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MY PROFILE] Load error: {ex.Message}");
                await DisplayAlert("Error", "Failed to load profile.", "OK");
            }
            finally
            {
                PageLoadingIndicator.IsRunning = false;
                PageLoadingIndicator.IsVisible = false;
            }
        }

        // ── Hero card ─────────────────────────────────────────────────

        private void PopulateHeroCard(MobileProfileDetails profile)
        {
            var fullName = profile.UserProfile?.FullName;
            var email = profile.Email;
            var phone = profile.PhoneNumber;
            var bio = profile.UserProfile?.Bio;
            var city = profile.UserProfile?.City;
            var country = profile.UserProfile?.Country;
            var photoUrl = profile.UserProfile?.ProfilePictureUrl;

            ProfileNameLabel.Text = fullName ?? "User";
            ProfileEmailLabel.Text = email ?? "";
            ProfileInitialsLabel.Text = GetInitials(fullName);

            // Role badge
            var role = (Role ?? "customer").ToLower();
            RoleBadgeLabel.Text = role switch
            {
                "artisan" => "Artisan",
                "admin" => "Admin",
                _ => "Customer"
            };
            RoleBadgeFrame.BackgroundColor = role switch
            {
                "artisan" => Color.FromArgb("#2DC98E"),
                "admin" => Color.FromArgb("#8B5CF6"),
                _ => Color.FromArgb("#2E6FD8")
            };

            if (!string.IsNullOrWhiteSpace(phone))
            {
                ProfilePhoneLabel.Text = phone;
                ProfilePhoneLabel.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(bio))
            {
                ProfileBioLabel.Text = bio;
                ProfileBioLabel.IsVisible = true;
            }

            var locationParts = new[] { city, country }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var location = string.Join(", ", locationParts);
            if (!string.IsNullOrWhiteSpace(location))
            {
                ProfileLocationLabel.Text = location;
                LocationRow.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(photoUrl))
                TryLoadPhoto(photoUrl);
            else
                ShowInitials();
        }

        // ── Artisan cards ─────────────────────────────────────────────

        private void PopulateArtisanCards(MobileProfileDetails profile)
        {
            var ap = profile.ArtisanProfile;
            if (ap == null) return;

            // Stats strip
            StatRatingLabel.Text = "—";   // averageRating not in MobileArtisanProfile yet; extend if needed
            StatProjectsLabel.Text = "—";   // completedProjects not in MobileArtisanProfile yet
            StatExperienceLabel.Text = ap.YearsOfExperience > 0 ? ap.YearsOfExperience.ToString() : "—";
            ArtisanStatsCard.IsVisible = true;

            // Business card
            BusinessNameLabel.Text = ap.BusinessName ?? "";
            SpecializationLabel.Text = ap.Specialization ?? "";

            if (ap.HourlyRate.HasValue)
            {
                HourlyRateLabel.Text = $"GH₵ {ap.HourlyRate.Value:F0} / hr";
                HourlyRateRow.IsVisible = true;
            }

            var avStatus = ap.AvailabilityStatus?.ToUpper();
            AvailabilityLabel.Text = avStatus switch
            {
                "AVAILABLE" => "Available",
                "BUSY" => "Busy",
                _ => "Unavailable"
            };
            AvailabilityBadge.BackgroundColor = avStatus switch
            {
                "AVAILABLE" => Color.FromArgb("#10B981"),
                "BUSY" => Color.FromArgb("#F59E0B"),
                _ => Color.FromArgb("#EF4444")
            };

            if (!string.IsNullOrWhiteSpace(ap.About))
            {
                AboutLabel.Text = ap.About;
                AboutSection.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(ap.ServicesOffered))
            {
                ServicesLayout.Children.Clear();
                foreach (var service in ap.ServicesOffered.Split(',', StringSplitOptions.RemoveEmptyEntries))
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
                            Text = service.Trim(),
                            FontSize = 12,
                            TextColor = Color.FromArgb("#2E6FD8")
                        }
                    });
                }
                ServicesSection.IsVisible = true;
            }

            ArtisanBusinessCard.IsVisible = true;
        }

        // ── Personal info card (Customer / Admin) ─────────────────────

        private void PopulatePersonalInfoCard(MobileProfileDetails profile)
        {
            var up = profile.UserProfile;
            if (up == null) return;

            bool hasAny = false;

            if (!string.IsNullOrWhiteSpace(up.Address))
            {
                AddressLabel.Text = up.Address;
                AddressRow.IsVisible = true;
                hasAny = true;
            }

            var cityParts = new[] { up.City, up.State, up.Country }
                .Where(x => !string.IsNullOrWhiteSpace(x));
            var cityStr = string.Join(", ", cityParts);
            if (!string.IsNullOrWhiteSpace(cityStr))
            {
                CityCountryLabel.Text = cityStr;
                CityCountryRow.IsVisible = true;
                hasAny = true;
            }

            PersonalInfoCard.IsVisible = hasAny;
        }

        // ── Avatar ────────────────────────────────────────────────────

        private void TryLoadPhoto(string path)
        {
            try
            {
                ImageSource source;
                if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    source = ImageSource.FromUri(new Uri(path));
                }
                else
                {
                    var fullUrl = $"{_apiConfig.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
                    source = ImageSource.FromUri(new Uri(fullUrl));
                }

                ProfilePhoto.Source = source;
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

        private string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpper()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpper();
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
                await Shell.Current.GoToAsync(nameof(EditProfilePage), new Dictionary<string, object>
                {
                    { "Role", Role }
                });
            }
            catch
            {
                await DisplayAlert("Info", "Edit Profile page is not yet available.", "OK");
            }
        }
    }
}