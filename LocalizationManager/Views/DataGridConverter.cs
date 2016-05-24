using System;
using System.Data;
using System.Globalization;
using System.Windows.Data;

namespace LocalizationManager.Views {
    public class DataGridConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            DataTable dt = (DataTable)value;

            return dt.DefaultView;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
