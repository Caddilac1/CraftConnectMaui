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

            // Group chat
            Routing.RegisterRoute("chat", typeof(ChatPage));

            // ── Private (DM) chat ──────────────────────────────────────
            Routing.RegisterRoute(nameof(PrivateChatPage), typeof(PrivateChatPage));

            // Store
            Routing.RegisterRoute("store", typeof(StorePage));

            // Cart flow
            Routing.RegisterRoute("cart", typeof(CartPage));
            Routing.RegisterRoute("CheckoutPage", typeof(CheckoutPage));

            // AI / Feed
            Routing.RegisterRoute("addgroup", typeof(AiFeedChatPage));

            // Misc
            Routing.RegisterRoute("ImageViewerPage", typeof(ImageViewerPage));
            Routing.RegisterRoute("ProfileSettingsPage", typeof(ProfileSettingsPage));
            Routing.RegisterRoute("EditProfilePage", typeof(EditProfilePage));
            Routing.RegisterRoute("NotificationsSettingsPage", typeof(NotificationsSettingsPage));
            Routing.RegisterRoute("PrivacySecurityPage", typeof(PrivacySecurityPage));
            Routing.RegisterRoute("PaymentMethodsPage", typeof(PaymentMethodsPage));
            Routing.RegisterRoute("HelpSupportPage", typeof(HelpSupportPage));
            Routing.RegisterRoute("paystackwebview", typeof(PaystackWebViewPage));
            Routing.RegisterRoute("CreateInvoicePage", typeof(CreateInvoicePage));
            Routing.RegisterRoute("ReviewInvoicePage", typeof(ReviewInvoicePage));

            // ── Profile ────────────────────────────────────────────────
            // Must use nameof so SettingsPage.OnMyProfileClicked resolves correctly
            Routing.RegisterRoute(nameof(MyProfilePage), typeof(MyProfilePage));

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