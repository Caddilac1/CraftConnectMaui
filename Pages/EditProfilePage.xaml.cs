using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class EditProfilePage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly IUserService _userService;
        private UserProfile _currentUser;
        private ArtisanUser _artisanUser;
        private string _role;

        // ── Passthrough properties set by UpdatesFeedPage ──────────────
        public string? ReturnFeedId { get; set; }
        public string? ReturnFeedTitle { get; set; }
        public List<(string Id, string Title)>? AllFeedsSnapshot { get; set; }

        public EditProfilePage(AuthService authService, IUserService userService)
        {
            InitializeComponent();
            _authService = authService;
            _userService = userService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDataAsync();
        }

        private async Task LoadDataAsync()
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
                    AvatarInitialsLabel.Text = GetInitials(_currentUser.FullName);
                    FullNameEntry.Text = _currentUser.FullName ?? "";
                    PhoneEntry.Text = _currentUser.PhoneNumber ?? "";
                    EmailEntry.Text = _currentUser.Email ?? "";

                    if (_role.Equals("Artisan", StringComparison.OrdinalIgnoreCase) &&
                        _currentUser is ArtisanUser artisan)
                    {
                        _artisanUser = artisan;
                        ArtisanSection.IsVisible = true;
                        BusinessNameEntry.Text = artisan.BusinessName ?? "";
                        SpecializationsEntry.Text = artisan.Specializations?.Any() == true
                            ? string.Join(", ", artisan.Specializations) : "";
                        AvailabilitySwitch.IsToggled = artisan.IsAvailable;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EDIT PROFILE] {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            ErrorLabel.IsVisible = false;

            if (string.IsNullOrWhiteSpace(FullNameEntry.Text))
            {
                ErrorLabel.Text = "Full name cannot be empty.";
                ErrorLabel.IsVisible = true;
                return;
            }

            try
            {
                SaveIndicator.IsVisible = true;
                SaveIndicator.IsRunning = true;
                SaveButtonLabel.IsVisible = false;

                // Update phone if changed
                if (!string.IsNullOrWhiteSpace(PhoneEntry.Text) &&
                    PhoneEntry.Text != _currentUser?.PhoneNumber)
                {
                    await _userService.UpdatePhoneNumberAsync(PhoneEntry.Text);
                }

                // Update artisan-specific fields
                if (_artisanUser != null)
                {
                    _artisanUser.BusinessName = BusinessNameEntry.Text?.Trim();
                    _artisanUser.Specializations = SpecializationsEntry.Text?
                        .Split(',')
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList();
                    _artisanUser.IsAvailable = AvailabilitySwitch.IsToggled;
                    await _userService.UpdateUserAsync(_artisanUser);
                }
                else if (_currentUser != null)
                {
                    _currentUser.FullName = FullNameEntry.Text?.Trim();
                    await _userService.UpdateUserAsync(_currentUser);
                }

                await DisplayAlert("Success", "Profile updated successfully.", "OK");

                // ── If we arrived here from UpdatesFeedPage (no artisan profile),
                //    push CreateProposalPage instead of going back. ──────────────
                if (!string.IsNullOrEmpty(ReturnFeedId))
                {
                    var createPage = Handler?.MauiContext?.Services
                        .GetService<CreateProposalPage>();

                    if (createPage != null)
                    {
                        createPage.AvailableProjects =
                            AllFeedsSnapshot ?? new List<(string, string)>();
                        createPage.PreselectedFeedId = ReturnFeedId;

                        System.Diagnostics.Debug.WriteLine(
                            $"[EditProfile] Profile saved — pushing CreateProposalPage. " +
                            $"ReturnFeedId: {ReturnFeedId}, Title: {ReturnFeedTitle}");

                        await Navigation.PushAsync(createPage);
                        return; // do NOT fall through to GoToAsync("..")
                    }

                    // DI resolution failed — go back gracefully
                    System.Diagnostics.Debug.WriteLine(
                        "[EditProfile] Could not resolve CreateProposalPage from DI.");
                    await DisplayAlert(
                        "Notice",
                        "Profile saved! Please find and apply to the job manually.",
                        "OK");
                }

                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                ErrorLabel.Text = "Failed to save. Please try again.";
                ErrorLabel.IsVisible = true;
                System.Diagnostics.Debug.WriteLine($"[EDIT PROFILE] Save error: {ex.Message}");
            }
            finally
            {
                SaveIndicator.IsRunning = false;
                SaveIndicator.IsVisible = false;
                SaveButtonLabel.IsVisible = true;
            }
        }

        private string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length == 1
                ? parts[0][0].ToString().ToUpper()
                : $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        private async void OnBackClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");
    }
}