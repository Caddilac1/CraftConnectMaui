using System;
using System.Threading.Tasks;
using CraftConnect_Mobile_App.Models;

namespace CraftConnect_Mobile_App.Services
{
    public class UserService : IUserService
    {
        // Existing methods
        public Task<bool> HasUnreadUpdatesAsync()
        {
            return Task.FromResult(false);
        }

        public Task<bool> HasMissedCallsAsync()
        {
            return Task.FromResult(false);
        }

        public Task<string> GetCurrentUserIdAsync()
        {
            return Task.FromResult("user123");
        }

        public Task<string> GetCurrentUserNameAsync()
        {
            return Task.FromResult("John Doe");
        }

        public Task<string> GetCurrentUserProfileImageAsync()
        {
            return Task.FromResult("default_profile.png");
        }

        // New methods required by Settings page
        public UserProfile GetCurrentUser()
        {
            // Return a mock user for now - in production, get from authentication/storage
            return new ArtisanUser
            {
                Id = "user123",
                FullName = "John Doe",
                Email = "john.doe@example.com",
                PhoneNumber = "+233 24 123 4567",
                Role = "Artisan",
                ProfileImageUrl = "default_profile.png",
                BusinessName = "Artisan Services Ltd.",
                Specializations = new List<string> { "Carpentry", "Plumbing" },
                IsAvailable = true,
                Rating = 4.5,
                CompletedJobs = 42
            };
        }

        public Task<bool> UpdateEmailAsync(string newEmail)
        {
            // TODO: Implement actual email update logic (API call, database update)
            // For now, return success
            return Task.FromResult(true);
        }

        public Task<bool> UpdatePhoneNumberAsync(string phoneNumber)
        {
            // TODO: Implement actual phone update logic
            return Task.FromResult(true);
        }

        public Task<bool> UpdateUserAsync(UserProfile user)
        {
            // TODO: Implement actual user update logic (API call, database update)
            return Task.FromResult(true);
        }

        public Task<bool> UpdateNotificationPreferenceAsync(bool enabled)
        {
            // TODO: Save notification preference to local storage or backend
            return Task.FromResult(true);
        }

        public Task<bool> UpdateEmailNotificationPreferenceAsync(bool enabled)
        {
            // TODO: Save email notification preference
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAccountAsync(string password)
        {
            // TODO: Implement account deletion logic
            // Verify password, call API, delete local data
            // For demo, accept any non-empty password
            return Task.FromResult(!string.IsNullOrWhiteSpace(password));
        }

        public Task LogoutAsync()
        {
            // TODO: Clear authentication tokens, local storage, etc.
            return Task.CompletedTask;
        }
    }
}