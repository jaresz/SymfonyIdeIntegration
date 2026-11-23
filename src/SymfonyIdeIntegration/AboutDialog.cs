using System.Diagnostics;

namespace SymfonyIdeIntegration;

internal sealed class AboutDialog : Form
{
    internal AboutDialog(string version)
    {
        Text = "About Symfony IDE Integration";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowIcon = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(800, 552);
        BackColor = Color.FromArgb(15, 20, 36);
        ForeColor = Color.White;

        var scene = new BridgeScene { Dock = DockStyle.Fill };
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 132 };
        var title = new Label
        {
            Text = "Symfony IDE Integration", AutoSize = true, Location = new(23, 13),
            Font = new Font("Segoe UI", 18, FontStyle.Bold)
        };
        const string author = "Jakub Reiter";
        const string authorUrl = "https://jakubreiter.pl/";
        var credit = new LinkLabel
        {
            Text = $"Version {version}   ·   Author: {author}", AutoSize = true,
            ForeColor = Color.FromArgb(129, 225, 221), LinkColor = Color.FromArgb(129, 225, 221),
            ActiveLinkColor = Color.White, VisitedLinkColor = Color.FromArgb(129, 225, 221),
            Location = new(26, 61)
        };
        var authorStart = credit.Text.IndexOf(author, StringComparison.Ordinal);
        credit.Links.Add(authorStart, author.Length, authorUrl);
        credit.LinkClicked += OpenLink;
        const string licenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";
        const string licenseName = "GNU GPL version 3";
        var terms = new LinkLabel
        {
            Text = $"Licensed under the {licenseName}.", AutoSize = true,
            ForeColor = Color.FromArgb(185, 193, 211), LinkColor = Color.FromArgb(185, 193, 211),
            ActiveLinkColor = Color.White, VisitedLinkColor = Color.FromArgb(185, 193, 211),
            Location = new(26, 88)
        };
        var licenseStart = terms.Text.IndexOf(licenseName, StringComparison.Ordinal);
        terms.Links.Add(licenseStart, licenseName.Length, licenseUrl);
        terms.LinkClicked += OpenLink;
        const string repositoryUrl = "https://github.com/jaresz/SymfonyIdeIntegration";
        var source = new LinkLabel
        {
            Text = "Source code on GitHub", AutoSize = true,
            LinkColor = terms.LinkColor, ActiveLinkColor = terms.ActiveLinkColor,
            VisitedLinkColor = terms.VisitedLinkColor, Location = new(26, 109)
        };
        source.Links.Add(0, source.Text.Length, repositoryUrl);
        source.LinkClicked += OpenLink;
        var close = new Button
        {
            Text = "Close", DialogResult = DialogResult.Cancel, Size = new(84, 32),
            FlatStyle = FlatStyle.Flat, Location = new(690, 67)
        };
        close.FlatAppearance.BorderColor = Color.FromArgb(85, 112, 133);
        CancelButton = close;
        footer.Controls.AddRange([title, credit, terms, source, close]);
        Controls.Add(scene);
        Controls.Add(footer);
    }

    private static void OpenLink(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link?.LinkData is string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }
}
