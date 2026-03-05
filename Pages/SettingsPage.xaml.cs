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

                _currentUser = await _userService.LoadUserProfileAsync();

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

                    // Initials fallback
                    AvatarInitialsLabel.Text = GetInitials(_currentUser.FullName);

                    // ── Load real profile photo if available ──────────
                    if (!string.IsNullOrWhiteSpace(_currentUser.ProfileImageUrl))
                        ShowProfilePhoto(_currentUser.ProfileImageUrl);
                    // ─────────────────────────────────────────────────

                    _primaryRole = role;

                    if (_primaryRole.Equals("Artisan", StringComparison.OrdinalIgnoreCase) &&
                        _currentUser is ArtisanUser artisan)
                    {
                        _artisanUser = artisan;
                        AvailabilitySwitch.IsToggled = artisan.IsAvailable;
                    }

                    ConfigureUIForRole(_primaryRole);
                }
                else
                {
                    UserNameLabel.Text = "User";
                    UserEmailLabel.Text = "";
                    AvatarInitialsLabel.Text = "?";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Error loading user data: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Profile photo helpers ─────────────────────────────────────

        private void ShowProfilePhoto(string path)
        {
            AvatarImage.Source = ImageSource.FromFile(path);
            AvatarPhotoFrame.IsVisible = true;
            AvatarInitialsFrame.IsVisible = false;
        }

        private void ShowInitials()
        {
            AvatarPhotoFrame.IsVisible = false;
            AvatarInitialsFrame.IsVisible = true;
        }

        // ── Change photo (tap the 📷 badge) ──────────────────────────

        private async void OnChangePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select profile photo"
                });

                if (result != null)
                {
                    ShowProfilePhoto(result.FullPath);

                    // TODO: upload result.OpenReadAsync() to your API
                    // and save the returned URL to _currentUser.ProfilePhotoUrl
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Photo pick error: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────

        private string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        private void ConfigureUIForRole(string role)
        {
            ArtisanSectionContainer.IsVisible = false;
            AdminSectionContainer.IsVisible = false;

            switch (role?.ToLower())
            {
                case "artisan":
                    ArtisanSectionContainer.IsVisible = true;
                    break;
                case "admin":
                    ArtisanSectionContainer.IsVisible = true;
                    AdminSectionContainer.IsVisible = true;
                    break;
            }
        }

        // ── ACCOUNT ──────────────────────────────────────────────────

        private async void OnProfileSettingsClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("ProfileSettingsPage"); }
            catch { await DisplayAlert("Info", "Profile Settings page is not yet available.", "OK"); }
        }

        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("EditProfilePage"); }
            catch { await DisplayAlert("Info", "Edit Profile page is not yet available.", "OK"); }
        }

        private async void OnNotificationsClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("NotificationsSettingsPage"); }
            catch { await DisplayAlert("Info", "Notifications page is not yet available.", "OK"); }
        }

        // ── ARTISAN ───────────────────────────────────────────────────

        private async void OnEditBusinessClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("BusinessProfilePage"); }
            catch { await DisplayAlert("Info", "Business Profile page is not yet available.", "OK"); }
        }

        private async void OnAvailabilityToggled(object sender, ToggledEventArgs e)
        {
            if (_artisanUser == null)
            {
                AvailabilitySwitch.IsToggled = !e.Value;
                return;
            }

            try
            {
                _artisanUser.IsAvailable = e.Value;
                bool success = await _userService.UpdateUserAsync(_artisanUser);

                if (!success)
                {
                    await DisplayAlert("Error", "Failed to update availability.", "OK");
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

        // ── ADMIN ─────────────────────────────────────────────────────

        private async void OnManageUsersClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("ManageUsersPage"); }
            catch { await DisplayAlert("Info", "Manage Users page is not yet available.", "OK"); }
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

        // ── SECURITY ──────────────────────────────────────────────────

        private async void OnPrivacySecurityClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("PrivacySecurityPage"); }
            catch { await DisplayAlert("Info", "Privacy & Security page is not yet available.", "OK"); }
        }

        private async void OnPaymentMethodsClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("PaymentMethodsPage"); }
            catch { await DisplayAlert("Info", "Payment Methods page is not yet available.", "OK"); }
        }

        // ── SUPPORT ───────────────────────────────────────────────────

        private async void OnHelpClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("HelpPage"); }
            catch { await DisplayAlert("Info", "Help & Support page is not yet available.", "OK"); }
        }

        private async void OnTermsClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("TermsPage"); }
            catch { await DisplayAlert("Info", "Terms & Conditions page is not yet available.", "OK"); }
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await DisplayAlert("About CraftConnect",
                "Version 1.0.0\n\n© 2024 CraftConnect\nConnecting skilled artisans with customers.\n\nAll rights reserved.", "OK");
        }

        // ── LOGOUT / DELETE ───────────────────────────────────────────

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
                        await DisplayAlert("Account Deleted", "Your account has been permanently deleted.", "OK");
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