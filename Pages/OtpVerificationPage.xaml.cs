using Microsoft.Maui.Controls;
using System.Diagnostics;
using CraftConnect_Mobile_App.PageModels;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class OtpVerificationPage : ContentPage
    {
        public OtpVerificationPage(OtpVerificationPageModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            SetupEventHandlers();
            Debug.WriteLine("[OTP PAGE] Initialized");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            var viewModel = BindingContext as OtpVerificationPageModel;
            Debug.WriteLine($"[OTP PAGE] OnAppearing — Phone: '{viewModel?.Phone}'");

            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), () =>
            {
                OtpEntry?.Focus();
            });
        }

        private void SetupEventHandlers()
        {
            OtpEntry.TextChanged += OnOtpTextChanged;
            OtpEntry.Completed += OnOtpCompleted;
        }

        private void OnOtpTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.NewTextValue) && e.NewTextValue.Length == 6)
            {
                OtpEntry.Unfocus();

                var viewModel = BindingContext as OtpVerificationPageModel;
                if (viewModel?.VerifyOtpCommand?.CanExecute(null) == true)
                    viewModel.VerifyOtpCommand.Execute(null);
            }
        }

        private void OnOtpCompleted(object sender, EventArgs e)
        {
            var viewModel = BindingContext as OtpVerificationPageModel;
            if (viewModel?.VerifyOtpCommand?.CanExecute(null) == true)
                viewModel.VerifyOtpCommand.Execute(null);
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