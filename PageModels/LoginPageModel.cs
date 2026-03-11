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

        private string _emailOrPhone = string.Empty;
        private string _password = string.Empty;
        private bool _isBusy = false;
        private bool _isPasswordVisible = false;
        private bool _rememberMe = false;

        private int _captchaId = 0;
        private string _captchaQuestion = string.Empty;
        private string _captchaAnswer = string.Empty;

        private bool _isPasswordMode = false;   // OTP mode by default

        private string _mainButtonText = "Send OTP Code";
        private string _toggleButtonText = "🔑 Password Login";
        private string _authModeBadgeIcon = "🛡";
        private string _authModeBadgeText = "Secure OTP Authentication";
        private string _authModeSubtitle = "Sign in with OTP for instant access";

        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private string _infoMessage = string.Empty;

        public LoginPageModel(AuthService authService)
        {
            _authService = authService;

            MainActionCommand = new Command(async () => await MainActionAsync(), () => !IsBusy);
            ToggleAuthModeCommand = new Command(ToggleAuthMode);
            TogglePasswordVisibilityCommand = new Command(() => IsPasswordVisible = !IsPasswordVisible);
            NavigateToSignUpCommand = new Command(async () => await Shell.Current.GoToAsync("RegisterPage"));
            ForgotPasswordCommand = new Command(async () => await Shell.Current.GoToAsync("ForgotPasswordPage"));
            RefreshCaptchaCommand = new Command(async () => await LoadCaptchaAsync());
            TestConnectionCommand = new Command(async () => await _authService.TestConnectionAsync());

            // Social stubs — wire up real providers as needed
            LoginWithGoogleCommand = new Command(() => Debug.WriteLine("[SOCIAL] Google"));
            LoginWithFacebookCommand = new Command(() => Debug.WriteLine("[SOCIAL] Facebook"));
            LoginWithAppleCommand = new Command(() => Debug.WriteLine("[SOCIAL] Apple"));

            _ = LoadCaptchaAsync();
        }

        // ── Core properties ───────────────────────────────────────────

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
            set { _isBusy = value; OnPropertyChanged(); ((Command)MainActionCommand).ChangeCanExecute(); }
        }

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set { _isPasswordVisible = value; OnPropertyChanged(); }
        }

        public bool RememberMe
        {
            get => _rememberMe;
            set { _rememberMe = value; OnPropertyChanged(); }
        }

        // ── Mode flags ────────────────────────────────────────────────

        /// <summary>True when using password login — shows email + password fields.</summary>
        public bool IsPasswordMode
        {
            get => _isPasswordMode;
            set { _isPasswordMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsOtpMode)); }
        }

        /// <summary>True when using OTP login — shows phone field only.</summary>
        public bool IsOtpMode => !_isPasswordMode;

        // ── Captcha ───────────────────────────────────────────────────

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

        // ── UI labels ─────────────────────────────────────────────────

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

        public string AuthModeBadgeIcon
        {
            get => _authModeBadgeIcon;
            set { _authModeBadgeIcon = value; OnPropertyChanged(); }
        }

        public string AuthModeBadgeText
        {
            get => _authModeBadgeText;
            set { _authModeBadgeText = value; OnPropertyChanged(); }
        }

        public string AuthModeSubtitle
        {
            get => _authModeSubtitle;
            set { _authModeSubtitle = value; OnPropertyChanged(); }
        }

        // ── Messages ──────────────────────────────────────────────────

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
        public ICommand TestConnectionCommand { get; }
        public ICommand LoginWithGoogleCommand { get; }
        public ICommand LoginWithFacebookCommand { get; }
        public ICommand LoginWithAppleCommand { get; }

        // ── Captcha load ──────────────────────────────────────────────

        private async Task LoadCaptchaAsync()
        {
            try
            {
                var result = await _authService.GetCaptchaAsync();
                _captchaId = result.Id;
                CaptchaQuestion = result.Question;
                CaptchaAnswer = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CAPTCHA] {ex.Message}");
                _captchaId = 0;
                CaptchaQuestion = "5 + 3 = ?";
            }
        }

        // ── Main action ───────────────────────────────────────────────

        private async Task MainActionAsync()
        {
            if (IsBusy) return;
            ClearMessages();

            if (string.IsNullOrWhiteSpace(CaptchaAnswer))
            {
                ErrorMessage = "Please answer the captcha question.";
                return;
            }

            IsBusy = true;
            try
            {
                if (IsPasswordMode) await LoginWithPasswordAsync();
                else await SendOtpAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"An error occurred: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SendOtpAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailOrPhone))
            {
                ErrorMessage = "Please enter your phone number.";
                return;
            }

            var result = await _authService.SendOtpAsync(new OtpSendRequest
            {
                Phone = EmailOrPhone.Trim(),
                CaptchaId = _captchaId,
                CaptchaAnswer = CaptchaAnswer.Trim()
            });

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
                await LoadCaptchaAsync();
            }
        }

        private async Task LoginWithPasswordAsync()
        {
            if (string.IsNullOrWhiteSpace(EmailOrPhone))
            {
                ErrorMessage = "Please enter your email address.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your password.";
                return;
            }

            var result = await _authService.LoginWithPasswordAsync(new PasswordLoginRequest
            {
                Email = EmailOrPhone.Trim(),
                Password = Password,
                CaptchaId = _captchaId,
                CaptchaAnswer = CaptchaAnswer.Trim()
            });

            if (result.Success)
            {
                SuccessMessage = "Login successful!";
                await Task.Delay(200);
                await Shell.Current.GoToAsync("//GroupChatListPage");
            }
            else
            {
                ErrorMessage = result.Error ?? "Invalid credentials. Please try again.";
                await LoadCaptchaAsync();
            }
        }

        // ── Toggle mode ───────────────────────────────────────────────

        private void ToggleAuthMode()
        {
            IsPasswordMode = !IsPasswordMode;
            EmailOrPhone = string.Empty;
            Password = string.Empty;
            ClearMessages();

            if (IsPasswordMode)
            {
                MainButtonText = "Sign In";
                ToggleButtonText = "🛡 OTP Login";
                AuthModeBadgeIcon = "🔒";
                AuthModeBadgeText = "Secure Password Authentication";
                AuthModeSubtitle = "Sign in with your password";
            }
            else
            {
                MainButtonText = "Send OTP Code";
                ToggleButtonText = "🔑 Password Login";
                AuthModeBadgeIcon = "🛡";
                AuthModeBadgeText = "Secure OTP Authentication";
                AuthModeSubtitle = "Sign in with OTP for instant access";
            }

            _ = LoadCaptchaAsync();
        }

        private void ClearMessages()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            InfoMessage = string.Empty;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
