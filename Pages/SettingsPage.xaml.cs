using Microsoft.Maui.Controls;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Services;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class SettingsPage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly IUserService _userService;
        private UserProfile _currentUser;
        private ArtisanUser _artisanUser;
        private string _primaryRole;

        public SettingsPage(AuthService authService, IUserService userService)
        {
            InitializeComponent();
            _authService = authService;
            _userService = userService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadUserDataAsync();
        }

        private async Task LoadUserDataAsync()
        {
            try
            {
                IsBusy = true;

                // Load user profile from API
                _currentUser = await _userService.LoadUserProfileAsync();

                // Get role from JWT token
                var token = await _authService.GetTokenAsync();
                string role = "Customer";

                if (!string.IsNullOrEmpty(token))
                {
                    var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                    role = jwt.Claims.FirstOrDefault(c =>
                        c.Type == "role" ||
                        c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                        ?.Value ?? "Customer";
                }

                if (_currentUser != null)
                {
                    UserNameLabel.Text = _currentUser.FullName ?? "User";
                    UserEmailLabel.Text = _currentUser.Email ?? "";
                    EmailLabel.Text = _currentUser.Email ?? "";
                    PhoneLabel.Text = _currentUser.PhoneNumber ?? "Not set";

                    _primaryRole = role;
                    UserRoleLabel.Text = GetRoleDisplayName(_primaryRole);

                    if (_primaryRole.Equals("Artisan", StringComparison.OrdinalIgnoreCase) &&
                        _currentUser is ArtisanUser artisan)
                    {
                        _artisanUser = artisan;

                        BusinessNameLabel.Text = artisan.BusinessName ?? "Not set";
                        SpecializationLabel.Text = artisan.Specializations?.Any() == true
                            ? string.Join(", ", artisan.Specializations)
                            : "Not set";

                        AvailabilitySwitch.IsToggled = artisan.IsAvailable;
                        AvailabilityLabel.Text = artisan.IsAvailable ? "Available" : "Unavailable";
                        AvailabilityLabel.TextColor = artisan.IsAvailable
                            ? Color.FromArgb("#10B981")
                            : Color.FromArgb("#EF4444");
                    }

                    ConfigureUIForRole(_primaryRole);
                }
                else
                {
                    await DisplayAlert("Error", "Unable to load user profile. Please try logging in again.", "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error loading user data: {ex.Message}");
                await DisplayAlert("Error", "Failed to load user data. Please try again.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string GetRoleDisplayName(string role) => role?.ToLower() switch
        {
            "admin" => "Administrator",
            "artisan" => "Artisan",
            "customer" => "Customer",
            _ => "User"
        };

        private void ConfigureUIForRole(string role)
        {
            HideAllRoleSpecificSections();

            switch (role?.ToLower())
            {
                case "artisan": ShowArtisanSections(); break;
                case "admin": ShowAdminSections(); break;
            }
        }

        private void HideAllRoleSpecificSections()
        {
            ArtisanSectionHeader.IsVisible = false;
            BusinessProfileFrame.IsVisible = false;
            SpecializationFrame.IsVisible = false;
            AvailabilityFrame.IsVisible = false;
            AdminSectionHeader.IsVisible = false;
            ManageUsersFrame.IsVisible = false;
            SystemReportsFrame.IsVisible = false;
            VerificationFrame.IsVisible = false;
        }

        private void ShowArtisanSections()
        {
            ArtisanSectionHeader.IsVisible = true;
            BusinessProfileFrame.IsVisible = true;
            SpecializationFrame.IsVisible = true;
            AvailabilityFrame.IsVisible = true;
        }

        private void ShowAdminSections()
        {
            AdminSectionHeader.IsVisible = true;
            ManageUsersFrame.IsVisible = true;
            SystemReportsFrame.IsVisible = true;
            VerificationFrame.IsVisible = true;
        }

        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("EditProfilePage"); }
            catch { await DisplayAlert("Info", "Profile editing page is not yet available.", "OK"); }
        }

        private async void OnEditEmailClicked(object sender, EventArgs e)
        {
            if (_currentUser == null) { await DisplayAlert("Error", "User data not loaded.", "OK"); return; }

            string result = await DisplayPromptAsync("Change Email", "Enter your new email address",
                initialValue: _currentUser.Email, keyboard: Keyboard.Email);

            if (!string.IsNullOrWhiteSpace(result) && result != _currentUser.Email)
            {
                try
                {
                    IsBusy = true;
                    bool success = await _userService.UpdateEmailAsync(result);
                    if (success)
                    {
                        _currentUser.Email = result;
                        UserEmailLabel.Text = result;
                        EmailLabel.Text = result;
                        await DisplayAlert("Success", "Email updated successfully", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to update email. Please try again.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to update email: {ex.Message}", "OK");
                }
                finally { IsBusy = false; }
            }
        }

        private async void OnEditPhoneClicked(object sender, EventArgs e)
        {
            if (_currentUser == null) { await DisplayAlert("Error", "User data not loaded.", "OK"); return; }

            string result = await DisplayPromptAsync("Change Phone", "Enter your new phone number",
                initialValue: _currentUser.PhoneNumber, keyboard: Keyboard.Telephone);

            if (!string.IsNullOrWhiteSpace(result) && result != _currentUser.PhoneNumber)
            {
                try
                {
                    IsBusy = true;
                    bool success = await _userService.UpdatePhoneNumberAsync(result);
                    if (success)
                    {
                        _currentUser.PhoneNumber = result;
                        PhoneLabel.Text = result;
                        await DisplayAlert("Success", "Phone number updated successfully", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to update phone number. Please try again.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to update phone: {ex.Message}", "OK");
                }
                finally { IsBusy = false; }
            }
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("ChangePasswordPage"); }
            catch { await DisplayAlert("Info", "Password change page is not yet available.", "OK"); }
        }

        private async void OnEditBusinessClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("BusinessProfilePage"); }
            catch { await DisplayAlert("Info", "Business profile page is not yet available.", "OK"); }
        }

        private async void OnEditSpecializationClicked(object sender, EventArgs e)
        {
            if (_artisanUser == null) { await DisplayAlert("Error", "Artisan profile not loaded.", "OK"); return; }

            string currentSpecs = _artisanUser.Specializations?.Any() == true
                ? string.Join(", ", _artisanUser.Specializations) : "";

            string result = await DisplayPromptAsync("Update Specialization",
                "Enter your specializations (comma-separated)", initialValue: currentSpecs);

            if (!string.IsNullOrWhiteSpace(result) && result != currentSpecs)
            {
                try
                {
                    IsBusy = true;
                    _artisanUser.Specializations = result.Split(',')
                        .Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

                    bool success = await _userService.UpdateUserAsync(_artisanUser);
                    if (success)
                    {
                        SpecializationLabel.Text = string.Join(", ", _artisanUser.Specializations);
                        await DisplayAlert("Success", "Specialization updated", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to update specialization.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to update specialization: {ex.Message}", "OK");
                }
                finally { IsBusy = false; }
            }
        }

        private async void OnAvailabilityToggled(object sender, ToggledEventArgs e)
        {
            if (_artisanUser == null)
            {
                await DisplayAlert("Error", "Artisan profile not loaded.", "OK");
                AvailabilitySwitch.IsToggled = !e.Value;
                return;
            }

            try
            {
                _artisanUser.IsAvailable = e.Value;
                bool success = await _userService.UpdateUserAsync(_artisanUser);

                if (success)
                {
                    AvailabilityLabel.Text = e.Value ? "Available" : "Unavailable";
                    AvailabilityLabel.TextColor = e.Value
                        ? Color.FromArgb("#10B981")
                        : Color.FromArgb("#EF4444");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to update availability status.", "OK");
                    AvailabilitySwitch.IsToggled = !e.Value;
                    _artisanUser.IsAvailable = !e.Value;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update availability: {ex.Message}", "OK");
                AvailabilitySwitch.IsToggled = !e.Value;
                _artisanUser.IsAvailable = !e.Value;
            }
        }

        private async void OnManageUsersClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("ManageUsersPage"); }
            catch { await DisplayAlert("Info", "User management page is not yet available.", "OK"); }
        }

        private async void OnViewReportsClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("ReportsPage"); }
            catch { await DisplayAlert("Info", "Reports page is not yet available.", "OK"); }
        }

        private async void OnVerificationsClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("VerificationPage"); }
            catch { await DisplayAlert("Info", "Verification page is not yet available.", "OK"); }
        }

        private async void OnNotificationsToggled(object sender, ToggledEventArgs e)
        {
            try
            {
                bool success = await _userService.UpdateNotificationPreferenceAsync(e.Value);
                if (!success)
                {
                    await DisplayAlert("Error", "Failed to update notification settings.", "OK");
                    ((Switch)sender).IsToggled = !e.Value;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update notifications: {ex.Message}", "OK");
                ((Switch)sender).IsToggled = !e.Value;
            }
        }

        private async void OnEmailNotificationsToggled(object sender, ToggledEventArgs e)
        {
            try
            {
                bool success = await _userService.UpdateEmailNotificationPreferenceAsync(e.Value);
                if (!success)
                {
                    await DisplayAlert("Error", "Failed to update email notification settings.", "OK");
                    ((Switch)sender).IsToggled = !e.Value;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update email notifications: {ex.Message}", "OK");
                ((Switch)sender).IsToggled = !e.Value;
            }
        }

        private async void OnLanguageClicked(object sender, EventArgs e)
        {
            string result = await DisplayActionSheet("Select Language", "Cancel", null,
                "English", "French", "Spanish", "German");

            if (result != "Cancel" && result != null)
                await DisplayAlert("Language", $"Language changed to {result}\n\nThis feature will be implemented in a future update.", "OK");
        }

        private async void OnHelpClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("HelpPage"); }
            catch { await DisplayAlert("Info", "Help page is not yet available.", "OK"); }
        }

        private async void OnTermsClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("TermsPage"); }
            catch { await DisplayAlert("Info", "Terms & Conditions page is not yet available.", "OK"); }
        }

        private async void OnPrivacyClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("PrivacyPage"); }
            catch { await DisplayAlert("Info", "Privacy Policy page is not yet available.", "OK"); }
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await DisplayAlert("About CraftConnect",
                "Version 1.0.0\n\n© 2024 CraftConnect\nConnecting skilled artisans with customers.\n\nAll rights reserved.", "OK");
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Logout", "Are you sure you want to logout?", "Yes", "No");
            if (!confirm) return;

            try
            {
                IsBusy = true;
                await _userService.LogoutAsync();
                await _authService.LogoutAsync();
                await Shell.Current.GoToAsync("//LoginPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", "Failed to logout. Please try again.", "OK");
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Logout error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private async void OnDeleteAccountClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Delete Account",
                "⚠️ WARNING ⚠️\n\nThis action is irreversible. All your data will be permanently deleted.\n\nAre you absolutely sure?",
                "Delete", "Cancel");

            if (!confirm) return;

            string password = await DisplayPromptAsync("Final Confirmation",
                "Enter your password to confirm deletion:", placeholder: "Password",
                maxLength: 50, keyboard: Keyboard.Text);

            if (!string.IsNullOrWhiteSpace(password))
            {
                try
                {
                    IsBusy = true;
                    bool success = await _userService.DeleteAccountAsync(password);
                    if (success)
                    {
                        await DisplayAlert("Account Deleted", "Your account has been permanently deleted", "OK");
                        await Shell.Current.GoToAsync("//LoginPage");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to delete account. Please check your password.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", "Failed to delete account. Please check your password.", "OK");
                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Delete account error: {ex.Message}");
                }
                finally { IsBusy = false; }
            }
        }
    }
}