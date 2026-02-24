using CraftConnect_Mobile_App.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.PageModels
{
    public class LoginPageModel : INotifyPropertyChanged
    {
        private readonly AuthService _authService;

        // Basic fields
        private string _emailOrPhone = string.Empty;
        private string _password = string.Empty;
        private bool _isBusy = false;
        private bool _isPasswordVisible = false;
        private bool _rememberMe = false;

        // Captcha
        private int _captchaId = 0;
        private string _captchaQuestion = string.Empty;
        private string _captchaAnswer = string.Empty;

        // Mode
        private bool _isPasswordMode = false;
        private string _mainButtonText = "Send OTP";
        private string _toggleButtonText = "🔑 Use Password Instead";
        private string _authModeBadge = "🔐 OTP Authentication";

        // Messages
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private string _infoMessage = string.Empty;

        public LoginPageModel(AuthService authService)
        {
            _authService = authService;

            MainActionCommand = new Command(async () => await MainActionAsync(), () => !IsBusy);
            ToggleAuthModeCommand = new Command(ToggleAuthMode);
            TogglePasswordVisibilityCommand = new Command(TogglePasswordVisibility);
            NavigateToSignUpCommand = new Command(async () => await Shell.Current.GoToAsync("RegisterPage"));
            ForgotPasswordCommand = new Command(async () => await Shell.Current.GoToAsync("ForgotPasswordPage"));
            RefreshCaptchaCommand = new Command(async () => await LoadCaptchaAsync());

            // Load captcha on startup
            _ = LoadCaptchaAsync();

            Debug.WriteLine("[LOGIN PAGE MODEL] Initialized");
        }

        // ── Properties ────────────────────────────────────────────────

        /// <summary>
        /// Unified email-or-phone field (used by both OTP and Password modes).
        /// In OTP mode the value is treated as a phone number;
        /// in Password mode it is treated as an email address.
        /// </summary>
        public string EmailOrPhone
        {
            get => _emailOrPhone;
            set { _emailOrPhone = value; OnPropertyChanged(); ClearMessages(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value; OnPropertyChanged(); ClearMessages(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                ((Command)MainActionCommand).ChangeCanExecute();
            }
        }

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set { _isPasswordVisible = value; OnPropertyChanged(); }
        }

        public bool IsPasswordMode
        {
            get => _isPasswordMode;
            set { _isPasswordMode = value; OnPropertyChanged(); }
        }

        public bool RememberMe
        {
            get => _rememberMe;
            set { _rememberMe = value; OnPropertyChanged(); }
        }

        public string CaptchaQuestion
        {
            get => _captchaQuestion;
            set { _captchaQuestion = value; OnPropertyChanged(); }
        }

        public string CaptchaAnswer
        {
            get => _captchaAnswer;
            set { _captchaAnswer = value; OnPropertyChanged(); ClearMessages(); }
        }

        public string MainButtonText
        {
            get => _mainButtonText;
            set { _mainButtonText = value; OnPropertyChanged(); }
        }

        public string ToggleButtonText
        {
            get => _toggleButtonText;
            set { _toggleButtonText = value; OnPropertyChanged(); }
        }

        public string AuthModeBadge
        {
            get => _authModeBadge;
            set { _authModeBadge = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public string SuccessMessage
        {
            get => _successMessage;
            set { _successMessage = value; OnPropertyChanged(); }
        }

        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────

        public ICommand MainActionCommand { get; }
        public ICommand ToggleAuthModeCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }
        public ICommand NavigateToSignUpCommand { get; }
        public ICommand ForgotPasswordCommand { get; }
        public ICommand RefreshCaptchaCommand { get; }

        // ── Load Captcha ──────────────────────────────────────────────

        private async Task LoadCaptchaAsync()
        {
            try
            {
                var result = await _authService.GetCaptchaAsync();
                _captchaId = result.Id;
                CaptchaQuestion = result.Question;
                CaptchaAnswer = string.Empty;
                Debug.WriteLine($"[CAPTCHA] Loaded: {CaptchaQuestion}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CAPTCHA] Failed to load: {ex.Message}");
                // Fallback
                _captchaId = 0;
                CaptchaQuestion = "What is 5 + 3?";
            }
        }

        // ── Main Action ───────────────────────────────────────────────

        private async Task MainActionAsync()
        {
            if (IsBusy) return;

            ClearMessages();

            if (string.IsNullOrWhiteSpace(CaptchaAnswer))
            {
                ErrorMessage = "Please answer the captcha question";
                return;
            }

            IsBusy = true;

            try
            {
                if (IsPasswordMode)
                    await LoginWithPasswordAsync();
                else
                    await SendOtpAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MAIN ACTION] Exception: {ex.Message}");
                ErrorMessage = $"An error occurred: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Send OTP  →  POST /api/auth/login/otp/send ───────────────

        private async Task SendOtpAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailOrPhone))
            {
                ErrorMessage = "Please enter your phone number";
                return;
            }

            Debug.WriteLine($"[SEND OTP] Phone: {EmailOrPhone}");

            var request = new OtpSendRequest
            {
                Phone = EmailOrPhone.Trim(),
                CaptchaId = _captchaId,
                CaptchaAnswer = CaptchaAnswer.Trim()
            };

            var result = await _authService.SendOtpAsync(request);

            Debug.WriteLine($"[SEND OTP] Success: {result.Success}");

            if (result.Success)
            {
                SuccessMessage = "OTP sent! Check your phone.";
                await Task.Delay(800);

                await Shell.Current.GoToAsync("OtpVerificationPage",
                    new Dictionary<string, object> { { "phone", EmailOrPhone.Trim() } });
            }
            else
            {
                ErrorMessage = result.Error ?? "Failed to send OTP. Please try again.";
                await LoadCaptchaAsync(); // Refresh captcha on failure
            }
        }

        // ── Login with Password  →  POST /api/auth/login/password ────

        private async Task LoginWithPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailOrPhone))
            {
                ErrorMessage = "Please enter your email";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your password";
                return;
            }

            Debug.WriteLine($"[LOGIN PASSWORD] Email: {EmailOrPhone}");

            var request = new PasswordLoginRequest
            {
                Email = EmailOrPhone.Trim(),
                Password = Password,
                CaptchaId = _captchaId,
                CaptchaAnswer = CaptchaAnswer.Trim()
            };

            var result = await _authService.LoginWithPasswordAsync(request);

            Debug.WriteLine($"[LOGIN PASSWORD] Success: {result.Success}");

            if (result.Success)
            {
                SuccessMessage = "Login successful!";
                await Task.Delay(200);
                await Shell.Current.GoToAsync("//main/GroupChatListPage");
            }
            else
            {
                ErrorMessage = result.Error ?? "Invalid credentials. Please try again.";
                await LoadCaptchaAsync(); // Refresh captcha on failure
            }
        }

        // ── Toggle Auth Mode ──────────────────────────────────────────

        private void ToggleAuthMode()
        {
            IsPasswordMode = !IsPasswordMode;
            Password = string.Empty;
            ClearMessages();

            if (IsPasswordMode)
            {
                MainButtonText = "Sign In";
                ToggleButtonText = "📱 Use OTP Instead";
                AuthModeBadge = "🔑 Password Authentication";
            }
            else
            {
                MainButtonText = "Send OTP";
                ToggleButtonText = "🔑 Use Password Instead";
                AuthModeBadge = "🔐 OTP Authentication";
            }

            // Refresh captcha when switching modes
            _ = LoadCaptchaAsync();

            Debug.WriteLine($"[TOGGLE MODE] Switched to: {(IsPasswordMode ? "Password" : "OTP")} mode");
        }

        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        private void ClearMessages()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            InfoMessage = string.Empty;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}