namespace SymfonyIdeIntegration;

internal static class Program
{
    private static int ReportError(Exception error)
    {
        string message = error is IntegrationException
            ? error.Message
            : $"An unexpected error occurred.\n\n{error.Message}\n\nOpen Last invocation… from the tray menu for link diagnostics, if available.";
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
                // Publish even invalid links, and send the final snapshot before
                // showing an error dialog so it can be inspected immediately.
                var report = new InvocationReport { Url = url };
                report.Publish();
                try
                {
                    EditorLauncher.Open(url, report);
                }
                catch (Exception error)
                {
                    report.Result = "Error";
                    report.ErrorType = error.GetType().Name;
                    report.ErrorMessage = error.Message;
                    throw;
                }
                finally
                {
                    report.FinishedAt = DateTimeOffset.UtcNow;
                    report.Publish();
                }
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
