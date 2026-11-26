using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SymfonyIdeIntegration;

internal sealed record EditorLink(
    string ProjectDirectory, string FilePath, int? Line, string? Distribution = null, string? SshHost = null)
{
    internal const string Scheme = "Q3a-Symfony2ide";

    internal static EditorLink Resolve(string url, InvocationReport? report = null)
    {
        int queryStart = url.IndexOf('?');
        if (queryStart < 0 ||
            (!url[..queryStart].Equals($"{Scheme}://open/", StringComparison.OrdinalIgnoreCase) &&
             !url[..queryStart].Equals($"{Scheme}://open", StringComparison.OrdinalIgnoreCase)))
            throw new IntegrationException($"The link must start with {Scheme}://open/?");

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string pair in url[(queryStart + 1)..].Split('&'))
        {
            int separator = pair.IndexOf('=');
            if (separator > 0)
                parameters[pair[..separator]] = Uri.UnescapeDataString(pair[(separator + 1)..]);
        }

        // Capture the actual decoded parameters before validation so a rejected
        // link still explains what it supplied. The same dictionary drives parsing.
        if (report is not null)
        {
            report.Parameters = parameters;
            report.InputProject = parameters.GetValueOrDefault("project_path") ?? parameters.GetValueOrDefault("projectDir") ?? parameters.GetValueOrDefault("project");
            report.InputFile = parameters.GetValueOrDefault("file") ?? parameters.GetValueOrDefault("url");
            report.Connection = parameters.GetValueOrDefault("connect_to") is { } mode
                ? (mode.Length == 0 ? "Invalid (empty)" : mode.ToUpperInvariant())
                : parameters.ContainsKey("project_path") || parameters.ContainsKey("project") ||
                    parameters.ContainsKey("ssh_host") || parameters.ContainsKey("sshHost")
                    ? "Not specified (remote link)" : "Windows";
        }

        int? line = null;
        if (parameters.TryGetValue("line", out string? value))
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < 1)
                throw new IntegrationException("The 'line' parameter must be a positive integer.");
            line = number;
            if (report is not null)
                report.Line = line;
        }

        string? file = parameters.GetValueOrDefault("file");
        if (!parameters.TryGetValue("connect_to", out string? connection))
        {
            if (!string.IsNullOrEmpty(parameters.GetValueOrDefault("project")) ||
                parameters.ContainsKey("sshHost") || parameters.ContainsKey("ssh_host") ||
                parameters.ContainsKey("project_path"))
                throw new IntegrationException(
                    "Update the remote link to use 'connect_to=WSL' with 'dist' or 'connect_to=SSH' with 'ssh_host'. " +
                    "Set 'project_path' to the full project directory.");
            return ResolveLocal(parameters, file, line);
        }

        bool isWsl = connection.Equals("WSL", StringComparison.OrdinalIgnoreCase);
        if (!isWsl && !connection.Equals("SSH", StringComparison.OrdinalIgnoreCase))
            throw new IntegrationException("The 'connect_to' parameter must be WSL or SSH.");

        string? directory = parameters.GetValueOrDefault("project_path");
        if (directory is null || !IsAbsoluteLinuxDirectory(directory))
            throw new IntegrationException("The 'project_path' parameter must be an absolute Linux directory.");
        directory = directory == "/" ? directory : directory.TrimEnd('/');

        return isWsl
            ? ResolveWsl(parameters.GetValueOrDefault("dist"), directory, file, line)
            : ResolveSsh(parameters.GetValueOrDefault("ssh_host"), directory, file, line);
    }

    private static EditorLink ResolveSsh(string? host, string directory, string? file, int? line)
    {
        if (string.IsNullOrEmpty(host) || !char.IsAsciiLetterOrDigit(host[0]) ||
            !host.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_'))
            throw new IntegrationException("The 'ssh_host' parameter must be an SSH host alias, such as symfony-dev.");

        string prefix = directory.TrimEnd('/') + '/';
        if (file is null || !file.StartsWith(prefix, StringComparison.Ordinal) ||
            !file[prefix.Length..].Split('/').All(IsLinuxPathSegment) || file.Contains(':'))
            throw new IntegrationException("The 'file' parameter must point to a file inside the SSH project directory and cannot contain a colon.");

        return new(directory, file, line, SshHost: host);
    }

    private static EditorLink ResolveWsl(string? distribution, string directory, string? file, int? line)
    {
        if (distribution is null || !IsDistribution(distribution))
            throw new IntegrationException("The 'dist' parameter must be a valid WSL distribution name, such as Ubuntu.");

        const string containerRoot = "/var/www/html/";
        if (file is null || !file.StartsWith(containerRoot, StringComparison.Ordinal) ||
            !file[containerRoot.Length..].Split('/').All(IsLinuxPathSegment))
            throw new IntegrationException("The 'file' parameter must point to a file inside /var/www/html.");

        return new(directory, $"{directory.TrimEnd('/')}/{file[containerRoot.Length..]}", line, distribution);
    }

    private static bool IsAbsoluteLinuxDirectory(string root) =>
        root == "/" || (root.StartsWith('/') && root.TrimEnd('/') is { Length: > 1 } trimmed &&
            trimmed[1..].Split('/').All(IsLinuxPathSegment));

    private static bool IsDistribution(string value) =>
        IsLinuxPathSegment(value) && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !value.EndsWith(' ') && !value.EndsWith('.');

    private static bool IsLinuxPathSegment(string value) =>
        value.Length > 0 && value is not "." and not ".." &&
        !value.Contains('/') && !value.Contains('\\') && !value.Any(char.IsControl);

    private static EditorLink ResolveLocal(Dictionary<string, string> parameters, string? file, int? line)
    {
        bool findProject = false;
        string? directory = parameters.GetValueOrDefault("projectDir");
        if (file is null && parameters.TryGetValue("url", out string? fileUri))
        {
            const string prefix = "file://";
            if (!fileUri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new IntegrationException("The 'url' parameter must contain a file:// URL on a Windows drive.");
            file = fileUri[prefix.Length..];
            if (file.StartsWith('/'))
                file = file[1..];
            findProject = directory is null;
        }

        string filePath = FullWindowsPath(file, "file");
        if (!File.Exists(filePath))
            throw new IntegrationException($"The file \"{filePath}\" does not exist.");

        string projectDirectory = findProject
            ? FindIdeaDirectory(filePath)
            : FullWindowsPath(directory, "projectDir");
        if (!Directory.Exists(projectDirectory))
            throw new IntegrationException($"The project directory \"{projectDirectory}\" does not exist.");

        if (!IsInside(filePath, projectDirectory) ||
            !IsInside(FinalWindowsPath(filePath), FinalWindowsPath(projectDirectory)))
            throw new IntegrationException("The 'file' parameter must point to a file inside the project directory.");

        return new(projectDirectory, filePath, line);
    }

    private static string FullWindowsPath(string? value, string parameter)
    {
        if (value is not { Length: >= 3 } || !char.IsAsciiLetter(value[0]) || value[1] != ':' ||
            value[2] is not ('/' or '\\') || value.AsSpan(2).Contains(':') ||
            value.Any(char.IsControl))
            throw new IntegrationException($"The '{parameter}' parameter must be an absolute path on a Windows drive.");
        return Path.GetFullPath(value.Replace('/', '\\'));
    }

    private static string FindIdeaDirectory(string filePath)
    {
        for (DirectoryInfo? parent = Directory.GetParent(filePath); parent is not null; parent = parent.Parent)
        {
            if (Directory.Exists(Path.Combine(parent.FullName, ".idea")))
                return parent.FullName;
        }
        throw new IntegrationException($"No parent directory of \"{filePath}\" contains an .idea folder. Add 'projectDir' to the link.");
    }

    private static bool IsInside(string file, string directory) =>
        file.StartsWith(directory.TrimEnd('\\') + '\\', StringComparison.OrdinalIgnoreCase);

    private static string FinalWindowsPath(string path)
    {
        // Open the target, including a directory, without requesting read access.
        const uint shareReadWriteDelete = 7;
        const uint openExisting = 3;
        const uint backupSemantics = 0x02000000;
        using var handle = CreateFile(path, 0, shareReadWriteDelete, IntPtr.Zero, openExisting, backupSemantics, IntPtr.Zero);
        if (handle.IsInvalid)
            throw new IOException($"Could not check the path \"{path}\" (Windows error {Marshal.GetLastWin32Error()}).");
        var buffer = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
        if (length == 0 || length >= buffer.Capacity)
            throw new IOException($"Could not resolve the path \"{path}\".");
        return buffer.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security,
        uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
}
