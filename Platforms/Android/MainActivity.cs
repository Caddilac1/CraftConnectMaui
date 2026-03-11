using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace CraftConnect_Mobile_App
{
    [Activity(Theme = "@style/Maui.SplashTheme",
              MainLauncher = true,
              ConfigurationChanges = ConfigChanges.ScreenSize |
                                     ConfigChanges.Orientation |
                                     ConfigChanges.UiMode |
                                     ConfigChanges.ScreenLayout |
                                     ConfigChanges.SmallestScreenSize |
                                     ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            try
            {
                Window?.SetSoftInputMode(SoftInput.AdjustResize | SoftInput.StateHidden);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MainActivity] ❌ SoftInputMode error: {ex.Message}");
            }

            ConfigureWindowInsets();
        }

        private void ConfigureWindowInsets()
        {
            if (Window == null) return;

            try
            {
                Window.SetStatusBarColor(Android.Graphics.Color.Transparent);
                Window.SetNavigationBarColor(
                    Android.Graphics.Color.ParseColor("#F2F4F7"));

                // Edge-to-edge — content draws behind system bars
                WindowCompat.SetDecorFitsSystemWindows(Window, false);

                // #1B2B3A matches LoginPage background — the FIRST page shown.
                // Each subsequent page uses its own BoxView to fill the status
                // bar area with the correct color for that page.
                Window.DecorView.SetBackgroundColor(
                    Android.Graphics.Color.ParseColor("#1B2B3A"));

                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                    Window.NavigationBarContrastEnforced = false;

                System.Diagnostics.Debug.WriteLine(
                    "[MainActivity] ✅ Window insets configured (edge-to-edge)");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MainActivity] ❌ Window insets error: {ex.Message}");
            }
        }
    }
}