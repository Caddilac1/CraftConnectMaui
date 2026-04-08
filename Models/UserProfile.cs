using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CraftConnect_Mobile_App.Models
{
    /// <summary>
    /// Matches the UserDto returned by GET api/profilesapi/customer/me.
    /// Property names match the JSON the API sends exactly so the default
    /// deserializer maps them without extra configuration.
    /// </summary>
    public class UserProfile
    {
        // ── Core identity ─────────────────────────────────────────────
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }

        // ── API sends "profilePicture" not "profileImageUrl" ──────────
        // [JsonPropertyName] maps the camelCase JSON key to this property.
        // ProfileImageUrl is a [JsonIgnore] alias so existing UI bindings
        // that reference ProfileImageUrl keep compiling without changes.
        [JsonPropertyName("profilePicture")]
        public string ProfilePicture { get; set; }

        [JsonIgnore]
        public string ProfileImageUrl
        {
            get => ProfilePicture;
            set => ProfilePicture = value;
        }

        // ── Location fields ───────────────────────────────────────────
        public string Address { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }

        // ── Profile fields ────────────────────────────────────────────
        public string Bio { get; set; }
        public DateTime? DateJoined { get; set; }
    }

    /// <summary>
    /// Extends UserProfile with artisan-specific fields.
    /// Matches the ArtisanProfileDto returned by GET api/profilesapi/artisan/me.
    /// </summary>
    public class ArtisanUser : UserProfile
    {
        // ── Artisan profile fields ────────────────────────────────────
        public string BusinessName { get; set; }
        public string Slug { get; set; }
        public string Specialization { get; set; }   // raw string from API e.g. "Plumbing, Electrical"
        public string ExperienceLevel { get; set; }
        public int YearsOfExperience { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int CompletedProjects { get; set; }
        public string AvailabilityStatus { get; set; }   // "Available" | "Unavailable" | "Busy"
        public decimal? HourlyRate { get; set; }
        public string About { get; set; }
        public string ProfessionalBio { get; set; }
        public string BusinessAddress { get; set; }
        public string ArtisanSpeciality { get; set; }
        public double? ServiceRadius { get; set; }
        public string ServicesOffered { get; set; }
        public string LicenseNumber { get; set; }
        public string Certification { get; set; }
        public string BusinessRegistration { get; set; }
        public string TaxId { get; set; }
        public string InsuranceDetails { get; set; }
        public bool IsVerified { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ── Writable computed aliases for SettingsPageViewModel ───────

        /// <summary>
        /// Read/write. Getting derives from AvailabilityStatus;
        /// setting updates AvailabilityStatus so the two stay in sync.
        /// </summary>
        [JsonIgnore]
        public bool IsAvailable
        {
            get => AvailabilityStatus?.Equals("Available", StringComparison.OrdinalIgnoreCase) ?? false;
            set => AvailabilityStatus = value ? "Available" : "Unavailable";
        }

        /// <summary>
        /// Read/write list view of Specialization string.
        /// Assigning a new list updates the Specialization string so the API
        /// field stays in sync when UpdateUserAsync is called.
        /// </summary>
        private List<string> _specializations;

        [JsonIgnore]
        public List<string> Specializations
        {
            get => _specializations ??= string.IsNullOrEmpty(Specialization)
                ? new List<string>()
                : new List<string>(Specialization.Split(',', StringSplitOptions.TrimEntries));
            set
            {
                _specializations = value;
                Specialization = value != null ? string.Join(", ", value) : null;
            }
        }

        // ── Read-only aliases (UI bindings that existed before) ───────

        /// <summary>Alias for CompletedProjects — keeps old UI bindings compiling.</summary>
        [JsonIgnore]
        public int CompletedJobs => CompletedProjects;

        /// <summary>Alias for AverageRating as double — keeps old UI bindings compiling.</summary>
        [JsonIgnore]
        public double Rating => (double)AverageRating;
    }
}