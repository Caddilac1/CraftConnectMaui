using Android.App;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using System;

namespace CraftConnect_Mobile_App.Platforms.Android
{
    public static class AndroidInsetService
    {
        public static int GetNavigationBarHeight()
        {
            try
            {
                var activity = Platform.CurrentActivity as Activity;
                if (activity?.Window == null) return 0;

                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    var windowInsets = activity.Window?.DecorView?.RootWindowInsets;
                    if (windowInsets == null) return 0;

                    var navInsets = windowInsets.GetInsets(WindowInsets.Type.NavigationBars());
                    return navInsets.Bottom;
                }
                else
                {
                    // Fallback: use resource dimension
                    var resourceId = activity.Resources.GetIdentifier("navigation_bar_height", "dimen", "android");
                    if (resourceId > 0)
                    {
                        return activity.Resources.GetDimensionPixelSize(resourceId);
                    }

                    return 0;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}
