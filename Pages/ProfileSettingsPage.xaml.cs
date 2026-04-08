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
    /// Account settings page — same for all roles.
    /// Sections:
    ///   1. Change password  (POST api/Auth/ChangePassword or equivalent)
    ///   2. Preferences      (language + timezone → saved via PUT api/ProfilesApi)
    /// </summary>
    public partial class ProfileSettingsPage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly ApiConfig _apiConfig;

        // Cached original values so we can build a minimal PUT body
        private string _originalLanguage;
        private string _originalTimezone;
        private string _originalEmail;
        private string _originalPhone;

        public ProfileSettingsPage(AuthService authService, ApiConfig apiConfig)
        {
            InitializeComponent();
            _authService = authService;
            _apiConfig = apiConfig;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadPreferencesAsync();
        }

        // ── Load existing preferences ─────────────────────────────────

        private async Task LoadPreferencesAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var url = $"{_apiConfig.BaseUrl.TrimEnd('/')}/api/ProfilesApi/MyProfile";
                var response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode) return;

                var json = await response.Content.ReadAsStringAsync();
                var root = JsonDocument.Parse(json).RootElement;

                _originalEmail = root.TryGetProperty("email", out var em) ? em.GetString() : "";
                _originalPhone = root.TryGetProperty("phoneNumber", out var ph) ? ph.GetString() : "";

                var up = root.TryGetProperty("userProfile", out var u) ? (JsonElement?)u : null;
                _originalLanguage = up?.TryGetProperty("preferredLanguage", out var lang) == true ? lang.GetString() : null;
                _originalTimezone = up?.TryGetProperty("timezone", out var tz) == true ? tz.GetString() : null;

                // Pre-select pickers
                if (!string.IsNullOrWhiteSpace(_originalLanguage))
                    LanguagePicker.SelectedItem = _originalLanguage;

                if (!string.IsNullOrWhiteSpace(_originalTimezone))
                    TimezonePicker.SelectedItem = _originalTimezone;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PROFILE SETTINGS] Load error: {ex.Message}");
            }
        }

        // ── Password ──────────────────────────────────────────────────

        private void OnNewPasswordChanged(object sender, TextChangedEventArgs e)
            => UpdatePasswordMatchLabel();

        private void OnConfirmPasswordChanged(object sender, TextChangedEventArgs e)
            => UpdatePasswordMatchLabel();

        private void UpdatePasswordMatchLabel()
        {
            var newPwd = NewPasswordEntry.Text;
            var confirmPwd = ConfirmPasswordEntry.Text;

            if (string.IsNullOrEmpty(newPwd) && string.IsNullOrEmpty(confirmPwd))
            {
                PasswordMatchLabel.IsVisible = false;
                return;
            }

            PasswordMatchLabel.IsVisible = true;

            if (newPwd == confirmPwd)
            {
                PasswordMatchLabel.Text = "✓ Passwords match";
                PasswordMatchLabel.TextColor = Color.FromArgb("#10B981");
            }
            else
            {
                PasswordMatchLabel.Text = "✗ Passwords do not match";
                PasswordMatchLabel.TextColor = Color.FromArgb("#EF4444");
            }
        }

        private async void OnUpdatePasswordClicked(object sender, EventArgs e)
        {
            var current = CurrentPasswordEntry.Text?.Trim();
            var newPwd = NewPasswordEntry.Text;
            var confirm = ConfirmPasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(current))
            {
                await DisplayAlert("Validation", "Please enter your current password.", "OK");
                return;
            }
            if (string.IsNullOrWhiteSpace(newPwd) || newPwd.Length < 8)
            {
                await DisplayAlert("Validation", "New password must be at least 8 characters.", "OK");
                return;
            }
            if (newPwd != confirm)
            {
                await DisplayAlert("Validation", "New passwords do not match.", "OK");
                return;
            }

            try
            {
                SaveIndicator.IsRunning = true;
                SaveIndicator.IsVisible = true;

                var token = await _authService.GetTokenAsync();
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var payload = new { currentPassword = current, newPassword = newPwd };
                var body = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                // Adjust endpoint to match your actual ChangePassword API route
                var url = $"{_apiConfig.BaseUrl.TrimEnd('/')}/api/Auth/ChangePassword";
                var response = await client.PostAsync(url, body);

                if (response.IsSuccessStatusCode)
                {
                    await DisplayAlert("Success", "Password updated successfully!", "OK");
                    CurrentPasswordEntry.Text = "";
                    NewPasswordEntry.Text = "";
                    ConfirmPasswordEntry.Text = "";
                    PasswordMatchLabel.IsVisible = false;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[PROFILE SETTINGS] Password change failed: {error}");
                    await DisplayAlert("Error", "Failed to update password. Please check your current password.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PROFILE SETTINGS] Password error: {ex.Message}");
                await DisplayAlert("Error", "An unexpected error occurred.", "OK");
            }
            finally
            {
                SaveIndicator.IsRunning = false;
                SaveIndicator.IsVisible = false;
            }
        }

        // ── Preferences ───────────────────────────────────────────────

        private async void OnSavePreferencesClicked(object sender, EventArgs e)
        {
            var language = LanguagePicker.SelectedItem?.ToString();
            var timezone = TimezonePicker.SelectedItem?.ToString();

            if (string.IsNullOrWhiteSpace(language) && string.IsNullOrWhiteSpace(timezone))
            {
                await DisplayAlert("Validation", "Please select at least one preference to save.", "OK");
                return;
            }

            try
            {
                SaveIndicator.IsRunning = true;
                SaveIndicator.IsVisible = true;

                var token = await _authService.GetTokenAsync();
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                // PUT body — preserve all other userProfile fields, only update lang/timezone
                var payload = new
                {
                    email = _originalEmail,
                    phoneNumber = _originalPhone,
                    userProfile = new
                    {
                        preferredLanguage = language ?? _originalLanguage,
                        timezone = timezone ?? _originalTimezone
                    },
                    artisanProfile = (object)null
                };

                var body = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                var url = $"{_apiConfig.BaseUrl.TrimEnd('/')}/api/ProfilesApi";
                var response = await client.PutAsync(url, body);

                if (response.IsSuccessStatusCode)
                {
                    _originalLanguage = language ?? _originalLanguage;
                    _originalTimezone = timezone ?? _originalTimezone;
                    await DisplayAlert("Success", "Preferences saved!", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to save preferences.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PROFILE SETTINGS] Preferences error: {ex.Message}");
                await DisplayAlert("Error", "An unexpected error occurred.", "OK");
            }
            finally
            {
                SaveIndicator.IsRunning = false;
                SaveIndicator.IsVisible = false;
            }
        }

        // ── Navigation ────────────────────────────────────────────────

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}