/*using System.Globalization;

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
    /// <summary>Negates a bool — used to show/hide elements.</summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;
    }

    // ── BoolToStringConverter ───────────────────────────────────────────────
    /// <summary>Returns TrueValue or FalseValue string based on a bool binding.</summary>
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
    /// <summary>
    /// Converts a 0.0–1.0 double to a pixel width for the progress bar fill.
    /// Used as a singleton because the bar width is dynamic; wire up in
    /// SizeChanged if you need true responsive width.
    /// For a simple approach, returns a fixed max of 260 × progress.
    /// Replace 260 with the actual track width if you measure it at runtime.
    /// </summary>
    public class ProgressWidthConverter : IValueConverter
    {
        public static readonly ProgressWidthConverter Instance = new();

        /// <summary>Maximum track width in pixels. Adjust to your layout.</summary>
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

}*/