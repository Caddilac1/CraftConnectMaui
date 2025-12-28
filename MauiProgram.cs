using CraftConnect_Mobile_App.Pages;
using CraftConnect_Mobile_App.Services;
using CraftConnect_Mobile_App.PageModels;
using CommunityToolkit.Maui;
using CraftConnect_Mobile_App;
using Microsoft.Extensions.Logging;

namespace CraftConnect_Mobile_App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()          // REQUIRED for Toolkit
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // ✅ Add Material Icons font for WhatsApp-style icons (from second file)
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                });


            // SERVICES
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<IChatService, ChatService>();
            builder.Services.AddTransient<StorePage>();
            builder.Services.AddSingleton<IUserFeedService, UserFeedService>();
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IUserService, UserService>();
            builder.Services.AddSingleton<AiFeedChatService>();


            // VIEWMODELS
            builder.Services.AddTransient<LoginPageModel>();
            builder.Services.AddTransient<GroupChatListPageModel>();
            builder.Services.AddTransient<ChatPageModel>();
            builder.Services.AddTransient<StorePageModel>();
            builder.Services.AddTransient<OtpVerificationPageModel>();
            builder.Services.AddTransient<SettingsPageViewModel>();
            builder.Services.AddTransient<AiFeedChatPageModel>();

            // PAGES
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<GroupChatListPage>();
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<OtpVerificationPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<AiFeedChatPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}