using CraftConnect_Mobile_App.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Diagnostics;

namespace CraftConnect_Mobile_App.PageModels
{
    [QueryProperty(nameof(Phone), "phone")]
    public class OtpVerificationPageModel : INotifyPropertyChanged, IDisposable
    {
        private readonly AuthService _authService;
        private System.Timers.Timer _resendTimer;
        private int _secondsRemaining = 60;

        private string _phone = string.Empty;
        private string _otpCode = string.Empty;
        private bool _isBusy;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private string _infoMessage = string.Empty;
        private bool _canResend;
        private string _timerText = "Resend OTP in 60s";

        public OtpVerificationPageModel(AuthService authService)
        {
            _authService = authService;

            VerifyOtpCommand = new Command(async () => await VerifyOtpAsync(), () => !IsBusy);
            ResendOtpCommand = new Command(async () => await ResendOtpAsync(), () => !IsBusy && CanResend);
            BackToLoginCommand = new Command(async () => await Shell.Current.GoToAsync("//LoginPage"));

            StartResendTimer();

            Debug.WriteLine("[OTP VM] Initialized");
        }

        // ── Properties ────────────────────────────────────────────────

        public string Phone
        {
            get => _phone;
            set
            {
                _phone = Uri.UnescapeDataString(value ?? string.Empty);
                Debug.WriteLine($"[OTP VM] Phone set to: {_phone}");
                OnPropertyChanged();
            }
        }

        public string OtpCode
        {
            get => _otpCode;
            set
            {
                _otpCode = value;
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
                ((Command)VerifyOtpCommand).ChangeCanExecute();
                ((Command)ResendOtpCommand).ChangeCanExecute();
            }
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
            set { _timerText = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────

        public ICommand VerifyOtpCommand { get; }
        public ICommand ResendOtpCommand { get; }
        public ICommand BackToLoginCommand { get; }

        // ── Timer ─────────────────────────────────────────────────────

        private void StartResendTimer()
        {
            _secondsRemaining = 60;
            CanResend = false;

            _resendTimer?.Dispose();
            _resendTimer = new System.Timers.Timer(1000);

            _resendTimer.Elapsed += (sender, e) =>
            {
                _secondsRemaining--;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (_secondsRemaining > 0)
                    {
                        TimerText = $"Resend OTP in {_secondsRemaining}s";
                    }
                    else
                    {
                        _resendTimer?.Stop();
                        CanResend = true;
                        TimerText = string.Empty;
                    }
                });
            };

            _resendTimer.Start();
        }

        // ── Verify OTP  →  POST /api/auth/login/otp/verify ───────────

        private async Task VerifyOtpAsync()
        {
            if (IsBusy) return;

            ClearMessages();

            if (string.IsNullOrWhiteSpace(OtpCode))
            {
                ErrorMessage = "Please enter the OTP code";
                return;
            }

            if (OtpCode.Length != 6)
            {
                ErrorMessage = "OTP code must be 6 digits";
                return;
            }

            if (string.IsNullOrWhiteSpace(Phone))
            {
                ErrorMessage = "Phone number is missing. Please restart the login process.";
                return;
            }

            IsBusy = true;

            try
            {
                Debug.WriteLine($"[OTP VM] Verifying OTP for phone: {Phone}");

                var request = new OtpVerifyRequest
                {
                    Phone = Phone,
                    Code = OtpCode
                };

                var result = await _authService.VerifyOtpAsync(request);

                Debug.WriteLine($"[OTP VM] Verify result: Success={result.Success}");

                if (result.Success)
                {
                    SuccessMessage = "Verification successful!";
                    await Task.Delay(800);
                    await Shell.Current.GoToAsync("//main/GroupChatListPage");
                }
                else
                {
                    ErrorMessage = result.Error ?? "Invalid or expired OTP";
                    OtpCode = string.Empty;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP VM] Exception: {ex.Message}");
                ErrorMessage = "Verification failed. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Resend OTP  →  POST /api/auth/login/otp/send (no captcha) ─

        private async Task ResendOtpAsync()
        {
            if (IsBusy || !CanResend) return;

            IsBusy = true;
            ClearMessages();

            try
            {
                Debug.WriteLine($"[OTP VM] Resending OTP to: {Phone}");

                // Resend uses the same send endpoint
                // Captcha ID 0 with answer "8" is the fallback the server accepts
                var request = new OtpSendRequest
                {
                    Phone = Phone,
                    CaptchaId = 0,
                    CaptchaAnswer = "8"
                };

                var result = await _authService.SendOtpAsync(request);

                if (result.Success)
                {
                    SuccessMessage = "OTP resent! Check your phone.";
                    StartResendTimer();
                }
                else
                {
                    ErrorMessage = result.Error ?? "Failed to resend OTP. Please try again.";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OTP VM] Resend exception: {ex.Message}");
                ErrorMessage = "Failed to resend OTP. Please try again.";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearMessages()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;
            InfoMessage = string.Empty;
        }

        public void Dispose()
        {
            _resendTimer?.Stop();
            _resendTimer?.Dispose();
            _resendTimer = null;
            Debug.WriteLine("[OTP VM] Disposed");
        }

        // ── INotifyPropertyChanged ────────────────────────────────────

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}