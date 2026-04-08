using Microsoft.Maui.Controls;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.Pages
{
    /// <summary>
    /// Read-only profile card. Fetches fresh data from GET api/ProfilesApi/MyProfile.
    /// Displays role-specific sections:
    ///   - All roles  : hero card (photo, name, role badge, email, bio, location)
    ///   - Artisan    : stats strip + business card (business name, specialization, rate, availability, services)
    ///   - Customer / Admin : personal info card (address, city/country)
    /// </summary>
    [QueryProperty(nameof(Role), "Role")]
    public partial class MyProfilePage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly ApiConfig _apiConfig;

        public string Role { get; set; } = "Customer";

        public MyProfilePage(AuthService authService, ApiConfig apiConfig)
        {
            InitializeComponent();
            _authService = authService;
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

                var token = await _authService.GetTokenAsync();
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_apiConfig.BaseUrl.TrimEnd('/')}/api/ProfilesApi/MyProfile";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Error", "Could not load profile.", "OK");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                PopulateHeroCard(root);

                var role = (Role ?? "customer").ToLower();

                if (role == "artisan")
                {
                    PopulateArtisanCards(root);
                }
                else
                {
                    PopulatePersonalInfoCard(root);
                }

                ProfileCard.IsVisible = true;
                EditProfileButton.IsVisible = true;
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

        private void PopulateHeroCard(JsonElement root)
        {
            var userProfile = root.TryGetProperty("userProfile", out var up) ? (JsonElement?)up : null;

            var fullName = userProfile?.TryGetProperty("fullName", out var fn) == true ? fn.GetString() : null;
            var email    = root.TryGetProperty("email", out var em) ? em.GetString() : null;
            var phone    = root.TryGetProperty("phoneNumber", out var ph) ? ph.GetString() : null;
            var bio      = userProfile?.TryGetProperty("bio", out var b) == true ? b.GetString() : null;
            var city     = userProfile?.TryGetProperty("city", out var c) == true ? c.GetString() : null;
            var country  = userProfile?.TryGetProperty("country", out var co) == true ? co.GetString() : null;
            var photoUrl = userProfile?.TryGetProperty("profilePictureUrl", out var pic) == true ? pic.GetString() : null;

            ProfileNameLabel.Text  = fullName ?? "User";
            ProfileEmailLabel.Text = email ?? "";
            ProfileInitialsLabel.Text = GetInitials(fullName);

            // Role badge
            var role = (Role ?? "customer").ToLower();
            RoleBadgeLabel.Text = role switch
            {
                "artisan" => "Artisan",
                "admin"   => "Admin",
                _         => "Customer"
            };
            RoleBadgeFrame.BackgroundColor = role switch
            {
                "artisan" => Color.FromArgb("#2DC98E"),
                "admin"   => Color.FromArgb("#8B5CF6"),
                _         => Color.FromArgb("#2E6FD8")
            };

            // Phone
            if (!string.IsNullOrWhiteSpace(phone))
            {
                ProfilePhoneLabel.Text = phone;
                ProfilePhoneLabel.IsVisible = true;
            }

            // Bio
            if (!string.IsNullOrWhiteSpace(bio))
            {
                ProfileBioLabel.Text = bio;
                ProfileBioLabel.IsVisible = true;
            }

            // Location
            var locationParts = new[] { city, country }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var location = string.Join(", ", locationParts);
            if (!string.IsNullOrWhiteSpace(location))
            {
                ProfileLocationLabel.Text = location;
                LocationRow.IsVisible = true;
            }

            // Avatar photo
            if (!string.IsNullOrWhiteSpace(photoUrl))
                TryLoadPhoto(photoUrl);
            else
                ShowInitials();
        }

        // ── Artisan cards ─────────────────────────────────────────────

        private void PopulateArtisanCards(JsonElement root)
        {
            if (!root.TryGetProperty("artisanProfile", out var ap) ||
                ap.ValueKind == JsonValueKind.Null)
                return;

            // Stats strip
            var rating   = ap.TryGetProperty("averageRating", out var r)   ? r.GetDecimal() : 0m;
            var projects = ap.TryGetProperty("completedProjects", out var p) ? p.GetInt32()  : 0;
            var years    = ap.TryGetProperty("yearsOfExperience", out var y) ? y.GetInt32()  : 0;

            StatRatingLabel.Text    = rating > 0 ? $"{rating:F1}⭐" : "—";
            StatProjectsLabel.Text  = projects.ToString();
            StatExperienceLabel.Text = years > 0 ? years.ToString() : "—";
            ArtisanStatsCard.IsVisible = true;

            // Business card
            BusinessNameLabel.Text   = ap.TryGetProperty("businessName",   out var bn) ? bn.GetString() ?? "" : "";
            SpecializationLabel.Text = ap.TryGetProperty("specialization",  out var sp) ? sp.GetString() ?? "" : "";

            if (ap.TryGetProperty("hourlyRate", out var hr) && hr.ValueKind != JsonValueKind.Null)
            {
                HourlyRateLabel.Text = $"GH₵ {hr.GetDecimal():F0} / hr";
                HourlyRateRow.IsVisible = true;
            }

            var availability = ap.TryGetProperty("availabilityStatus", out var avs) ? avs.GetString() : "UNAVAILABLE";
            AvailabilityLabel.Text = availability?.ToUpper() switch
            {
                "AVAILABLE"   => "Available",
                "BUSY"        => "Busy",
                _             => "Unavailable"
            };
            AvailabilityBadge.BackgroundColor = availability?.ToUpper() switch
            {
                "AVAILABLE" => Color.FromArgb("#10B981"),
                "BUSY"      => Color.FromArgb("#F59E0B"),
                _           => Color.FromArgb("#EF4444")
            };

            var about = ap.TryGetProperty("about", out var abt) ? abt.GetString() : null;
            if (!string.IsNullOrWhiteSpace(about))
            {
                AboutLabel.Text = about;
                AboutSection.IsVisible = true;
            }

            var services = ap.TryGetProperty("servicesOffered", out var svc) ? svc.GetString() : null;
            if (!string.IsNullOrWhiteSpace(services))
            {
                ServicesLayout.Children.Clear();
                foreach (var service in services.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var chip = new Frame
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
                    };
                    ServicesLayout.Children.Add(chip);
                }
                ServicesSection.IsVisible = true;
            }

            ArtisanBusinessCard.IsVisible = true;
        }

        // ── Personal info card (Customer / Admin) ─────────────────────

        private void PopulatePersonalInfoCard(JsonElement root)
        {
            var userProfile = root.TryGetProperty("userProfile", out var up) ? (JsonElement?)up : null;
            if (userProfile == null) return;

            var address = userProfile?.TryGetProperty("address", out var a) == true ? a.GetString() : null;
            var city    = userProfile?.TryGetProperty("city",    out var c) == true ? c.GetString() : null;
            var state   = userProfile?.TryGetProperty("state",   out var s) == true ? s.GetString() : null;
            var country = userProfile?.TryGetProperty("country", out var co) == true ? co.GetString() : null;

            bool hasAny = false;

            if (!string.IsNullOrWhiteSpace(address))
            {
                AddressLabel.Text = address;
                AddressRow.IsVisible = true;
                hasAny = true;
            }

            var cityParts = new[] { city, state, country }
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
