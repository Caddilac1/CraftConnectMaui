using Microsoft.Maui.Controls;
using System;
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

                // Get current user info from AuthService for roles
                var authUser = await _authService.GetCurrentUserAsync();

                if (_currentUser != null && authUser != null)
                {
                    // Display basic info
                    UserNameLabel.Text = _currentUser.FullName ?? "User";
                    UserEmailLabel.Text = _currentUser.Email ?? "";
                    EmailLabel.Text = _currentUser.Email ?? "";
                    PhoneLabel.Text = _currentUser.PhoneNumber ?? "Not set";

                    // Get primary role
                    _primaryRole = authUser.Roles?.FirstOrDefault() ?? _currentUser.Role ?? "Customer";
                    UserRoleLabel.Text = GetRoleDisplayName(_primaryRole);

                    // Load artisan-specific data if user is artisan
                    if (_primaryRole.Equals("Artisan", StringComparison.OrdinalIgnoreCase) &&
                        _currentUser is ArtisanUser artisan)
                    {
                        _artisanUser = artisan;

                        BusinessNameLabel.Text = artisan.BusinessName ?? "Not set";
                        SpecializationLabel.Text = artisan.Specializations?.Any() == true
                            ? string.Join(", ", artisan.Specializations)
                            : "Not set";

                        AvailabilitySwitch.IsToggled = artisan.IsAvailable;

                        if (artisan.IsAvailable)
                        {
                            AvailabilityLabel.Text = "Available";
                            AvailabilityLabel.TextColor = Color.FromArgb("#10B981");
                        }
                        else
                        {
                            AvailabilityLabel.Text = "Unavailable";
                            AvailabilityLabel.TextColor = Color.FromArgb("#EF4444");
                        }
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
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Stack trace: {ex.StackTrace}");
                await DisplayAlert("Error", "Failed to load user data. Please try again.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private string GetRoleDisplayName(string role)
        {
            return role?.ToLower() switch
            {
                "admin" => "Administrator",
                "artisan" => "Artisan",
                "customer" => "Customer",
                _ => "User"
            };
        }

        private void ConfigureUIForRole(string role)
        {
            // Hide all role-specific sections first
            HideAllRoleSpecificSections();

            // Show sections based on role
            switch (role?.ToLower())
            {
                case "artisan":
                    ShowArtisanSections();
                    break;

                case "admin":
                    ShowAdminSections();
                    break;

                case "customer":
                    // Customer has only basic settings
                    break;
            }
        }

        private void HideAllRoleSpecificSections()
        {
            // Artisan sections
            ArtisanSectionHeader.IsVisible = false;
            BusinessProfileFrame.IsVisible = false;
            SpecializationFrame.IsVisible = false;
            AvailabilityFrame.IsVisible = false;

            // Admin sections
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

        // Profile Actions
        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("EditProfilePage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "Profile editing page is not yet available.", "OK");
            }
        }

        // Account Actions
        private async void OnEditEmailClicked(object sender, EventArgs e)
        {
            if (_currentUser == null)
            {
                await DisplayAlert("Error", "User data not loaded.", "OK");
                return;
            }

            string result = await DisplayPromptAsync(
                "Change Email",
                "Enter your new email address",
                initialValue: _currentUser.Email,
                keyboard: Keyboard.Email);

            if (!string.IsNullOrWhiteSpace(result) && result != _currentUser.Email)
            {
                try
                {
                    IsBusy = true;

                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Updating email to: {result}");
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
                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error updating email: {ex.Message}");
                    await DisplayAlert("Error", $"Failed to update email: {ex.Message}", "OK");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async void OnEditPhoneClicked(object sender, EventArgs e)
        {
            if (_currentUser == null)
            {
                await DisplayAlert("Error", "User data not loaded.", "OK");
                return;
            }

            string result = await DisplayPromptAsync(
                "Change Phone",
                "Enter your new phone number",
                initialValue: _currentUser.PhoneNumber,
                keyboard: Keyboard.Telephone);

            if (!string.IsNullOrWhiteSpace(result) && result != _currentUser.PhoneNumber)
            {
                try
                {
                    IsBusy = true;

                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Updating phone to: {result}");
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
                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error updating phone: {ex.Message}");
                    await DisplayAlert("Error", $"Failed to update phone: {ex.Message}", "OK");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("ChangePasswordPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "Password change page is not yet available.", "OK");
            }
        }

        // Artisan-Specific Actions
        private async void OnEditBusinessClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("BusinessProfilePage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "Business profile page is not yet available.", "OK");
            }
        }

        private async void OnEditSpecializationClicked(object sender, EventArgs e)
        {
            if (_artisanUser == null)
            {
                await DisplayAlert("Error", "Artisan profile not loaded.", "OK");
                return;
            }

            string currentSpecs = _artisanUser.Specializations?.Any() == true
                ? string.Join(", ", _artisanUser.Specializations)
                : "";

            string result = await DisplayPromptAsync(
                "Update Specialization",
                "Enter your specializations (comma-separated)",
                initialValue: currentSpecs);

            if (!string.IsNullOrWhiteSpace(result) && result != currentSpecs)
            {
                try
                {
                    IsBusy = true;

                    // Parse comma-separated specializations
                    _artisanUser.Specializations = result
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Updating specializations: {string.Join(", ", _artisanUser.Specializations)}");
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
                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error updating specialization: {ex.Message}");
                    await DisplayAlert("Error", $"Failed to update specialization: {ex.Message}", "OK");
                }
                finally
                {
                    IsBusy = false;
                }
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
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Updating availability to: {e.Value}");

                _artisanUser.IsAvailable = e.Value;
                bool success = await _userService.UpdateUserAsync(_artisanUser);

                if (success)
                {
                    if (e.Value)
                    {
                        AvailabilityLabel.Text = "Available";
                        AvailabilityLabel.TextColor = Color.FromArgb("#10B981");
                    }
                    else
                    {
                        AvailabilityLabel.Text = "Unavailable";
                        AvailabilityLabel.TextColor = Color.FromArgb("#EF4444");
                    }
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
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error updating availability: {ex.Message}");
                await DisplayAlert("Error", $"Failed to update availability: {ex.Message}", "OK");
                AvailabilitySwitch.IsToggled = !e.Value;
                _artisanUser.IsAvailable = !e.Value;
            }
        }

        // Admin-Specific Actions
        private async void OnManageUsersClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("ManageUsersPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "User management page is not yet available.", "OK");
            }
        }

        private async void OnViewReportsClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("ReportsPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "Reports page is not yet available.", "OK");
            }
        }

        private async void OnVerificationsClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("VerificationPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "Verification page is not yet available.", "OK");
            }
        }

        // Preferences Actions
        private async void OnNotificationsToggled(object sender, ToggledEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Updating push notifications to: {e.Value}");
                bool success = await _userService.UpdateNotificationPreferenceAsync(e.Value);

                if (!success)
                {
                    await DisplayAlert("Error", "Failed to update notification settings.", "OK");
                    ((Switch)sender).IsToggled = !e.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error updating notifications: {ex.Message}");
                await DisplayAlert("Error", $"Failed to update notifications: {ex.Message}", "OK");
                ((Switch)sender).IsToggled = !e.Value;
            }
        }

        private async void OnEmailNotificationsToggled(object sender, ToggledEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Updating email notifications to: {e.Value}");
                bool success = await _userService.UpdateEmailNotificationPreferenceAsync(e.Value);

                if (!success)
                {
                    await DisplayAlert("Error", "Failed to update email notification settings.", "OK");
                    ((Switch)sender).IsToggled = !e.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error updating email notifications: {ex.Message}");
                await DisplayAlert("Error", $"Failed to update email notifications: {ex.Message}", "OK");
                ((Switch)sender).IsToggled = !e.Value;
            }
        }

        private async void OnLanguageClicked(object sender, EventArgs e)
        {
            string result = await DisplayActionSheet(
                "Select Language",
                "Cancel",
                null,
                "English",
                "French",
                "Spanish",
                "German");

            if (result != "Cancel" && result != null)
            {
                await DisplayAlert("Language", $"Language changed to {result}\n\nThis feature will be implemented in a future update.", "OK");
                // TODO: Implement actual language change logic
            }
        }

        // Support & Legal Actions
        private async void OnHelpClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("HelpPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "Help page is not yet available.", "OK");
            }
        }

        private async void OnTermsClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("TermsPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "Terms & Conditions page is not yet available.", "OK");
            }
        }

        private async void OnPrivacyClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("PrivacyPage");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Navigation error: {ex.Message}");
                await DisplayAlert("Info", "Privacy Policy page is not yet available.", "OK");
            }
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await DisplayAlert(
                "About CraftConnect",
                "Version 1.0.0\n\n© 2024 CraftConnect\nConnecting skilled artisans with customers.\n\nAll rights reserved.",
                "OK");
        }

        // Account Actions
        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "Logout",
                "Are you sure you want to logout?",
                "Yes",
                "No");

            if (confirm)
            {
                try
                {
                    IsBusy = true;

                    System.Diagnostics.Debug.WriteLine("[SETTINGS] Logging out...");

                    await _userService.LogoutAsync();
                    await _authService.LogoutAsync();

                    System.Diagnostics.Debug.WriteLine("[SETTINGS] Logout successful, navigating to login...");

                    // Navigate to login page
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error during logout: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Stack trace: {ex.StackTrace}");
                    await DisplayAlert("Error", "Failed to logout. Please try again.", "OK");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        private async void OnDeleteAccountClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "Delete Account",
                "⚠️ WARNING ⚠️\n\nThis action is irreversible. All your data will be permanently deleted.\n\nAre you absolutely sure?",
                "Delete",
                "Cancel");

            if (confirm)
            {
                string password = await DisplayPromptAsync(
                    "Final Confirmation",
                    "Enter your password to confirm deletion:",
                    placeholder: "Password",
                    maxLength: 50,
                    keyboard: Keyboard.Text);

                if (!string.IsNullOrWhiteSpace(password))
                {
                    try
                    {
                        IsBusy = true;

                        System.Diagnostics.Debug.WriteLine("[SETTINGS] Attempting to delete account...");
                        bool success = await _userService.DeleteAccountAsync(password);

                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine("[SETTINGS] Account deleted successfully");
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
                        System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error deleting account: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[SETTINGS] Stack trace: {ex.StackTrace}");
                        await DisplayAlert("Error", "Failed to delete account. Please check your password.", "OK");
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                }
            }
        }
    }
}