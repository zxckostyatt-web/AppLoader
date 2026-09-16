namespace DownloadHub;

/// <summary>Маленький диалог ввода строки (в WinForms своего нет).</summary>
public static class Prompt
{
    public static string? Show(IWin32Window owner, string message, string title, string initial = "")
    {
        using var form = new Form
        {
            Text = title,
            ClientSize = new Size(460, 130),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false
        };

        var label = new Label { Text = message, Left = 12, Top = 15, Width = 430, AutoSize = false };
        var input = new TextBox { Left = 12, Top = 42, Width = 430, Text = initial };
        var ok = new Button { Text = "ОК", DialogResult = DialogResult.OK, Left = 262, Top = 80, Width = 85 };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Left = 357, Top = 80, Width = 85 };

        form.Controls.AddRange(new Control[] { label, input, ok, cancel });
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog(owner) == DialogResult.OK ? input.Text : null;
    }
}
