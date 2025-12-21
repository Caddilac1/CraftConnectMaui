using Microsoft.Maui.Controls;
using System;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class SettingsPage : ContentPage
    {
        // Hardcoded user data for demonstration
        private UserRole currentUserRole = UserRole.Artisan; // Change this to test different roles
        private string userName = "John Doe";
        private string userEmail = "john.doe@example.com";
        private string userPhone = "+233 24 123 4567";
        private string businessName = "Artisan Services Ltd.";
        private string specialization = "Carpentry, Plumbing";

        public enum UserRole
        {
            Customer,
            Artisan,
            Admin,
            Moderator
        }

        public SettingsPage()
        {
            InitializeComponent();
            LoadUserData();
            ConfigureUIForRole();
        }

        private void LoadUserData()
        {
            // Load hardcoded data (later replace with actual user data)
            UserNameLabel.Text = userName;
            UserEmailLabel.Text = userEmail;
            EmailLabel.Text = userEmail;
            PhoneLabel.Text = userPhone;

            // Set role display
            UserRoleLabel.Text = GetRoleDisplayName();

            // Artisan-specific data
            if (currentUserRole == UserRole.Artisan)
            {
                BusinessNameLabel.Text = businessName;
                SpecializationLabel.Text = specialization;
            }
        }

        private string GetRoleDisplayName()
        {
            return currentUserRole switch
            {
                UserRole.Admin => "Administrator",
                UserRole.Artisan => "Artisan",
                UserRole.Moderator => "Moderator",
                UserRole.Customer => "Customer",
                _ => "User"
            };
        }

        private void ConfigureUIForRole()
        {
            // Hide all role-specific sections first
            HideAllRoleSpecificSections();

            // Show sections based on role
            switch (currentUserRole)
            {
                case UserRole.Artisan:
                    ShowArtisanSections();
                    break;

                case UserRole.Admin:
                    ShowAdminSections();
                    ShowArtisanSections(); // Admin can also be artisan
                    break;

                case UserRole.Moderator:
                    ShowModeratorSections();
                    break;

                case UserRole.Customer:
                    // Customer has only basic settings, no special sections
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

        private void ShowModeratorSections()
        {
            AdminSectionHeader.IsVisible = true;
            SystemReportsFrame.IsVisible = true;
            VerificationFrame.IsVisible = true;
        }

        // Profile Actions
        private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Edit Profile", "Navigate to profile editing page", "OK");
            // TODO: Navigate to profile edit page
        }

        // Account Actions
        private async void OnEditEmailClicked(object sender, EventArgs e)
        {
            string result = await DisplayPromptAsync(
                "Change Email",
                "Enter your new email address",
                initialValue: userEmail,
                keyboard: Keyboard.Email);

            if (!string.IsNullOrWhiteSpace(result))
            {
                userEmail = result;
                UserEmailLabel.Text = result;
                EmailLabel.Text = result;
                await DisplayAlert("Success", "Email updated successfully", "OK");
            }
        }

        private async void OnEditPhoneClicked(object sender, EventArgs e)
        {
            string result = await DisplayPromptAsync(
                "Change Phone",
                "Enter your new phone number",
                initialValue: userPhone,
                keyboard: Keyboard.Telephone);

            if (!string.IsNullOrWhiteSpace(result))
            {
                userPhone = result;
                PhoneLabel.Text = result;
                await DisplayAlert("Success", "Phone number updated successfully", "OK");
            }
        }

        private async void OnChangePasswordClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Change Password", "Navigate to password change page", "OK");
            // TODO: Navigate to change password page
        }

        // Artisan-Specific Actions
        private async void OnEditBusinessClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Business Profile", "Navigate to business profile editor", "OK");
            // TODO: Navigate to business profile page
        }

        private async void OnEditSpecializationClicked(object sender, EventArgs e)
        {
            string result = await DisplayPromptAsync(
                "Update Specialization",
                "Enter your specializations (comma-separated)",
                initialValue: specialization);

            if (!string.IsNullOrWhiteSpace(result))
            {
                specialization = result;
                SpecializationLabel.Text = result;
                await DisplayAlert("Success", "Specialization updated", "OK");
            }
        }

        private void OnAvailabilityToggled(object sender, ToggledEventArgs e)
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

            // TODO: Update availability status in backend
        }

        // Admin-Specific Actions
        private async void OnManageUsersClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Manage Users", "Navigate to user management panel", "OK");
            // TODO: Navigate to user management page
        }

        private async void OnViewReportsClicked(object sender, EventArgs e)
        {
            await DisplayAlert("View Reports", "Navigate to reports dashboard", "OK");
            // TODO: Navigate to reports page
        }

        private async void OnVerificationsClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Verifications", "Navigate to artisan verification queue", "OK");
            // TODO: Navigate to verification page
        }

        // Preferences Actions
        private void OnNotificationsToggled(object sender, ToggledEventArgs e)
        {
            // TODO: Save notification preference
            var status = e.Value ? "enabled" : "disabled";
            Console.WriteLine($"Push notifications {status}");
        }

        private void OnEmailNotificationsToggled(object sender, ToggledEventArgs e)
        {
            // TODO: Save email notification preference
            var status = e.Value ? "enabled" : "disabled";
            Console.WriteLine($"Email notifications {status}");
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
                await DisplayAlert("Language", $"Language changed to {result}", "OK");
                // TODO: Implement language change
            }
        }

        // Support & Legal Actions
        private async void OnHelpClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Help & Support", "Navigate to help center", "OK");
            // TODO: Navigate to help page or open support chat
        }

        private async void OnTermsClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Terms & Conditions", "Navigate to terms page", "OK");
            // TODO: Navigate to terms page or open web view
        }

        private async void OnPrivacyClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Privacy Policy", "Navigate to privacy policy page", "OK");
            // TODO: Navigate to privacy page or open web view
        }

        private async void OnAboutClicked(object sender, EventArgs e)
        {
            await DisplayAlert(
                "About Artisan Marketplace",
                "Version 1.0.0\n\n© 2024 Artisan Marketplace\nAll rights reserved.",
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
                // TODO: Implement logout logic
                await DisplayAlert("Logged Out", "You have been logged out successfully", "OK");
                // Navigate to login page
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
                bool doubleConfirm = await DisplayAlert(
                    "Final Confirmation",
                    "Type your password to confirm deletion:",
                    "Confirm",
                    "Cancel");

                if (doubleConfirm)
                {
                    // TODO: Implement account deletion
                    await DisplayAlert("Account Deleted", "Your account has been permanently deleted", "OK");
                }
            }
        }

        // Helper method to simulate role changes for testing
        public void SetUserRole(UserRole role)
        {
            currentUserRole = role;
            UserRoleLabel.Text = GetRoleDisplayName();
            ConfigureUIForRole();
        }
    }
}