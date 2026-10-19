using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text;

namespace EasyMonitoring.App.Icons;

internal static class ArcIconFactory
{
    private const int FrameCount = 5;
    private const int IconSize = 32;

    public static IReadOnlyList<Icon> CreateFrames()
    {
        var frames = new List<Icon>(FrameCount);

        for (var index = 0; index < FrameCount; index++)
        {
            frames.Add(CreateFrame(index * (360f / FrameCount)));
        }

        return frames;
    }

    private static Icon CreateFrame(float startAngle)
    {
        using var bitmap = new Bitmap(IconSize, IconSize, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var bounds = new Rectangle(6, 6, 20, 20);

        using var outline = new Pen(Color.FromArgb(230, 30, 30, 30), 6f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        using var arc = new Pen(Color.FromArgb(255, 255, 150, 20), 3.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        graphics.DrawArc(outline, bounds, startAngle, 270);
        graphics.DrawArc(arc, bounds, startAngle, 270);

        return ToIcon(bitmap);
    }

    private static Icon ToIcon(Bitmap bitmap)
    {
        using var pngStream = new MemoryStream();
        bitmap.Save(pngStream, ImageFormat.Png);
        var png = pngStream.ToArray();

        using var iconStream = new MemoryStream();

        using (var writer = new BinaryWriter(iconStream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((ushort)0);
            writer.Write((ushort)1);
            writer.Write((ushort)1);

            writer.Write((byte)IconSize);
            writer.Write((byte)IconSize);
            writer.Write((byte)0);
            writer.Write((byte)0);

            writer.Write((ushort)1);
            writer.Write((ushort)32);

            writer.Write((uint)png.Length);
            writer.Write((uint)(6 + 16));
            writer.Write(png);
        }

        iconStream.Position = 0;

        using var icon = new Icon(iconStream);
        return (Icon)icon.Clone();
    }
}
