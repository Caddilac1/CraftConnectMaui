using CommunityToolkit.Maui;
using CraftConnect_Mobile_App;
using CraftConnect_Mobile_App.PageModels;
using CraftConnect_Mobile_App.Pages;
using CraftConnect_Mobile_App.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;

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
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                });

            // ========================================
            // 🔧 FIX GRAY NAVIGATION BAR - Android
            // ========================================
            builder.ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android => android
                    .OnCreate((activity, bundle) =>
                    {
                        // Remove gray navigation bar immediately on app start
                        if (activity?.Window != null)
                        {
                            try
                            {
                                // Make navigation bar fully transparent
                                activity.Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);

                                // Disable gray contrast enforcement (Android 10+)
                                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                                {
                                    activity.Window.NavigationBarContrastEnforced = false;
                                }

                                // Enable edge-to-edge (Android 11+)
                                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
                                {
                                    activity.Window.SetDecorFitsSystemWindows(false);
                                }

                                System.Diagnostics.Debug.WriteLine("[MauiProgram] ✅ Navigation bar transparency applied globally");
                            }
                            catch (System.Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MauiProgram] ❌ Error setting navigation bar: {ex.Message}");
                            }
                        }
                    })
                    .OnResume((activity) =>
                    {
                        // Re-apply on resume to ensure it stays transparent
                        if (activity?.Window != null)
                        {
                            activity.Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);

                            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                            {
                                activity.Window.NavigationBarContrastEnforced = false;
                            }
                        }
                    }));
#endif
            });

            // ========================================
            // SERVICES - All as Singletons
            // ========================================
            builder.Services.AddSingleton<AuthService>();
            builder.Services.AddSingleton<IChatService, ChatService>();
            builder.Services.AddSingleton<IUserFeedService, UserFeedService>(); // ✅ Changed to Singleton like ChatService
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IUserService, UserService>();
            builder.Services.AddSingleton<AiFeedChatService>();

            // ========================================
            // VIEWMODELS
            // ========================================
            builder.Services.AddTransient<LoginPageModel>();
            builder.Services.AddTransient<GroupChatListPageModel>();
            builder.Services.AddTransient<ChatPageModel>();
            builder.Services.AddTransient<StorePageModel>();
            builder.Services.AddTransient<OtpVerificationPageModel>();
            builder.Services.AddTransient<SettingsPageViewModel>();
            builder.Services.AddTransient<AiFeedChatPageModel>();
            builder.Services.AddTransient<UpdatesFeedPageModel>();

            // ========================================
            // PAGES
            // ========================================
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<GroupChatListPage>();
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<OtpVerificationPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<AiFeedChatPage>();
            builder.Services.AddTransient<StorePage>();
            builder.Services.AddTransient<UpdatesFeedPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}