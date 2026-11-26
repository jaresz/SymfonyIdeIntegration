using System.Globalization;
using System.Text;

namespace SymfonyIdeIntegration;

// A snapshot belongs to a single protocol invocation. The tray keeps only the
// newest snapshot; none of this information is written to disk.
internal sealed class InvocationReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Revision { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Url { get; set; } = "";
    public Dictionary<string, string>? Parameters { get; set; }
    public string? Editor { get; set; }
    public string? Connection { get; set; }
    public string? InputProject { get; set; }
    public string? InputFile { get; set; }
    public string? ResolvedProject { get; set; }
    public string? ResolvedFile { get; set; }
    public int? Line { get; set; }
    public string? TargetProject { get; set; }
    public string? TargetFile { get; set; }
    public string? Executable { get; set; }
    public string[]? Arguments { get; set; }
    public string Stage { get; set; } = "Starting";
    public string Result { get; set; } = "Processing";
    public int? SshExitCode { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }

    internal void Publish()
    {
        Revision++;
        InvocationChannel.Publish(this);
    }

    internal bool IsNewerThan(InvocationReport other)
    {
        // A slow SSH request must not replace a newer click when it finishes.
        int order = StartedAt.CompareTo(other.StartedAt);
        if (order == 0)
            order = Id.CompareTo(other.Id);
        return order > 0 || (order == 0 && Revision > other.Revision);
    }

    internal string FormatDetails()
    {
        var details = new StringBuilder();
        void Field(string label, string? value) => details.AppendLine($"{label}: {value ?? "Not available"}");
        void Section(string title) => details.AppendLine().AppendLine(title).AppendLine(new string('─', title.Length));

        Field("Started", StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        if (FinishedAt is { } finished)
            Field("Finished", finished.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture));
        Field("Result", Result);
        Field("Stage", Stage);
        Field("Editor", Editor);
        Field("Connection", Connection);
        if (SshExitCode is { } exitCode)
            Field("SSH exit code", exitCode.ToString(CultureInfo.InvariantCulture));
        if (ErrorType is not null || ErrorMessage is not null)
        {
            Section("Error");
            Field("Type", ErrorType);
            Field("Message", ErrorMessage);
        }

        Section("Original link");
        details.AppendLine(Url);
        Section("Decoded parameters");
        if (Parameters is null)
            details.AppendLine("Not available");
        else if (Parameters.Count == 0)
            details.AppendLine("None");
        else
            foreach (var (key, value) in Parameters)
                Field(key, value);

        Section("Path resolution");
        Field("Input project", InputProject);
        Field("Input file", InputFile);
        Field("Resolved project", ResolvedProject);
        Field("Resolved file", ResolvedFile);
        Field("Line", Line?.ToString(CultureInfo.InvariantCulture));
        Field("Project passed to editor", TargetProject);
        Field("File passed to editor", TargetFile);

        Section("Command");
        Field("Executable", Executable);
        details.AppendLine("Arguments (in order):");
        if (Arguments is null)
            details.AppendLine("Not available");
        else
            for (int i = 0; i < Arguments.Length; i++)
                details.AppendLine($"  [{i + 1}] {Arguments[i]}");

        // Show the exact data as text, including multi-line SSH arguments. NUL
        // characters must be escaped because WinForms text controls terminate on them.
        return details.ToString().Replace("\0", "\\0").ReplaceLineEndings(Environment.NewLine);
    }
}
