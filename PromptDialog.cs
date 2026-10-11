using System;
using System.Drawing;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed class PromptDialog : Form
    {
        private readonly TextBox _textBox;

        private PromptDialog(string title, string prompt, string initialValue)
        {
            Text = title;
            ClientSize = new Size(380, 130);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9.0f);

            var lblPrompt = new Label
            {
                Text = prompt,
                Location = new Point(12, 12),
                Size = new Size(356, 32)
            };

            _textBox = new TextBox
            {
                Text = initialValue ?? string.Empty,
                Location = new Point(12, 48),
                Size = new Size(356, 23)
            };

            var btnOk = new Button
            {
                Text = "OK",
                Location = new Point(208, 88),
                Size = new Size(75, 26),
                DialogResult = DialogResult.OK
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(293, 88),
                Size = new Size(75, 26),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange([lblPrompt, _textBox, btnOk, btnCancel]);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Shown += (s, e) =>
            {
                _textBox.Focus();
                _textBox.SelectAll();
            };
        }

        public static string Show(IWin32Window owner, string title, string prompt, string initialValue = "")
        {
            using var dlg = new PromptDialog(title, prompt, initialValue);
            return dlg.ShowDialog(owner) == DialogResult.OK ? dlg._textBox.Text.Trim() : null;
        }
    }
}