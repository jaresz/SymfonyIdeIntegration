namespace SymfonyIdeIntegration;

internal static class EditorDiscovery
{
    internal static string ExecutableName(Editor editor) => editor == Editor.VsCode ? "Code.exe" : "phpstorm64.exe";

    internal static string? ResolveExecutable(UserSettings settings)
    {
        string? path = settings.Editor == Editor.VsCode ? settings.VsCodePath : settings.PhpStormPath;
        if (string.IsNullOrWhiteSpace(path))
            path = Find(settings.Editor);

        return path is not null && Path.IsPathFullyQualified(path) &&
            Path.GetFileName(path).Equals(ExecutableName(settings.Editor), StringComparison.OrdinalIgnoreCase) &&
            File.Exists(path) ? path : null;
    }

    internal static string? Find(Editor editor)
    {
        string executable = ExecutableName(editor);
        foreach (string entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            string directory = entry.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(directory))
                continue;

            string candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
                return candidate;

            if (editor == Editor.VsCode && File.Exists(Path.Combine(directory, "code.cmd")))
            {
                string sibling = Path.GetFullPath(Path.Combine(directory, "..", executable));
                if (File.Exists(sibling))
                    return sibling;
            }
        }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programsX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (editor == Editor.VsCode)
        {
            return new[] { Path.Combine(local, "Programs"), programs, programsX86 }
                .Select(directory => Path.Combine(directory, "Microsoft VS Code", executable))
                .FirstOrDefault(File.Exists);
        }

        var roots = new[] { Path.Combine(local, "Programs"), Path.Combine(programs, "JetBrains"), Path.Combine(programsX86, "JetBrains") };
        return roots.Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateDirectories(root, "PhpStorm*", new EnumerationOptions { IgnoreInaccessible = true }))
            .OrderByDescending(PhpStormVersion)
            .Select(directory => Path.Combine(directory, "bin", executable))
            .FirstOrDefault(File.Exists);
    }

    private static Version PhpStormVersion(string directory)
    {
        string suffix = Path.GetFileName(directory)["PhpStorm".Length..].Trim();
        return Version.TryParse(suffix, out Version? version) ? version : new Version();
    }
}
