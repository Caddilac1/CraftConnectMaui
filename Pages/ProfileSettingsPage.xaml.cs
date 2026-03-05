using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class ProfileSettingsPage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly IUserService _userService;
        private UserProfile _currentUser;
        private string _role;

        public ProfileSettingsPage(AuthService authService, IUserService userService)
        {
            InitializeComponent();
            _authService = authService;
            _userService = userService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadProfileAsync();
        }

        private async Task LoadProfileAsync()
        {
            try
            {
                IsBusy = true;
                _currentUser = await _userService.LoadUserProfileAsync();

                var token = await _authService.GetTokenAsync();
                _role = "Customer";
                if (!string.IsNullOrEmpty(token))
                {
                    var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                    _role = jwt.Claims.FirstOrDefault(c =>
                        c.Type == "role" ||
                        c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                        ?.Value ?? "Customer";
                }

                if (_currentUser != null)
                {
                    FullNameLabel.Text = _currentUser.FullName ?? "—";
                    FullNameHeaderLabel.Text = _currentUser.FullName ?? "User";
                    EmailLabel.Text = _currentUser.Email ?? "—";
                    PhoneLabel.Text = _currentUser.PhoneNumber ?? "Not set";
                    AccountTypeLabel.Text = GetRoleDisplay(_role);
                    RoleLabel.Text = GetRoleDisplay(_role);
                    AvatarInitialsLabel.Text = GetInitials(_currentUser.FullName);

                    if (_role.Equals("Artisan", StringComparison.OrdinalIgnoreCase) &&
                        _currentUser is ArtisanUser artisan)
                    {
                        ArtisanInfoSection.IsVisible = true;
                        BusinessNameLabel.Text = artisan.BusinessName ?? "Not set";
                        SpecializationsLabel.Text = artisan.Specializations?.Any() == true
                            ? string.Join(", ", artisan.Specializations) : "Not set";

                        AvailabilityLabel.Text = artisan.IsAvailable ? "Available" : "Unavailable";
                        AvailabilityLabel.TextColor = artisan.IsAvailable
                            ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
                        AvailabilityBadge.BackgroundColor = artisan.IsAvailable
                            ? Color.FromArgb("#D1FAE5") : Color.FromArgb("#FEE2E2");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PROFILE SETTINGS] {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpper()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        private string GetRoleDisplay(string role) => role?.ToLower() switch
        {
            "admin" => "Administrator",
            "artisan" => "Artisan",
            "customer" => "Customer",
            _ => "User"
        };

        private async void OnBackClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");

        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            try { await Shell.Current.GoToAsync("EditProfilePage"); }
            catch { await DisplayAlert("Info", "Edit Profile page is not yet available.", "OK"); }
        }
    }
}
