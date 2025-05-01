using System;
using System.Globalization;
using System.Windows.Data;

namespace ClipSage.App.Converters
{
    public class BooleanToMonitoringTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMonitoring)
            {
                return isMonitoring ? "Pause" : "Resume";
            }
            return "Toggle";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
