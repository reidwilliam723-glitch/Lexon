using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Lexon.Ui;

/// <summary>
/// Shared Lexon mark: rounded indigo tile with a white L.
/// </summary>
public static class LexonIconFactory
{
    public static readonly Color Brand = Color.FromArgb(37, 99, 235);
    public static readonly Color BrandMuted = Color.FromArgb(148, 163, 184);
    public static readonly Color BrandError = Color.FromArgb(220, 38, 38);
    public static readonly Color BrandLearning = Color.FromArgb(5, 150, 105);

    public static Icon CreateStatusIcon(Color fill, int size = 16)
    {
        using var bitmap = Render(size, fill);
        return IconFromBitmap(bitmap);
    }

    public static Icon CreateApplicationIcon()
    {
        using var bitmap = Render(32, Brand);
        return IconFromBitmap(bitmap);
    }

    public static void WriteIco(string path)
    {
        var sizes = new[] { 16, 32, 48, 256 };
        var images = sizes.Select(size =>
        {
            using var bitmap = Render(size, Brand);
            return ToPng(bitmap);
        }).ToList();

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)images.Count);

        var offset = 6 + (16 * images.Count);
        for (var i = 0; i < images.Count; i++)
        {
            var size = sizes[i];
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(images[i].Length);
            writer.Write(offset);
            offset += images[i].Length;
        }

        foreach (var png in images)
        {
            writer.Write(png);
        }
    }

    public static Bitmap Render(int size, Color fill)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        var pad = Math.Max(1, size / 16);
        var tile = new Rectangle(pad, pad, size - pad * 2, size - pad * 2);
        var radius = Math.Max(2, size / 5);
        using (var path = Rounded(tile, radius))
        using (var brush = new SolidBrush(fill))
        {
            g.FillPath(brush, path);
        }

        using var letter = new SolidBrush(Color.White);
        var stem = Math.Max(2, (int)Math.Round(size * 0.16));
        var bar = Math.Max(2, (int)Math.Round(size * 0.14));
        var left = tile.X + (int)Math.Round(tile.Width * 0.26);
        var top = tile.Y + (int)Math.Round(tile.Height * 0.2);
        var tall = (int)Math.Round(tile.Height * 0.58);
        var wide = (int)Math.Round(tile.Width * 0.46);
        g.FillRectangle(letter, left, top, stem, tall);
        g.FillRectangle(letter, left, top + tall - bar, wide, bar);
        return bitmap;
    }

    private static Icon IconFromBitmap(Bitmap bitmap)
    {
        var handle = bitmap.GetHicon();
        try
        {
            using var fromHandle = Icon.FromHandle(handle);
            return (Icon)fromHandle.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static byte[] ToPng(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    private static GraphicsPath Rounded(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}
