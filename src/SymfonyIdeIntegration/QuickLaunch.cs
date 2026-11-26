using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SymfonyIdeIntegration;

internal static class QuickLaunch
{
    internal static ToolStripMenuItem[] CreateItems()
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Internet Explorer", "Quick Launch");

        FileSystemInfo[] entries;
        try
        {
            // Match the visible contents of the Quick Launch folder, excluding
            // desktop.ini and Windows' hidden User Pinned infrastructure.
            entries = new DirectoryInfo(directory).EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true
            }).ToArray();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Quick Launch is optional; its absence must not break the tray menu.
            return [];
        }

        return entries.Select(CreateItem)
            .OrderBy(item => item.Text, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static ToolStripMenuItem CreateItem(FileSystemInfo entry)
    {
        // Ask Explorer for the shortcut's own display name and icon, including
        // icons customized in shortcut properties, rather than guessing from its extension.
        IntPtr result = SHGetFileInfo(entry.FullName, 0, out ShellFileInfo info,
            (uint)Marshal.SizeOf<ShellFileInfo>(), ShgfiIcon | ShgfiSmallIcon | ShgfiDisplayName);
        string name = entry.Name;
        if (result != IntPtr.Zero && !string.IsNullOrEmpty(info.DisplayName))
            name = info.DisplayName;
        else if (entry.Extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
            || entry.Extension.Equals(".url", StringComparison.OrdinalIgnoreCase))
            name = Path.GetFileNameWithoutExtension(entry.Name);

        Bitmap image;
        if (info.Icon != IntPtr.Zero)
        {
            try
            {
                using Icon icon = Icon.FromHandle(info.Icon);
                image = icon.ToBitmap();
            }
            finally
            {
                // SHGetFileInfo transfers ownership of the native HICON to us.
                DestroyIcon(info.Icon);
            }
        }
        else
        {
            image = SystemIcons.Application.ToBitmap();
        }

        return new ToolStripMenuItem(name.Replace("&", "&&"), image, (_, _) => Open(entry.FullName));
    }

    private static void Open(string path)
    {
        try
        {
            // Let Windows open the original item, preserving a shortcut's target,
            // arguments, working directory and normal file associations.
            using Process? process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or IOException
            or UnauthorizedAccessException)
        {
            MessageBox.Show($"Could not open Quick Launch item.\n\n{path}\n\n{error.Message}",
                "Symfony IDE Integration", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private const uint ShgfiIcon = 0x00000100;
    private const uint ShgfiDisplayName = 0x00000200;
    private const uint ShgfiSmallIcon = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint fileAttributes, out ShellFileInfo fileInfo,
        uint fileInfoSize, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
