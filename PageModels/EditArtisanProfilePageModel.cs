using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using CraftConnect_Mobile_App.Services;
using Microsoft.Maui.Controls;

namespace CraftConnect_Mobile_App.PageModels
{
    public class EditArtisanProfilePageModel : INotifyPropertyChanged
    {
        // ═══════════════════════════════════════════════════════════════
        // DEPENDENCIES
        // ═══════════════════════════════════════════════════════════════

        private readonly IProfileApiService _profileService;

        // ═══════════════════════════════════════════════════════════════
        // RETURN-CONTEXT
        // Set by the page before calling InitialiseAsync.
        // When ReturnFeedId is non-null, a successful save fires
        // NavigateToProposalRequested instead of NavigateBackRequested.
        // ═══════════════════════════════════════════════════════════════

        public string? ReturnFeedId { get; set; }
        public string? ReturnFeedTitle { get; set; }

        // ═══════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════

        public EditArtisanProfilePageModel(IProfileApiService profileService)
        {
            _profileService = profileService;

            // Commands
            PickAvatarCommand = new Command(async () => await PickAvatarAsync());
            SetAvailableCommand = new Command(() => SetAvailability("Available"));
            SetBusyCommand = new Command(() => SetAvailability("Busy"));
            SetUnavailableCommand = new Command(() => SetAvailability("Unavailable"));
            AddServiceCommand = new Command(AddService);
            RemoveServiceCommand = new Command<string>(RemoveService);
            SaveCommand = new Command(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new Command(async () => await CancelAsync());

            // Default collections
            ServicesOffered = new ObservableCollection<string>();

            LanguageOptions = new List<string>
            {
                "English", "Twi", "Ga", "Ewe", "Hausa", "French", "Other"
            };

            TimezoneOptions = new List<string>
            {
                "Africa/Accra", "Africa/Lagos", "Africa/Nairobi",
                "Europe/London", "America/New_York", "America/Los_Angeles"
            };

            TradeOptions = new List<string>
            {
                "Carpenter", "Electrician", "Plumber", "Mason / Bricklayer",
                "Painter", "Tiler", "Welder / Fabricator", "AC Technician",
                "Roofer", "Glazier", "Landscaper", "Interior Designer",
                "General Contractor", "Other"
            };

            ExperienceLevelOptions = new List<string>
            {
                "Beginner", "Intermediate", "Advanced", "Expert"
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // NAVIGATION / PROPOSAL-REDIRECT FLAG
        // ═══════════════════════════════════════════════════════════════

        private bool _isProposalRedirect;
        public bool IsProposalRedirect
        {
            get => _isProposalRedirect;
            set { _isProposalRedirect = value; OnPropertyChanged(); OnPropertyChanged(nameof(SaveBtnText)); }
        }

        public string SaveBtnText => IsProposalRedirect ? "Save & Continue" : "Save Changes";

        // ═══════════════════════════════════════════════════════════════
        // PROFILE FLAGS
        // ═══════════════════════════════════════════════════════════════

        private bool _isNewArtisanProfile;
        public bool IsNewArtisanProfile
        {
            get => _isNewArtisanProfile;
            set { _isNewArtisanProfile = value; OnPropertyChanged(); }
        }

        private bool _artisanIsVerified;
        public bool ArtisanIsVerified
        {
            get => _artisanIsVerified;
            set { _artisanIsVerified = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // AVATAR / DISPLAY
        // ═══════════════════════════════════════════════════════════════

        private string _profilePictureUrl;
        public string ProfilePictureUrl
        {
            get => _profilePictureUrl;
            set
            {
                _profilePictureUrl = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasProfilePicture));
            }
        }

        public bool HasProfilePicture => !string.IsNullOrWhiteSpace(ProfilePictureUrl);

        private string _displayName;
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }

        private string _initials;
        public string Initials
        {
            get => _initials;
            set { _initials = value; OnPropertyChanged(); }
        }

        private string _displayCity;
        public string DisplayCity
        {
            get => _displayCity;
            set { _displayCity = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // PROFILE COMPLETION
        // ═══════════════════════════════════════════════════════════════

        private double _profileCompletion;
        public double ProfileCompletion
        {
            get => _profileCompletion;
            set
            {
                _profileCompletion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProfileCompletionText));
            }
        }

        public string ProfileCompletionText => $"{ProfileCompletion:F0}%";

        // ═══════════════════════════════════════════════════════════════
        // PLATFORM STATS
        // ═══════════════════════════════════════════════════════════════

        private int _artisanCompletedProjects;
        public int ArtisanCompletedProjects
        {
            get => _artisanCompletedProjects;
            set { _artisanCompletedProjects = value; OnPropertyChanged(); }
        }

        private int _artisanTotalViews;
        public int ArtisanTotalViews
        {
            get => _artisanTotalViews;
            set { _artisanTotalViews = value; OnPropertyChanged(); }
        }

        private double _averageRating;
        public double AverageRating
        {
            get => _averageRating;
            set
            {
                _averageRating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RatingText));
                OnPropertyChanged(nameof(Star1On));
                OnPropertyChanged(nameof(Star2On));
                OnPropertyChanged(nameof(Star3On));
                OnPropertyChanged(nameof(Star4On));
                OnPropertyChanged(nameof(Star5On));
            }
        }

