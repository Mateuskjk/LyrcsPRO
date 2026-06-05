using Avalonia.Data.Converters;
using LyricsPro.ViewModels;
using System;
using System.Globalization;

namespace LyricsPro.Converters;

public class SectionTitleConverter : IValueConverter
{
    public static readonly SectionTitleConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is NavSection s ? s switch
        {
            NavSection.Home   => "Início",
            NavSection.Search => "Buscar Letras",
            NavSection.Lyrics => "Biblioteca de Letras",
            NavSection.Media  => "Biblioteca de Mídia",
            NavSection.Bible  => "Bíblia",
            NavSection.Live   => "Projetor — Ao Vivo",
            _                 => ""
        } : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
