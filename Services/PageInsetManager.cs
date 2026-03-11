using Microsoft.Maui.Controls;
using System.Collections.Generic;

namespace CraftConnect_Mobile_App.Services
{
    /// <summary>
    /// Applies Android navigation bar inset as bottom padding to pages at runtime.
    /// NEVER touches top padding — each page manages its own top padding
    /// via ApplyStatusBarPadding() in its code-behind.
    /// </summary>
    public static class PageInsetManager
    {
        private static readonly Dictionary<Layout, Thickness> _originalElementPaddings = new();

        public static void ApplyInsetToPage(Page page, int bottomInset)
        {
            if (page == null) return;

            // Preserve top — owned by each page's ApplyStatusBarPadding()
            // Only set bottom inset directly (not additive — prevents stacking on re-navigation)
            page.Padding = new Thickness(
                page.Padding.Left,
                page.Padding.Top,
                page.Padding.Right,
                bottomInset
            );
        }

        public static void RestorePagePadding(Page page)
        {
            if (page == null) return;
            page.Padding = new Thickness(
                page.Padding.Left,
                page.Padding.Top,
                page.Padding.Right,
                0
            );
        }

        public static void ApplyInsetToElement(Layout element, int bottomInset)
        {
            if (element == null) return;
            if (!_originalElementPaddings.ContainsKey(element))
                _originalElementPaddings[element] = element.Padding;

            var original = _originalElementPaddings[element];
            element.Padding = new Thickness(
                original.Left,
                original.Top,
                original.Right,
                original.Bottom + bottomInset
            );
        }

        public static void RestoreElementPadding(Layout element)
        {
            if (element == null) return;
            if (_originalElementPaddings.TryGetValue(element, out var original))
            {
                element.Padding = original;
                _originalElementPaddings.Remove(element);
            }
        }
    }
}