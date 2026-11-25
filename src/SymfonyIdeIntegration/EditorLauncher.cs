using System.Diagnostics;
using System.Globalization;

namespace SymfonyIdeIntegration;

internal static class EditorLauncher
{
    internal static void Open(string url)
    {
        var settings = UserSettings.Load(out _);
        var link = EditorLink.Resolve(url);
        var command = CreateCommand(link, settings);
        using var process = Process.Start(command);
        IntegrationLog.Update($"Editor launch requested: {settings.Editor}");
    }

    internal static ProcessStartInfo CreateCommand(EditorLink link, UserSettings settings)
    {
        bool useCode = settings.Editor == Editor.VsCode;
        if (link.SshHost is not null && !useCode)
            throw new IntegrationException("Select Visual Studio Code in the tray menu to open an SSH project.");

        string path = EditorDiscovery.ResolveExecutable(settings) ??
            throw new IntegrationException($"Choose an existing {EditorDiscovery.ExecutableName(settings.Editor)} file in Settings.");

        var command = new ProcessStartInfo(path) { UseShellExecute = false };
        string file = link.FilePath;
        string directory = link.ProjectDirectory;
        string? line = link.Line?.ToString(CultureInfo.InvariantCulture);
        if (useCode)
        {
            string? remote = link.SshHost is { } host ? $"ssh-remote+{host}" :
                link.Distribution is { } distro ? $"wsl+{distro}" : null;
            if (remote is not null)
            {
                string authority = link.Distribution is { } distribution ? $"wsl+{Uri.EscapeDataString(distribution)}" : remote;
                string folder = string.Join('/', directory.Split('/').Select(Uri.EscapeDataString));
                AddArguments(command, "--remote", remote, "--folder-uri", $"vscode-remote://{authority}{folder}");
            }
            else
            {
                AddArguments(command, directory);
            }
            AddArguments(command, "--goto", line is null ? file : $"{file}:{line}");
        }
        else
        {
            if (link.Distribution is { } distro)
            {
                string share = $@"\\wsl.localhost\{distro}";
                directory = share + directory.Replace('/', '\\');
                file = share + file.Replace('/', '\\');
            }
            AddArguments(command, directory);
            if (line is not null)
                AddArguments(command, "--line", line);
            AddArguments(command, file);
        }
        return command;
    }

    private static void AddArguments(ProcessStartInfo command, params string[] arguments)
    {
        foreach (string argument in arguments)
            command.ArgumentList.Add(argument);
    }
}
