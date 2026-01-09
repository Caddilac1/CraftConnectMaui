using System;
using System.Globalization;
using System.Linq;
using Microsoft.Maui.Graphics;

namespace CraftConnect_Mobile_App.Converters
{
    public class IsNotNullOrEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                return !string.IsNullOrWhiteSpace(stringValue);
            }
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Inverts boolean value (true -> false, false -> true)
    /// </summary>
    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;
            return false;
        }
    }

    public class StringNotEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PasswordIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible && isVisible)
                return "eye_off.png"; // Icon when password is visible (to hide it)
            return "eye.png"; // Icon when password is hidden (to show it)
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IntToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LastMessageTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                var now = DateTime.Now;

                if (dateTime.Date == now.Date)
                {
                    return dateTime.ToString("HH:mm");
                }
                else if (dateTime.Date == now.Date.AddDays(-1))
                {
                    return "Yesterday";
                }
                else if (dateTime.Year == now.Year)
                {
                    return dateTime.ToString("dd MMM");
                }
                else
                {
                    return dateTime.ToString("dd/MM/yy");
                }
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class UnreadToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int unreadCount)
            {
                return unreadCount > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is string paramString)
            {
                if (paramString == "Updates" && value is bool isUpdatesActive && isUpdatesActive)
                    return Color.FromArgb("#075E54");
                else if (paramString == "Calls" && value is bool isCallsActive && isCallsActive)
                    return Color.FromArgb("#075E54");
                else if (paramString == "Communities" && value is bool isCommunitiesActive && isCommunitiesActive)
                    return Color.FromArgb("#075E54");
                else if (paramString == "Chats" && value is bool isChatsActive && isChatsActive)
                    return Color.FromArgb("#075E54");
                else if (paramString == "Settings" && value is bool isSettingsActive && isSettingsActive)
                    return Color.FromArgb("#075E54");

                return Color.FromArgb("#8E8E93");
            }

            if (value is bool hasUnread && parameter is string colorType)
            {
                if (colorType == "ChatName")
                {
                    return hasUnread ? Color.FromArgb("#075E54") : Color.FromArgb("#1A1A1A");
                }
                else if (colorType == "LastMessage")
                {
                    return hasUnread ? Color.FromArgb("#075E54") : Color.FromArgb("#8E8E93");
                }
                else if (colorType == "TimeStamp")
                {
                    return hasUnread ? Color.FromArgb("#25D366") : Color.FromArgb("#666666");
                }
            }

            return Color.FromArgb("#8E8E93");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToInitialsConverter : IValueConverter, IMultiValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrEmpty(name))
            {
                if (parameter as string == "color")
                {
                    return GenerateColorFromString(name);
                }

                var mainPart = name.Split('-')[0].Trim();
                var words = mainPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (words.Length >= 2)
                {
                    return $"{words[0][0]}{words[1][0]}".ToUpper();
                }
                else if (mainPart.Length >= 2)
                {
                    return mainPart.Substring(0, 2).ToUpper();
                }
                else if (mainPart.Length == 1)
                {
                    return mainPart.ToUpper();
                }

                return "??";
            }
            return "??";
        }

        private Color GenerateColorFromString(string text)
        {
            int hash = text.GetHashCode();

            Color[] colors = new[]
            {
                Color.FromArgb("#FF6B8E23"),
                Color.FromArgb("#FF2E8B57"),
                Color.FromArgb("#FF4682B4"),
                Color.FromArgb("#FF8A2BE2"),
                Color.FromArgb("#FFDC143C"),
                Color.FromArgb("#FF20B2AA"),
                Color.FromArgb("#FF9370DB"),
                Color.FromArgb("#FF32CD32"),
                Color.FromArgb("#FF4169E1"),
                Color.FromArgb("#FF8B4513"),
                Color.FromArgb("#FF483D8B"),
                Color.FromArgb("#FF2F4F4F"),
            };

            int index = Math.Abs(hash) % colors.Length;
            return colors[index];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 99 ? "99+" : count.ToString();
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsGroupToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isGroup)
            {
                return isGroup ? "👥" : "👤";
            }
            return "👤";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsMutedToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMuted)
            {
                return isMuted ? "🔇" : "";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class IsPinnedToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPinned)
            {
                return isPinned ? "📌" : "";
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LastMessagePreviewConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string message)
            {
                if (string.IsNullOrEmpty(message))
                    return "";

                const int maxLength = 40;
                if (message.Length > maxLength)
                {
                    return message.Substring(0, maxLength) + "...";
                }
                return message;
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class TabSelectionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentTab && parameter is string tabName)
            {
                return currentTab == tabName;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class OnlineStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isOnline)
            {
                return isOnline ? Color.FromArgb("#25D366") : Color.FromArgb("#8E8E93");
            }
            return Color.FromArgb("#8E8E93");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class MessageStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                switch (status)
                {
                    case 0: return "✓";
                    case 1: return "✓✓";
                    case 2: return Color.FromArgb("#25D366");
                    default: return Color.FromArgb("#8E8E93");
                }
            }
            return Color.FromArgb("#8E8E93");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts the first letter of a string to uppercase (for avatar)
    /// </summary>
    public class FirstLetterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text && !string.IsNullOrEmpty(text))
            {
                return text.Substring(0, 1).ToUpper();
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns true if string IS empty
    /// </summary>
    public class StringIsEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return string.IsNullOrWhiteSpace(text);
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Returns true if value is greater than zero (for cart badge visibility)
    /// </summary>
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int intValue)
            {
                return intValue > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PasswordTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isVisible)
            {
                return isVisible ? "Hide" : "Show";
            }
            return "Show";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts URL to absolute URL for image loading (WhatsApp-style chat attachments)
    /// </summary>
    public class UrlToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrEmpty(url))
            {
                // If it's already an absolute URL, return as is
                if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return url;
                }

                // Convert relative URL to absolute
                var baseUrl = Preferences.Get("api_base_url", "https://192.168.33.112:7023");
                baseUrl = baseUrl.TrimEnd('/');
                var relativePath = url.TrimStart('/');

                return $"{baseUrl}/{relativePath}";
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts downloading state to button color (Green for download, Red for cancel)
    /// </summary>
    public class DownloadButtonColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isDownloading)
            {
                // Red when downloading (to cancel), Green when ready to download
                return isDownloading ? Color.FromArgb("#F44336") : Color.FromArgb("#25D366");
            }
            return Color.FromArgb("#25D366");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts downloading state to status text
    /// </summary>
    public class DownloadStatusTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter needs the full message object to show file size
            // We'll handle this differently - see below
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}