using Microsoft.Maui.Controls;
using System.Collections.Generic;

namespace CraftConnect_Mobile_App.Services
{
    /// <summary>
    /// Applies Android navigation bar inset as bottom padding to pages at runtime.
    /// Provides methods to apply IME inset specifically to input containers so headers stay fixed.
    /// </summary>
    public static class PageInsetManager
    {
        // Store original paddings so we can restore if needed
        private static readonly Dictionary<Page, Thickness> _originalPaddings = new();
        private static readonly Dictionary<Layout, Thickness> _originalElementPaddings = new();

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

        // Apply inset to a specific layout element (e.g., the message input area) instead of the whole page
        public static void ApplyInsetToElement(Layout element, int bottomInset)
        {
            if (element == null) return;

            if (!_originalElementPaddings.ContainsKey(element))
            {
                _originalElementPaddings[element] = element.Padding;
            }

            var original = _originalElementPaddings[element];
            element.Padding = new Thickness(original.Left, original.Top, original.Right, original.Bottom + bottomInset);
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
