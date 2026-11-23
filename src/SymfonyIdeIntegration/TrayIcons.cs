using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace SymfonyIdeIntegration;

internal static class TrayIcons
{
    internal static Icon Load(UserSettings settings)
    {
        try
        {
            if (EditorDiscovery.ResolveExecutable(settings) is { } executable &&
                Icon.ExtractAssociatedIcon(executable) is { } associatedIcon)
                return associatedIcon;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException
            or ExternalException)
        {
            // The editor may be unavailable. Keep the tray usable with the corresponding fallback.
        }

        return CreateFallback(settings.Editor);
    }

    private static Icon CreateFallback(Editor editor)
    {
        bool vsCode = editor == Editor.VsCode;
        Size size = SystemInformation.SmallIconSize;
        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Segoe UI", size.Height * 0.55f, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var backdrop = new SolidBrush(vsCode ? Color.FromArgb(0, 95, 158) : Color.FromArgb(94, 32, 113)))
        using (var alignment = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            graphics.FillRectangle(backdrop, 0, 0, size.Width, size.Height);
            graphics.DrawString(vsCode ? "VS" : "PS", font, Brushes.White,
                new RectangleF(0, 0, size.Width, size.Height), alignment);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
