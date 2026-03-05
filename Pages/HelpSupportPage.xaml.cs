using Microsoft.Maui.Controls;
using System;

namespace CraftConnect_Mobile_App.Pages
{
    public partial class HelpSupportPage : ContentPage
    {
        public HelpSupportPage()
        {
            InitializeComponent();
        }

        // ── Contact actions ───────────────────────────────────────────

        private async void OnWhatsAppClicked(object sender, EventArgs e)
        {
            try
            {
                var uri = new Uri("https://wa.me/233300000000");
                await Browser.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
            }
            catch
            {
                await DisplayAlert("Error", "Could not open WhatsApp. Please try again.", "OK");
            }
        }

        private async void OnEmailSupportClicked(object sender, EventArgs e)
        {
            try
            {
                if (Email.Default.IsComposeSupported)
                {
                    var message = new EmailMessage
                    {
                        To = new System.Collections.Generic.List<string> { "support@craftconnect.com" },
                        Subject = "CraftConnect Support Request"
                    };
                    await Email.Default.ComposeAsync(message);
                }
                else
                {
                    await DisplayAlert("Email", "Please email us at support@craftconnect.com", "OK");
                }
            }
            catch
            {
                await DisplayAlert("Email", "Please email us at support@craftconnect.com", "OK");
            }
        }

        private async void OnCallClicked(object sender, EventArgs e)
        {
            try
            {
                PhoneDialer.Default.Open("+233300000000");
            }
            catch
            {
                await DisplayAlert("Call Us", "Phone: +233 30 000 0000\nAvailable Mon–Fri, 8am–6pm", "OK");
            }
        }

        // ── FAQ Accordion ─────────────────────────────────────────────

        private void OnFaq1Tapped(object sender, EventArgs e)
            => ToggleFaq(Faq1Answer, Faq1Icon);

        private void OnFaq2Tapped(object sender, EventArgs e)
            => ToggleFaq(Faq2Answer, Faq2Icon);

        private void OnFaq3Tapped(object sender, EventArgs e)
            => ToggleFaq(Faq3Answer, Faq3Icon);

        private void OnFaq4Tapped(object sender, EventArgs e)
            => ToggleFaq(Faq4Answer, Faq4Icon);

        private void ToggleFaq(Frame answerFrame, Image iconImage)
        {
            bool isOpen = answerFrame.IsVisible;
            answerFrame.IsVisible = !isOpen;
            // Rotate chevron: right = closed, down = open
            iconImage.Rotation = isOpen ? 0 : 90;
        }

        private async void OnBackClicked(object sender, EventArgs e)
            => await Shell.Current.GoToAsync("..");
    }
}
