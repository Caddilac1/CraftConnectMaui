using System.Text.Json.Serialization;

namespace CraftConnect_Mobile_App.Models
{
    public class LoginRequest
    {
        public string EmailOrPhone { get; set; } = string.Empty;

        // Make it nullable and ignore when null
        [JsonPropertyName("password")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Password { get; set; }

        public bool UsePassword { get; set; } = false;
        public bool RememberMe { get; set; } = false;
    }
}