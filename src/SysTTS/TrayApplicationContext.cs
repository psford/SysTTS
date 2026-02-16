using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace SysTTS;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly CancellationTokenSource _appCts;

    public TrayApplicationContext(CancellationTokenSource appCts)
    {
        _appCts = appCts;

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("SysTTS - Running", null, null!).Enabled = false;
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Quit", null, OnQuit);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateEmojiIcon("\U0001F50A"),
            Text = "SysTTS",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };
    }

    private static Icon CreateEmojiIcon(string emoji)
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.Clear(Color.Transparent);

        // Dark circle background for visibility on any taskbar theme
        using var bgBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
        graphics.FillEllipse(bgBrush, 0, 0, size - 1, size - 1);

        using var font = new Font("Segoe UI Emoji", 20, FontStyle.Regular, GraphicsUnit.Pixel);
        var textSize = graphics.MeasureString(emoji, font);
        var x = (size - textSize.Width) / 2;
        var y = (size - textSize.Height) / 2;
        graphics.DrawString(emoji, font, Brushes.White, x, y);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _contextMenu?.Dispose();
        _notifyIcon.Dispose();
        _appCts.Cancel();
        ExitThread();
    }
}
