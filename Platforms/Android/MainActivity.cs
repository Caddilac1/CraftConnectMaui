using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MainActivity] ❌ SoftInputMode error: {ex.Message}");
            }

            try
            {
                if (Window == null) return;

                // Black status bar, white icons
                Window.SetStatusBarColor(Android.Graphics.Color.Black);

                // Dark grey nav bar matching app header, white icons
                Window.SetNavigationBarColor(
                    Android.Graphics.Color.ParseColor("#1B2B3A"));

                // Disable contrast enforcement so Android doesn't override colors
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    Window.StatusBarContrastEnforced = false;
                    Window.NavigationBarContrastEnforced = false;
                }

                // Clear light icon flags so icons show white on dark backgrounds
                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    Window.InsetsController?.SetSystemBarsAppearance(
                        0,
                        (int)WindowInsetsControllerAppearance.LightStatusBars |
                        (int)WindowInsetsControllerAppearance.LightNavigationBars);
                }
                else if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
#pragma warning disable CA1422
                    var flags = Window.DecorView.SystemUiVisibility;
                    flags &= ~(Android.Views.StatusBarVisibility)
                                (SystemUiFlags.LightStatusBar |
                                 SystemUiFlags.LightNavigationBar);
                    Window.DecorView.SystemUiVisibility = flags;
#pragma warning restore CA1422
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[MainActivity] ❌ Bar color error: {ex.Message}");
            }
        }
    }
}