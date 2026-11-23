using System.Diagnostics;
using System.Globalization;

namespace SymfonyIdeIntegration;

internal static class EditorLauncher
{
    internal static void Open(string url)
    {
        var settings = UserSettings.Load(out _);
        var link = EditorLink.Resolve(url, settings);
        var command = CreateCommand(link, settings);
        using var process = Process.Start(command);
        IntegrationLog.Update($"Editor launch requested: {settings.Editor}");
    }

    internal static ProcessStartInfo CreateCommand(EditorLink link, UserSettings settings)
    {
        bool useCode = settings.Editor == Editor.VsCode;
        string path = EditorDiscovery.ResolveExecutable(settings) ??
            throw new IntegrationException($"Choose an existing {EditorDiscovery.ExecutableName(settings.Editor)} file in Settings.");

        var command = new ProcessStartInfo(path) { UseShellExecute = false };
        string file = link.FilePath;
        string directory = link.ProjectDirectory;
        string? line = link.Line?.ToString(CultureInfo.InvariantCulture);
        if (useCode)
        {
            if (link.Distribution is { } distro)
            {
                string remote = $"wsl+{distro}";
                string folder = string.Join('/', directory.Split('/').Select(Uri.EscapeDataString));
                AddArguments(command, "--remote", remote, "--folder-uri", $"vscode-remote://wsl+{Uri.EscapeDataString(distro)}{folder}");
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
