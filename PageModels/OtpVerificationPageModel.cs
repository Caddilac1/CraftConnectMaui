using CraftConnect_Mobile_App.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Diagnostics;
using System.Timers;

namespace CraftConnect_Mobile_App.PageModels
{
    [QueryProperty(nameof(Email), "email")]
    [QueryProperty(nameof(OtpToken), "otpToken")]
    [QueryProperty(nameof(HasPasswordOption), "hasPassword")]
    public class OtpVerificationPageModel : INotifyPropertyChanged, IDisposable
    {
        private readonly AuthService _authService;
        private System.Timers.Timer _resendTimer;
        private int _secondsRemaining = 60;

        private string _email = string.Empty;
        private string _otpToken = string.Empty;
        private string _otpCode = string.Empty;
        private bool _hasPasswordOption;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private string _infoMessage = string.Empty;
        private bool _canResend;
        private string _timerText = "Resend OTP in 60s";

        public OtpVerificationPageModel(AuthService authService)
        {
            _authService = authService;

            Debug.WriteLine($"[OTP VM] ============================================");
            Debug.WriteLine($"[OTP VM] Constructor called");
            Debug.WriteLine($"[OTP VM] ============================================");

            VerifyOtpCommand = new Command(async () => await VerifyOtpAsync(), () => !IsBusy);
            ResendOtpCommand = new Command(async () => await ResendOtpAsync(), () => !IsBusy && CanResend);
            SwitchToPasswordCommand = new Command(async () => await SwitchToPasswordAsync());
            BackToLoginCommand = new Command(async () => await BackToLoginAsync());

            StartResendTimer();
        }

        // Properties
        public string Email
        {
            get => _email;
            set
            {
                _email = Uri.UnescapeDataString(value ?? string.Empty);
                Debug.WriteLine($"[OTP VM] Email set to: {_email}");
                OnPropertyChanged();
            }
        }

        public string OtpToken
        {
            get => _otpToken;
            set
            {
                _otpToken = Uri.UnescapeDataString(value ?? string.Empty);
                Debug.WriteLine($"[OTP VM] OtpToken set to: {_otpToken}");
                OnPropertyChanged();
            }
        }

        public string OtpCode
        {
            get => _otpCode;
            set
            {
                _otpCode = value;
                Debug.WriteLine($"[OTP VM] OtpCode set to: {_otpCode}");
                OnPropertyChanged();
                ClearMessages();
            }
        }

