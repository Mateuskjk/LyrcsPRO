using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace LyricsPro.Converters;

public class BoolToBrushConverter : IValueConverter
{
    // Blank button: orange when blank is ON (tela preta ativa)
    public static readonly BoolToBrushConverter BlankInstance = new(
        trueColor:  "#C0392B",   // red = blank active
        falseColor: "#2A1608"    // dark orange = normal
    );

    private readonly SolidColorBrush _trueBrush;
    private readonly SolidColorBrush _falseBrush;

    public BoolToBrushConverter(string trueColor, string falseColor)
    {
        _trueBrush  = new SolidColorBrush(Color.Parse(trueColor));
        _falseBrush = new SolidColorBrush(Color.Parse(falseColor));
    }

    public object? Convert(object? v, Type t, object? p, CultureInfo c)
        => v is true ? _trueBrush : _falseBrush;

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}

public class BoolToStringConverter : IValueConverter
{
    public static readonly BoolToStringConverter BlankInstance = new("Tela Preta ON", "Tela Preta");

    private readonly string _trueText;
    private readonly string _falseText;

    public BoolToStringConverter(string trueText, string falseText)
    {
        _trueText  = trueText;
        _falseText = falseText;
    }

    public object? Convert(object? v, Type t, object? p, CultureInfo c)
        => v is true ? _trueText : _falseText;

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}
