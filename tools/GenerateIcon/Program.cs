/// <summary>
/// Renders logo.svg → logo.png (256px) and logo.ico (16,32,48,64,128,256 px)
/// Usage: dotnet run -- <svg_path> <out_dir>
/// </summary>
using SkiaSharp;
using Svg.Skia;

var svgPath = args.Length > 0 ? args[0] : "../../src/LyricsPro/Assets/logo.svg";
var outDir  = args.Length > 1 ? args[1] : "../../src/LyricsPro/Assets";
Directory.CreateDirectory(outDir);

Console.WriteLine($"Rendering {svgPath} ...");

// ── Render PNG at each needed size ───────────────────────────────
int[] sizes = [16, 32, 48, 64, 128, 256];
var pngFiles = new List<(int size, byte[] data)>();

foreach (var sz in sizes)
{
    var svg = new SKSvg();
    svg.Load(svgPath);

    var picture = svg.Picture!;
    var srcBounds = picture.CullRect;

    using var bitmap = new SKBitmap(sz, sz);
    using var canvas = new SKCanvas(bitmap);
    canvas.Clear(SKColors.Transparent);

    float scale = sz / Math.Max(srcBounds.Width, srcBounds.Height);
    canvas.Scale(scale);
    canvas.DrawPicture(picture);
    canvas.Flush();

    using var img  = SKImage.FromBitmap(bitmap);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    var bytes = data.ToArray();
    pngFiles.Add((sz, bytes));

    // Save the 256px version as logo.png
    if (sz == 256)
        File.WriteAllBytes(Path.Combine(outDir, "logo.png"), bytes);

    Console.WriteLine($"  {sz}x{sz} ✓");
}

// ── Pack into .ico ─────────────────────────────────────────────
// ICO format: header + directory + pixel data
var icoPath = Path.Combine(outDir, "logo.ico");
using var ico = new FileStream(icoPath, FileMode.Create);
using var w   = new BinaryWriter(ico);

// Header
w.Write((short)0);           // reserved
w.Write((short)1);           // type: icon
w.Write((short)pngFiles.Count);

// Directory entries (each 16 bytes)
int dataOffset = 6 + pngFiles.Count * 16;
var offsets = new List<int>();
foreach (var (sz, bytes) in pngFiles)
{
    w.Write((byte)(sz == 256 ? 0 : sz));  // width  (0 = 256)
    w.Write((byte)(sz == 256 ? 0 : sz));  // height
    w.Write((byte)0);    // color count (0 = >8bpp)
    w.Write((byte)0);    // reserved
    w.Write((short)1);   // planes
    w.Write((short)32);  // bit count
    w.Write(bytes.Length);
    w.Write(dataOffset);
    offsets.Add(dataOffset);
    dataOffset += bytes.Length;
}

// Image data
foreach (var (_, bytes) in pngFiles)
    w.Write(bytes);

Console.WriteLine($"\nSaved:");
Console.WriteLine($"  {Path.Combine(outDir, "logo.png")}");
Console.WriteLine($"  {icoPath}");
Console.WriteLine("\nDone!");
