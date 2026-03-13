using CraftConnect_Mobile_App.Pages;
using CraftConnect_Mobile_App.Services;
using System;
using System.Linq;

namespace CraftConnect_Mobile_App
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Kill ALL Shell chrome — background, navbar, tabbar
            this.BackgroundColor = Colors.Transparent;
            Shell.SetBackgroundColor(this, Colors.Transparent);
            Shell.SetNavBarIsVisible(this, false);
            Shell.SetTabBarIsVisible(this, false);
            Shell.SetTabBarBackgroundColor(this, Colors.Transparent);
            Shell.SetNavBarHasShadow(this, false);

            Routing.RegisterRoute("chat", typeof(ChatPage));
            Routing.RegisterRoute("store", typeof(StorePage));
            Routing.RegisterRoute(nameof(OtpVerificationPage), typeof(OtpVerificationPage));
            Routing.RegisterRoute("aifeedchat", typeof(AiFeedChatPage));
            Routing.RegisterRoute("ImageViewerPage", typeof(ImageViewerPage));
            Routing.RegisterRoute("ProfileSettingsPage", typeof(Pages.ProfileSettingsPage));
            Routing.RegisterRoute("EditProfilePage", typeof(Pages.EditProfilePage));
            Routing.RegisterRoute("NotificationsSettingsPage", typeof(Pages.NotificationsSettingsPage));
            Routing.RegisterRoute("PrivacySecurityPage", typeof(Pages.PrivacySecurityPage));
            Routing.RegisterRoute("PaymentMethodsPage", typeof(Pages.PaymentMethodsPage));
            Routing.RegisterRoute("HelpSupportPage", typeof(Pages.HelpSupportPage));
            Routing.RegisterRoute(nameof(Pages.RegisterPage), typeof(Pages.RegisterPage));

            this.Navigated += AppShell_Navigated;
        }

        private void AppShell_Navigated(object sender, ShellNavigatedEventArgs e)
        {
            // Re-suppress Shell chrome on every navigation in case MAUI re-applies it
            Shell.SetTabBarIsVisible(this, false);
            Shell.SetTabBarBackgroundColor(this, Colors.Transparent);
            Shell.SetNavBarIsVisible(this, false);
        }
    }
}