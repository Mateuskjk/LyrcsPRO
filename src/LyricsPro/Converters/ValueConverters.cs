using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System;
using System.Globalization;
using System.IO;

namespace LyricsPro.Converters;

// Already defined in EnumToBoolConverter.cs — ZeroToBool, NonZeroToBool, PathToImage, IndexToBool

public class ZeroToBoolConverter : IValueConverter
{
    public static readonly ZeroToBoolConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c) => v is int i && i == 0;
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class NonZeroToBoolConverter : IValueConverter
{
    public static readonly NonZeroToBoolConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c) => v is int i && i > 0;
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class IndexToBoolConverter : IValueConverter
{
    public static readonly IndexToBoolConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c)
    {
        if (v is int idx && p is string s && int.TryParse(s, out var target))
            return idx == target;
        return false;
    }
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class NullToColorConverter : IValueConverter
{
    public static readonly NullToColorConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c)
        => string.IsNullOrEmpty(v as string)
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#444"))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#D4610A"));
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

public class PathToImageConverter : IValueConverter
{
    public static readonly PathToImageConverter Instance = new();
    public object? Convert(object? v, Type t, object? p, CultureInfo c)
    {
        if (v is string path && File.Exists(path))
        {
            try { return new Bitmap(path); } catch { }
        }
        return null;
    }
    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
