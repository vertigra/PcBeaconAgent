using System;
using System.Globalization;
using Microsoft.Maui.Controls;    
using Microsoft.Maui.Graphics;    

namespace PcBeaconAgent.Client.Android.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isOnline)
            {
                return isOnline ? Color.FromArgb("#4CAF50") : Color.FromArgb("#F44336");
            }

            return Colors.Gray;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
