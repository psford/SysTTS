using System.Drawing;
using System.Windows.Forms;

namespace SysTTS;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly CancellationTokenSource _appCts;

    public TrayApplicationContext(CancellationTokenSource appCts)
    {
        _appCts = appCts;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("SysTTS - Running", null, null!).Enabled = false;
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Quit", null, OnQuit);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "SysTTS",
            Visible = true,
            ContextMenuStrip = contextMenu
        };
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _appCts.Cancel();
        ExitThread();
    }
}
