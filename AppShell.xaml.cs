using CraftConnect_Mobile_App.Pages;
using CraftConnect_Mobile_App.Services;

namespace CraftConnect_Mobile_App
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Kill ALL Shell chrome
            this.BackgroundColor = Colors.Transparent;
            Shell.SetBackgroundColor(this, Colors.Transparent);
            Shell.SetNavBarIsVisible(this, false);
            Shell.SetTabBarIsVisible(this, false);
            Shell.SetTabBarBackgroundColor(this, Colors.Transparent);
            Shell.SetNavBarHasShadow(this, false);

            // Auth
            Routing.RegisterRoute(nameof(OtpVerificationPage), typeof(OtpVerificationPage));
            Routing.RegisterRoute(nameof(RegisterPage), typeof(RegisterPage));

            // Chat
            Routing.RegisterRoute("chat", typeof(ChatPage));

            // Store
            Routing.RegisterRoute("store", typeof(StorePage));

            // Cart flow — registered as routes (not ShellContent) so GoToAsync("..")
            // works correctly and the back arrow returns to the previous page
            Routing.RegisterRoute("cart", typeof(CartPage));
            Routing.RegisterRoute("CheckoutPage", typeof(CheckoutPage));

            // AI / Feed
            Routing.RegisterRoute("aifeedchat", typeof(AiFeedChatPage));

            // Misc
            Routing.RegisterRoute("ImageViewerPage", typeof(ImageViewerPage));
            Routing.RegisterRoute("ProfileSettingsPage", typeof(ProfileSettingsPage));
            Routing.RegisterRoute("EditProfilePage", typeof(EditProfilePage));
            Routing.RegisterRoute("NotificationsSettingsPage", typeof(NotificationsSettingsPage));
            Routing.RegisterRoute("PrivacySecurityPage", typeof(PrivacySecurityPage));
            Routing.RegisterRoute("PaymentMethodsPage", typeof(PaymentMethodsPage));
            Routing.RegisterRoute("HelpSupportPage", typeof(HelpSupportPage));
            Routing.RegisterRoute("paystackwebview", typeof(PaystackWebViewPage));

            this.Navigated += AppShell_Navigated;
        }

        private void AppShell_Navigated(object sender, ShellNavigatedEventArgs e)
        {
            Shell.SetTabBarIsVisible(this, false);
            Shell.SetTabBarBackgroundColor(this, Colors.Transparent);
            Shell.SetNavBarIsVisible(this, false);
        }
    }
}