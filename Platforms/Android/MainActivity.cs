using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui;

namespace CraftConnect_Mobile_App
{
    [Activity(Theme = "@style/Maui.SplashTheme",
              MainLauncher = true,
              ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // CRITICAL: Remove gray navigation bar
            RemoveGrayNavigationBar();
        }

        private void RemoveGrayNavigationBar()
        {
            if (Window == null) return;

            try
            {
                // Make navigation bar completely transparent
                Window.SetNavigationBarColor(Android.Graphics.Color.Transparent);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    // Android 11+ (API 30+) - Most modern approach
                    Window.SetDecorFitsSystemWindows(false);

                    // Disable the gray scrim/contrast
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                    {
                        Window.NavigationBarContrastEnforced = false;
                    }
                }
                else if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    // Android 8-10 (API 26-29)
                    var uiOptions = (int)Window.DecorView.SystemUiVisibility;
                    uiOptions |= (int)SystemUiFlags.LayoutStable;
                    uiOptions |= (int)SystemUiFlags.LayoutHideNavigation;
                    uiOptions |= (int)SystemUiFlags.LightNavigationBar;
                    Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;
                }

                System.Diagnostics.Debug.WriteLine("[MainActivity] ✅ Navigation bar transparency applied");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] ❌ Error setting navigation bar: {ex.Message}");
            }
        }
    }
}