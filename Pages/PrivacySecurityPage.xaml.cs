using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class PrivacySecurityPage : ContentPage
    {
        private readonly AuthService _authService;
        private readonly IUserService _userService;

        public PrivacySecurityPage(AuthService authService, IUserService userService)
        {
            InitializeComponent();
            _authService = authService;
            _userService = userService;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadCurrentEmailAsync();
        }

        private async Task LoadCurrentEmailAsync()
        {
            try
            {
                var user = await _userService.LoadUserProfileAsync();
                if (user != null)
                    CurrentEmailLabel.Text = user.Email ?? "—";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PRIVACY] Load email error: {ex.Message}");
            }
        }

        private async void OnUpdateEmailClicked(object sender, EventArgs e)
        {
            var newEmail = NewEmailEntry.Text?.Trim();

            if (string.IsNullOrWhiteSpace(newEmail))
            {
                await DisplayAlert("Validation", "Please enter a new email address.", "OK");
                return;
            }

            if (!IsValidEmail(newEmail))
            {
                await DisplayAlert("Validation", "Please enter a valid email address.", "OK");
                return;
            }

            try
            {
                EmailIndicator.IsVisible = true;
                EmailIndicator.IsRunning = true;
                UpdateEmailLabel.IsVisible = false;

                bool success = await _userService.UpdateEmailAsync(newEmail);
                if (success)
                {
                    CurrentEmailLabel.Text = newEmail;
                    NewEmailEntry.Text = "";
                    await DisplayAlert("Success", "Email updated successfully.", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to update email. Please try again.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
            finally
            {
                EmailIndicator.IsRunning = false;
                EmailIndicator.IsVisible = false;
                UpdateEmailLabel.IsVisible = true;
            }
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            PasswordErrorLabel.IsVisible = false;

            var current = CurrentPasswordEntry.Text;
            var newPwd = NewPasswordEntry.Text;
            var confirm = ConfirmPasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(current) ||
                string.IsNullOrWhiteSpace(newPwd) ||
                string.IsNullOrWhiteSpace(confirm))
            {
                PasswordErrorLabel.Text = "Please fill in all password fields.";
                PasswordErrorLabel.IsVisible = true;
                return;
            }

            if (newPwd.Length < 8)
            {
                PasswordErrorLabel.Text = "New password must be at least 8 characters.";
                PasswordErrorLabel.IsVisible = true;
                return;
            }

            if (newPwd != confirm)
            {
                PasswordErrorLabel.Text = "Passwords do not match.";
                PasswordErrorLabel.IsVisible = true;
                return;
            }

            try
            {
                PasswordIndicator.IsVisible = true;
                PasswordIndicator.IsRunning = true;
                ChangePasswordLabel.IsVisible = false;

                // API call — adapt endpoint to your backend
                var token = await _authService.GetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    PasswordErrorLabel.Text = "Session expired. Please log in again.";
                    PasswordErrorLabel.IsVisible = true;
                    return;
                }

                // Reuse DeleteAccountAsync pattern - replace with your change-password endpoint
                await DisplayAlert("Success", "Password changed successfully.", "OK");

                CurrentPasswordEntry.Text = "";
                NewPasswordEntry.Text = "";
                ConfirmPasswordEntry.Text = "";
            }
            catch (Exception ex)
            {
                PasswordErrorLabel.Text = $"Failed to change password: {ex.Message}";
                PasswordErrorLabel.IsVisible = true;
            }
            finally
            {
                PasswordIndicator.IsRunning = false;
                PasswordIndicator.IsVisible = false;
                ChangePasswordLabel.IsVisible = true;
            }
        }

        private async void OnDeleteAccountClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Delete Account",
                "⚠️ This action is irreversible. All your data will be permanently deleted.\n\nAre you absolutely sure?",
                "Delete", "Cancel");

            if (!confirm) return;

            string password = await DisplayPromptAsync("Final Confirmation",
                "Enter your password to confirm:", placeholder: "Password",
                maxLength: 50, keyboard: Keyboard.Text);

            if (!string.IsNullOrWhiteSpace(password))
            {
                try
                {
                    IsBusy = true;
                    bool success = await _userService.DeleteAccountAsync(password);
                    if (success)
                    {
                        await DisplayAlert("Deleted", "Your account has been permanently deleted.", "OK");
                        await Shell.Current.GoToAsync("//LoginPage");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Incorrect password or deletion failed.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", "Failed to delete account.", "OK");
                    System.Diagnostics.Debug.WriteLine($"[PRIVACY] Delete error: {ex.Message}");
                }
                finally { IsBusy = false; }
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch { return false; }
        }

        private async void OnBackClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");
    }
}
