using Microsoft.Maui.Controls;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Services;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Controls;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class SettingsPage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly IUserService _userService;
        private readonly ApiConfig _apiConfig;

        private UserProfile _currentUser;
        private ArtisanUser _artisanUser;
        private string _primaryRole;

        private bool _suppressAvailabilityToggle;

        public SettingsPage(AuthService authService, IUserService userService, ApiConfig apiConfig)
        {
            InitializeComponent();
            _authService = authService;
            _userService = userService;
            _apiConfig = apiConfig;
        }

        // ── Lifecycle ─────────────────────────────────────────────────

        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            this.FindByName<BottomNavBar>("BottomNav")?.SyncTab("Settings");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadUserDataAsync();
        }

        // ── Data loading ─────────────────────────────────────────────

        private async Task LoadUserDataAsync()
        {
            try
            {
                IsBusy = true;

                _currentUser = await _userService.LoadUserProfileAsync();

                var token = await _authService.GetTokenAsync();
                _primaryRole = "Customer";

                if (!string.IsNullOrEmpty(token))
                {
                    var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                    _primaryRole = jwt.Claims
                        .FirstOrDefault(c =>
                            c.Type == "role" ||
                            c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                        ?.Value ?? "Customer";
                }

                if (_currentUser != null)
                {
                    UserNameLabel.Text = _currentUser.FullName ?? "User";
                    UserEmailLabel.Text = _currentUser.Email ?? "";
                    AvatarInitialsLabel.Text = GetInitials(_currentUser.FullName);

                    var imageUrl = _currentUser.ProfileImageUrl;
                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] ProfileImageUrl = '{imageUrl}'");

                    if (!string.IsNullOrWhiteSpace(imageUrl))
                        await TryLoadProfileImageAsync(imageUrl);
                    else
                        ShowInitials();

                    if (_primaryRole.Equals("Artisan", StringComparison.OrdinalIgnoreCase) &&
                        _currentUser is ArtisanUser artisan)
                    {
                        _artisanUser = artisan;

                        _suppressAvailabilityToggle = true;
                        AvailabilitySwitch.IsToggled = artisan.IsAvailable;
                        _suppressAvailabilityToggle = false;
                    }

                    ConfigureUIForRole(_primaryRole);
                }
                else
                {
                    UserNameLabel.Text = "User";
                    UserEmailLabel.Text = "";
                    AvatarInitialsLabel.Text = "?";
                    ShowInitials();
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

        // ── Profile image helpers ─────────────────────────────────────

        private async Task TryLoadProfileImageAsync(string path)
        {
            try
            {
                AvatarLoadingIndicator.IsRunning = true;
                AvatarLoadingIndicator.IsVisible = true;
                AvatarInitialsFrame.IsVisible = false;
                AvatarPhotoFrame.IsVisible = false;

                ImageSource source;

                if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    source = ImageSource.FromUri(new Uri(path));
                }
                else if (path.StartsWith("/") || path.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
                {
                    var fullUrl = $"{_apiConfig.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
                    System.Diagnostics.Debug.WriteLine($"[SETTINGS] Resolved image URL: {fullUrl}");
                    source = ImageSource.FromUri(new Uri(fullUrl));
                }
                else
                {
                    source = ImageSource.FromFile(path);
                }

                AvatarImage.Source = source;
                await Task.Delay(100);
                AvatarPhotoFrame.IsVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Image load failed: {ex.Message}");
                ShowInitials();
            }
            finally
            {
                AvatarLoadingIndicator.IsRunning = false;
                AvatarLoadingIndicator.IsVisible = false;
            }
        }

        private void OnAvatarImageLoadError(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[SETTINGS] AvatarImage load error — showing initials.");
            ShowInitials();
        }

        private void ShowInitials()
        {
            AvatarPhotoFrame.IsVisible = false;
            AvatarInitialsFrame.IsVisible = true;
        }

        private string GetInitials(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpper()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        // ── Photo picker ──────────────────────────────────────────────

        private async void OnChangePhotoClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select profile photo"
                });

                if (result == null) return;

                await TryLoadProfileImageAsync(result.FullPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Photo pick error: {ex.Message}");
            }
        }

        // ── Role-based UI ─────────────────────────────────────────────

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

        // ── ACCOUNT handlers ─────────────────────────────────────────

        /// <summary>
        /// Navigates to a dedicated read-only public profile card page.
        /// </summary>
        private async void OnMyProfileClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(MyProfilePage), new Dictionary<string, object>
                {
                    { "Role", _primaryRole }
                });
            }
            catch
            {
                await DisplayAlert("Info", "My Profile page is not yet available.", "OK");
            }
        }

        /// <summary>
        /// Navigates to an edit form whose fields adapt to the user's role:
        ///   Customer / Admin → personal info (name, bio, address, photo)
        ///   Artisan          → business info (business name, specialization, rate, etc.)
        /// </summary>
        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(EditProfilePage), new Dictionary<string, object>
                {
                    { "Role", _primaryRole }
                });
            }
            catch
            {
                await DisplayAlert("Info", "Edit Profile page is not yet available.", "OK");
            }
        }

        /// <summary>
        /// Navigates to account settings: change password, preferred language, timezone.
        /// Same for all roles.
        /// </summary>
        private async void OnProfileSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(ProfileSettingsPage));
            }
            catch
            {
                await DisplayAlert("Info", "Profile Settings page is not yet available.", "OK");
            }
        }

        private async void OnNotificationsClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("NotificationsSettingsPage"); }
            catch { await DisplayAlert("Info", "Notifications page is not yet available.", "OK"); }
        }

        // ── ARTISAN handlers ──────────────────────────────────────────

        /// <summary>
        /// Business Profile → advanced/legal fields only:
        /// license, certification, business registration, tax ID, insurance, verification status.
        /// </summary>
        private async void OnEditBusinessClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync(nameof(BusinessProfilePage));
            }
            catch
            {
                await DisplayAlert("Info", "Business Profile page is not yet available.", "OK");
            }
        }

        private async void OnAvailabilityToggled(object sender, ToggledEventArgs e)
        {
            if (_suppressAvailabilityToggle) return;

            if (_artisanUser == null)
            {
                _suppressAvailabilityToggle = true;
                AvailabilitySwitch.IsToggled = !e.Value;
                _suppressAvailabilityToggle = false;
                return;
            }

            try
            {
                _artisanUser.IsAvailable = e.Value;
                bool success = await _userService.UpdateUserAsync(_artisanUser);

                if (!success)
                {
                    await DisplayAlert("Error", "Failed to update availability.", "OK");

                    _suppressAvailabilityToggle = true;
                    AvailabilitySwitch.IsToggled = !e.Value;
                    _suppressAvailabilityToggle = false;
                    _artisanUser.IsAvailable = !e.Value;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to update availability: {ex.Message}", "OK");

                _suppressAvailabilityToggle = true;
                AvailabilitySwitch.IsToggled = !e.Value;
                _suppressAvailabilityToggle = false;
                _artisanUser.IsAvailable = !e.Value;
            }
        }

        // ── ADMIN handlers ────────────────────────────────────────────

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

        // ── SECURITY handlers ─────────────────────────────────────────

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

        // ── SUPPORT handlers ──────────────────────────────────────────

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
                "Version 1.0.0\n\n© 2024 CraftConnect\nConnecting skilled artisans with customers.\n\nAll rights reserved.",
                "OK");
        }

        // ── LOGOUT / DELETE handlers ──────────────────────────────────

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Logout",
                "Are you sure you want to logout?", "Yes", "No");
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
                "Enter your password to confirm deletion:",
                placeholder: "Password", maxLength: 50,
                keyboard: Keyboard.Text);

            if (string.IsNullOrWhiteSpace(password)) return;

            try
            {
                IsBusy = true;
                bool success = await _userService.DeleteAccountAsync(password);

                if (success)
                {
                    await DisplayAlert("Account Deleted",
                        "Your account has been permanently deleted.", "OK");
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    await DisplayAlert("Error",
                        "Failed to delete account. Please check your password.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    "Failed to delete account. Please check your password.", "OK");
                System.Diagnostics.Debug.WriteLine($"[SETTINGS] Delete account error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }
    }
}