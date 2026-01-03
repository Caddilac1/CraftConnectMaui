// MAUI/Models/UserFeedDto.cs
namespace CraftConnect_Mobile_App.Models
{
    public class UserFeedDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JobCategory { get; set; } = string.Empty;
        public string InvoiceImage { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime? PreferredStartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public int ViewsCount { get; set; }
        public int CommentsCount { get; set; }
        public int LikesCount { get; set; }
        public int DislikesCount { get; set; }
        public int ReportsCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsFlagged { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserProfileImage { get; set; } = string.Empty;
        public string UserPhoneNumber { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string PriorityDisplay { get; set; } = string.Empty;
        public bool IsExpired { get; set; }

        // Additional properties for User object from API
        public UserBasicDto? User { get; set; }
    }

    // User Basic Info DTO
    public class UserBasicDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string ProfilePicture { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class CreateUserFeedDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JobCategory { get; set; } = string.Empty;
        public string InvoiceImage { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime? PreferredStartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public string Priority { get; set; } = "Medium";
    }

    public class UpdateUserFeedDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string JobCategory { get; set; } = string.Empty;
        public string InvoiceImage { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public DateTime? PreferredStartDate { get; set; }
        public DateTime? Deadline { get; set; }
        public string Priority { get; set; } = "Medium";
        public string Status { get; set; } = string.Empty;
    }
}