using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace CraneControl.App.ViewModels;

/// <summary>Zamienia tryb automatyczny na kursor "wskaźnik", sygnalizując możliwość kliknięcia.</summary>
public sealed class BoolToCursorConverter : IValueConverter
{
    public static readonly BoolToCursorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new Cursor(StandardCursorType.Hand) : new Cursor(StandardCursorType.Arrow);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
