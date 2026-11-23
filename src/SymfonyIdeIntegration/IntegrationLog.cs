using System.Globalization;
using System.Text;

namespace SymfonyIdeIntegration;

internal static class IntegrationLog
{
    private const int MaxBytes = 1024 * 1024;
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    internal static string FilePath => Path.Combine(UserSettings.DataDirectory, "integration.log");

    internal static void Update(string? message = null)
    {
        try
        {
            using var mutex = new Mutex(false, "Q3a-Symfony2ide.Log");
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(2)))
                    return;
            }
            catch (AbandonedMutexException)
            {
                // Ownership transfers to this process.
            }

            try
            {
                RetainEntries(FilePath, DateTimeOffset.UtcNow, message);
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void RetainEntries(string path, DateTimeOffset now, string? message)
    {
        bool exists = File.Exists(path);
        if (!exists && message is null)
            return;

        var entries = new Queue<(string Text, int Bytes)>();
        long bytes = 0;
        int newlineBytes = Utf8.GetByteCount(Environment.NewLine);
        DateTimeOffset cutoff = now.AddDays(-7);
        bool changed = message is not null || (exists && new FileInfo(path).Length > MaxBytes);

        void Keep(string entry)
        {
            int size = Utf8.GetByteCount(entry) + newlineBytes;
            entries.Enqueue((entry, size));
            bytes += size;
            while (bytes > MaxBytes)
            {
                bytes -= entries.Dequeue().Bytes;
                changed = true;
            }
        }

        if (exists)
        {
            foreach (string line in File.ReadLines(path, Utf8))
            {
                int separator = line.IndexOfAny([' ', '\t']);
                if (separator < 0 ||
                    !DateTimeOffset.TryParseExact(line.AsSpan(0, separator), "O", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var timestamp) || timestamp <= cutoff)
                {
                    changed = true;
                    continue;
                }

                if (line[separator] == ' ')
                {
                    // Older entries contain paths, exception messages and stack traces.
                    Keep(FormattableString.Invariant($"{timestamp:O}\tPrevious event"));
                    changed = true;
                }
                else
                {
                    Keep(line);
                }
            }
        }
        if (message is not null)
            Keep(FormattableString.Invariant($"{now:O}\t{message}"));
        if (!changed)
            return;
        if (entries.Count == 0)
        {
            File.Delete(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllLines(temporary, entries.Select(entry => entry.Text), Utf8);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
