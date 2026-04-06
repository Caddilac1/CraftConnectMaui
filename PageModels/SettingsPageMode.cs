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

    // ── Observable properties ─────────────────────────────────────

    [ObservableProperty] private UserProfile _currentUser;
    [ObservableProperty] private string _userName = "User";
    [ObservableProperty] private string _userEmail = "";
    [ObservableProperty] private string _userRole = "Customer";
    [ObservableProperty] private string _phoneNumber = "";
    [ObservableProperty] private string _businessName = "";
    [ObservableProperty] private string _specialization = "";

    /// <summary>
    /// Displayed availability label — kept in sync with IsAvailable.
    /// </summary>
    [ObservableProperty] private string _availabilityStatus = "Available";

    /// <summary>
    /// Bound to the availability Switch. Setting this also updates
    /// AvailabilityStatus automatically via the partial method below.
    /// </summary>
    [ObservableProperty] private bool _isAvailable = true;

    [ObservableProperty] private bool _notificationsEnabled = true;
    [ObservableProperty] private bool _emailNotificationsEnabled = true;
    [ObservableProperty] private bool _showArtisanSection;
    [ObservableProperty] private bool _showAdminSection;
    [ObservableProperty] private string _appVersion = "v1.0.0";
    [ObservableProperty] private string _languageText = "English";

    // ── Constructor ───────────────────────────────────────────────

    public SettingsPageViewModel(
        IUserService userService,
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _userService = userService;
        _navigationService = navigationService;
        _dialogService = dialogService;

        LoadUserData();
    }

    // ── Keep AvailabilityStatus label in sync with the bool ───────

    partial void OnIsAvailableChanged(bool value)
    {
        AvailabilityStatus = value ? "Available" : "Unavailable";
    }

    // ── Data loading ──────────────────────────────────────────────

    private void LoadUserData()
    {
        CurrentUser = _userService.GetCurrentUser();

        if (CurrentUser != null)
        {
            UserName = CurrentUser.FullName ?? "User";
            UserEmail = CurrentUser.Email ?? "";
            UserRole = CurrentUser.Role ?? "Customer";
            PhoneNumber = CurrentUser.PhoneNumber ?? "";

            ShowArtisanSection = UserRole is "Artisan" or "Admin";
            ShowAdminSection = UserRole == "Admin";

            if (CurrentUser is ArtisanUser artisan)
            {
                BusinessName = artisan.BusinessName ?? "";
                Specialization = string.Join(", ", artisan.Specializations ?? new List<string>());
                IsAvailable = artisan.IsAvailable;
            }
        }
        else
        {
            // Demo / design-time defaults
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

    // ── ACCOUNT commands ──────────────────────────────────────────

    [RelayCommand]
    private async Task EditProfile() =>
        await _navigationService.NavigateToAsync("EditProfilePage");

    [RelayCommand]
    private async Task EditEmail()
    {
        var result = await _dialogService.ShowPromptAsync(
            "Update Email", "Enter your new email address:",
            initialValue: UserEmail, keyboard: Keyboard.Email);

        if (string.IsNullOrWhiteSpace(result)) return;

        if (!IsValidEmail(result))
        {
            await _dialogService.ShowAlertAsync("Invalid Email",
                "Please enter a valid email address.");
            return;
        }

        bool success = await _userService.UpdateEmailAsync(result);
        if (success)
        {
            UserEmail = result;
            await _dialogService.ShowToastAsync("Email updated successfully!");
        }
        else
        {
            await _dialogService.ShowAlertAsync("Error",
                "Failed to update email. Please try again.");
        }
    }

    [RelayCommand]
    private async Task EditPhone()
    {
        var result = await _dialogService.ShowPromptAsync(
            "Update Phone Number", "Enter your new phone number:",
            initialValue: PhoneNumber, keyboard: Keyboard.Telephone);

        if (string.IsNullOrWhiteSpace(result)) return;

        bool success = await _userService.UpdatePhoneNumberAsync(result);
        if (success)
        {
            PhoneNumber = result;
            await _dialogService.ShowToastAsync("Phone number updated successfully!");
        }
        else
        {
            await _dialogService.ShowAlertAsync("Error",
                "Failed to update phone number. Please try again.");
        }
    }

    [RelayCommand]
    private async Task ChangePassword() =>
        await _navigationService.NavigateToAsync("ChangePasswordPage");

    // ── ARTISAN commands ──────────────────────────────────────────

    [RelayCommand]
    private async Task EditBusinessProfile() =>
        await _navigationService.NavigateToAsync("BusinessProfilePage");

    [RelayCommand]
    private async Task EditSpecialization()
    {
        var options = new[]
        {
            "Carpentry", "Plumbing", "Electrical", "Masonry",
            "Painting",  "Welding",  "Tailoring",  "Hairdressing"
        };

        var current = Specialization?
            .Split(',')
            .Select(s => s.Trim())
            .ToArray() ?? Array.Empty<string>();

        var selected = await _dialogService.ShowMultiSelectAsync(
            "Select Specializations", options, current);

        if (selected == null || selected.Length == 0) return;

        Specialization = string.Join(", ", selected);

        if (CurrentUser is ArtisanUser artisan)
        {
            artisan.Specializations = selected.ToList();
            await _userService.UpdateUserAsync(artisan);
        }
    }

    [RelayCommand]
    private async Task ToggleAvailability()
    {
        IsAvailable = !IsAvailable;

        if (CurrentUser is ArtisanUser artisan)
        {
            artisan.IsAvailable = IsAvailable;
            await _userService.UpdateUserAsync(artisan);
        }

        var statusText = IsAvailable ? "available" : "unavailable";
        await _dialogService.ShowToastAsync($"You are now {statusText} for work");
    }

    // ── ADMIN commands ────────────────────────────────────────────

    [RelayCommand]
    private async Task ManageUsers() =>
        await _navigationService.NavigateToAsync("ManageUsersPage");

    [RelayCommand]
    private async Task ViewReports() =>
        await _navigationService.NavigateToAsync("SystemReportsPage");

    [RelayCommand]
    private async Task ManageVerifications() =>
        await _navigationService.NavigateToAsync("ArtisanVerificationsPage");

    // ── NOTIFICATIONS commands ────────────────────────────────────

    [RelayCommand]
    private async Task ToggleNotifications()
    {
        NotificationsEnabled = !NotificationsEnabled;
        await _userService.UpdateNotificationPreferenceAsync(NotificationsEnabled);
        await _dialogService.ShowToastAsync(
            $"Push notifications {(NotificationsEnabled ? "enabled" : "disabled")}");
    }

    [RelayCommand]
    private async Task ToggleEmailNotifications()
    {
        EmailNotificationsEnabled = !EmailNotificationsEnabled;
        await _userService.UpdateEmailNotificationPreferenceAsync(EmailNotificationsEnabled);
        await _dialogService.ShowToastAsync(
            $"Email notifications {(EmailNotificationsEnabled ? "enabled" : "disabled")}");
    }

    // ── SUPPORT commands ──────────────────────────────────────────

    [RelayCommand]
    private async Task ChangeLanguage()
    {
        var languages = new[] { "English", "French", "Spanish", "Arabic" };

        var selected = await _dialogService.ShowActionSheetAsync(
            "Select Language", "Cancel", null, languages);

        if (selected == null || selected == "Cancel") return;

        LanguageText = selected;
        await _dialogService.ShowToastAsync($"Language changed to {selected}");
    }

    [RelayCommand]
    private async Task OpenHelp() =>
        await _navigationService.NavigateToAsync("HelpPage");

    [RelayCommand]
    private async Task OpenTerms() =>
        await _navigationService.NavigateToAsync("TermsPage");

    [RelayCommand]
    private async Task OpenPrivacy() =>
        await _navigationService.NavigateToAsync("PrivacyPage");

    [RelayCommand]
    private async Task OpenAbout() =>
        await _dialogService.ShowAlertAsync("About CraftConnect",
            $"CraftConnect App\nVersion: {AppVersion}\n\n© 2024 CraftConnect Ltd.\nAll rights reserved.",
            "OK");

    // ── LOGOUT / DELETE commands ──────────────────────────────────

    [RelayCommand]
    private async Task Logout()
    {
        bool confirm = await _dialogService.ShowConfirmAsync(
            "Logout", "Are you sure you want to logout?",
            "Yes, Logout", "Cancel");

        if (!confirm) return;

        await _userService.LogoutAsync();
        await _navigationService.NavigateToAsync("//LoginPage");
    }

    [RelayCommand]
    private async Task DeleteAccount()
    {
        bool confirm = await _dialogService.ShowConfirmAsync(
            "Delete Account",
            "This will permanently delete your account and all associated data. This action cannot be undone.",
            "Delete Account", "Cancel", isDestructive: true);

        if (!confirm) return;

        var password = await _dialogService.ShowPromptAsync(
            "Confirm Deletion",
            "Please enter your password to confirm account deletion:",
            placeholder: "Your password", isPassword: true);

        if (string.IsNullOrWhiteSpace(password)) return;

        bool success = await _userService.DeleteAccountAsync(password);
        if (success)
        {
            await _dialogService.ShowAlertAsync("Account Deleted",
                "Your account has been successfully deleted.");
            await _navigationService.NavigateToAsync("//LoginPage");
        }
        else
        {
            await _dialogService.ShowAlertAsync("Deletion Failed",
                "Incorrect password or account deletion failed.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch { return false; }
    }
}