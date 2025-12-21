namespace CraftConnect_Mobile_App.Models
{
    public class LoginResponse
    {
        public string Message { get; set; }
        public string Token { get; set; }
        public string Email { get; set; }
        public string UserId { get; set; }
        public string FullName { get; set; }
        public bool Success { get; set; }
    }
}
