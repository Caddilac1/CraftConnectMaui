using System;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CraftConnect_Mobile_App.Converters
{
    // ── BoolToColorConverter ────────────────────────────────────────────────
    /// <summary>Returns TrueColor or FalseColor based on a bool binding.</summary>
    public class BoolToColorConverter : IValueConverter
    {
        public Color TrueColor { get; set; } = Colors.Black;
        public Color FalseColor { get; set; } = Colors.Gray;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? TrueColor : FalseColor;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── InverseBoolConverter ────────────────────────────────────────────────
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;
    }

    // ── InvertedBoolConverter ───────────────────────────────────────────────
    /// <summary>Alias for InverseBoolConverter — required by App.xaml.</summary>
    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;
    }

    // ── BoolToStringConverter ───────────────────────────────────────────────
    public class BoolToStringConverter : IValueConverter
    {
        public string TrueValue { get; set; } = "Yes";
        public string FalseValue { get; set; } = "No";

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is true ? TrueValue : FalseValue;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── ProgressWidthConverter ──────────────────────────────────────────────
    public class ProgressWidthConverter : IValueConverter
    {
        public static readonly ProgressWidthConverter Instance = new();
        public double MaxWidth { get; set; } = 260;

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is double d) return Math.Max(0, d * MaxWidth);
            if (value is float f) return Math.Max(0, f * MaxWidth);
            return 0d;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── IsNotNullOrEmptyConverter ───────────────────────────────────────────
    public class IsNotNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string s) return !string.IsNullOrWhiteSpace(s);
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── StringNotEmptyConverter ─────────────────────────────────────────────
    public class StringNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !string.IsNullOrEmpty(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── StringEmptyConverter ────────────────────────────────────────────────
    public class StringEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── StringIsEmptyConverter ──────────────────────────────────────────────
    public class StringIsEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text) return string.IsNullOrWhiteSpace(text);
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── PasswordIconConverter ───────────────────────────────────────────────
    public class PasswordIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible && isVisible) return "eye_off.png";
            return "eye.png";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── PasswordTextConverter ───────────────────────────────────────────────
    public class PasswordTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible) return isVisible ? "Hide" : "Show";
            return "Show";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── IntToBoolConverter ──────────────────────────────────────────────────
    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count) return count > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── GreaterThanZeroConverter ────────────────────────────────────────────
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue) return intValue > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── LastMessageTimeConverter ────────────────────────────────────────────
    public class LastMessageTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                var now = DateTime.Now;
                if (dateTime.Date == now.Date) return dateTime.ToString("HH:mm");
                if (dateTime.Date == now.Date.AddDays(-1)) return "Yesterday";
                if (dateTime.Year == now.Year) return dateTime.ToString("dd MMM");
                return dateTime.ToString("dd/MM/yy");
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── UnreadToVisibilityConverter ─────────────────────────────────────────
    public class UnreadToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int unreadCount) return unreadCount > 0;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── StringToInitialsConverter ───────────────────────────────────────────
    public class StringToInitialsConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrEmpty(name))
            {
                if (parameter as string == "color") return GenerateColorFromString(name);
                var mainPart = name.Split('-')[0].Trim();
                var words = mainPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 2) return $"{words[0][0]}{words[1][0]}".ToUpper();
                if (mainPart.Length >= 2) return mainPart.Substring(0, 2).ToUpper();
                if (mainPart.Length == 1) return mainPart.ToUpper();
                return "??";
            }
            return "??";
        }

        private Color GenerateColorFromString(string text)
        {
            int hash = text.GetHashCode();
            Color[] colors = {
                Color.FromArgb("#FF6B8E23"), Color.FromArgb("#FF2E8B57"),
                Color.FromArgb("#FF4682B4"), Color.FromArgb("#FF8A2BE2"),
                Color.FromArgb("#FFDC143C"), Color.FromArgb("#FF20B2AA"),
                Color.FromArgb("#FF9370DB"), Color.FromArgb("#FF32CD32"),
                Color.FromArgb("#FF4169E1"), Color.FromArgb("#FF8B4513"),
                Color.FromArgb("#FF483D8B"), Color.FromArgb("#FF2F4F4F"),
            };
            return colors[Math.Abs(hash) % colors.Length];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── MessageCountToVisibilityConverter ───────────────────────────────────
    public class MessageCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count) return count > 99 ? "99+" : count.ToString();
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── IsGroupToIconConverter ──────────────────────────────────────────────
    public class IsGroupToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isGroup) return isGroup ? "👥" : "👤";
            return "👤";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── IsMutedToIconConverter ──────────────────────────────────────────────
    public class IsMutedToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMuted) return isMuted ? "🔇" : "";
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── IsPinnedToIconConverter ─────────────────────────────────────────────
    public class IsPinnedToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPinned) return isPinned ? "📌" : "";
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── LastMessagePreviewConverter ─────────────────────────────────────────
    public class LastMessagePreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string message)
            {
                if (string.IsNullOrEmpty(message)) return "";
                const int maxLength = 40;
                return message.Length > maxLength ? message.Substring(0, maxLength) + "..." : message;
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── TabSelectionConverter ───────────────────────────────────────────────
    public class TabSelectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentTab && parameter is string tabName)
                return currentTab == tabName;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── OnlineStatusConverter ───────────────────────────────────────────────
    public class OnlineStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isOnline)
                return isOnline ? Color.FromArgb("#25D366") : Color.FromArgb("#8E8E93");
            return Color.FromArgb("#8E8E93");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── MessageStatusConverter ──────────────────────────────────────────────
    public class MessageStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
                return status switch
                {
                    0 => "✓",
                    1 => "✓✓",
                    2 => Color.FromArgb("#25D366"),
                    _ => Color.FromArgb("#8E8E93")
                };
            return Color.FromArgb("#8E8E93");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── FirstLetterConverter ────────────────────────────────────────────────
    public class FirstLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
                return text.Substring(0, 1).ToUpper();
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── UrlToImageConverter ─────────────────────────────────────────────────
    public class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return url;

                var baseUrl = Preferences.Get("api_base_url", "https://192.168.33.112:7023");
                return $"{baseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── DownloadButtonColorConverter ────────────────────────────────────────
    public class DownloadButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDownloading)
                return isDownloading ? Color.FromArgb("#F44336") : Color.FromArgb("#25D366");
            return Color.FromArgb("#25D366");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // ── DownloadStatusTextConverter ─────────────────────────────────────────
    public class DownloadStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}