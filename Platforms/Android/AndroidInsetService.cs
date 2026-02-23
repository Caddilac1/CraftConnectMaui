using Android.App;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using System;

namespace CraftConnect_Mobile_App.Platforms.Android
{
    public class AndroidInsets
    {
        public int NavigationBarHeight { get; set; }
        public int ImeHeight { get; set; }
        public bool IsImeVisible { get; set; }
    }

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

        // New: return both navigation bar and IME insets and IME visibility
        public static AndroidInsets GetInsets()
        {
            var result = new AndroidInsets();

            try
            {
                var activity = Platform.CurrentActivity as Activity;
                if (activity?.Window == null) return result;

                if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
                {
                    var windowInsets = activity.Window?.DecorView?.RootWindowInsets;
                    if (windowInsets == null) return result;

                    var navInsets = windowInsets.GetInsets(WindowInsets.Type.NavigationBars());
                    var imeInsets = windowInsets.GetInsets(WindowInsets.Type.Ime());

                    result.NavigationBarHeight = navInsets.Bottom;
                    result.ImeHeight = imeInsets.Bottom;
                    result.IsImeVisible = windowInsets.IsVisible(WindowInsets.Type.Ime());
                }
                else
                {
                    // older platforms: navigation bar resource, IME unknown
                    var resourceId = activity.Resources.GetIdentifier("navigation_bar_height", "dimen", "android");
                    if (resourceId > 0)
                    {
                        result.NavigationBarHeight = activity.Resources.GetDimensionPixelSize(resourceId);
                    }

                    result.ImeHeight = 0;
                    result.IsImeVisible = false;
                }
            }
            catch (Exception)
            {
                // swallow, return defaults
            }

            return result;
        }
    }
}
