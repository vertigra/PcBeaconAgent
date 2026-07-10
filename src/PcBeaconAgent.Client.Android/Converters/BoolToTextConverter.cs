using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace PcBeaconAgent.Client.Android.Converters
{
    public class BoolToTextConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return (value is bool isOnline && isOnline) ? "Online" : "Offline";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
