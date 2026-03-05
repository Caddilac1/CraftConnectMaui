using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CraftConnect_Mobile_App.PageModels;

/// <summary>
/// PageModel for the Edit Artisan Profile page.
/// </summary>
public partial class EditProfilePageModel : ObservableObject
{
    // ── Navigation / routing ────────────────────────────────────────────

    [ObservableProperty]
    private string identityUserId = string.Empty;

    [ObservableProperty]
    private string? returnUrl;

    [ObservableProperty]
    private bool isProposalRedirect;

    // ── IdentityUser fields ─────────────────────────────────────────────

    [ObservableProperty]
    private string? email;

    [ObservableProperty]
    private string? phoneNumber;

    // ── Display helpers ─────────────────────────────────────────────────

    [ObservableProperty]
    private string displayName = "Artisan";

    [ObservableProperty]
    private string initials = "?";

    [ObservableProperty]
    private string displayCity = string.Empty;

    // ── Avatar ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private string? profilePictureUrl;

    [ObservableProperty]
    private bool hasProfilePicture;

    // ── UserProfile fields ──────────────────────────────────────────────

    [ObservableProperty]
    private int userProfileId;

    [ObservableProperty]
    private string? fullName;

    [ObservableProperty]
    private string? bio;

    [ObservableProperty]
    private string? address;

    [ObservableProperty]
    private string? city;

    [ObservableProperty]
    private string? state;

    [ObservableProperty]
    private string? country;

    [ObservableProperty]
    private string? postalCode;

    [ObservableProperty]
    private string? preferredLanguage;

    [ObservableProperty]
    private string? timezone;

    // Stats (read-only, platform-managed)

    [ObservableProperty]
    private int artisanCompletedProjects;

    [ObservableProperty]
    private int artisanTotalViews;

    [ObservableProperty]
    private decimal? artisanAverageRating;

    [ObservableProperty]
    private bool artisanIsVerified;

    [ObservableProperty]
    private DateTime? artisanVerifiedDate;

    [ObservableProperty]
    private decimal? overallReliabilityScore;

    [ObservableProperty]
    private int totalTransactions;

    // ── ArtisanProfile fields ───────────────────────────────────────────

    [ObservableProperty]
    private string artisanProfileId = string.Empty;

    [ObservableProperty]
    private bool isNewArtisanProfile = true;

    [ObservableProperty]
    private string businessName = string.Empty;

    [ObservableProperty]
    private string specialization = string.Empty;

    [ObservableProperty]
    private int yearsOfExperience;

    [ObservableProperty]
    private string experienceLevel = string.Empty;

    [ObservableProperty]
    private decimal? hourlyRate;

    [ObservableProperty]
    private int? serviceRadius;

    [ObservableProperty]
    private string availabilityStatus = "AVAILABLE";

    [ObservableProperty]
    private string? licenseNumber;

    [ObservableProperty]
    private string? certification;

    [ObservableProperty]
    private string? businessRegistration;

    [ObservableProperty]
    private string? taxId;

    [ObservableProperty]
    private string? insuranceDetails;

    [ObservableProperty]
    private string? artisanSpeciality;

    [ObservableProperty]
    private string? businessAddress;

    [ObservableProperty]
    private string? professionalBio;

    [ObservableProperty]
    private string? about;

    // ── Services offered (chip list) ────────────────────────────────────

    public ObservableCollection<string> ServicesOffered { get; } = new();

    [ObservableProperty]
    private string newServiceEntry = string.Empty;

    // ── Profile completion progress ─────────────────────────────────────

    [ObservableProperty]
    private double profileCompletion;

    [ObservableProperty]
    private string profileCompletionText = "0%";

    // ── UI state ────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool isBusy;

    // ── Availability radio helpers ──────────────────────────────────────
    // All three are mutually exclusive — setting one clears the others
    // by writing through AvailabilityStatus and raising all three notifications.

    public bool IsAvailable
    {
        get => AvailabilityStatus == "AVAILABLE";
        set
        {
            if (!value) return;
            AvailabilityStatus = "AVAILABLE";
            OnPropertyChanged();
        }
    }

    public bool IsBusy2
    {
        get => AvailabilityStatus == "BUSY";
        set
        {
            if (!value) return;
            AvailabilityStatus = "BUSY";
            OnPropertyChanged();
        }
    }

