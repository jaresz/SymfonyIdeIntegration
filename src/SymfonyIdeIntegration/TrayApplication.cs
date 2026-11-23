using System.Reflection;

namespace SymfonyIdeIntegration;

internal sealed class TrayApplication : ApplicationContext
{
    private readonly ContextMenuStrip menu = new();
    private readonly NotifyIcon trayIcon;
    private readonly ToolStripMenuItem vsCodeItem = new("Visual Studio Code");
    private readonly ToolStripMenuItem phpStormItem = new("PhpStorm");
    private readonly System.Threading.Timer logCleanup;
    private UserSettings settings;
    private bool settingsDamaged;
    private Icon? currentIcon;

    internal TrayApplication()
    {
        settings = UserSettings.Load(out settingsDamaged);
        vsCodeItem.Click += (_, _) => Save(settings with { Editor = Editor.VsCode });
        phpStormItem.Click += (_, _) => Save(settings with { Editor = Editor.PhpStorm });
        menu.Items.AddRange([vsCodeItem, phpStormItem, new ToolStripSeparator(),
            new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings()),
            new ToolStripMenuItem("About…", null, (_, _) => ShowAbout()),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread())]);
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
        logCleanup = new(_ => IntegrationLog.Update(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
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
            IntegrationLog.Update($"Could not save settings: {error.GetType().Name}");
            MessageBox.Show("Could not save settings. See integration.log in %LOCALAPPDATA%\\SymfonyIdeIntegration for details.",
                "Symfony IDE Integration", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            logCleanup.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            currentIcon?.Dispose();
            menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
