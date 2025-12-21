using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public interface IUserService
    {
        // Your existing methods
        Task<bool> HasUnreadUpdatesAsync();
        Task<bool> HasMissedCallsAsync();
        Task<string> GetCurrentUserIdAsync();
        Task<string> GetCurrentUserNameAsync();
        Task<string> GetCurrentUserProfileImageAsync();

        // Add these new methods for Settings page
        UserProfile GetCurrentUser();
        Task<bool> UpdateEmailAsync(string newEmail);
        Task<bool> UpdatePhoneNumberAsync(string phoneNumber);
        Task<bool> UpdateUserAsync(UserProfile user);
        Task<bool> UpdateNotificationPreferenceAsync(bool enabled);
        Task<bool> UpdateEmailNotificationPreferenceAsync(bool enabled);
        Task<bool> DeleteAccountAsync(string password);
        Task LogoutAsync();
    }
}