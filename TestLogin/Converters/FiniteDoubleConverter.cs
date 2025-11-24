using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace TestLogin.Converters
{
    public class FiniteDoubleConverter : IValueConverter
    {
        public double Fallback { get; set; } = 100.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && !double.IsInfinity(d) && !double.IsNaN(d))
                return d;
            return Fallback;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
