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

            // Register modal/detail pages that aren't in the Shell hierarchy
            // These pages are navigated to using Shell.GoToAsync()

            // ✅ Register ChatPage with simple route name to avoid conflicts
            Routing.RegisterRoute("chat", typeof(ChatPage));
            Routing.RegisterRoute("store", typeof(StorePage));
            Routing.RegisterRoute(nameof(OtpVerificationPage), typeof(OtpVerificationPage));
            Routing.RegisterRoute("main/UpdatesFeedPage", typeof(UpdatesFeedPage));
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("aifeedchat", typeof(AiFeedChatPage));
            Routing.RegisterRoute("ImageViewerPage", typeof(ImageViewerPage));
            Routing.RegisterRoute("ProfileSettingsPage", typeof(Pages.ProfileSettingsPage));
            Routing.RegisterRoute("EditProfilePage", typeof(Pages.EditProfilePage));
            Routing.RegisterRoute("NotificationsSettingsPage", typeof(Pages.NotificationsSettingsPage));
            Routing.RegisterRoute("PrivacySecurityPage", typeof(Pages.PrivacySecurityPage));
            Routing.RegisterRoute("PaymentMethodsPage", typeof(Pages.PaymentMethodsPage));
            Routing.RegisterRoute("HelpSupportPage", typeof(Pages.HelpSupportPage));

            // Note: LoginPage and GroupChatListPage don't need registration
            // because they're already defined in AppShell.xaml as ShellContent

            // Apply insets when Shell appears and when navigation occurs
            this.Appearing += AppShell_Appearing;
            this.Navigated += AppShell_Navigated;
        }

        private void AppShell_Appearing(object sender, EventArgs e)
        {
            ApplyInsetsIfAndroid();
        }

        private void AppShell_Navigated(object sender, ShellNavigatedEventArgs e)
        {
            ApplyInsetsIfAndroid();
        }

        private void ApplyInsetsIfAndroid()
        {
#if ANDROID
            try
            {
                var bottom = Platforms.Android.AndroidInsetService.GetNavigationBarHeight();

                // Apply to the currently displayed page
                var current = Shell.Current?.CurrentPage;
                if (current != null)
                {
                    PageInsetManager.ApplyInsetToPage(current, bottom);
                }

                // Also apply to top modal page if any
                var modalStack = Shell.Current?.Navigation?.ModalStack;
                if (modalStack != null && modalStack.Count > 0)
                {
                    var topModal = modalStack.Last();
                    PageInsetManager.ApplyInsetToPage(topModal, bottom);
                }
            }
            catch (Exception)
            {
                // swallow
            }
#endif
        }
    }
}