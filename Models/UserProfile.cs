namespace CraftConnect_Mobile_App.Models
{
    public class UserProfile
    {
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }
        public string ProfileImageUrl { get; set; }
    }

    public class ArtisanUser : UserProfile
    {
        public string BusinessName { get; set; }
        public List<string> Specializations { get; set; } = new();
        public bool IsAvailable { get; set; } = true;
        public double Rating { get; set; }
        public int CompletedJobs { get; set; }
    }
}
