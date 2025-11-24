using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TestLogin.Converters
{
    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Use fully-qualified Color to avoid ambiguity with Autodesk.Revit.DB.Color
            if (value is System.Windows.Media.Color c)
                return new SolidColorBrush(c);

            if (value is SolidColorBrush brush)
                return brush;

            // Use fully-qualified Binding to avoid ambiguity with Autodesk.Revit.DB.Binding
            return System.Windows.Data.Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
