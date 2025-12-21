using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CraftConnect_Mobile_App.Models;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App.Pages;

public partial class SettingsPageViewModel : ObservableObject
{
    private readonly IUserService _userService;
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private UserProfile _currentUser;

    [ObservableProperty]
    private string _userName;

    [ObservableProperty]
    private string _userEmail;

    [ObservableProperty]
    private string _userRole;

    [ObservableProperty]
    private string _phoneNumber;

    [ObservableProperty]
    private string _businessName;

    [ObservableProperty]
    private string _specialization;

    [ObservableProperty]
    private string _availabilityStatus;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private bool _notificationsEnabled;

    [ObservableProperty]
    private bool _emailNotificationsEnabled;

    [ObservableProperty]
    private bool _showArtisanSection;

    [ObservableProperty]
    private bool _showAdminSection;

    [ObservableProperty]
    private string _appVersion;

    [ObservableProperty]
    private string _languageText;

    public SettingsPageViewModel(IUserService userService, INavigationService navigationService, IDialogService dialogService)
    {
        _userService = userService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        // Initialize with default values
        AppVersion = "v1.0.0";
        NotificationsEnabled = true;
        EmailNotificationsEnabled = true;
        IsAvailable = true;
        AvailabilityStatus = "Available";
        LanguageText = "English";

        LoadUserData();
    }

    private void LoadUserData()
    {
        CurrentUser = _userService.GetCurrentUser();

        if (CurrentUser != null)
        {
            UserName = CurrentUser.FullName;
            UserEmail = CurrentUser.Email;
            UserRole = CurrentUser.Role;
            PhoneNumber = CurrentUser.PhoneNumber;

            // Show/hide sections based on user role
            ShowArtisanSection = CurrentUser.Role == "Artisan" || CurrentUser.Role == "Admin";
            ShowAdminSection = CurrentUser.Role == "Admin";

            // Load artisan-specific data if applicable
            if (CurrentUser is ArtisanUser artisanUser)
            {
                BusinessName = artisanUser.BusinessName;
                Specialization = string.Join(", ", artisanUser.Specializations);
                IsAvailable = artisanUser.IsAvailable;
                AvailabilityStatus = artisanUser.IsAvailable ? "Available" : "Unavailable";
            }
        }
        else
        {
            // Default values for demo
            UserName = "John Doe";
            UserEmail = "john.doe@example.com";
            UserRole = "Artisan";
            PhoneNumber = "+233 24 123 4567";
            BusinessName = "Artisan Services Ltd.";
            Specialization = "Carpentry, Plumbing";
            ShowArtisanSection = true;
            ShowAdminSection = false;
        }
    }

    [RelayCommand]
    private async Task EditProfile()
    {
        await _navigationService.NavigateToAsync("EditProfilePage");
    }

    [RelayCommand]
    private async Task EditEmail()
    {
        var result = await _dialogService.ShowPromptAsync(
            "Update Email",
            "Enter your new email address:",
            initialValue: UserEmail,
            keyboard: Keyboard.Email);

        if (!string.IsNullOrWhiteSpace(result))
        {
            if (IsValidEmail(result))
            {
                var success = await _userService.UpdateEmailAsync(result);
                if (success)
                {
                    UserEmail = result;
                    await _dialogService.ShowToastAsync("Email updated successfully!");
                }
                else
                {
                    await _dialogService.ShowAlertAsync("Error", "Failed to update email. Please try again.");
                }
            }
            else
            {
                await _dialogService.ShowAlertAsync("Invalid Email", "Please enter a valid email address.");
            }
        }
    }

    [RelayCommand]
    private async Task EditPhone()
    {
        var result = await _dialogService.ShowPromptAsync(
            "Update Phone Number",
            "Enter your new phone number:",
            initialValue: PhoneNumber,
            keyboard: Keyboard.Telephone);

        if (!string.IsNullOrWhiteSpace(result))
        {
            var success = await _userService.UpdatePhoneNumberAsync(result);
            if (success)
            {
                PhoneNumber = result;
                await _dialogService.ShowToastAsync("Phone number updated successfully!");
            }
            else
            {
                await _dialogService.ShowAlertAsync("Error", "Failed to update phone number. Please try again.");
            }
        }
    }

    [RelayCommand]
    private async Task ChangePassword()
    {
        await _navigationService.NavigateToAsync("ChangePasswordPage");
    }

    [RelayCommand]
    private async Task EditBusinessProfile()
    {
        await _navigationService.NavigateToAsync("BusinessProfilePage");
    }