    public bool IsUnavailable
    {
        get => AvailabilityStatus == "UNAVAILABLE";
        set
        {
            if (!value) return;
            AvailabilityStatus = "UNAVAILABLE";
            OnPropertyChanged();
        }
    }

    // ── Star rating helpers ─────────────────────────────────────────────

    public bool Star1On => (artisanAverageRating ?? 0) >= 1;
    public bool Star2On => (artisanAverageRating ?? 0) >= 2;
    public bool Star3On => (artisanAverageRating ?? 0) >= 3;
    public bool Star4On => (artisanAverageRating ?? 0) >= 4;
    public bool Star5On => (artisanAverageRating ?? 0) >= 5;

    public string RatingText => artisanAverageRating.HasValue
        ? artisanAverageRating.Value.ToString("F1")
        : "—";

    // ── Dropdown option lists ───────────────────────────────────────────

    public List<string> LanguageOptions { get; } = new()
    {
        "", "English (US)", "French", "Twi", "Hausa"
    };

    public List<string> TimezoneOptions { get; } = new()
    {
        "", "Africa/Accra (GMT+0)", "Africa/Lagos (GMT+1)",
        "Africa/Nairobi (GMT+3)", "Europe/London"
    };

    public List<string> TradeOptions { get; } = new()
    {
        "", "Plumber", "Electrician", "Carpenter", "Painter",
        "Mason / Bricklayer", "Welder / Fabricator", "Tiler",
        "AC Technician", "Glazier / Window Fitter", "Interior Designer",
        "Landscaper", "General Handyman", "Other"
    };

    public List<string> ExperienceLevelOptions { get; } = new()
    {
        "", "Beginner (0–2 yrs)", "Intermediate (3–5 yrs)",
        "Experienced (6–10 yrs)", "Expert (10+ yrs)"
    };

    // ── Commands ────────────────────────────────────────────────────────

    // Wired in code-behind because they need direct UI / nav context
    public ICommand SetAvailableCommand { get; set; } = new Command(() => { });
    public ICommand SetBusyCommand { get; set; } = new Command(() => { });
    public ICommand SetUnavailableCommand { get; set; } = new Command(() => { });

    /// <summary>
    /// Exposed with a public setter so the code-behind can replace it
    /// with its own handler that drives Navigation.PushAsync rather than Shell.
    /// </summary>
    public IAsyncRelayCommand SaveCommand { get; set; }

    // ── Constructor ─────────────────────────────────────────────────────

    public EditProfilePageModel()
    {
        // Default implementation — replaced by the code-behind in its constructor.
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    // ── Initialise / load ───────────────────────────────────────────────

    public void Initialise(
        string identityUserId,
        string? email,
        string? phoneNumber,
        string? returnUrl,
        int userProfileId,
        string? fullName,
        string? bio,
        string? address,
        string? city,
        string? state,
        string? country,
        string? postalCode,
        string? profilePictureUrl,
        string? preferredLanguage,
        string? timezone,
        int completedProjects,
        int totalViews,
        decimal? rating,
        bool isVerified,
        DateTime? verifiedDate,
        decimal? reliabilityScore,
        int totalTransactions,
        string? artisanProfileId,
        string? businessName,
        string? specialization,
        int yearsOfExperience,
        string? experienceLevel,
        decimal? hourlyRate,
        int? serviceRadius,
        string? availabilityStatus,
        string? licenseNumber,
        string? certification,
        string? businessRegistration,
        string? taxId,
        string? insuranceDetails,
        string? artisanSpeciality,
        string? businessAddress,
        string? professionalBio,
        string? about,
        string? servicesOffered)
    {
        IdentityUserId = identityUserId;
        Email = email;
        PhoneNumber = phoneNumber;
        ReturnUrl = returnUrl;
        IsProposalRedirect = !string.IsNullOrEmpty(returnUrl);

        UserProfileId = userProfileId;
        FullName = fullName;
        Bio = bio;
        Address = address;
        City = city;
        State = state;
        Country = country;
        PostalCode = postalCode;
        ProfilePictureUrl = profilePictureUrl;
        HasProfilePicture = !string.IsNullOrWhiteSpace(profilePictureUrl);
        PreferredLanguage = preferredLanguage;
        Timezone = timezone;

        ArtisanCompletedProjects = completedProjects;
        ArtisanTotalViews = totalViews;
        ArtisanAverageRating = rating;
        ArtisanIsVerified = isVerified;
        ArtisanVerifiedDate = verifiedDate;
        OverallReliabilityScore = reliabilityScore;
        TotalTransactions = totalTransactions;

        ArtisanProfileId = artisanProfileId ?? string.Empty;
        IsNewArtisanProfile = string.IsNullOrEmpty(artisanProfileId);
        BusinessName = businessName ?? string.Empty;
        Specialization = specialization ?? string.Empty;
        YearsOfExperience = yearsOfExperience;
        ExperienceLevel = experienceLevel ?? string.Empty;
        HourlyRate = hourlyRate;
        ServiceRadius = serviceRadius;
        AvailabilityStatus = availabilityStatus ?? "AVAILABLE";
        LicenseNumber = licenseNumber;
        Certification = certification;
        BusinessRegistration = businessRegistration;
        TaxId = taxId;
        InsuranceDetails = insuranceDetails;
        ArtisanSpeciality = artisanSpeciality;
        BusinessAddress = businessAddress;
        ProfessionalBio = professionalBio;
        About = about;

        ServicesOffered.Clear();
        if (!string.IsNullOrWhiteSpace(servicesOffered))
        {
            foreach (var s in servicesOffered
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s)))
            {
                ServicesOffered.Add(s);
            }
        }

