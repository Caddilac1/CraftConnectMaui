using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public interface IUserService
    {
        // Existing methods
        Task<bool> HasUnreadUpdatesAsync();
        Task<bool> HasMissedCallsAsync();
        Task<string> GetCurrentUserIdAsync();
        Task<string> GetCurrentUserNameAsync();
        Task<string> GetCurrentUserProfileImageAsync();

        // Settings page methods
        UserProfile GetCurrentUser();
        Task<UserProfile> LoadUserProfileAsync(); // ✅ ADD THIS METHOD
        Task<bool> UpdateEmailAsync(string newEmail);
        Task<bool> UpdatePhoneNumberAsync(string phoneNumber);
        Task<bool> UpdateUserAsync(UserProfile user);
        Task<bool> UpdateNotificationPreferenceAsync(bool enabled);
        Task<bool> UpdateEmailNotificationPreferenceAsync(bool enabled);
        Task<bool> DeleteAccountAsync(string password);
        Task LogoutAsync();
    }
}