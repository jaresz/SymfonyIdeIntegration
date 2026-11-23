using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace SymfonyIdeIntegration;

internal sealed record EditorLink(string ProjectDirectory, string FilePath, int? Line, string? Distribution = null)
{
    internal const string Scheme = "Q3a-Symfony2ide";

    internal static EditorLink Resolve(string url, UserSettings settings)
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

        int? line = null;
        if (parameters.TryGetValue("line", out string? value))
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < 1)
                throw new IntegrationException("The 'line' parameter must be a positive integer.");
            line = number;
        }

        string? project = parameters.GetValueOrDefault("project");
        string? file = parameters.GetValueOrDefault("file");
        return string.IsNullOrEmpty(project)
            ? ResolveLocal(parameters, file, line)
            : ResolveWsl(settings, project, file, line);
    }

    private static EditorLink ResolveWsl(UserSettings settings, string project, string? file, int? line)
    {
        if (!IsWslSegment(project))
            throw new IntegrationException("The 'project' parameter must be a single WSL directory name.");

        string root = settings.WslProjectsRoot.TrimEnd('/');
        if (!IsWslRoot(settings.WslProjectsRoot))
            throw new IntegrationException("In Settings, enter an absolute path for the WSL projects directory, such as /home/username/projects.");
        if (!IsDistribution(settings.WslDistribution))
            throw new IntegrationException("In Settings, enter a valid WSL distribution name, such as Ubuntu.");

        const string containerRoot = "/var/www/html/";
        if (file is null || !file.StartsWith(containerRoot, StringComparison.Ordinal) ||
            !file[containerRoot.Length..].Split('/').All(IsWslSegment))
            throw new IntegrationException("The 'file' parameter must point to a file inside /var/www/html.");

        string directory = $"{root}/{project}";
        return new(directory, $"{directory}/{file[containerRoot.Length..]}", line, settings.WslDistribution);
    }

    internal static bool IsWslRoot(string root) =>
        root == "/" || (root.StartsWith('/') && root.TrimEnd('/') is { Length: > 1 } trimmed &&
            trimmed[1..].Split('/').All(IsWslSegment));

    internal static bool IsDistribution(string value) =>
        IsWslSegment(value) && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !value.EndsWith(' ') && !value.EndsWith('.');

    private static bool IsWslSegment(string value) =>
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
