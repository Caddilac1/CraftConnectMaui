using CraftConnect_Mobile_App.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.PageModels
{
    public class RegisterPageModel : INotifyPropertyChanged
    {
        // ════════════════════════════════════════════════════════════════════
        // Mirrors the pattern used in LoginPageModel:
        //  - INotifyPropertyChanged  (no CommunityToolkit source generators)
        //  - AuthService injected via constructor  (already registered in DI)
        //  - ICommand via new Command(...)
        // ════════════════════════════════════════════════════════════════════

        private readonly AuthService _authService;

        // ── Colour constants ─────────────────────────────────────────────────
        private const string ActiveBorder = "#F5A623";
        private const string ActiveBackground = "#FFF8EE";
        private const string ActiveLabel = "#E08A00";
        private const string InactiveBorder = "#E4EAF0";
        private const string InactiveBackground = "Transparent";
        private const string InactiveLabel = "#9BAABB";

        // ════════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ════════════════════════════════════════════════════════════════════

        public RegisterPageModel(AuthService authService)
        {
            _authService = authService;

            RegisterCommand = new Command(async () => await RegisterAsync(), () => !IsBusy);
            SelectRegistrationTypeCommand = new Command<string>(SetRegistrationType);
            TogglePasswordVisibilityCommand = new Command(() => IsPasswordVisible = !IsPasswordVisible);
            ToggleConfirmPasswordVisibilityCommand = new Command(() => IsConfirmPasswordVisible = !IsConfirmPasswordVisible);
            NavigateToLoginCommand = new Command(async () => await Shell.Current.GoToAsync("//LoginPage"));
            RegisterWithGoogleCommand = new Command(() => { ClearMessages(); InfoMessage = "Google sign-up coming soon."; });
            RegisterWithFacebookCommand = new Command(() => { ClearMessages(); InfoMessage = "Facebook sign-up coming soon."; });
            RegisterWithAppleCommand = new Command(() => { ClearMessages(); InfoMessage = "Apple sign-up coming soon."; });

            // Default role + captcha
            SetRegistrationType("Customer");
            RefreshCaptcha();
        }

        // ════════════════════════════════════════════════════════════════════
        // PERSONAL FIELDS
        // ════════════════════════════════════════════════════════════════════

        private string _firstName = string.Empty;
        private string _lastName = string.Empty;
        private string _email = string.Empty;
        private string _phone = string.Empty;
        private string _address = string.Empty;
        private string _password = string.Empty;
        private string _confirmPassword = string.Empty;

        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); ClearMessages(); } }
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); ClearMessages(); } }
        public string Email { get => _email; set { _email = value; OnPropertyChanged(); ClearMessages(); } }
        public string Phone { get => _phone; set { _phone = value; OnPropertyChanged(); ClearMessages(); } }
        public string Address { get => _address; set { _address = value; OnPropertyChanged(); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); ClearMessages(); } }
        public string ConfirmPassword { get => _confirmPassword; set { _confirmPassword = value; OnPropertyChanged(); ClearMessages(); } }

        // ════════════════════════════════════════════════════════════════════
        // BUSINESS FIELDS
        // ════════════════════════════════════════════════════════════════════

        private string _businessName = string.Empty;
        private string _businessDescription = string.Empty;
        private string _businessPhone = string.Empty;
        private string _businessEmail = string.Empty;
        private string _businessAddress = string.Empty;
        private string _locationName = string.Empty;
        private string _locationTown = string.Empty;
        private string _locationAddress = string.Empty;
        private string _locationPhone = string.Empty;

        public string BusinessName { get => _businessName; set { _businessName = value; OnPropertyChanged(); } }
        public string BusinessDescription { get => _businessDescription; set { _businessDescription = value; OnPropertyChanged(); } }
        public string BusinessPhone { get => _businessPhone; set { _businessPhone = value; OnPropertyChanged(); } }
        public string BusinessEmail { get => _businessEmail; set { _businessEmail = value; OnPropertyChanged(); } }
        public string BusinessAddress { get => _businessAddress; set { _businessAddress = value; OnPropertyChanged(); } }
        public string LocationName { get => _locationName; set { _locationName = value; OnPropertyChanged(); } }
        public string LocationTown { get => _locationTown; set { _locationTown = value; OnPropertyChanged(); } }
        public string LocationAddress { get => _locationAddress; set { _locationAddress = value; OnPropertyChanged(); } }
        public string LocationPhone { get => _locationPhone; set { _locationPhone = value; OnPropertyChanged(); } }

        // Business Type picker
        private ObservableCollection<RegisterBusinessTypeDto> _businessTypes = new();
        public ObservableCollection<RegisterBusinessTypeDto> BusinessTypes
        {
            get => _businessTypes;
            set { _businessTypes = value; OnPropertyChanged(); }
        }

        private RegisterBusinessTypeDto? _selectedBusinessType;
        public RegisterBusinessTypeDto? SelectedBusinessType
        {
            get => _selectedBusinessType;
            set { _selectedBusinessType = value; OnPropertyChanged(); }
        }

        // ════════════════════════════════════════════════════════════════════
        // PASSWORD VISIBILITY
        // ════════════════════════════════════════════════════════════════════

        private bool _isPasswordVisible = false;
        private bool _isConfirmPasswordVisible = false;

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set { _isPasswordVisible = value; OnPropertyChanged(); }
        }

        public bool IsConfirmPasswordVisible
        {
            get => _isConfirmPasswordVisible;
            set { _isConfirmPasswordVisible = value; OnPropertyChanged(); }
        }

        // ════════════════════════════════════════════════════════════════════
        // REGISTRATION TYPE
        // ════════════════════════════════════════════════════════════════════

        private string _selectedRegistrationType = "Customer";
        public string SelectedRegistrationType
        {
            get => _selectedRegistrationType;
            set { _selectedRegistrationType = value; OnPropertyChanged(); }
        }

        private bool _isBusinessSectionVisible = false;
        public bool IsBusinessSectionVisible
        {
            get => _isBusinessSectionVisible;
            set { _isBusinessSectionVisible = value; OnPropertyChanged(); }
        }

        private bool _isArtisanSelected = false;
        public bool IsArtisanSelected
        {
            get => _isArtisanSelected;
            set { _isArtisanSelected = value; OnPropertyChanged(); }
        }

        // ── Card colour / font properties ────────────────────────────────────
        private string _customerBorderColor = ActiveBorder;
        private string _customerBackgroundColor = ActiveBackground;
        private string _customerLabelColor = ActiveLabel;
        private string _customerLabelFontAttributes = "Bold";

        private string _artisanBorderColor = InactiveBorder;
        private string _artisanBackgroundColor = InactiveBackground;
        private string _artisanLabelColor = InactiveLabel;
        private string _artisanLabelFontAttributes = "None";

        private string _companyOwnerBorderColor = InactiveBorder;
        private string _companyOwnerBackgroundColor = InactiveBackground;
        private string _companyOwnerLabelColor = InactiveLabel;
        private string _companyOwnerLabelFontAttributes = "None";

        public string CustomerBorderColor { get => _customerBorderColor; set { _customerBorderColor = value; OnPropertyChanged(); } }
        public string CustomerBackgroundColor { get => _customerBackgroundColor; set { _customerBackgroundColor = value; OnPropertyChanged(); } }
        public string CustomerLabelColor { get => _customerLabelColor; set { _customerLabelColor = value; OnPropertyChanged(); } }
        public string CustomerLabelFontAttributes { get => _customerLabelFontAttributes; set { _customerLabelFontAttributes = value; OnPropertyChanged(); } }

        public string ArtisanBorderColor { get => _artisanBorderColor; set { _artisanBorderColor = value; OnPropertyChanged(); } }
        public string ArtisanBackgroundColor { get => _artisanBackgroundColor; set { _artisanBackgroundColor = value; OnPropertyChanged(); } }
        public string ArtisanLabelColor { get => _artisanLabelColor; set { _artisanLabelColor = value; OnPropertyChanged(); } }
        public string ArtisanLabelFontAttributes { get => _artisanLabelFontAttributes; set { _artisanLabelFontAttributes = value; OnPropertyChanged(); } }

        public string CompanyOwnerBorderColor { get => _companyOwnerBorderColor; set { _companyOwnerBorderColor = value; OnPropertyChanged(); } }
        public string CompanyOwnerBackgroundColor { get => _companyOwnerBackgroundColor; set { _companyOwnerBackgroundColor = value; OnPropertyChanged(); } }
        public string CompanyOwnerLabelColor { get => _companyOwnerLabelColor; set { _companyOwnerLabelColor = value; OnPropertyChanged(); } }
        public string CompanyOwnerLabelFontAttributes { get => _companyOwnerLabelFontAttributes; set { _companyOwnerLabelFontAttributes = value; OnPropertyChanged(); } }

        // ── Badge pill ───────────────────────────────────────────────────────
        private string _registrationTypeBadgeIcon = "🛒";
        private string _registrationTypeBadgeText = "Registering as Customer";

        public string RegistrationTypeBadgeIcon { get => _registrationTypeBadgeIcon; set { _registrationTypeBadgeIcon = value; OnPropertyChanged(); } }
        public string RegistrationTypeBadgeText { get => _registrationTypeBadgeText; set { _registrationTypeBadgeText = value; OnPropertyChanged(); } }

        // ── SetRegistrationType ──────────────────────────────────────────────
        private void SetRegistrationType(string type)
        {
            SelectedRegistrationType = type;
            IsBusinessSectionVisible = type is "Artisan" or "CompanyOwner";
            IsArtisanSelected = type == "Artisan";

            // Reset all cards to inactive
            CustomerBorderColor = InactiveBorder; CustomerBackgroundColor = InactiveBackground;
            CustomerLabelColor = InactiveLabel; CustomerLabelFontAttributes = "None";
            ArtisanBorderColor = InactiveBorder; ArtisanBackgroundColor = InactiveBackground;
            ArtisanLabelColor = InactiveLabel; ArtisanLabelFontAttributes = "None";
            CompanyOwnerBorderColor = InactiveBorder; CompanyOwnerBackgroundColor = InactiveBackground;
            CompanyOwnerLabelColor = InactiveLabel; CompanyOwnerLabelFontAttributes = "None";

            switch (type)
            {
                case "Customer":
                    CustomerBorderColor = ActiveBorder; CustomerBackgroundColor = ActiveBackground;
                    CustomerLabelColor = ActiveLabel; CustomerLabelFontAttributes = "Bold";
                    RegistrationTypeBadgeIcon = "🛒";
                    RegistrationTypeBadgeText = "Registering as Customer";
                    break;

                case "Artisan":
                    ArtisanBorderColor = ActiveBorder; ArtisanBackgroundColor = ActiveBackground;
                    ArtisanLabelColor = ActiveLabel; ArtisanLabelFontAttributes = "Bold";
                    RegistrationTypeBadgeIcon = "🔨";
                    RegistrationTypeBadgeText = "Registering as Artisan (includes Company)";
                    // Lazy-load business types on first switch
                    if (BusinessTypes.Count == 0) _ = LoadBusinessTypesAsync();
                    break;

                case "CompanyOwner":
                    CompanyOwnerBorderColor = ActiveBorder; CompanyOwnerBackgroundColor = ActiveBackground;
                    CompanyOwnerLabelColor = ActiveLabel; CompanyOwnerLabelFontAttributes = "Bold";
                    RegistrationTypeBadgeIcon = "🏢";
                    RegistrationTypeBadgeText = "Registering as Company Owner";
                    if (BusinessTypes.Count == 0) _ = LoadBusinessTypesAsync();
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // CAPTCHA  — local math question, same approach as LoginPageModel fallback
        // ════════════════════════════════════════════════════════════════════

        private int _captchaCorrectAnswer;
        private string _captchaQuestion = string.Empty;
        private string _captchaAnswer = string.Empty;

        public string CaptchaQuestion { get => _captchaQuestion; set { _captchaQuestion = value; OnPropertyChanged(); } }
        public string CaptchaAnswer { get => _captchaAnswer; set { _captchaAnswer = value; OnPropertyChanged(); ClearMessages(); } }

        private void RefreshCaptcha()
        {
            var rng = new Random();
            int a = rng.Next(1, 15);
            int b = rng.Next(1, 15);
            _captchaCorrectAnswer = a + b;
            CaptchaQuestion = $"What is {a} + {b} ?";
            CaptchaAnswer = string.Empty;
        }

        private bool IsCaptchaCorrect()
            => int.TryParse(CaptchaAnswer?.Trim(), out int ans) && ans == _captchaCorrectAnswer;

        // ════════════════════════════════════════════════════════════════════
        // MESSAGES + BUSY
        // ════════════════════════════════════════════════════════════════════

        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private string _infoMessage = string.Empty;
        private bool _isBusy = false;

        public string ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); } }
        public string SuccessMessage { get => _successMessage; set { _successMessage = value; OnPropertyChanged(); } }
        public string InfoMessage { get => _infoMessage; set { _infoMessage = value; OnPropertyChanged(); } }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(RegisterButtonText));
                ((Command)RegisterCommand).ChangeCanExecute();
            }
        }

        public string RegisterButtonText => IsBusy ? "Creating Account…" : "Create Account";

        private void ClearMessages()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            InfoMessage = string.Empty;
        }

        // ════════════════════════════════════════════════════════════════════
        // LOAD BUSINESS TYPES  —  delegates to AuthService
        // ════════════════════════════════════════════════════════════════════

        private async Task LoadBusinessTypesAsync()
        {
            try
            {
                InfoMessage = "Loading business types…";
                var types = await _authService.GetBusinessTypesAsync();
                BusinessTypes = new ObservableCollection<RegisterBusinessTypeDto>(types);
                InfoMessage = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BUSINESS TYPES] {ex.Message}");
                ErrorMessage = "Could not load business types. Please check your connection.";
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // REGISTER  —  delegates to AuthService
        // ════════════════════════════════════════════════════════════════════

        private async Task RegisterAsync()
        {
            if (IsBusy) return;
            ClearMessages();

            // ── Validation ───────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(FirstName))
            { ErrorMessage = "Please enter your first name."; return; }
            if (string.IsNullOrWhiteSpace(LastName))
            { ErrorMessage = "Please enter your last name."; return; }
            if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
            { ErrorMessage = "Please enter a valid email address."; return; }
            if (string.IsNullOrWhiteSpace(Phone))
            { ErrorMessage = "Please enter your phone number."; return; }
            if (Password.Length < 8)
            { ErrorMessage = "Password must be at least 8 characters."; return; }
            if (Password != ConfirmPassword)
            { ErrorMessage = "Passwords do not match."; return; }

            if (IsBusinessSectionVisible)
            {
                if (string.IsNullOrWhiteSpace(BusinessName))
                { ErrorMessage = "Please enter your business name."; return; }
                if (SelectedBusinessType == null)
                { ErrorMessage = "Please select a business type."; return; }
                if (string.IsNullOrWhiteSpace(LocationName))
                { ErrorMessage = "Please enter a name for your primary location."; return; }
                if (string.IsNullOrWhiteSpace(LocationTown))
                { ErrorMessage = "Please enter the town or city for your primary location."; return; }
            }

            if (!IsCaptchaCorrect())
            {
                ErrorMessage = "Incorrect verification answer. Please try again.";
                RefreshCaptcha();
                return;
            }

            IsBusy = true;
            try
            {
                var request = new RegisterRequest
                {
                    RegistrationType = SelectedRegistrationType,
                    FirstName = FirstName.Trim(),
                    LastName = LastName.Trim(),
                    Email = Email.Trim(),
                    Password = Password,
                    Phone = Phone.Trim(),
                    Address = NullIfEmpty(Address),
                    BusinessName = NullIfEmpty(BusinessName),
                    BusinessTypeId = SelectedBusinessType?.BusinessTypeId,
                    BusinessDescription = NullIfEmpty(BusinessDescription),
                    BusinessPhone = NullIfEmpty(BusinessPhone),
                    BusinessEmail = NullIfEmpty(BusinessEmail),
                    BusinessAddress = NullIfEmpty(BusinessAddress),
                    LocationName = NullIfEmpty(LocationName),
                    LocationTown = NullIfEmpty(LocationTown),
                    LocationAddress = NullIfEmpty(LocationAddress),
                    LocationPhone = NullIfEmpty(LocationPhone),
                };

                var response = await _authService.RegisterAsync(request);

                if (response.Success)
                {
                    SuccessMessage = "Account created successfully! Redirecting…";
                    await Task.Delay(800);
                    await Shell.Current.GoToAsync("//LoginPage");
                }
                else
                {
                    ErrorMessage = response.Message ?? "Registration failed. Please try again.";
                    RefreshCaptcha();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REGISTER] {ex.Message}");
                ErrorMessage = $"An error occurred: {ex.Message}";
                RefreshCaptcha();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string? NullIfEmpty(string? s)
            => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();

        // ════════════════════════════════════════════════════════════════════
        // COMMANDS
        // ════════════════════════════════════════════════════════════════════

        public ICommand RegisterCommand { get; }
        public ICommand SelectRegistrationTypeCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }
        public ICommand ToggleConfirmPasswordVisibilityCommand { get; }
        public ICommand NavigateToLoginCommand { get; }
        public ICommand RegisterWithGoogleCommand { get; }
        public ICommand RegisterWithFacebookCommand { get; }
        public ICommand RegisterWithAppleCommand { get; }

        // ════════════════════════════════════════════════════════════════════
        // INotifyPropertyChanged
        // ════════════════════════════════════════════════════════════════════

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    // ════════════════════════════════════════════════════════════════════════
    // DTOs used by RegisterPageModel
    // ════════════════════════════════════════════════════════════════════════

    public class RegisterBusinessTypeDto
    {
        public int BusinessTypeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class RegisterRequest
    {
        public string RegistrationType { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? BusinessName { get; set; }
        public int? BusinessTypeId { get; set; }
        public string? BusinessDescription { get; set; }
        public string? BusinessPhone { get; set; }
        public string? BusinessEmail { get; set; }
        public string? BusinessAddress { get; set; }
        public string? LocationName { get; set; }
        public string? LocationTown { get; set; }
        public string? LocationAddress { get; set; }
        public string? LocationPhone { get; set; }
    }

    public class RegisterApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }
}