using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace CraftConnect_Mobile_App
{
    [Activity(Theme = "@style/Maui.SplashTheme",
              MainLauncher = true,
              LaunchMode = LaunchMode.SingleTop,
              ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
              WindowSoftInputMode = SoftInput.AdjustResize)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop && Window != null)
            {
                // Set navigation bar to pure white
                Window.SetNavigationBarColor(new Android.Graphics.Color(255, 255, 255, 255));

                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    Window.SetDecorFitsSystemWindows(true);
                }

                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                {
                    Window.DecorView.SystemUiVisibility = (StatusBarVisibility)SystemUiFlags.LightNavigationBar;
                }
            }
        }
    }
}