        RefreshDisplayHelpers();
        RecalculateProgress();
    }

    // ── Partial property change hooks ───────────────────────────────────

    partial void OnFullNameChanged(string? value)
    {
        RefreshDisplayHelpers();
        RecalculateProgress();
    }

    partial void OnEmailChanged(string? value) => RecalculateProgress();
    partial void OnPhoneNumberChanged(string? value) => RecalculateProgress();

    partial void OnCityChanged(string? value)
    {
        RefreshDisplayHelpers();
        RecalculateProgress();
    }

    partial void OnCountryChanged(string? value) => RefreshDisplayHelpers();
    partial void OnAddressChanged(string? value) => RecalculateProgress();
    partial void OnBusinessNameChanged(string value) => RecalculateProgress();
    partial void OnSpecializationChanged(string value) => RecalculateProgress();
    partial void OnYearsOfExperienceChanged(int value) => RecalculateProgress();
    partial void OnHourlyRateChanged(decimal? value) => RecalculateProgress();
    partial void OnAboutChanged(string? value) => RecalculateProgress();

    partial void OnProfilePictureUrlChanged(string? value)
        => HasProfilePicture = !string.IsNullOrWhiteSpace(value);

    partial void OnAvailabilityStatusChanged(string value)
    {
        // Notify all three radio helpers so the UI reacts to any change
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsBusy2));
        OnPropertyChanged(nameof(IsUnavailable));
    }

    partial void OnArtisanAverageRatingChanged(decimal? value)
    {
        OnPropertyChanged(nameof(Star1On));
        OnPropertyChanged(nameof(Star2On));
        OnPropertyChanged(nameof(Star3On));
        OnPropertyChanged(nameof(Star4On));
        OnPropertyChanged(nameof(Star5On));
        OnPropertyChanged(nameof(RatingText));
    }

    // ── Relay commands ──────────────────────────────────────────────────

    [RelayCommand]
    private void AddService()
    {
        var val = NewServiceEntry?.Trim();
        if (string.IsNullOrEmpty(val)) return;
        if (ServicesOffered.Any(s => s.Equals(val, StringComparison.OrdinalIgnoreCase)))
        {
            NewServiceEntry = string.Empty;
            return;
        }
        ServicesOffered.Add(val);
        NewServiceEntry = string.Empty;
        RecalculateProgress();
    }

    [RelayCommand]
    private void RemoveService(string service)
    {
        ServicesOffered.Remove(service);
        RecalculateProgress();
    }

    [RelayCommand]
    private async Task PickAvatarAsync()
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync();
            if (result == null) return;
            ProfilePictureUrl = result.FullPath;
            HasProfilePicture = true;
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Photo", ex.Message, "OK");
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        if (!string.IsNullOrEmpty(ReturnUrl))
            await Shell.Current.GoToAsync(ReturnUrl);
        else
            await Shell.Current.GoToAsync("//ProfileDetailsPage");
    }

    // ── Default save (used when no code-behind override is present) ──────

    private async Task SaveAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            // TODO: inject and call your IProfileService / API client here.
            // var request = BuildSaveRequest();
            // await _profileService.SaveAsync(request);

            if (!string.IsNullOrEmpty(ReturnUrl))
                await Shell.Current.GoToAsync(ReturnUrl);
            else
                await Shell.Current.GoToAsync("//ProfileDetailsPage");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private void RefreshDisplayHelpers()
    {
        var name = FullName ?? Email ?? "Artisan";
        DisplayName = name;

        Initials = string.IsNullOrWhiteSpace(name)
            ? "?"
            : string.Concat(name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Take(2)
                .Select(w => w[0]))
              .ToUpper();

        DisplayCity = string.Join(", ",
            new[] { City, Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private void RecalculateProgress()
    {
        int filled = 0;
        const int total = 11;

        if (!string.IsNullOrWhiteSpace(Email)) filled++;
        if (!string.IsNullOrWhiteSpace(PhoneNumber)) filled++;
        if (!string.IsNullOrWhiteSpace(FullName)) filled++;
        if (!string.IsNullOrWhiteSpace(City)) filled++;
        if (!string.IsNullOrWhiteSpace(Address)) filled++;
        if (!string.IsNullOrWhiteSpace(BusinessName)) filled++;
        if (!string.IsNullOrWhiteSpace(Specialization)) filled++;
        if (YearsOfExperience > 0) filled++;
        if (HourlyRate.HasValue && HourlyRate > 0) filled++;
        if (!string.IsNullOrWhiteSpace(About)) filled++;
        if (ServicesOffered.Count > 0) filled++;

        var pct = (double)filled / total;
        ProfileCompletion = pct;
        ProfileCompletionText = $"{(int)(pct * 100)}%";
    }

    // ── Build save request ───────────────────────────────────────────────

    public ProfileSaveRequest BuildSaveRequest() => new()
    {
        IdentityUserId = IdentityUserId,
        Email = Email,
        PhoneNumber = PhoneNumber,
        UserProfileId = UserProfileId,
        FullName = FullName,
        Bio = Bio,
        Address = Address,
        City = City,
        State = State,
        Country = Country,
        PostalCode = PostalCode,
        ProfilePictureUrl = ProfilePictureUrl,
        PreferredLanguage = PreferredLanguage,
        Timezone = Timezone,
        ArtisanProfileId = ArtisanProfileId,
        BusinessName = BusinessName,
        Specialization = Specialization,
        YearsOfExperience = YearsOfExperience,
        ExperienceLevel = ExperienceLevel,
        HourlyRate = HourlyRate,
        ServiceRadius = ServiceRadius,
        AvailabilityStatus = AvailabilityStatus,
        LicenseNumber = LicenseNumber,
        Certification = Certification,
        BusinessRegistration = BusinessRegistration,
        TaxId = TaxId,
        InsuranceDetails = InsuranceDetails,
        ArtisanSpeciality = ArtisanSpeciality,
        BusinessAddress = BusinessAddress,
        ProfessionalBio = ProfessionalBio,
        About = About,
        ServicesOffered = string.Join(", ", ServicesOffered),
    };
}

// ── DTO ──────────────────────────────────────────────────────────────────────

public class ProfileSaveRequest
{
    public string IdentityUserId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public int UserProfileId { get; set; }
    public string? FullName { get; set; }
    public string? Bio { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? ProfilePictureUrl { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? Timezone { get; set; }
    public string ArtisanProfileId { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string Specialization { get; set; } = string.Empty;
    public int YearsOfExperience { get; set; }
    public string? ExperienceLevel { get; set; }
    public decimal? HourlyRate { get; set; }
    public int? ServiceRadius { get; set; }
    public string AvailabilityStatus { get; set; } = "AVAILABLE";
    public string? LicenseNumber { get; set; }
    public string? Certification { get; set; }
    public string? BusinessRegistration { get; set; }
    public string? TaxId { get; set; }
    public string? InsuranceDetails { get; set; }
    public string? ArtisanSpeciality { get; set; }
    public string? BusinessAddress { get; set; }
    public string? ProfessionalBio { get; set; }
    public string? About { get; set; }
    public string ServicesOffered { get; set; } = string.Empty;
}