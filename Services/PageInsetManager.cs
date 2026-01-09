using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CraftConnect_Mobile_App.Services
{
    /// <summary>
    /// Applies Android navigation bar inset as bottom padding to pages at runtime.
    /// Only affects Android (returns zero on other platforms).
    /// </summary>
    public static class PageInsetManager
    {
        // Store original paddings so we can restore if needed
        private static readonly Dictionary<Page, Thickness> _originalPaddings = new();

        public static void ApplyInsetToPage(Page page, int bottomInset)
        {
            if (page == null) return;

            if (!_originalPaddings.ContainsKey(page))
            {
                _originalPaddings[page] = page.Padding;
            }

            var original = _originalPaddings[page];
            page.Padding = new Thickness(original.Left, original.Top, original.Right, original.Bottom + bottomInset);
        }

        public static void RestorePagePadding(Page page)
        {
            if (page == null) return;
            if (_originalPaddings.TryGetValue(page, out var original))
            {
                page.Padding = original;
                _originalPaddings.Remove(page);
            }
        }
    }
}
