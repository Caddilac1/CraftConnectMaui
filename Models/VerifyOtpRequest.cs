

namespace CraftConnect_Mobile_App.Models
{
    public class VerifyOtpRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}
