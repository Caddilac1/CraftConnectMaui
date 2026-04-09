using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CraftConnect_Mobile_App.Models
{
    /// <summary>
    /// Unified mobile profile model.
    /// Matches UserDto from GET api/profilesapi/customer/me
    /// and the nested user object inside ArtisanProfileDto.
    /// </summary>
    public class UserProfile
    {
        // ── Core identity ─────────────────────────────────────────────
        public string Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Role { get; set; }

        [JsonPropertyName("profilePicture")]
        public string ProfilePicture { get; set; }

        [JsonIgnore]
        public string ProfileImageUrl
        {
            get => ProfilePicture;
            set => ProfilePicture = value;
        }

        // ── Location ──────────────────────────────────────────────────
        public string Address { get; set; }
        public string AddressLine2 { get; set; }
        public string PostalCode { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; }

        // ── Profile ───────────────────────────────────────────────────
        public string Bio { get; set; }
        public DateTime? DateJoined { get; set; }

        // ── Preferences ───────────────────────────────────────────────
        public string PreferredLanguage { get; set; }
        public string Timezone { get; set; }

        // ── Staff-specific (populated for Staff/Admin roles) ──────────
        public string StaffTypeName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string EmergencyContact { get; set; }

        // ── Computed helpers ──────────────────────────────────────────
        [JsonIgnore]
        public bool IsArtisan => Role?.Equals("Artisan", StringComparison.OrdinalIgnoreCase) ?? false;

        [JsonIgnore]
        public bool IsStaffOrAdmin =>
            Role?.Equals("Staff", StringComparison.OrdinalIgnoreCase) == true ||
            Role?.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;

        [JsonIgnore]
        public string DisplayName => !string.IsNullOrWhiteSpace(FullName) ? FullName : Email ?? "User";

        [JsonIgnore]
        public string LocationDisplay
        {
            get
            {
                var parts = new[] { City, State, Country };
                return string.Join(", ", System.Linq.Enumerable.Where(parts, p => !string.IsNullOrWhiteSpace(p)));
            }
        }
    }

    /// <summary>
    /// Extends UserProfile with artisan-specific fields.
    /// Matches ArtisanProfileDto from GET api/profilesapi/artisan/me.
    /// </summary>
    public class ArtisanUser : UserProfile
    {
        // ── Business core ─────────────────────────────────────────────
        public string BusinessName { get; set; }
        public string Slug { get; set; }
        public string Specialization { get; set; }
        public string ArtisanSpeciality { get; set; }
        public string ExperienceLevel { get; set; }
        public int YearsOfExperience { get; set; }

        // ── Stats ─────────────────────────────────────────────────────
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
        public int CompletedProjects { get; set; }

        // ── Availability & pricing ────────────────────────────────────
        public string AvailabilityStatus { get; set; }
        public decimal? HourlyRate { get; set; }
        public double? ServiceRadius { get; set; }

        // ── Descriptions ─────────────────────────────────────────────
        public string About { get; set; }
        public string ProfessionalBio { get; set; }
        public string ServicesOffered { get; set; }

        // ── Location ─────────────────────────────────────────────────
        public string BusinessAddress { get; set; }

        // ── Credentials ───────────────────────────────────────────────
        public string LicenseNumber { get; set; }
        public string Certification { get; set; }
        public string BusinessRegistration { get; set; }
        public string TaxId { get; set; }
        public string InsuranceDetails { get; set; }

        // ── Verification ──────────────────────────────────────────────
        public bool IsVerified { get; set; }
        public DateTime? VerifiedDate { get; set; }

        // ── Timestamps ────────────────────────────────────────────────
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ── Computed / aliases ────────────────────────────────────────
        [JsonIgnore]
        public bool IsAvailable
        {
            get => AvailabilityStatus?.Equals("Available", StringComparison.OrdinalIgnoreCase) ?? false;
            set => AvailabilityStatus = value ? "Available" : "Unavailable";
        }

        [JsonIgnore]
        public int CompletedJobs => CompletedProjects;

        [JsonIgnore]
        public double Rating => (double)AverageRating;

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

        [JsonIgnore]
        public bool HasCredentials =>
            !string.IsNullOrWhiteSpace(LicenseNumber) ||
            !string.IsNullOrWhiteSpace(Certification) ||
            !string.IsNullOrWhiteSpace(BusinessRegistration) ||
            !string.IsNullOrWhiteSpace(TaxId) ||
            !string.IsNullOrWhiteSpace(InsuranceDetails);

        [JsonIgnore]
        public string AvailabilityStatusUpper =>
            (AvailabilityStatus ?? "").ToUpper();
    }
}