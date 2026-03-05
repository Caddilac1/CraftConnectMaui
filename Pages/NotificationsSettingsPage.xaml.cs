using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class NotificationsSettingsPage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly IUserService _userService;

        public NotificationsSettingsPage(AuthService authService, IUserService userService)
        {
            InitializeComponent();
            _authService = authService;
            _userService = userService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await ConfigureForRoleAsync();
        }

        private async Task ConfigureForRoleAsync()
        {
            try
            {
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token)) return;

                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                var role = jwt.Claims.FirstOrDefault(c =>
                    c.Type == "role" ||
                    c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                    ?.Value ?? "Customer";

                // Show job requests row for artisans only
                bool isArtisan = role.Equals("Artisan", StringComparison.OrdinalIgnoreCase);
                JobRequestsRow.IsVisible = isArtisan;
                JobRequestsDivider.IsVisible = isArtisan;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NOTIFICATIONS] {ex.Message}");
            }
        }

        private async void OnAllNotificationsToggled(object sender, ToggledEventArgs e)
        {
            // Cascade to sub-toggles
            MessagesSwitch.IsEnabled = e.Value;
            JobRequestsSwitch.IsEnabled = e.Value;
            PromotionsSwitch.IsEnabled = e.Value;

            try
            {
                bool success = await _userService.UpdateNotificationPreferenceAsync(e.Value);
                if (!success)
                {
                    await DisplayAlert("Error", "Failed to update notification settings.", "OK");
                    AllNotificationsSwitch.IsToggled = !e.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NOTIFICATIONS] Toggle error: {ex.Message}");
                AllNotificationsSwitch.IsToggled = !e.Value;
            }
        }

        private async void OnEmailNotificationsToggled(object sender, ToggledEventArgs e)
        {
            WeeklySummarySwitch.IsEnabled = e.Value;

            try
            {
                bool success = await _userService.UpdateEmailNotificationPreferenceAsync(e.Value);
                if (!success)
                {
                    await DisplayAlert("Error", "Failed to update email notification settings.", "OK");
                    EmailNotificationsSwitch.IsToggled = !e.Value;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NOTIFICATIONS] Email toggle error: {ex.Message}");
                EmailNotificationsSwitch.IsToggled = !e.Value;
            }
        }

        private async void OnBackClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");
    }
}
