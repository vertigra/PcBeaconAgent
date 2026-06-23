using Microsoft.Maui.Controls;
using System;
using System.Globalization;

namespace PcBeaconAgent.Client.Android.Converters
{
    public class StringIsEmptyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}