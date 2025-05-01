using ClipSage.Core;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ClipSage.App.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DataTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipboardDataType dataType)
            {
                return dataType switch
                {
                    ClipboardDataType.Text => new SolidColorBrush(Color.FromRgb(33, 150, 243)), // Blue
                    ClipboardDataType.Image => new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158)), // Gray
                };
            }
            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Gray
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DataTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ClipboardDataType dataType)
            {
                return dataType switch
                {
                    ClipboardDataType.Text => "T",
                    ClipboardDataType.Image => "I",
                    _ => "?",
                };
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
