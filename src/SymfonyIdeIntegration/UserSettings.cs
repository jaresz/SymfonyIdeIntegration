using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SymfonyIdeIntegration;

internal enum Editor
{
    VsCode,
    PhpStorm
}

internal sealed record UserSettings
{
    private static readonly Encoding SettingsEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter<Editor>(allowIntegerValues: false) }
    };

    internal static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SymfonyIdeIntegration");
    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");

    public Editor Editor { get; init; } = Editor.VsCode;
    public string VsCodePath { get; init; } = "";
    public string PhpStormPath { get; init; } = "";

    internal static UserSettings Load(out bool damaged)
    {
        damaged = false;
        try
        {
            using var stream = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            using var reader = new StreamReader(stream, SettingsEncoding, detectEncodingFromByteOrderMarks: false);
            return JsonSerializer.Deserialize<UserSettings>(reader.ReadToEnd(), JsonOptions)
                ?? throw new JsonException("Expected a settings object.");
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException)
        {
            damaged = true;
            return new();
        }
        catch (FileNotFoundException)
        {
            return new();
        }
        catch (DirectoryNotFoundException)
        {
            return new();
        }
    }

    internal void Save()
    {
        Directory.CreateDirectory(DataDirectory);
        var temporaryPath = Path.Combine(DataDirectory, $"{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(this, JsonOptions));
            // Recheck the file on disk in case it changed after opening the tray.
            Load(out bool damaged);
            if (damaged)
            {
                string backupPath = Path.Combine(DataDirectory, $"settings.corrupt-{Guid.NewGuid():N}.json");
                File.Replace(temporaryPath, SettingsPath, backupPath);
            }
            else
            {
                File.Move(temporaryPath, SettingsPath, overwrite: true);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
