namespace SymfonyIdeIntegration;

internal static class Program
{
    private static int ReportError(Exception error)
    {
        IntegrationLog.Update($"Error: {error.GetType().Name}");
        string message = error is IntegrationException
            ? error.Message
            : "An unexpected error occurred. See integration.log in %LOCALAPPDATA%\\SymfonyIdeIntegration for details.";
        MessageBox.Show(message, "Symfony IDE Integration", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return 1;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        try
        {
            if (args is ["--open", string url])
            {
                EditorLauncher.Open(url);
            }
            else if (args.Length == 0)
            {
                using var trayMutex = new Mutex(false, "Q3a-Symfony2ide.Tray", out bool createdNew);
                if (createdNew)
                {
                    using var tray = new TrayApplication();
                    Application.Run(tray);
                }
            }
            else
            {
                throw new IntegrationException("Invalid command-line arguments.");
            }
            return 0;
        }
        catch (Exception error)
        {
            return ReportError(error);
        }
    }
}
