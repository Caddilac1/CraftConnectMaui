using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Maui;
using AndroidX.Core.View;

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

            // Ensure the window resizes when the soft keyboard appears so only the bottom area is pushed up
            try
            {
                Window?.SetSoftInputMode(SoftInput.AdjustResize | SoftInput.StateHidden);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] ❌ Error setting SoftInputMode: {ex.Message}");
            }

            // CRITICAL: Remove gray navigation bar or make it blend with app background
            RemoveGrayNavigationBar();
        }

        private void RemoveGrayNavigationBar()
        {
            if (Window == null) return;

            try
            {
                // Make navigation bar match app background so the gray strip is not visible
                // ChatPage background is #E8EAF6 - use the same color so the bar blends in
                Window.SetNavigationBarColor(Android.Graphics.Color.ParseColor("#E8EAF6"));

                // Also make status bar transparent so content can draw behind it
                Window.SetStatusBarColor(Android.Graphics.Color.Transparent);

                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    // Use system to fit system windows (do not draw behind bars)
                    WindowCompat.SetDecorFitsSystemWindows(Window, true);

                    // Ensure the decor view itself uses fitsSystemWindows to avoid extra inset drawing
                    Window.DecorView?.SetFitsSystemWindows(true);

                    // Disable the gray scrim/contrast on supported versions
                    if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                    {
                        Window.NavigationBarContrastEnforced = false;
                    }
                }
                else if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    // For older versions, avoid immersive layout flags so nav bar stays normal
                    var uiOptions = (int)Window.DecorView.SystemUiVisibility;
                    // Clear LayoutFullscreen/LayoutHideNavigation if previously set
                    uiOptions &= ~(int)SystemUiFlags.LayoutHideNavigation;
                    uiOptions &= ~(int)SystemUiFlags.LayoutFullscreen;
                    uiOptions |= (int)SystemUiFlags.LayoutStable;
                    Window.DecorView.SystemUiVisibility = (StatusBarVisibility)uiOptions;

                    Window.DecorView?.SetFitsSystemWindows(true);
                }

                System.Diagnostics.Debug.WriteLine("[MainActivity] ✅ Navigation bar color/fits applied");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainActivity] ❌ Error setting navigation bar: {ex.Message}");
            }
        }
    }
}