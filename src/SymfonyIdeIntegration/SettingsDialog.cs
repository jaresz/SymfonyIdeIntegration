namespace SymfonyIdeIntegration;

internal sealed class SettingsDialog : Form
{
    internal SettingsDialog(UserSettings settings, bool settingsDamaged, Func<UserSettings, bool> save)
    {
        Text = "Settings · Symfony IDE Integration";
        AutoScaleMode = AutoScaleMode.Font;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            Padding = new Padding(12)
        };
        Controls.Add(layout);

        TextBox code = AddEditorField(layout, 0, "Visual Studio Code", settings.VsCodePath, Editor.VsCode);
        TextBox phpStorm = AddEditorField(layout, 1, "PhpStorm", settings.PhpStormPath, Editor.PhpStorm);

        if (settingsDamaged)
        {
            var notice = new Label
            {
                Text = "The settings file is damaged. Defaults are in use. Saving will keep a copy of the damaged file.",
                AutoSize = true,
                MaximumSize = new Size(490, 0)
            };
            layout.Controls.Add(notice, 0, 2);
            layout.SetColumnSpan(notice, 3);
        }

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var ok = new Button { Text = "Save", AutoSize = true };
        buttons.Controls.AddRange([cancel, ok]);
        layout.Controls.Add(buttons, 0, settingsDamaged ? 3 : 2);
        layout.SetColumnSpan(buttons, 3);
        AcceptButton = ok;
        CancelButton = cancel;

        ok.Click += (_, _) =>
        {
            if (!ValidateEditorPath(code, Editor.VsCode) || !ValidateEditorPath(phpStorm, Editor.PhpStorm))
                return;
            if (save(settings with
                {
                    VsCodePath = code.Text.Trim(), PhpStormPath = phpStorm.Text.Trim()
                }))
                DialogResult = DialogResult.OK;
        };
    }

    private static TextBox AddField(TableLayoutPanel layout, int row, string label, string value)
    {
        var textBox = new TextBox { Text = value, Width = 370, Anchor = AnchorStyles.Left | AnchorStyles.Right };
        var caption = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left };
        layout.Controls.Add(caption, 0, row);
        layout.Controls.Add(textBox, 1, row);
        return textBox;
    }

    private TextBox AddEditorField(TableLayoutPanel layout, int row, string label, string value, Editor editor)
    {
        TextBox textBox = AddField(layout, row, label, value);
        textBox.PlaceholderText = EditorDiscovery.Find(editor) ?? "Detect automatically or choose a file";
        var browse = new Button { Text = "Browse…", AutoSize = true };
        layout.Controls.Add(browse, 2, row);
        browse.Click += (_, _) =>
        {
            string name = EditorDiscovery.ExecutableName(editor);
            using var picker = new OpenFileDialog { Filter = $"{name}|{name}", FileName = name };
            if (picker.ShowDialog(this) == DialogResult.OK)
                textBox.Text = picker.FileName;
        };
        return textBox;
    }

    private bool ValidateEditorPath(TextBox input, Editor editor)
    {
        string path = input.Text.Trim();
        string name = EditorDiscovery.ExecutableName(editor);
        if (path.Length == 0 || (Path.IsPathFullyQualified(path) && File.Exists(path) &&
            Path.GetFileName(path).Equals(name, StringComparison.OrdinalIgnoreCase)))
            return true;
        ShowError(input, $"Choose an existing {name} file or leave this field blank for automatic detection.");
        return false;
    }

    private void ShowError(TextBox field, string message)
    {
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        field.Focus();
    }
}
