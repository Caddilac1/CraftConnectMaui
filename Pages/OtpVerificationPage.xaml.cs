using Microsoft.Maui.Controls;
using System.Diagnostics;
using CraftConnect_Mobile_App.PageModels;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class OtpVerificationPage : ContentPage
    {
        public OtpVerificationPage(OtpVerificationPageModel viewModel)  // ✅ Inject ViewModel
        {
            InitializeComponent();

            Debug.WriteLine($"[OTP PAGE] ============================================");
            Debug.WriteLine($"[OTP PAGE] Constructor called");
            Debug.WriteLine($"[OTP PAGE] ViewModel injected: {viewModel != null}");

            // ✅ CRITICAL: Set the BindingContext
            BindingContext = viewModel;

            Debug.WriteLine($"[OTP PAGE] BindingContext set");
            Debug.WriteLine($"[OTP PAGE] ============================================");

            SetupEventHandlers();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            Debug.WriteLine($"[OTP PAGE] OnAppearing called");

            // Check if ViewModel is set
            var viewModel = BindingContext as OtpVerificationPageModel;
            Debug.WriteLine($"[OTP PAGE] ViewModel is null: {viewModel == null}");

            if (viewModel != null)
            {
                Debug.WriteLine($"[OTP PAGE] ViewModel Email: '{viewModel.Email}'");
                Debug.WriteLine($"[OTP PAGE] ViewModel OtpToken: '{viewModel.OtpToken}'");
                Debug.WriteLine($"[OTP PAGE] ViewModel HasPasswordOption: {viewModel.HasPasswordOption}");
                Debug.WriteLine($"[OTP PAGE] VerifyOtpCommand is null: {viewModel.VerifyOtpCommand == null}");
            }

            // Focus on OTP entry
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () =>
            {
                OtpEntry?.Focus();
                Debug.WriteLine($"[OTP PAGE] OTP Entry focused");
            });
        }

        private void SetupEventHandlers()
        {
            Debug.WriteLine($"[OTP PAGE] Setting up event handlers");
            OtpEntry.TextChanged += OnOtpTextChanged;
            OtpEntry.Completed += OnOtpCompleted;
        }

        private void OnOtpTextChanged(object sender, TextChangedEventArgs e)
        {
            Debug.WriteLine($"[OTP PAGE] Text changed: '{e.OldTextValue}' -> '{e.NewTextValue}'");

            if (!string.IsNullOrEmpty(e.NewTextValue) && e.NewTextValue.Length == 6)
            {
                Debug.WriteLine($"[OTP PAGE] 6 digits entered: {e.NewTextValue}");
                OtpEntry.Unfocus();

                // Auto-verify when 6 digits entered (optional)
                var viewModel = BindingContext as OtpVerificationPageModel;
                if (viewModel?.VerifyOtpCommand?.CanExecute(null) == true)
                {
                    Debug.WriteLine($"[OTP PAGE] Auto-triggering verification...");
                    viewModel.VerifyOtpCommand.Execute(null);
                }
            }
        }

        private void OnOtpCompleted(object sender, EventArgs e)
        {
            Debug.WriteLine($"[OTP PAGE] OnOtpCompleted event fired");

            var viewModel = BindingContext as OtpVerificationPageModel;

            if (viewModel?.VerifyOtpCommand?.CanExecute(null) == true)
            {
                Debug.WriteLine($"[OTP PAGE] ✅ Executing VerifyOtpCommand");
                viewModel.VerifyOtpCommand.Execute(null);
            }
            else
            {
                Debug.WriteLine($"[OTP PAGE] ❌ Cannot execute command");
            }
        }

        protected override bool OnBackButtonPressed()
        {
            var viewModel = BindingContext as OtpVerificationPageModel;
            if (viewModel?.BackToLoginCommand?.CanExecute(null) == true)
            {
                viewModel.BackToLoginCommand.Execute(null);
                return true;
            }
            return base.OnBackButtonPressed();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            OtpEntry.TextChanged -= OnOtpTextChanged;
            OtpEntry.Completed -= OnOtpCompleted;
        }
    }
}