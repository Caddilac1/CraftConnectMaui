using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public interface IUserService
    {
        Task<bool> HasUnreadUpdatesAsync();
        Task<bool> HasMissedCallsAsync();
        Task<string> GetCurrentUserIdAsync();
        Task<string> GetCurrentUserNameAsync();
        Task<string> GetCurrentUserProfileImageAsync();

        UserProfile GetCurrentUser();
        Task<UserProfile> LoadUserProfileAsync();
        Task<bool> HasArtisanProfileAsync();

        Task<bool> UpdateEmailAsync(string newEmail);
        Task<bool> UpdatePhoneNumberAsync(string phoneNumber);
        Task<bool> UpdateUserAsync(UserProfile user);
        Task<bool> UpdateNotificationPreferenceAsync(bool enabled);
        Task<bool> UpdateEmailNotificationPreferenceAsync(bool enabled);
        Task<bool> DeleteAccountAsync(string password);
        Task LogoutAsync();
    }
}