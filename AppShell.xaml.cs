using CraftConnect_Mobile_App.Pages;

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

            // Note: LoginPage and GroupChatListPage don't need registration
            // because they're already defined in AppShell.xaml as ShellContent
        }
    }
}