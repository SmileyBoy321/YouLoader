using System.Globalization;
using System.Windows.Data;

namespace YouLoader;

/// <summary>Returns the bound number, but never less than the parameter. Used to give the page a minimum height.</summary>
public sealed class AtLeastConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Math.Max((double)value, double.Parse((string)parameter, CultureInfo.InvariantCulture));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
