using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ClipSage.Core.Storage;

namespace ClipSage.App.Converters
{
    public class DataTypeToTextVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipboardDataType dataType)
            {
                return dataType == ClipboardDataType.Text ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