    [RelayCommand]
    private async Task EditSpecialization()
    {
        var specializations = new[]
        {
            "Carpentry",
            "Plumbing",
            "Electrical",
            "Masonry",
            "Painting",
            "Welding",
            "Tailoring",
            "Hairdressing"
        };

        var currentSpecs = Specialization?.Split(',').Select(s => s.Trim()).ToArray() ?? Array.Empty<string>();

        var selected = await _dialogService.ShowMultiSelectAsync(
            "Select Specializations",
            specializations,
            currentSpecs);

        if (selected != null && selected.Length > 0)
        {
            Specialization = string.Join(", ", selected);

            if (CurrentUser is ArtisanUser artisanUser)
            {
                artisanUser.Specializations = selected.ToList();
                await _userService.UpdateUserAsync(artisanUser);
            }
        }
    }

    [RelayCommand]
    private async Task ToggleAvailability()
    {
        IsAvailable = !IsAvailable;
        AvailabilityStatus = IsAvailable ? "Available" : "Unavailable";

        if (CurrentUser is ArtisanUser artisanUser)
        {
            artisanUser.IsAvailable = IsAvailable;
            await _userService.UpdateUserAsync(artisanUser);
        }

        var status = IsAvailable ? "available" : "unavailable";
        await _dialogService.ShowToastAsync($"You are now {status} for work");
    }

    [RelayCommand]
    private async Task ManageUsers()
    {
        await _navigationService.NavigateToAsync("ManageUsersPage");
    }

    [RelayCommand]
    private async Task ViewReports()
    {
        await _navigationService.NavigateToAsync("SystemReportsPage");
    }

    [RelayCommand]
    private async Task ManageVerifications()
    {
        await _navigationService.NavigateToAsync("ArtisanVerificationsPage");
    }

    [RelayCommand]
    private async Task ToggleNotifications()
    {
        NotificationsEnabled = !NotificationsEnabled;
        await _userService.UpdateNotificationPreferenceAsync(NotificationsEnabled);

        var status = NotificationsEnabled ? "enabled" : "disabled";
        await _dialogService.ShowToastAsync($"Push notifications {status}");
    }

    [RelayCommand]
    private async Task ToggleEmailNotifications()
    {
        EmailNotificationsEnabled = !EmailNotificationsEnabled;
        await _userService.UpdateEmailNotificationPreferenceAsync(EmailNotificationsEnabled);

        var status = EmailNotificationsEnabled ? "enabled" : "disabled";
        await _dialogService.ShowToastAsync($"Email notifications {status}");
    }

    [RelayCommand]
    private async Task ChangeLanguage()
    {
        var languages = new[] { "English", "French", "Spanish", "Arabic" };

        var selected = await _dialogService.ShowActionSheetAsync(
            "Select Language",
            "Cancel",
            null,
            languages);

        if (selected != null && selected != "Cancel")
        {
            LanguageText = selected;
            await _dialogService.ShowToastAsync($"Language changed to {selected}");
        }
    }

    [RelayCommand]
    private async Task OpenHelp()
    {
        await _navigationService.NavigateToAsync("HelpPage");
    }

    [RelayCommand]
    private async Task OpenTerms()
    {
        await _navigationService.NavigateToAsync("TermsPage");
    }

    [RelayCommand]
    private async Task OpenPrivacy()
    {
        await _navigationService.NavigateToAsync("PrivacyPage");
    }

    [RelayCommand]
    private async Task OpenAbout()
    {
        var aboutInfo = $"CraftConnect App\nVersion: {AppVersion}\n\n© 2024 CraftConnect Ltd.\nAll rights reserved.";

        await _dialogService.ShowAlertAsync(
            "About CraftConnect",
            aboutInfo,
            "OK");
    }

    [RelayCommand]
    private async Task Logout()
    {
        var confirm = await _dialogService.ShowConfirmAsync(
            "Logout",
            "Are you sure you want to logout?",
            "Yes, Logout",
            "Cancel");

        if (confirm)
        {
            await _userService.LogoutAsync();
            await _navigationService.NavigateToAsync("//LoginPage");
        }
    }

    [RelayCommand]
    private async Task DeleteAccount()
    {
        var confirm = await _dialogService.ShowConfirmAsync(
            "Delete Account",
            "This will permanently delete your account and all associated data. This action cannot be undone.",
            "Delete Account",
            "Cancel",
            isDestructive: true);

        if (confirm)
        {
            var password = await _dialogService.ShowPromptAsync(
                "Confirm Deletion",
                "Please enter your password to confirm account deletion:",
                placeholder: "Your password",
                isPassword: true);

            if (!string.IsNullOrWhiteSpace(password))
            {
                var success = await _userService.DeleteAccountAsync(password);
                if (success)
                {
                    await _dialogService.ShowAlertAsync(
                        "Account Deleted",
                        "Your account has been successfully deleted.");
                    await _navigationService.NavigateToAsync("//LoginPage");
                }
                else
                {
                    await _dialogService.ShowAlertAsync(
                        "Deletion Failed",
                        "Incorrect password or account deletion failed.");
                }
            }
        }
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}