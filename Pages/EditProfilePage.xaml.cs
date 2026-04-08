using Microsoft.Maui.Controls;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.Pages
{
    /// <summary>
    /// Role-aware edit form.
    ///   Customer / Admin → personal info (name, bio, address, city, state, country, postal, photo)
    ///   Artisan          → business info (business name, specialization, rate, availability, services, etc.)
    ///
    /// Loads:  GET  api/ProfilesApi/MyProfile
    /// Saves:  PUT  api/ProfilesApi
    /// </summary>
    [QueryProperty(nameof(Role), "Role")]
    public partial class EditProfilePage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly ApiConfig _apiConfig;

        public string Role { get; set; } = "Customer";

        // Keeps original JSON so we can re-use unchanged fields when building PUT body
        private JsonElement _originalRoot;
        private bool _hasOriginal;

        // Tracks a newly picked local photo path (not yet uploaded)
        private string _newLocalPhotoPath;

        public EditProfilePage(AuthService authService, ApiConfig apiConfig)
        {
            InitializeComponent();
            _authService = authService;
            _apiConfig = apiConfig;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            ConfigureForRole();
            await LoadDataAsync();
        }

        // ── Role configuration ────────────────────────────────────────

        private void ConfigureForRole()
        {
            var role = (Role ?? "customer").ToLower();

            PageTitleLabel.Text = role switch
            {
                "artisan" => "Edit Business Profile",
                "admin" => "Edit Profile",
                _ => "Edit Profile"
            };

            // Photo section visible for all
            PhotoSection.IsVisible = false; // shown after load

            // Show the correct section
            PersonalInfoSection.IsVisible = false;
            ArtisanInfoSection.IsVisible = false;
            SaveButton.IsVisible = false;
        }

        // ── Data loading ──────────────────────────────────────────────

        private async Task LoadDataAsync()
        {
            try
            {
                PageLoadingIndicator.IsRunning = true;
                PageLoadingIndicator.IsVisible = true;

                var token = await _authService.GetTokenAsync();
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_apiConfig.BaseUrl.TrimEnd('/')}/api/ProfilesApi/MyProfile";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Error", "Could not load profile data.", "OK");
                    return;
                }

                var json = await response.Content.ReadAsStringAsync();
                _originalRoot = JsonDocument.Parse(json).RootElement.Clone();
                _hasOriginal = true;

                var role = (Role ?? "customer").ToLower();

                if (role == "artisan")
                    PopulateArtisanFields(_originalRoot);
                else
                    PopulatePersonalFields(_originalRoot);

                PopulatePhoto(_originalRoot);

                PhotoSection.IsVisible = true;
                SaveButton.IsVisible = true;

                if (role == "artisan")
                    ArtisanInfoSection.IsVisible = true;
                else
                    PersonalInfoSection.IsVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EDIT PROFILE] Load error: {ex.Message}");
                await DisplayAlert("Error", "Failed to load profile.", "OK");
            }
            finally
            {
                PageLoadingIndicator.IsRunning = false;
                PageLoadingIndicator.IsVisible = false;
            }
        }

        // ── Field population ──────────────────────────────────────────

        private void PopulatePersonalFields(JsonElement root)
        {
            var up = root.TryGetProperty("userProfile", out var u) ? (JsonElement?)u : null;

            FullNameEntry.Text = up?.TryGetProperty("fullName", out var fn) == true ? fn.GetString() : "";
            BioEditor.Text = up?.TryGetProperty("bio", out var b) == true ? b.GetString() : "";
            AddressEntry.Text = up?.TryGetProperty("address", out var a) == true ? a.GetString() : "";
            CityEntry.Text = up?.TryGetProperty("city", out var c) == true ? c.GetString() : "";
            StateEntry.Text = up?.TryGetProperty("state", out var s) == true ? s.GetString() : "";
            CountryEntry.Text = up?.TryGetProperty("country", out var co) == true ? co.GetString() : "";
            PostalCodeEntry.Text = up?.TryGetProperty("postalCode", out var pc) == true ? pc.GetString() : "";

            EditInitialsLabel.Text = GetInitials(FullNameEntry.Text);
        }

        private void PopulateArtisanFields(JsonElement root)
        {
            var ap = root.TryGetProperty("artisanProfile", out var a) &&
                     a.ValueKind != JsonValueKind.Null ? (JsonElement?)a : null;

            BusinessNameEntry.Text = ap?.TryGetProperty("businessName", out var bn) == true ? bn.GetString() : "";
            SpecializationEntry.Text = ap?.TryGetProperty("specialization", out var sp) == true ? sp.GetString() : "";
            ArtisanSpecialityEntry.Text = ap?.TryGetProperty("artisanSpeciality", out var asf) == true ? asf.GetString() : "";
            AboutEditor.Text = ap?.TryGetProperty("about", out var ab) == true ? ab.GetString() : "";
            ProfessionalBioEditor.Text = ap?.TryGetProperty("professionalBio", out var pb) == true ? pb.GetString() : "";
            ServicesOfferedEditor.Text = ap?.TryGetProperty("servicesOffered", out var svc) == true ? svc.GetString() : "";
            BusinessAddressEntry.Text = ap?.TryGetProperty("businessAddress", out var ba) == true ? ba.GetString() : "";

            if (ap?.TryGetProperty("hourlyRate", out var hr) == true && hr.ValueKind != JsonValueKind.Null)
                HourlyRateEntry.Text = hr.GetDecimal().ToString("F2");

            if (ap?.TryGetProperty("yearsOfExperience", out var yr) == true)
                YearsExperienceEntry.Text = yr.GetInt32().ToString();

            if (ap?.TryGetProperty("serviceRadius", out var sr) == true && sr.ValueKind != JsonValueKind.Null)
                ServiceRadiusEntry.Text = sr.GetInt32().ToString();

            // Pickers
            var expLevel = ap?.TryGetProperty("experienceLevel", out var el) == true ? el.GetString() : "BEGINNER";
            ExperienceLevelPicker.SelectedItem = expLevel;

            var avail = ap?.TryGetProperty("availabilityStatus", out var av) == true ? av.GetString() : "AVAILABLE";
            AvailabilityPicker.SelectedItem = avail;

            // Show initials using business name as fallback
            EditInitialsLabel.Text = GetInitials(BusinessNameEntry.Text);
        }

        private void PopulatePhoto(JsonElement root)
        {
            var up = root.TryGetProperty("userProfile", out var u) ? (JsonElement?)u : null;
            var photoUrl = up?.TryGetProperty("profilePictureUrl", out var p) == true ? p.GetString() : null;

            if (!string.IsNullOrWhiteSpace(photoUrl))
                TryLoadPhoto(photoUrl);
            else
                ShowInitials();
        }

        // ── Save ──────────────────────────────────────────────────────

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            try
            {
                SaveButton.IsEnabled = false;
                SaveIndicator.IsRunning = true;
                SaveIndicator.IsVisible = true;

                var role = (Role ?? "customer").ToLower();

                object payload = role == "artisan"
                    ? BuildArtisanPayload()
                    : BuildPersonalPayload();

                var token = await _authService.GetTokenAsync();
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var body = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var url = $"{_apiConfig.BaseUrl.TrimEnd('/')}/api/ProfilesApi";
                var response = await client.PutAsync(url, body);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Success", "Profile updated successfully!", "OK");
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[EDIT PROFILE] Save failed: {responseJson}");
                    await DisplayAlert("Error", "Failed to save changes. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EDIT PROFILE] Save error: {ex.Message}");
                await DisplayAlert("Error", "An unexpected error occurred.", "OK");
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SaveIndicator.IsRunning = false;
                SaveIndicator.IsVisible = false;
            }
        }

        // ── Payload builders ──────────────────────────────────────────

        /// <summary>
        /// Builds the PUT body for Customer / Admin.
        /// Preserves existing artisanProfile (null or original) and email/phone from the original.
        /// </summary>
        private object BuildPersonalPayload()
        {
            // Carry over email/phone from original load
            var email = _hasOriginal && _originalRoot.TryGetProperty("email", out var em)
                ? em.GetString() : "";
            var phone = _hasOriginal && _originalRoot.TryGetProperty("phoneNumber", out var ph)
                ? ph.GetString() : "";

            return new
            {
                email,
                phoneNumber = phone,
                userProfile = new
                {
                    fullName = FullNameEntry.Text?.Trim(),
                    bio = BioEditor.Text?.Trim(),
                    address = AddressEntry.Text?.Trim(),
                    city = CityEntry.Text?.Trim(),
                    state = StateEntry.Text?.Trim(),
                    country = CountryEntry.Text?.Trim(),
                    postalCode = PostalCodeEntry.Text?.Trim(),
                    // Preserve existing photo URL — actual photo upload is a separate concern
                    profilePictureUrl = GetExistingPhotoUrl()
                },
                artisanProfile = (object)null
            };
        }

        /// <summary>
        /// Builds the PUT body for Artisan.
        /// Preserves existing userProfile fields and email/phone from the original.
        /// </summary>
        private object BuildArtisanPayload()
        {
            var email = _hasOriginal && _originalRoot.TryGetProperty("email", out var em)
                ? em.GetString() : "";
            var phone = _hasOriginal && _originalRoot.TryGetProperty("phoneNumber", out var ph)
                ? ph.GetString() : "";

            // Preserve existing userProfile unchanged
            string fullName = null, bio = null, address = null, city = null,
                   state = null, country = null, postalCode = null, existingPhotoUrl = null;

            if (_hasOriginal && _originalRoot.TryGetProperty("userProfile", out var up) &&
                up.ValueKind != JsonValueKind.Null)
            {
                fullName = up.TryGetProperty("fullName", out var fn) ? fn.GetString() : null;
                bio = up.TryGetProperty("bio", out var b) ? b.GetString() : null;
                address = up.TryGetProperty("address", out var a) ? a.GetString() : null;
                city = up.TryGetProperty("city", out var c) ? c.GetString() : null;
                state = up.TryGetProperty("state", out var s) ? s.GetString() : null;
                country = up.TryGetProperty("country", out var co) ? co.GetString() : null;
                postalCode = up.TryGetProperty("postalCode", out var pc) ? pc.GetString() : null;
                existingPhotoUrl = up.TryGetProperty("profilePictureUrl", out var pu) ? pu.GetString() : null;
            }

            decimal.TryParse(HourlyRateEntry.Text, out var hourlyRate);
            int.TryParse(YearsExperienceEntry.Text, out var yearsExp);
            int.TryParse(ServiceRadiusEntry.Text, out var serviceRadius);

            return new
            {
                email,
                phoneNumber = phone,
                userProfile = new
                {
                    fullName,
                    bio,
                    address,
                    city,
                    state,
                    country,
                    postalCode,
                    profilePictureUrl = existingPhotoUrl
                },
                artisanProfile = new
                {
                    businessName = BusinessNameEntry.Text?.Trim(),
                    specialization = SpecializationEntry.Text?.Trim(),
                    artisanSpeciality = ArtisanSpecialityEntry.Text?.Trim(),
                    about = AboutEditor.Text?.Trim(),
                    professionalBio = ProfessionalBioEditor.Text?.Trim(),
                    servicesOffered = ServicesOfferedEditor.Text?.Trim(),
                    businessAddress = BusinessAddressEntry.Text?.Trim(),
                    hourlyRate = hourlyRate > 0 ? (decimal?)hourlyRate : null,
                    yearsOfExperience = yearsExp,
                    serviceRadius = serviceRadius > 0 ? (int?)serviceRadius : null,
                    experienceLevel = ExperienceLevelPicker.SelectedItem?.ToString() ?? "BEGINNER",
                    availabilityStatus = AvailabilityPicker.SelectedItem?.ToString() ?? "AVAILABLE"
                }
            };
        }

        // ── Photo helpers ─────────────────────────────────────────────

        private async void OnChangePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select profile photo"
                });
                if (result == null) return;

                _newLocalPhotoPath = result.FullPath;
                TryLoadPhoto(_newLocalPhotoPath);

                // TODO: upload stream to API and store returned URL
                // using var stream = await result.OpenReadAsync();
                // var uploadedUrl = await _apiService.UploadProfilePictureAsync(stream);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EDIT PROFILE] Photo pick error: {ex.Message}");
            }
        }

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
                else if (path.StartsWith("/") || path.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                {
                    var fullUrl = $"{_apiConfig.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
                    source = ImageSource.FromUri(new Uri(fullUrl));
                }
                else
                {
                    source = ImageSource.FromFile(path);
                }
                EditPhoto.Source = source;
                EditPhotoFrame.IsVisible = true;
                EditInitialsFrame.IsVisible = false;
            }
            catch
            {
                ShowInitials();
            }
        }

        private void ShowInitials()
        {
            EditPhotoFrame.IsVisible = false;
            EditInitialsFrame.IsVisible = true;
        }

        private string GetExistingPhotoUrl()
        {
            if (_hasOriginal &&
                _originalRoot.TryGetProperty("userProfile", out var up) &&
                up.ValueKind != JsonValueKind.Null &&
                up.TryGetProperty("profilePictureUrl", out var pu))
                return pu.GetString();
            return null;
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpper()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        // ── Navigation ────────────────────────────────────────────────

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}