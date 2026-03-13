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
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("fa-regular-400.ttf", "FARegular");
                    fonts.AddFont("fa-solid-900.ttf", "FASolid");
                    fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
                });

            // ========================================
            // FIX GRAY NAVIGATION BAR - Android
            // ========================================
            builder.ConfigureLifecycleEvents(events =>
            {
#if ANDROID
                events.AddAndroid(android => android
                    .OnCreate((activity, bundle) =>
                    {
                        if (activity?.Window != null)
                        {
                            try
                            {
                                activity.Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);

                                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                                    activity.Window.NavigationBarContrastEnforced = false;

                                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
                                    activity.Window.SetDecorFitsSystemWindows(false);

                                System.Diagnostics.Debug.WriteLine("[MauiProgram] Navigation bar transparency applied globally");
                            }
                            catch (System.Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[MauiProgram] Error setting navigation bar: {ex.Message}");
                            }
                        }
                    })
                    .OnResume((activity) =>
                    {
                        if (activity?.Window != null)
                        {
                            activity.Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);

                            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Q)
                                activity.Window.NavigationBarContrastEnforced = false;
                        }
                    }));
#endif
            });

            // ========================================
            // SERVICES
            // ========================================
            builder.Services.AddSingleton<ApiConfig>();
            builder.Services.AddSingleton<AuthService>(sp =>
                new AuthService(sp.GetRequiredService<ApiConfig>()));
            builder.Services.AddSingleton<IChatService, ChatService>(sp =>
                new ChatService(sp.GetRequiredService<ApiConfig>()));
            builder.Services.AddSingleton<IChatSignalRService, ChatSignalRService>(sp =>
                new ChatSignalRService(sp.GetRequiredService<ApiConfig>()));
            builder.Services.AddSingleton<IUserFeedService, UserFeedService>(sp =>
                new UserFeedService(sp.GetRequiredService<ApiConfig>()));
            builder.Services.AddSingleton<INavigationService, NavigationService>();
            builder.Services.AddSingleton<IDialogService, DialogService>();
            builder.Services.AddSingleton<IUserService, UserService>();
            builder.Services.AddSingleton<AiFeedChatService>();
            builder.Services.AddSingleton<ArtisanProposalService>();
            builder.Services.AddSingleton<IProfileApiService>(sp =>
                new ProfileApiService(sp.GetRequiredService<ApiConfig>()));
            builder.Services.AddSingleton<IStoreService, StoreService>(sp =>
                new StoreService(sp.GetRequiredService<ApiConfig>()));

            // ========================================
            // VIEWMODELS
            // ========================================
            builder.Services.AddTransient<RegisterPageModel>();
            builder.Services.AddTransient<LoginPageModel>();
            builder.Services.AddTransient<GroupChatListPageModel>();
            builder.Services.AddTransient<ChatPageModel>();
            builder.Services.AddTransient<StorePageModel>(sp =>
                new StorePageModel(sp.GetRequiredService<IStoreService>()));
            builder.Services.AddTransient<OtpVerificationPageModel>();
            builder.Services.AddTransient<SettingsPageViewModel>();
            builder.Services.AddTransient<AiFeedChatPageModel>();
            builder.Services.AddTransient<UpdatesFeedPageModel>();

            // ========================================
            // PAGES - Core
            // ========================================
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<GroupChatListPage>();
            builder.Services.AddTransient<ChatPage>();
            builder.Services.AddTransient<OtpVerificationPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<AiFeedChatPage>();
            builder.Services.AddTransient<StorePage>();
            builder.Services.AddTransient<UpdatesFeedPage>();

            // ========================================
            // PAGES - Proposal flow
            // NOTE: CreateProposalPage moved here from ViewModels section
            //       where it was previously misregistered.
            // ========================================
            builder.Services.AddTransient<CreateProposalPage>();

            // ========================================
            // PAGES - Profile flow
            // FIX: EditArtisanProfilePage was never registered in DI,
            //      causing GetService<EditArtisanProfilePage>() to always
            //      return null and showing an Error dialog when the user
            //      tapped "Set Up Profile". Registered as Transient so
            //      each push gets a fresh instance with _initialized = false.
            // ========================================
            builder.Services.AddTransient<EditArtisanProfilePage>();

            // ========================================
            // PAGES - Settings sub-pages
            // ========================================
            builder.Services.AddTransient<ProfileSettingsPage>();
            builder.Services.AddTransient<EditProfilePage>();
            builder.Services.AddTransient<NotificationsSettingsPage>();
            builder.Services.AddTransient<PrivacySecurityPage>();
            builder.Services.AddTransient<PaymentMethodsPage>();
            builder.Services.AddTransient<HelpSupportPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}