        public string RatingText => _averageRating > 0 ? $"{_averageRating:F1}" : "—";
        public bool Star1On => _averageRating >= 1;
        public bool Star2On => _averageRating >= 2;
        public bool Star3On => _averageRating >= 3;
        public bool Star4On => _averageRating >= 4;
        public bool Star5On => _averageRating >= 5;

        private double? _overallReliabilityScore;
        public double? OverallReliabilityScore
        {
            get => _overallReliabilityScore;
            set { _overallReliabilityScore = value; OnPropertyChanged(); }
        }

        private int _totalTransactions;
        public int TotalTransactions
        {
            get => _totalTransactions;
            set { _totalTransactions = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // SECTION 1 — ACCOUNT INFORMATION
        // ═══════════════════════════════════════════════════════════════

        private string _email;
        public string Email
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _phoneNumber;
        public string PhoneNumber
        {
            get => _phoneNumber;
            set { _phoneNumber = value; OnPropertyChanged(); }
        }

        private string _fullName;
        public string FullName
        {
            get => _fullName;
            set
            {
                _fullName = value;
                OnPropertyChanged();
                UpdateDisplayName();
                UpdateInitials();
                RecalcCompletion();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SECTION 2 — PERSONAL INFORMATION
        // ═══════════════════════════════════════════════════════════════

        private string _address;
        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _city;
        public string City
        {
            get => _city;
            set { _city = value; OnPropertyChanged(); UpdateDisplayCity(); RecalcCompletion(); }
        }

        private string _state;
        public string State
        {
            get => _state;
            set { _state = value; OnPropertyChanged(); }
        }

        private string _country;
        public string Country
        {
            get => _country;
            set { _country = value; OnPropertyChanged(); }
        }

        private string _postalCode;
        public string PostalCode
        {
            get => _postalCode;
            set { _postalCode = value; OnPropertyChanged(); }
        }

        private string _preferredLanguage;
        public string PreferredLanguage
        {
            get => _preferredLanguage;
            set { _preferredLanguage = value; OnPropertyChanged(); }
        }

        private string _timezone;
        public string Timezone
        {
            get => _timezone;
            set { _timezone = value; OnPropertyChanged(); }
        }

        private string _bio;
        public string Bio
        {
            get => _bio;
            set { _bio = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // SECTION 3 — ARTISAN BUSINESS DETAILS
        // ═══════════════════════════════════════════════════════════════

        private string _businessName;
        public string BusinessName
        {
            get => _businessName;
            set { _businessName = value; OnPropertyChanged(); UpdateDisplayName(); RecalcCompletion(); }
        }

        private string _specialization;
        public string Specialization
        {
            get => _specialization;
            set { _specialization = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _yearsOfExperienceText;
        public string YearsOfExperience
        {
            get => _yearsOfExperienceText;
            set { _yearsOfExperienceText = value; OnPropertyChanged(); }
        }

        private string _experienceLevel;
        public string ExperienceLevel
        {
            get => _experienceLevel;
            set { _experienceLevel = value; OnPropertyChanged(); }
        }

        private string _hourlyRateText;
        public string HourlyRate
        {
            get => _hourlyRateText;
            set { _hourlyRateText = value; OnPropertyChanged(); }
        }

        private string _serviceRadiusText;
        public string ServiceRadius
        {
            get => _serviceRadiusText;
            set { _serviceRadiusText = value; OnPropertyChanged(); }
        }

        // ── Availability ──────────────────────────────────────────────

        private string _availabilityStatus = "Available";
        public string AvailabilityStatus
        {
            get => _availabilityStatus;
            set
            {
                _availabilityStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAvailable));
                OnPropertyChanged(nameof(IsBusy2));
            }
        }

        public bool IsAvailable => _availabilityStatus == "Available";
        public bool IsBusy2 => _availabilityStatus == "Busy";

        // ── Credentials ───────────────────────────────────────────────

        private string _licenseNumber;
        public string LicenseNumber
        {
            get => _licenseNumber;
            set { _licenseNumber = value; OnPropertyChanged(); }
        }

        private string _certification;
        public string Certification
        {
            get => _certification;
            set { _certification = value; OnPropertyChanged(); }
        }

        private string _businessRegistration;
        public string BusinessRegistration
        {
            get => _businessRegistration;
            set { _businessRegistration = value; OnPropertyChanged(); }
        }

        private string _taxId;
        public string TaxId
        {
            get => _taxId;
            set { _taxId = value; OnPropertyChanged(); }
        }

        private string _insuranceDetails;
        public string InsuranceDetails
        {
            get => _insuranceDetails;
            set { _insuranceDetails = value; OnPropertyChanged(); }
        }

        private string _artisanSpeciality;
        public string ArtisanSpeciality
        {
            get => _artisanSpeciality;
            set { _artisanSpeciality = value; OnPropertyChanged(); }
        }

        private string _businessAddress;
        public string BusinessAddress
        {
            get => _businessAddress;
            set { _businessAddress = value; OnPropertyChanged(); }
        }

        // ── Services Offered chips ────────────────────────────────────

        public ObservableCollection<string> ServicesOffered { get; }

        private string _newServiceEntry;
        public string NewServiceEntry
        {
            get => _newServiceEntry;
            set { _newServiceEntry = value; OnPropertyChanged(); }
        }

        // ── Bios ─────────────────────────────────────────────────────

        private string _professionalBio;
        public string ProfessionalBio
        {
            get => _professionalBio;
            set { _professionalBio = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _about;
        public string About
        {
            get => _about;
            set { _about = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // PICKER SOURCE LISTS
        // ═══════════════════════════════════════════════════════════════

        public List<string> LanguageOptions { get; }
        public List<string> TimezoneOptions { get; }
        public List<string> TradeOptions { get; }
        public List<string> ExperienceLevelOptions { get; }

        // ═══════════════════════════════════════════════════════════════
        // BUSY / LOADING
        // ═══════════════════════════════════════════════════════════════

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                (SaveCommand as Command)?.ChangeCanExecute();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // COMMANDS
        // ═══════════════════════════════════════════════════════════════

        public ICommand PickAvatarCommand { get; }
        public ICommand SetAvailableCommand { get; }
        public ICommand SetBusyCommand { get; }
        public ICommand SetUnavailableCommand { get; }
        public ICommand AddServiceCommand { get; }
        public ICommand RemoveServiceCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        // ═══════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Display a transient toast message.</summary>
        public event Action<string> ShowToastRequested;

        /// <summary>
        /// Navigate back to the previous page.
        /// Fired on cancel, or after a normal save with no return feed.
        /// </summary>
        public event Action NavigateBackRequested;

        /// <summary>
        /// Navigate forward to CreateProposalPage pre-loaded with this feed ID.
        /// Fired after a successful save when ReturnFeedId is set.
        /// </summary>
        public event Action<string> NavigateToProposalRequested;

        // ═══════════════════════════════════════════════════════════════
        // INITIALISE FROM API
        //
        // FIX: Wrapped GetMyProfileAsync in its own try/catch so a
        // NotFound (new user) or any network error is treated silently
        // as "new profile" — no Error dialog is shown.
        // The outer catch only handles truly unexpected exceptions.
        // ═══════════════════════════════════════════════════════════════

        public async Task InitialiseAsync(bool isProposalRedirect = false)
        {
            IsProposalRedirect = isProposalRedirect;
            IsBusy = true;

            try
            {
                // ── Fetch profile, treating NotFound / any error as "new user" ──
                MobileProfileDetails details = null;
                try
                {
                    details = await _profileService.GetMyProfileAsync();
                }
                catch (UnauthorizedAccessException)
                {
                    // Let the outer catch handle this specifically
                    throw;
                }
                catch (Exception fetchEx)
                {
                    // Network error, NotFound, or any other API failure:
                    // just treat as a brand-new profile — don't show an error.
                    System.Diagnostics.Debug.WriteLine(
                        $"[EditArtisanProfilePageModel] Profile fetch skipped (new user path): {fetchEx.Message}");
                }

                // ── If no data came back, this is a brand-new user ────────────
                if (details == null
                    || (details.UserProfile == null && details.ArtisanProfile == null))
                {
                    IsNewArtisanProfile = true;
                    UpdateDisplayName();
                    UpdateInitials();
                    UpdateDisplayCity();
                    RecalcCompletion();
                    return; // Render a blank form — no toast, no error dialog
                }

                // ── Populate account fields ───────────────────────────────────
                Email = details.Email;
                PhoneNumber = details.PhoneNumber;

                // ── Populate user profile fields ──────────────────────────────
                var up = details.UserProfile;
                if (up != null)
                {
                    FullName = up.FullName;
                    ProfilePictureUrl = up.ProfilePictureUrl;
                    Address = up.Address;
                    City = up.City;
                    State = up.State;
                    Country = up.Country;
                    PostalCode = up.PostalCode;
                    PreferredLanguage = up.PreferredLanguage;
                    Timezone = up.Timezone;
                    Bio = up.Bio;
                }

                // ── Populate artisan profile fields ───────────────────────────
                var ap = details.ArtisanProfile;
                if (ap != null)
                {
                    IsNewArtisanProfile = false;
                    BusinessName = ap.BusinessName;
                    Specialization = ap.Specialization;
                    YearsOfExperience = ap.YearsOfExperience.ToString();
                    ExperienceLevel = ap.ExperienceLevel;
                    HourlyRate = ap.HourlyRate?.ToString("F2") ?? string.Empty;
                    ServiceRadius = ap.ServiceRadius?.ToString() ?? string.Empty;
                    AvailabilityStatus = ap.AvailabilityStatus ?? "Available";
                    LicenseNumber = ap.LicenseNumber;
                    Certification = ap.Certification;
                    BusinessRegistration = ap.BusinessRegistration;
                    TaxId = ap.TaxId;
                    InsuranceDetails = ap.InsuranceDetails;
                    ArtisanSpeciality = ap.ArtisanSpeciality;
                    BusinessAddress = ap.BusinessAddress;
                    ProfessionalBio = ap.ProfessionalBio;
                    About = ap.About;

                    ServicesOffered.Clear();
                    if (!string.IsNullOrWhiteSpace(ap.ServicesOffered))
                        foreach (var s in ap.ServicesOffered.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            ServicesOffered.Add(s.Trim());
                }
                else
                {
                    // User profile exists but no artisan profile yet
                    IsNewArtisanProfile = true;
                }

                UpdateDisplayName();
                UpdateInitials();
                UpdateDisplayCity();
                RecalcCompletion();
            }
            catch (UnauthorizedAccessException)
            {
                ShowToastRequested?.Invoke("Session expired. Please log in again.");
            }
            catch (Exception ex)
            {
                // Truly unexpected error — log it but show a gentle toast,
                // NOT a system Error dialog, and still allow the user to
                // fill in the form as if they are new.
                System.Diagnostics.Debug.WriteLine(
                    $"[EditArtisanProfilePageModel] Unexpected init error: {ex.Message}");
                IsNewArtisanProfile = true;
                ShowToastRequested?.Invoke("Could not load existing profile. You can still create one.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // COMMAND IMPLEMENTATIONS
        // ═══════════════════════════════════════════════════════════════

        private async Task PickAvatarAsync()
        {
            try
            {
                var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Select profile picture"
                });

                if (result != null)
                    ProfilePictureUrl = result.FullPath;
            }
            catch (Exception ex)
            {
                ShowToastRequested?.Invoke($"Could not pick photo: {ex.Message}");
            }
        }

        private void SetAvailability(string status) => AvailabilityStatus = status;

        private void AddService()
        {
            var svc = NewServiceEntry?.Trim();
            if (string.IsNullOrWhiteSpace(svc)) return;
            if (!ServicesOffered.Contains(svc))
                ServicesOffered.Add(svc);
            NewServiceEntry = string.Empty;
        }

        private void RemoveService(string service)
        {
            if (service != null)
                ServicesOffered.Remove(service);
        }

        private async Task SaveAsync()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(BusinessName))
            {
                ShowToastRequested?.Invoke("Business Name is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Specialization))
            {
                ShowToastRequested?.Invoke("Trade / Specialization is required.");
                return;
            }

            IsBusy = true;

            try
            {
                int.TryParse(YearsOfExperience, out var yearsInt);
                decimal.TryParse(HourlyRate, out var rateDecimal);
                int.TryParse(ServiceRadius, out var radiusInt);

                bool success;

                if (IsNewArtisanProfile)
                {
                    var created = await _profileService.CreateArtisanProfileAsync(new CreateMobileArtisanProfile
                    {
                        BusinessName = BusinessName,
                        Specialization = Specialization,
                        YearsOfExperience = yearsInt,
                        ExperienceLevel = ExperienceLevel ?? "Beginner",
                        HourlyRate = rateDecimal > 0 ? rateDecimal : (decimal?)null,
                        ServiceRadius = radiusInt > 0 ? radiusInt : (int?)null,
                        AvailabilityStatus = AvailabilityStatus,
                        LicenseNumber = LicenseNumber,
                        Certification = Certification,
                        BusinessRegistration = BusinessRegistration,
                        TaxId = TaxId,
                        InsuranceDetails = InsuranceDetails,
                        ArtisanSpeciality = ArtisanSpeciality,
                        BusinessAddress = BusinessAddress,
                        ServicesOffered = string.Join(", ", ServicesOffered),
                        ProfessionalBio = ProfessionalBio,
                        About = About
                    });

                    success = created != null;
                    if (success) IsNewArtisanProfile = false;
                }
                else
                {
                    var updatePayload = new UpdateMobileProfile
                    {
                        Email = Email,
                        PhoneNumber = PhoneNumber,
                        UserProfile = new MobileUserProfile
                        {
                            FullName = FullName,
                            Bio = Bio,
                            Address = Address,
                            City = City,
                            State = State,
                            Country = Country,
                            PostalCode = PostalCode,
                            ProfilePictureUrl = ProfilePictureUrl,
                            PreferredLanguage = PreferredLanguage,
                            Timezone = Timezone
                        },
                        ArtisanProfile = new MobileArtisanProfile
                        {
                            BusinessName = BusinessName,
                            Specialization = Specialization,
                            YearsOfExperience = yearsInt,
                            ExperienceLevel = ExperienceLevel,
                            HourlyRate = rateDecimal > 0 ? rateDecimal : (decimal?)null,
                            ServiceRadius = radiusInt > 0 ? radiusInt : (int?)null,
                            AvailabilityStatus = AvailabilityStatus,
                            LicenseNumber = LicenseNumber,
                            Certification = Certification,
                            BusinessRegistration = BusinessRegistration,
                            TaxId = TaxId,
                            InsuranceDetails = InsuranceDetails,
                            ArtisanSpeciality = ArtisanSpeciality,
                            BusinessAddress = BusinessAddress,
                            ServicesOffered = string.Join(", ", ServicesOffered),
                            ProfessionalBio = ProfessionalBio,
                            About = About
                        }
                    };

                    success = await _profileService.UpdateProfileAsync(updatePayload);
                }

                if (success)
                {
                    RecalcCompletion();

                    // ── Decide where to navigate after a successful save ───────
                    if (!string.IsNullOrWhiteSpace(ReturnFeedId))
                    {
                        // Came from Send Proposal flow — forward to proposal form
                        ShowToastRequested?.Invoke("Profile saved! Opening proposal form…");
                        await Task.Delay(800);
                        NavigateToProposalRequested?.Invoke(ReturnFeedId);
                    }
                    else
                    {
                        // Normal save — go back to wherever we came from
                        ShowToastRequested?.Invoke(IsProposalRedirect
                            ? "Profile saved! Returning to proposal…"
                            : "Profile saved successfully.");
                        await Task.Delay(800);
                        NavigateBackRequested?.Invoke();
                    }
                }
                else
                {
                    ShowToastRequested?.Invoke("Save failed. Please try again.");
                }
            }
            catch (UnauthorizedAccessException)
            {
                ShowToastRequested?.Invoke("Session expired. Please log in again.");
            }
            catch (Exception ex)
            {
                ShowToastRequested?.Invoke($"Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task CancelAsync()
        {
            NavigateBackRequested?.Invoke();
            await Task.CompletedTask;
        }

        // ═══════════════════════════════════════════════════════════════
        // PRIVATE HELPERS
        // ═══════════════════════════════════════════════════════════════

        private void UpdateDisplayName()
        {
            DisplayName = !string.IsNullOrWhiteSpace(BusinessName)
                ? BusinessName
                : (!string.IsNullOrWhiteSpace(FullName) ? FullName : "Artisan");
        }

        private void UpdateInitials()
        {
            if (string.IsNullOrWhiteSpace(FullName))
            {
                Initials = "?";
                return;
            }

            var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Initials = parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                : parts[0][0].ToString().ToUpperInvariant();
        }

        private void UpdateDisplayCity() =>
            DisplayCity = string.IsNullOrWhiteSpace(City) ? "Location not set" : City;

        private void RecalcCompletion()
        {
            var fields = new[]
            {
                !string.IsNullOrWhiteSpace(FullName),
                !string.IsNullOrWhiteSpace(Email),
                !string.IsNullOrWhiteSpace(PhoneNumber),
                !string.IsNullOrWhiteSpace(Address),
                !string.IsNullOrWhiteSpace(City),
                !string.IsNullOrWhiteSpace(Bio),
                !string.IsNullOrWhiteSpace(BusinessName),
                !string.IsNullOrWhiteSpace(Specialization),
                !string.IsNullOrWhiteSpace(HourlyRate),
                !string.IsNullOrWhiteSpace(ProfessionalBio),
                !string.IsNullOrWhiteSpace(About),
                ServicesOffered.Count > 0
            };

            int filled = 0;
            foreach (var f in fields) if (f) filled++;
            ProfileCompletion = Math.Round((double)filled / fields.Length * 100);
        }

        // ═══════════════════════════════════════════════════════════════
        // INOTIFYPROPERTYCHANGED
        // ═══════════════════════════════════════════════════════════════

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}