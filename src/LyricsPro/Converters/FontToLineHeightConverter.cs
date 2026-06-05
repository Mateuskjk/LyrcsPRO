using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace LyricsPro.Converters;

public class FontToLineHeightConverter : IValueConverter
{
    public static readonly FontToLineHeightConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c)
        => v is double fs ? fs * 1.45 : 1.45;
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}
