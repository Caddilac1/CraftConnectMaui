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

        // OTP-first mode fields
        private bool _isPasswordMode = false; // false = OTP mode, true = Password mode
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

            // Commands
            MainActionCommand = new Command(async () => await MainActionAsync(), () => !IsBusy);
            ToggleAuthModeCommand = new Command(ToggleAuthMode);
            TogglePasswordVisibilityCommand = new Command(TogglePasswordVisibility);
            //TestConnectionCommand = new Command(async () => await TestConnectionAsync());
            NavigateToSignUpCommand = new Command(async () => await NavigateToSignUpAsync());
            ForgotPasswordCommand = new Command(async () => await ForgotPasswordAsync());

            Debug.WriteLine($"[VIEWMODEL] LoginPageModel initialized in OTP mode");
        }

        // Properties
        public string EmailOrPhone
        {
            get => _emailOrPhone;
            set
            {
                _emailOrPhone = value;
                OnPropertyChanged();
                ClearMessages();
            }
        }

        public string Password
        {
            get => _password;
            set
            {
                _password = value;
                OnPropertyChanged();
                ClearMessages();
            }
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
            set
            {
                _isPasswordVisible = value;
                OnPropertyChanged();
            }
        }

        public bool RememberMe
        {
            get => _rememberMe;
            set
            {
                _rememberMe = value;
                OnPropertyChanged();
            }
        }

        public bool IsPasswordMode
        {
            get => _isPasswordMode;
            set
            {
                _isPasswordMode = value;
                OnPropertyChanged();
            }
        }

        public string MainButtonText
        {
            get => _mainButtonText;
            set
            {
                _mainButtonText = value;
                OnPropertyChanged();
            }
        }

        public string ToggleButtonText
        {
            get => _toggleButtonText;
            set
            {
                _toggleButtonText = value;
                OnPropertyChanged();
            }
        }

        public string AuthModeBadge
        {
            get => _authModeBadge;
            set
            {
                _authModeBadge = value;
                OnPropertyChanged();
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        public string SuccessMessage
        {
            get => _successMessage;
            set
            {
                _successMessage = value;
                OnPropertyChanged();
            }
        }

        public string InfoMessage
        {
            get => _infoMessage;
            set
            {
                _infoMessage = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand MainActionCommand { get; }
        public ICommand ToggleAuthModeCommand { get; }
        public ICommand TogglePasswordVisibilityCommand { get; }
        public ICommand TestConnectionCommand { get; }
        public ICommand NavigateToSignUpCommand { get; }
        public ICommand ForgotPasswordCommand { get; }

        // ============================================================
        // MAIN ACTION - Either Send OTP or Login with Password
        // ============================================================
        private async Task MainActionAsync()
        {
            if (IsBusy) return;

            Debug.WriteLine($"[MAIN ACTION] Starting - Mode: {(IsPasswordMode ? "Password" : "OTP")}");
            ClearMessages();

            // Validate email/phone
            if (string.IsNullOrWhiteSpace(EmailOrPhone))
            {
                ErrorMessage = "Please enter your email or phone number";
                return;
            }

            IsBusy = true;

            try
            {
                if (IsPasswordMode)
                {
                    // Password Mode - Login with password
                    await LoginWithPasswordAsync();
                }
                else
                {
                    // OTP Mode - Send OTP
                    await SendOtpAsync();
                }
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

        // ============================================================
        // SEND OTP
        // ============================================================
        private async Task SendOtpAsync()
        {
            Debug.WriteLine($"[SEND OTP] Sending OTP to: {EmailOrPhone}");

            try
            {
                var request = new LoginRequest
                {
                    EmailOrPhone = EmailOrPhone.Trim(),
                    UsePassword = false
                };

                var response = await _authService.LoginAsync(request);

                Debug.WriteLine($"[SEND OTP] Response - Success: {response.Success}, RequiresOtp: {response.RequiresOtp}");

                if (response.Success && response.RequiresOtp)
                {
                    // OTP sent successfully - navigate to OTP verification page
                    SuccessMessage = "OTP sent to your email!";
                    Debug.WriteLine($"[SEND OTP] Navigating to OTP verification page");

                    await Task.Delay(1000); // Brief delay to show message

                    // Navigate to OTP verification page
                    var navigationParameter = new Dictionary<string, object>
                    {
                        { "email", response.Email },
                        { "otpToken", response.OtpToken },
                        { "hasPassword", response.HasPassword }
                    };

                    await Shell.Current.GoToAsync("OtpVerificationPage", navigationParameter);
                }
                else if (!response.Success && response.RequiresOtp)
                {
                    // Account exists but no password - OTP sent
                    InfoMessage = response.Message;

                    await Task.Delay(1500);

                    var navigationParameter = new Dictionary<string, object>
                    {
                        { "email", response.Email },
                        { "otpToken", response.OtpToken },
                        { "hasPassword", response.HasPassword }
                    };

                    await Shell.Current.GoToAsync("OtpVerificationPage", navigationParameter);
                }
                else
                {
                    ErrorMessage = response.Message ?? "Failed to send OTP";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SEND OTP] Exception: {ex.Message}");
                ErrorMessage = "Failed to send OTP. Please try again.";
            }
        }

        // ============================================================
        // LOGIN WITH PASSWORD
        // ============================================================
        private async Task LoginWithPasswordAsync()
        {
            Debug.WriteLine($"[LOGIN PASSWORD] Logging in with password");

            // Validate password
            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your password";
                return;
            }

            try
            {
                var request = new LoginRequest
                {
                    EmailOrPhone = EmailOrPhone.Trim(),
                    Password = Password,
                    UsePassword = true
                };

                var response = await _authService.LoginAsync(request);

                Debug.WriteLine($"[LOGIN PASSWORD] Response - Success: {response.Success}");

                if (response.Success && !string.IsNullOrEmpty(response.Token))
                {
                    // Login successful
                    SuccessMessage = "Login successful!";

                    // Verify token was saved
                    var savedToken = await SecureStorage.GetAsync("auth_token");
                    Debug.WriteLine($"[LOGIN PASSWORD] Token verified: {!string.IsNullOrEmpty(savedToken)}");

                    if (!string.IsNullOrEmpty(savedToken))
                    {
                        await Task.Delay(200); // Small delay for SecureStorage
                        Debug.WriteLine($"[LOGIN PASSWORD] Navigating to main page");
                        await Shell.Current.GoToAsync("//main/GroupChatListPage");
                    }
                    else
                    {
                        ErrorMessage = "Login succeeded but token storage failed. Please try again.";
                    }
                }
                else if (!response.Success && response.RequiresOtp)
                {
                    // No password set - need to use OTP
                    ErrorMessage = response.Message ?? "No password set. Please use OTP login.";

                    // Optionally auto-switch to OTP mode
                    await Task.Delay(2000);
                    ToggleAuthMode();
                }
                else
                {
                    ErrorMessage = response.Message ?? "Invalid credentials";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LOGIN PASSWORD] Exception: {ex.Message}");
                ErrorMessage = $"Login failed: {ex.Message}";
            }
        }

        // ============================================================
        // TOGGLE AUTH MODE
        // ============================================================
        private void ToggleAuthMode()
        {
            IsPasswordMode = !IsPasswordMode;
            Password = string.Empty; // Clear password when switching
            ClearMessages();

            Debug.WriteLine($"[TOGGLE MODE] Switched to: {(IsPasswordMode ? "Password" : "OTP")} mode");

            if (IsPasswordMode)
            {
                // Switched to Password Mode
                MainButtonText = "Sign In";
                ToggleButtonText = "📧 Use OTP Instead";
                AuthModeBadge = "🔑 Password Authentication";
            }
            else
            {
                // Switched to OTP Mode
                MainButtonText = "Send OTP";
                ToggleButtonText = "🔑 Use Password Instead";
                AuthModeBadge = "🔐 OTP Authentication";
            }
        }

        // ============================================================
        // TOGGLE PASSWORD VISIBILITY
        // ============================================================
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
            Debug.WriteLine($"[PASSWORD] Visibility toggled to: {IsPasswordVisible}");
        }

        // ============================================================
        // NAVIGATE TO SIGN UP
        // ============================================================
        private async Task NavigateToSignUpAsync()
        {
            Debug.WriteLine($"[NAVIGATION] Navigating to sign up page");
            await Shell.Current.GoToAsync("RegisterPage");
        }

        // ============================================================
        // FORGOT PASSWORD
        // ============================================================
        private async Task ForgotPasswordAsync()
        {
            Debug.WriteLine($"[NAVIGATION] Navigating to forgot password page");
            await Shell.Current.GoToAsync("ForgotPasswordPage");
        }

        // ============================================================
        // TEST CONNECTION
        // ============================================================
        /*private async Task TestConnectionAsync()
        {
            if (IsBusy) return;

            Debug.WriteLine($"[TEST CONNECTION] Starting connection test");
            IsBusy = true;
            InfoMessage = "Testing connection...";

            try
            {
                var basicHttpResult = await _authService.TestBasicHttp();
                Debug.WriteLine($"[TEST CONNECTION] Basic HTTP result: {basicHttpResult}");

                var dnsResult = await _authService.TestDnsResolution();
                Debug.WriteLine($"[TEST CONNECTION] DNS result: {dnsResult}");

                var isConnected = await _authService.TestConnectionAsync();
                Debug.WriteLine($"[TEST CONNECTION] API connection result: {isConnected}");

                if (isConnected)
                {
                    Debug.WriteLine($"[TEST CONNECTION] All tests passed");
                    await Application.Current.MainPage.DisplayAlert(
                        "Success",
                        $"Successfully connected to API!\n\nDebug Info:\n{basicHttpResult}\n{dnsResult}",
                        "OK");
                    InfoMessage = string.Empty;
                }
                else
                {
                    Debug.WriteLine($"[TEST CONNECTION] API connection failed");
                    ErrorMessage = $"Connection failed.\nDebug Info:\n{basicHttpResult}\n{dnsResult}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TEST CONNECTION] Exception: {ex.Message}");
                ErrorMessage = $"Connection test failed: {ex.Message}";
            }
            finally
            {
                Debug.WriteLine($"[TEST CONNECTION] Connection test completed");
                IsBusy = false;
            }
        }*/

        // ============================================================
        // CLEAR MESSAGES
        // ============================================================
        private void ClearMessages()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            InfoMessage = string.Empty;
        }

        // ============================================================
        // PROPERTY CHANGED
        // ============================================================
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}