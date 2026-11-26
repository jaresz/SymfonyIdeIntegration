using System.Diagnostics;
using System.Globalization;

namespace SymfonyIdeIntegration;

internal static class PhpStormRemoteLauncher
{
    // PhpStorm Remote Development runs the IDE on the SSH host and displays it
    // locally in JetBrains Client. Its serverMode command can forward a file-opening
    // request to the running backend when given its project directory.
    // Discover that backend instead of assuming a fixed Toolbox or Gateway install path.
    private const string OpenFileScript = """
        project=$1
        file=$2
        line=$3
        launcher=

        # Only inspect IDE processes owned by the SSH user. Linux may truncate
        # the remote-dev-server process name to remote-dev-serv.
        for pid in $(pgrep -u "$(id -u)" -x 'phpstorm|remote-dev-serv|remote-dev-server'); do
            args="/proc/$pid/cmdline"
            # Linux stores process arguments separated by NUL bytes. Match the
            # entire project argument to avoid selecting a similarly named project.
            # Also require a remote development command rather than a desktop IDE.
            grep -zFxq -e "$project" -e "$project/" "$args" 2>/dev/null || continue
            grep -zFxq -e serverMode -e run "$args" 2>/dev/null || continue
            executable=$(readlink "/proc/$pid/exe") || continue
            case "$executable" in
                */bin/phpstorm|*/bin/remote-dev-server) ;;
                *) continue ;;
            esac
            # Use the launcher from the running backend's installation so that
            # version-specific IDE configuration and runtime files match.
            candidate="${executable%/*}/remote-dev-server"
            if [ ! -x "$candidate" ]; then
                candidate="$candidate.sh"
            fi
            [ -x "$candidate" ] || continue
            # Refuse to choose between different matching backend installations.
            if [ -n "$launcher" ] && [ "$launcher" != "$candidate" ]; then
                exit 41
            fi
            launcher=$candidate
        done

        # Dedicated exit codes allow the Windows application to explain failures.
        # An existing backend and file are required before sending the request.
        [ -n "$launcher" ] || exit 40
        [ -f "$file" ] || exit 42

        # serverMode identifies the project-specific backend. Supply --line only
        # when the link includes a line number, and limit how long the request can run.
        set -- serverMode "$project"
        if [ -n "$line" ]; then
            set -- "$@" --line "$line"
        fi
        timeout 20 "$launcher" "$@" "$file"
        """;

    internal static ProcessStartInfo CreateCommand(EditorLink link)
    {
        // Use the Windows OpenSSH client directly so it reads the user's existing
        // SSH configuration and resolves the alias supplied in the Symfony link.
        string ssh = Path.Combine(Environment.SystemDirectory, "OpenSSH", "ssh.exe");
        if (!File.Exists(ssh))
            throw new IntegrationException("Install the Windows OpenSSH Client to open SSH projects in PhpStorm.");

        // OpenSSH passes its command to the remote login shell. Quote each value
        // for that shell in addition to using ArgumentList for Windows.
        // Linux shell scripts also require LF line endings, regardless of checkout settings.
        string remoteCommand = "sh -c " + Quote(OpenFileScript.ReplaceLineEndings("\n")) + " sh " +
            Quote(link.ProjectDirectory) + " " + Quote(link.FilePath) + " " +
            Quote(link.Line?.ToString(CultureInfo.InvariantCulture) ?? "");
        var command = new ProcessStartInfo(ssh) { UseShellExecute = false, CreateNoWindow = true };
        // Links are opened from a GUI application without a terminal for password
        // prompts. Fail on interactive authentication or an untrusted host key.
        foreach (string argument in new[]
        {
            "-T", "-o", "BatchMode=yes", "-o", "StrictHostKeyChecking=yes",
            "-o", "ConnectTimeout=5", link.SshHost!, remoteCommand
        })
            command.ArgumentList.Add(argument);
        return command;
    }

    internal static void WaitForOpen(Process process, InvocationReport? report = null)
    {
        // The local deadline also covers SSH failures before or after the remote
        // launcher's own timeout, so a click cannot leave us waiting indefinitely.
        if (!process.WaitForExit(30000))
        {
            if (!process.HasExited)
                process.Kill();
            throw new IntegrationException("The SSH request to PhpStorm timed out.");
        }

        // Codes 40–42 come from OpenFileScript; 124 is the timeout utility's
        // exit code, and 255 is the usual OpenSSH connection failure code.
        int exitCode = process.ExitCode;
        if (report is not null)
            report.SshExitCode = exitCode;
        string? error = exitCode switch
        {
            0 => null,
            40 => "Open the remote project in PhpStorm Remote Development first, then click the link again.",
            41 => "More than one PhpStorm backend matches this SSH project. Close the duplicate session and try again.",
            42 => "The referenced file does not exist on the SSH host.",
            124 => "The SSH request to PhpStorm timed out.",
            255 => "Could not connect using the configured SSH host. Check the OpenSSH configuration, authentication and trusted host key.",
            _ => "The remote PhpStorm backend could not open the file."
        };
        if (error is not null)
            throw new IntegrationException(error);
    }

    // POSIX single quotes preserve spaces and shell metacharacters. An embedded
    // apostrophe is represented by a separately double-quoted apostrophe.
    private static string Quote(string value) => "'" + value.Replace("'", "'\"'\"'") + "'";
}
