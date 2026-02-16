using System.Drawing;
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
            Icon = SystemIcons.Application,
            Text = "SysTTS",
            Visible = true,
            ContextMenuStrip = _contextMenu
        };
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
