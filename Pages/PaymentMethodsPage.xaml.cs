using Microsoft.Maui.Controls;
using System;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class PaymentMethodsPage : ContentPage
    {
        public PaymentMethodsPage()
        {
            InitializeComponent();
        }

        private async void OnMomoClicked(object sender, EventArgs e)
        {
            string result = await DisplayPromptAsync(
                "Link Mobile Money",
                "Enter your MoMo number (e.g. 0241234567):",
                keyboard: Keyboard.Telephone,
                maxLength: 12);

            if (!string.IsNullOrWhiteSpace(result))
            {
                // Mask the number for display: 024*****67
                string masked = result.Length >= 4
                    ? result[..3] + "*****" + result[^2..]
                    : result;

                MomoNumberLabel.Text = masked;
                MomoStatusLabel.Text = "LINKED";
                MomoStatusBadge.BackgroundColor = Color.FromArgb("#D1FAE5");
                MomoStatusLabel.TextColor = Color.FromArgb("#10B981");

                await DisplayAlert("Success", "Mobile Money number linked successfully.", "OK");
            }
        }

        private async void OnCardClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Bank Card",
                "Card payment integration will be available in the next update.\n\nCurrently supported: MTN MoMo, Vodafone Cash, AirtelTigo Money.",
                "OK");
        }

        private async void OnBackClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");
    }
}
