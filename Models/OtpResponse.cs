namespace CraftConnect_Mobile_App.Models
{
    public class OtpResponse
    {
        public bool Success { get; set; }
        public bool RequiresOtp { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool HasPassword { get; set; } // Indicates if user can use password login
    }
}