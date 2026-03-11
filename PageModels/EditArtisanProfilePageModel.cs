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
        // ═══════════════════════════════════════════════════════════════

        public string? ReturnFeedId { get; set; }
        public string? ReturnFeedTitle { get; set; }

        // ═══════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════

        public EditArtisanProfilePageModel(IProfileApiService profileService)
        {
            _profileService = profileService;

            PickAvatarCommand = new Command(async () => await PickAvatarAsync());
            SaveCommand = new Command(async () => await SaveAsync(), () => !IsBusy);
            CancelCommand = new Command(async () => await CancelAsync());

            // kept for any legacy bindings that may still exist
            SetAvailableCommand = new Command(() => SetAvailability("Available"));
            SetBusyCommand = new Command(() => SetAvailability("Busy"));
            SetUnavailableCommand = new Command(() => SetAvailability("Unavailable"));
            AddServiceCommand = new Command(AddService);
            RemoveServiceCommand = new Command<string>(RemoveService);

            ServicesOffered = new ObservableCollection<string>();

            TradeOptions = new List<string>
            {
                "Carpenter", "Electrician", "Plumber", "Mason / Bricklayer",
                "Painter", "Tiler", "Welder / Fabricator", "AC Technician",
                "Roofer", "Glazier", "Landscaper", "Interior Designer",
                "General Contractor", "Other"
            };

            // kept so bindings in any other pages don't break
            LanguageOptions = new List<string>
            {
                "English", "Twi", "Ga", "Ewe", "Hausa", "French", "Other"
            };
            TimezoneOptions = new List<string>
            {
                "Africa/Accra", "Africa/Lagos", "Africa/Nairobi",
                "Europe/London", "America/New_York", "America/Los_Angeles"
            };
            ExperienceLevelOptions = new List<string>
            {
                "Beginner", "Intermediate", "Advanced", "Expert"
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // PROPOSAL-REDIRECT FLAG
        // ═══════════════════════════════════════════════════════════════

        private bool _isProposalRedirect;
        public bool IsProposalRedirect
        {
            get => _isProposalRedirect;
            set { _isProposalRedirect = value; OnPropertyChanged(); }
        }

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
            set { _profilePictureUrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProfilePicture)); }
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

        // kept for legacy avatar/display city bindings
        private string _displayCity;
        public string DisplayCity
        {
            get => _displayCity;
            set { _displayCity = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // PROFILE COMPLETION  (kept — used by RecalcCompletion)
        // ═══════════════════════════════════════════════════════════════

        private double _profileCompletion;
        public double ProfileCompletion
        {
            get => _profileCompletion;
            set { _profileCompletion = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProfileCompletionText)); }
        }
        public string ProfileCompletionText => $"{ProfileCompletion:F0}%";

        // ═══════════════════════════════════════════════════════════════
        // PLATFORM STATS  (kept for any legacy stats card bindings)
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
        // SECTION 1 — PERSONAL DETAILS  (pre-filled, still editable)
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
            set { _phoneNumber = value; OnPropertyChanged(); RecalcCompletion(); }
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

        private string _address;
        public string Address
        {
            get => _address;
            set { _address = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // EXTENDED PERSONAL INFO  (still available for other pages)
        // ═══════════════════════════════════════════════════════════════

        private string _city;
        public string City
        {
            get => _city;
            set { _city = value; OnPropertyChanged(); UpdateDisplayCity(); }
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
            set { _bio = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // SECTION 2 — BUSINESS INFORMATION
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

        private string _professionalBio;   // used as "Business Description" in the form
        public string ProfessionalBio
        {
            get => _professionalBio;
            set { _professionalBio = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _businessAddress;
        public string BusinessAddress
        {
            get => _businessAddress;
            set { _businessAddress = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _businessPhone;
        public string BusinessPhone
        {
            get => _businessPhone;
            set { _businessPhone = value; OnPropertyChanged(); }
        }

        private string _businessEmail;
        public string BusinessEmail
        {
            get => _businessEmail;
            set { _businessEmail = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // SECTION 3 — PRIMARY BUSINESS LOCATION
        // ═══════════════════════════════════════════════════════════════

        private string _locationName;
        public string LocationName
        {
            get => _locationName;
            set { _locationName = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _locationAddress;
        public string LocationAddress
        {
            get => _locationAddress;
            set { _locationAddress = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _locationTown;
        public string LocationTown
        {
            get => _locationTown;
            set { _locationTown = value; OnPropertyChanged(); RecalcCompletion(); }
        }

        private string _locationPhone;
        public string LocationPhone
        {
            get => _locationPhone;
            set { _locationPhone = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // EXTRA ARTISAN FIELDS  (kept — sent to API on update path)
        // ═══════════════════════════════════════════════════════════════

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

        private string _about;
        public string About
        {
            get => _about;
            set { _about = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> ServicesOffered { get; }

        private string _newServiceEntry;
        public string NewServiceEntry
        {
            get => _newServiceEntry;
            set { _newServiceEntry = value; OnPropertyChanged(); }
        }

        // ═══════════════════════════════════════════════════════════════
        // PICKER LISTS
        // ═══════════════════════════════════════════════════════════════

        public List<string> TradeOptions { get; }
        public List<string> LanguageOptions { get; }
        public List<string> TimezoneOptions { get; }
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

        public event Action<string> ShowToastRequested;
        public event Action NavigateBackRequested;
        public event Action<string> NavigateToProposalRequested;

        // ═══════════════════════════════════════════════════════════════
        // INITIALISE FROM API
        // ═══════════════════════════════════════════════════════════════

        public async Task InitialiseAsync(bool isProposalRedirect = false)
        {
            IsProposalRedirect = isProposalRedirect;
            IsBusy = true;

            try
            {
                MobileProfileDetails details = null;
                try
                {
                    details = await _profileService.GetMyProfileAsync();
                }
                catch (UnauthorizedAccessException) { throw; }
                catch (Exception fetchEx)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[EditArtisanProfilePageModel] Profile fetch skipped: {fetchEx.Message}");
                }

                if (details == null
                    || (details.UserProfile == null && details.ArtisanProfile == null))
                {
                    IsNewArtisanProfile = true;
                    UpdateDisplayName();
                    UpdateInitials();
                    UpdateDisplayCity();
                    RecalcCompletion();
                    return;
                }

                // ── Account fields ────────────────────────────────────
                Email = details.Email;
                PhoneNumber = details.PhoneNumber;

                // ── User profile → pre-fill personal section ──────────
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

                    // Pre-fill location section from personal address data
                    // so the user doesn't start with a blank Location card.
                    if (string.IsNullOrWhiteSpace(LocationName))
                        LocationName = "Main Workshop";
                    if (string.IsNullOrWhiteSpace(LocationAddress))
                        LocationAddress = up.Address;
                    if (string.IsNullOrWhiteSpace(LocationTown))
                        LocationTown = up.City;
                    if (string.IsNullOrWhiteSpace(LocationPhone))
                        LocationPhone = details.PhoneNumber;
                }

                // ── Artisan profile ───────────────────────────────────
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

                    // Overwrite location pre-fill with saved artisan address if present
                    if (!string.IsNullOrWhiteSpace(ap.BusinessAddress))
                        LocationAddress = ap.BusinessAddress;

                    ServicesOffered.Clear();
                    if (!string.IsNullOrWhiteSpace(ap.ServicesOffered))
                        foreach (var s in ap.ServicesOffered.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            ServicesOffered.Add(s.Trim());
                }
                else
                {
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
        // SAVE
        // ═══════════════════════════════════════════════════════════════

        private async Task SaveAsync()
        {
            if (IsBusy) return;

            // ── Validation ────────────────────────────────────────────
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
            if (string.IsNullOrWhiteSpace(ProfessionalBio))
            {
                ShowToastRequested?.Invoke("Business Description is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(BusinessAddress))
            {
                ShowToastRequested?.Invoke("Business Address is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(LocationName))
            {
                ShowToastRequested?.Invoke("Location Name is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(LocationAddress))
            {
                ShowToastRequested?.Invoke("Location Address is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(LocationTown))
            {
                ShowToastRequested?.Invoke("Town / City is required.");
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
                    var created = await _profileService.CreateArtisanProfileAsync(
                        new CreateMobileArtisanProfile
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
                    success = await _profileService.UpdateProfileAsync(new UpdateMobileProfile
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
                    });
                }

                if (success)
                {
                    RecalcCompletion();

                    if (!string.IsNullOrWhiteSpace(ReturnFeedId))
                    {
                        ShowToastRequested?.Invoke("Profile saved! Opening proposal form…");
                        await Task.Delay(800);
                        NavigateToProposalRequested?.Invoke(ReturnFeedId);
                    }
                    else
                    {
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

        // ═══════════════════════════════════════════════════════════════
        // COMMAND HELPERS
        // ═══════════════════════════════════════════════════════════════

        private async Task PickAvatarAsync()
        {
            try
            {
                var result = await MediaPicker.Default.PickPhotoAsync(
                    new MediaPickerOptions { Title = "Select profile picture" });
                if (result != null) ProfilePictureUrl = result.FullPath;
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
            if (!ServicesOffered.Contains(svc)) ServicesOffered.Add(svc);
            NewServiceEntry = string.Empty;
        }

        private void RemoveService(string service)
        {
            if (service != null) ServicesOffered.Remove(service);
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
            if (string.IsNullOrWhiteSpace(FullName)) { Initials = "?"; return; }
            var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Initials = parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
                : parts[0][0].ToString().ToUpperInvariant();
        }

        private void UpdateDisplayCity() =>
            DisplayCity = string.IsNullOrWhiteSpace(City) ? "Location not set" : City;

        private void RecalcCompletion()
        {
            // Core required fields from the new 3-section form
            var fields = new[]
            {
                !string.IsNullOrWhiteSpace(FullName),
                !string.IsNullOrWhiteSpace(Email),
                !string.IsNullOrWhiteSpace(PhoneNumber),
                !string.IsNullOrWhiteSpace(Address),
                !string.IsNullOrWhiteSpace(BusinessName),
                !string.IsNullOrWhiteSpace(Specialization),
                !string.IsNullOrWhiteSpace(ProfessionalBio),
                !string.IsNullOrWhiteSpace(BusinessAddress),
                !string.IsNullOrWhiteSpace(LocationName),
                !string.IsNullOrWhiteSpace(LocationAddress),
                !string.IsNullOrWhiteSpace(LocationTown),
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