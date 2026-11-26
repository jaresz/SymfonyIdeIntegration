namespace SymfonyIdeIntegration;

internal sealed class LastInvocationDialog : Form
{
    private readonly InvocationChannel channel;
    private readonly Font detailsFont = new("Consolas", 10f);
    private readonly TextBox details = new()
    {
        Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Both, WordWrap = false, MaxLength = 0
    };
    private readonly Button copy = new() { Text = "Copy details", AutoSize = true, Enabled = false };
    private readonly System.Windows.Forms.Timer refresh = new() { Interval = 200 };
    private InvocationReport? displayed;

    internal LastInvocationDialog(InvocationChannel channel)
    {
        this.channel = channel;
        details.Font = detailsFont;
        Text = "Last invocation · Symfony IDE Integration";
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 600);
        MinimumSize = new Size(480, 320);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        var close = new Button { Text = "Close", AutoSize = true };
        close.Click += (_, _) => Close();
        CancelButton = close;
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(details.Text); }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                MessageBox.Show(this, "Could not copy the details. Try again.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        buttons.Controls.AddRange([close, copy]);
        layout.Controls.Add(details, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);

        // Poll only while the window is open. The WinForms timer runs on the UI
        // thread and avoids updating a form from the background pipe listener.
        details.Text = "No invocation has been received yet in this session.";
        refresh.Tick += (_, _) => RefreshDetails();
        RefreshDetails();
        refresh.Start();
    }

    private void RefreshDetails()
    {
        InvocationReport? report = channel.Latest;
        if (ReferenceEquals(report, displayed))
            return;
        displayed = report;
        details.Text = report?.FormatDetails() ?? "No invocation has been received yet in this session.";
        copy.Enabled = report is not null;
        details.SelectionStart = 0;
        details.SelectionLength = 0;
        details.ScrollToCaret();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            refresh.Dispose();
        base.Dispose(disposing);
        if (disposing)
            detailsFont.Dispose();
    }
}
