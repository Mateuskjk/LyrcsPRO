using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using QRCoder;
using System;
using System.Globalization;
using System.IO;

namespace LyricsPro.Converters;

public class UrlToQrConverter : IValueConverter
{
    public static readonly UrlToQrConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string url || string.IsNullOrEmpty(url)) return null;
        try
        {
            var qr       = new QRCodeGenerator();
            var data     = qr.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var pngQr    = new PngByteQRCode(data);
            var pngBytes = pngQr.GetGraphic(6,
                darkColorRgba:  [0xD4, 0x61, 0x0A, 0xFF],   // orange
                lightColorRgba: [0x11, 0x11, 0x11, 0xFF]);   // dark bg

            using var ms = new MemoryStream(pngBytes);
            return new Bitmap(ms);
        }
        catch { return null; }
    }

    public object? ConvertBack(object? v, Type t, object? p, CultureInfo c)
        => throw new NotSupportedException();
}