        public bool HasPasswordOption
        {
            get => _hasPasswordOption;
            set
            {
                _hasPasswordOption = value;
                Debug.WriteLine($"[OTP VM] HasPasswordOption set to: {_hasPasswordOption}");
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                Debug.WriteLine($"[OTP VM] IsBusy set to: {_isBusy}");
                OnPropertyChanged();
                ((Command)VerifyOtpCommand).ChangeCanExecute();
                ((Command)ResendOtpCommand).ChangeCanExecute();
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

        public bool CanResend
        {
            get => _canResend;
            set
            {
                _canResend = value;
                OnPropertyChanged();
                ((Command)ResendOtpCommand).ChangeCanExecute();
            }
        }

        public string TimerText
        {
            get => _timerText;
            set
            {
                _timerText = value;
                OnPropertyChanged();
            }
        }

        // Commands
        public ICommand VerifyOtpCommand { get; }
        public ICommand ResendOtpCommand { get; }
        public ICommand SwitchToPasswordCommand { get; }
        public ICommand BackToLoginCommand { get; }

        // Timer
        private void StartResendTimer()
        {
            _secondsRemaining = 60;
            CanResend = false;

            _resendTimer?.Dispose();
            _resendTimer = new System.Timers.Timer(1000);

            _resendTimer.Elapsed += (sender, e) =>
            {
                _secondsRemaining--;

                if (_secondsRemaining > 0)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        TimerText = $"Resend OTP in {_secondsRemaining}s";
                    });
                }
                else
                {
                    _resendTimer?.Stop();
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        CanResend = true;
                        TimerText = string.Empty;
                    });
                }
            };

            _resendTimer.Start();
        }

        // Verify OTP
        private async Task VerifyOtpAsync()
        {
            Debug.WriteLine($"\n[OTP VM] ============================================");
            Debug.WriteLine($"[OTP VM] VerifyOtpAsync called");
            Debug.WriteLine($"[OTP VM] IsBusy: {IsBusy}");
            Debug.WriteLine($"[OTP VM] Email: {Email}");
            Debug.WriteLine($"[OTP VM] OtpCode: {OtpCode}");
            Debug.WriteLine($"[OTP VM] OtpToken: {OtpToken}");
            Debug.WriteLine($"[OTP VM] ============================================");

            if (IsBusy)
            {
                Debug.WriteLine($"[OTP VM] Already busy, returning");
                return;
            }

            ClearMessages();

            if (string.IsNullOrWhiteSpace(OtpCode))
            {
                ErrorMessage = "Please enter the OTP code";
                Debug.WriteLine($"[OTP VM] ERROR: OTP code is empty");
                return;
            }

            if (OtpCode.Length != 6)
            {
                ErrorMessage = "OTP code must be 6 digits";
                Debug.WriteLine($"[OTP VM] ERROR: OTP code length is {OtpCode.Length}, expected 6");
                return;
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Email is missing. Please restart the login process.";
                Debug.WriteLine($"[OTP VM] ERROR: Email is empty");
                return;
            }

            if (string.IsNullOrWhiteSpace(OtpToken))
            {
                ErrorMessage = "OTP token is missing. Please restart the login process.";
                Debug.WriteLine($"[OTP VM] ERROR: OtpToken is empty");
                return;
            }

            IsBusy = true;

            try
            {
                var request = new VerifyOtpRequest
                {
                    Email = Email,
                    Otp = OtpCode,
                    Token = OtpToken
                };

                Debug.WriteLine($"[OTP VM] Calling _authService.VerifyOtpAsync...");
                var response = await _authService.VerifyOtpAsync(request);
                Debug.WriteLine($"[OTP VM] Response received: Success={response.Success}, Message={response.Message}");

                if (response.Success && !string.IsNullOrEmpty(response.Token))
                {
                    SuccessMessage = "Verification successful!";
                    Debug.WriteLine($"[OTP VM] ✅ Verification successful! Token received.");
                    Debug.WriteLine($"[OTP VM] Navigating to main page...");

                    await Task.Delay(1000);
                    await Shell.Current.GoToAsync("//main/GroupChatListPage");

                    Debug.WriteLine($"[OTP VM] Navigation completed");
                }
                else
                {
                    ErrorMessage = response.Message ?? "Invalid or expired OTP";
                    Debug.WriteLine($"[OTP VM] ❌ Verification failed: {ErrorMessage}");
                    OtpCode = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP VM] ❌ EXCEPTION in VerifyOtp:");
                Debug.WriteLine($"[OTP VM] Message: {ex.Message}");
                Debug.WriteLine($"[OTP VM] StackTrace: {ex.StackTrace}");
                ErrorMessage = "Verification failed. Please try again.";
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine($"[OTP VM] VerifyOtpAsync completed");
                Debug.WriteLine($"[OTP VM] ============================================\n");
            }
        }

        // Resend OTP
        private async Task ResendOtpAsync()
        {
            Debug.WriteLine($"[OTP VM] ResendOtpAsync called");

            if (IsBusy || !CanResend)
            {
                Debug.WriteLine($"[OTP VM] Cannot resend - IsBusy: {IsBusy}, CanResend: {CanResend}");
                return;
            }

            IsBusy = true;
            ClearMessages();

            try
            {
                Debug.WriteLine($"[OTP VM] Calling _authService.ResendOtpAsync for: {Email}");
                var response = await _authService.ResendOtpAsync(Email);
                Debug.WriteLine($"[OTP VM] Resend response: Success={response.Success}, Message={response.Message}");

                if (response.Success)
                {
                    SuccessMessage = "OTP resent successfully!";

                    if (!string.IsNullOrEmpty(response.OtpToken))
                    {
                        OtpToken = response.OtpToken;
                        Debug.WriteLine($"[OTP VM] New OTP token received: {response.OtpToken}");
                    }

                    StartResendTimer();
                }
                else
                {
                    ErrorMessage = response.Message ?? "Failed to resend OTP";
                    Debug.WriteLine($"[OTP VM] Resend failed: {ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP VM] ResendOtp exception: {ex.Message}");
                ErrorMessage = "Failed to resend OTP. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Switch to Password
        private async Task SwitchToPasswordAsync()
        {
            Debug.WriteLine($"[OTP VM] Switching to password login...");
            await Shell.Current.GoToAsync("//LoginPage");
        }

        // Back to Login
        private async Task BackToLoginAsync()
        {
            Debug.WriteLine($"[OTP VM] Going back to login...");
            await Shell.Current.GoToAsync("//LoginPage");
        }

        private void ClearMessages()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            InfoMessage = string.Empty;
        }

        public void Dispose()
        {
            Debug.WriteLine($"[OTP VM] Disposing...");
            _resendTimer?.Stop();
            _resendTimer?.Dispose();
            _resendTimer = null;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}