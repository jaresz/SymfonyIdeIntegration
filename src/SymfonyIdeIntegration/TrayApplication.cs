using System.Reflection;

namespace SymfonyIdeIntegration;

internal sealed class TrayApplication : ApplicationContext
{
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem vsCodeItem = new("Visual Studio Code");
    private readonly ToolStripMenuItem phpStormItem = new("PhpStorm");
    private readonly ToolStripSeparator quickLaunchSeparator = new();
    private readonly List<ToolStripMenuItem> quickLaunchItems = [];
    private readonly InvocationChannel invocations;
    private LastInvocationDialog? invocationDialog;
    private UserSettings settings;
    private bool settingsDamaged;
    private bool disposed;
    private Icon? currentIcon;

    internal TrayApplication()
    {
        settings = UserSettings.Load(out settingsDamaged);
        vsCodeItem.Click += (_, _) => Save(settings with { Editor = Editor.VsCode });
        phpStormItem.Click += (_, _) => Save(settings with { Editor = Editor.PhpStorm });
        menu.Items.AddRange([quickLaunchSeparator, vsCodeItem, phpStormItem, new ToolStripSeparator(),
            new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings()),
            new ToolStripMenuItem("Last invocation…", null, (_, _) => OpenLastInvocation()),
            new ToolStripMenuItem("About…", null, (_, _) => ShowAbout()),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread())]);
        menu.Opening += (_, _) => RefreshQuickLaunch();
        trayIcon = new NotifyIcon
        {
            ContextMenuStrip = menu
        };
        trayIcon.MouseUp += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
                menu.Show(Cursor.Position, ToolStripDropDownDirection.AboveRight);
        };
        UpdateMenu();
        trayIcon.Visible = true;
        invocations = new InvocationChannel();
    }

    private void RefreshQuickLaunch()
    {
        // Re-read Quick Launch before either mouse button opens the menu, so
        // added, renamed and removed shortcuts appear without restarting the tray.
        ClearQuickLaunch();
        int index = menu.Items.IndexOf(quickLaunchSeparator);
        foreach (ToolStripMenuItem item in QuickLaunch.CreateItems())
        {
            menu.Items.Insert(index++, item);
            quickLaunchItems.Add(item);
        }
        quickLaunchSeparator.Available = quickLaunchItems.Count > 0;
    }

    private void ClearQuickLaunch()
    {
        foreach (ToolStripMenuItem item in quickLaunchItems)
        {
            menu.Items.Remove(item);
            // The shell icons are our bitmaps; disposing a menu item does not own them.
            Image? image = item.Image;
            item.Dispose();
            image?.Dispose();
        }
        quickLaunchItems.Clear();
    }

    private void UpdateMenu()
    {
        vsCodeItem.Checked = settings.Editor == Editor.VsCode;
        phpStormItem.Checked = settings.Editor == Editor.PhpStorm;
        trayIcon.Text = $"Symfony IDE Integration · {(vsCodeItem.Checked ? vsCodeItem.Text : phpStormItem.Text)}";
        Icon updatedIcon = TrayIcons.Load(settings);
        trayIcon.Icon = updatedIcon;
        currentIcon?.Dispose();
        currentIcon = updatedIcon;
    }

    private void OpenLastInvocation()
    {
        // Keep a single modeless window so the tray remains usable and the
        // window can display subsequent requests without being reopened.
        if (invocationDialog is { IsDisposed: false })
        {
            if (invocationDialog.WindowState == FormWindowState.Minimized)
                invocationDialog.WindowState = FormWindowState.Normal;
            invocationDialog.Activate();
            return;
        }
        invocationDialog = new LastInvocationDialog(invocations);
        invocationDialog.FormClosed += (_, _) => invocationDialog = null;
        invocationDialog.Show();
        invocationDialog.Activate();
    }

    private static void ShowAbout()
    {
        string version = typeof(TrayApplication).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        using var dialog = new AboutDialog(version);
        dialog.ShowDialog();
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsDialog(settings, settingsDamaged, Save);
        dialog.ShowDialog();
    }

    private bool Save(UserSettings updated)
    {
        try
        {
            updated.Save();
            settings = updated;
            settingsDamaged = false;
            UpdateMenu();
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Could not save settings.\n\n{error.Message}",
                "Symfony IDE Integration", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        // WinForms disposes the application context when its message loop ends;
        // Program's using statement also disposes it. Release owned resources once.
        if (disposing && !disposed)
        {
            disposed = true;
            invocationDialog?.Dispose();
            invocations.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            currentIcon?.Dispose();
            ClearQuickLaunch();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
