using System.Diagnostics;
using System.Globalization;

namespace SymfonyIdeIntegration;

internal static class EditorLauncher
{
    internal static void Open(string url, InvocationReport report)
    {
        report.Stage = "Loading editor settings";
        var settings = UserSettings.Load(out _);
        report.Editor = settings.Editor == Editor.VsCode ? "Visual Studio Code" : "PhpStorm";
        report.Stage = "Resolving link";
        var link = EditorLink.Resolve(url, report);
        report.ResolvedProject = link.ProjectDirectory;
        report.ResolvedFile = link.FilePath;
        report.Line = link.Line;
        report.Stage = "Preparing editor command";
        var command = CreateCommand(link, settings, report);
        report.Executable = command.FileName;
        report.Arguments = command.ArgumentList.ToArray();
        report.Stage = "Starting editor command";
        using var process = Process.Start(command);
        if (process is null)
            throw new IntegrationException("Could not start the editor command.");
        // An SSH process can start successfully even when its connection or
        // remote file-opening request fails. Check its exit code before reporting success.
        if (link.SshHost is not null && settings.Editor == Editor.PhpStorm)
        {
            report.Stage = "Waiting for PhpStorm over SSH";
            report.Publish();
            PhpStormRemoteLauncher.WaitForOpen(process, report);
            report.Result = "SSH command completed successfully";
        }
        else
        {
            // The local editor or VS Code Remote CLI may return before the UI
            // opens the file. Starting the process is not proof of file opening.
            report.Result = "Editor launch requested";
        }
        report.Stage = "Completed";
    }

    internal static ProcessStartInfo CreateCommand(EditorLink link, UserSettings settings, InvocationReport? report = null)
    {
        bool useCode = settings.Editor == Editor.VsCode;
        if (report is not null)
        {
            report.TargetProject = link.ProjectDirectory;
            report.TargetFile = link.FilePath;
        }
        // PhpStorm's local executable does not open files in a Remote Development
        // session. Send the request to the IDE backend on the SSH host instead.
        if (link.SshHost is not null && !useCode)
            return PhpStormRemoteLauncher.CreateCommand(link);

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
                // The folder URI selects the remote workspace even when the path
                // contains spaces or has no file extension.
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
                // PhpStorm runs locally for WSL projects, so give it the Windows
                // network share paths for that distribution's Linux filesystem.
                string share = $@"\\wsl.localhost\{distro}";
                directory = share + directory.Replace('/', '\\');
                file = share + file.Replace('/', '\\');
                if (report is not null)
                {
                    report.TargetProject = directory;
                    report.TargetFile = file;
                }
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
