using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;


namespace CraftConnect_Mobile_App
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        MainLauncher = true,
        ConfigurationChanges =
            ConfigChanges.ScreenSize |
            ConfigChanges.Orientation |
            ConfigChanges.UiMode |
            ConfigChanges.ScreenLayout |
            ConfigChanges.SmallestScreenSize |
            ConfigChanges.Density,
        WindowSoftInputMode = SoftInput.AdjustResize
    )]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // ❌ DO NOT call SetDecorFitsSystemWindows(false)
        }
    }
}
