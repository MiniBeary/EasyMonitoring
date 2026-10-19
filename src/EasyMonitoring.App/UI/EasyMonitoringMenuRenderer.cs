using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EasyMonitoring.App.UI;

internal sealed class EasyMonitoringMenuRenderer : ToolStripProfessionalRenderer
{
    public EasyMonitoringMenuRenderer()
        : base(new EasyMonitoringColorTable())
    {
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(MenuTheme.Background);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(MenuTheme.Border);
        var bounds = e.ToolStrip.ClientRectangle;
        e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var color = e.Item.Pressed
            ? MenuTheme.Pressed
            : e.Item.Selected
                ? MenuTheme.Hover
                : MenuTheme.Background;

        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var bounds = e.TextRectangle.IsEmpty
            ? e.Item.ContentRectangle
            : e.TextRectangle;

        TextRenderer.DrawText(
            e.Graphics,
            e.Text,
            e.TextFont ?? e.Item.Font,
            bounds,
            MenuTheme.Foreground,
            e.TextFormat);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(MenuTheme.Separator);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 4, y, e.Item.Width - 4, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        var bounds = e.ImageRectangle;
        if (bounds.IsEmpty)
        {
            bounds = e.Item.ContentRectangle;
        }

        using var pen = new Pen(MenuTheme.Foreground, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawLines(pen,
        [
            new Point(bounds.Left + 2, bounds.Top + (bounds.Height * 2 / 3)),
            new Point(bounds.Left + (bounds.Width / 2), bounds.Bottom - 2),
            new Point(bounds.Right - 2, bounds.Top + 2),
        ]);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = MenuTheme.Foreground;
        base.OnRenderArrow(e);
    }
}

internal sealed class EasyMonitoringColorTable : ProfessionalColorTable
{
    public override Color MenuItemSelected => MenuTheme.Hover;

    public override Color MenuItemSelectedGradientBegin => MenuTheme.Hover;

    public override Color MenuItemSelectedGradientEnd => MenuTheme.Hover;

    public override Color MenuItemPressedGradientBegin => MenuTheme.Pressed;

    public override Color MenuItemPressedGradientEnd => MenuTheme.Pressed;

    public override Color MenuItemBorder => MenuTheme.Border;

    public override Color ToolStripDropDownBackground => MenuTheme.Background;

    public override Color SeparatorDark => MenuTheme.Separator;

    public override Color SeparatorLight => MenuTheme.Separator;

    public override Color ImageMarginGradientBegin => MenuTheme.Background;

    public override Color ImageMarginGradientMiddle => MenuTheme.Background;

    public override Color ImageMarginGradientEnd => MenuTheme.Background;
}
