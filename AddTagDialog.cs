using System;
using System.Drawing;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed class AddTagDialog : Form
    {
        private readonly CardFilterService _filterService;
        private readonly ComboBox _cmbKey;
        private readonly ComboBox _cmbValue;

        public string SelectedKey => _cmbKey.Text.Trim();
        public string SelectedValue => _cmbValue.Text.Trim();

        private AddTagDialog(string cardName, CardFilterService filterService, string initialCategory = null)
        {
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));

            Text = "Assign Custom Tag";
            ClientSize = new Size(380, 190);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9.0f);

            var lblHeader = new Label
            {
                Text = $"Assign tag to card: {cardName}",
                Location = new Point(12, 12),
                Size = new Size(356, 20),
                Font = new Font("Segoe UI", 9.0f, FontStyle.Bold)
            };

            var lblKey = new Label
            {
                Text = "Category:",
                Location = new Point(12, 40),
                Size = new Size(356, 16)
            };

            _cmbKey = new ComboBox
            {
                Location = new Point(12, 58),
                Size = new Size(356, 23),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            var lblValue = new Label
            {
                Text = "Tag:",
                Location = new Point(12, 90),
                Size = new Size(356, 16)
            };

            _cmbValue = new ComboBox
            {
                Location = new Point(12, 108),
                Size = new Size(356, 23),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems
            };

            var btnOk = new Button
            {
                Text = "Assign Tag",
                Location = new Point(190, 148),
                Size = new Size(90, 26)
            };
            btnOk.Click += BtnOk_Click;

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(288, 148),
                Size = new Size(80, 26),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange([lblHeader, lblKey, _cmbKey, lblValue, _cmbValue, btnOk, btnCancel]);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            // Wire events BEFORE populating keys so initial selection triggers tag population
            _cmbKey.SelectedIndexChanged += (s, e) => PopulateValuesForKey();
            _cmbKey.TextChanged += (s, e) => PopulateValuesForKey();

            PopulateKeys(initialCategory);

            Shown += (s, e) => _cmbKey.Focus();
        }

        private void PopulateKeys(string initialCategory = null)
        {
            _cmbKey.Items.Clear();
            var keys = _filterService.GetCustomKeyNames();
            foreach (var k in keys)
            {
                if (!string.IsNullOrWhiteSpace(k))
                {
                    _cmbKey.Items.Add(k);
                }
            }

            if (!string.IsNullOrWhiteSpace(initialCategory))
            {
                int idx = _cmbKey.FindStringExact(initialCategory);
                if (idx >= 0)
                {
                    _cmbKey.SelectedIndex = idx;
                }
                else
                {
                    _cmbKey.Text = initialCategory;
                }
            }
            else if (_cmbKey.Items.Count > 0)
            {
                _cmbKey.SelectedIndex = 0;
            }

            PopulateValuesForKey();
        }

        private void PopulateValuesForKey()
        {
            string key = _cmbKey.Text.Trim();
            string prevTag = _cmbValue.Text.Trim();

            _cmbValue.Items.Clear();
            _cmbValue.Text = string.Empty; // Explicitly clear old text

            if (string.IsNullOrWhiteSpace(key)) return;

            var values = _filterService.GetCustomKeyValues(key);
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    _cmbValue.Items.Add(v);
                }
            }

            // 1. If the previous tag also exists in the new category, preserve it
            if (!string.IsNullOrEmpty(prevTag))
            {
                int matchIdx = _cmbValue.FindStringExact(prevTag);
                if (matchIdx >= 0)
                {
                    _cmbValue.SelectedIndex = matchIdx;
                    return;
                }
            }

            // 2. Otherwise snap to the first tag in the new category
            if (_cmbValue.Items.Count > 0)
            {
                _cmbValue.SelectedIndex = 0;
            }
            else
            {
                _cmbValue.SelectedIndex = -1;
                _cmbValue.Text = string.Empty;
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedKey))
            {
                MessageBox.Show(this, "Please enter or select a category.", "Assign Tag", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _cmbKey.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedValue))
            {
                MessageBox.Show(this, "Please enter or select a tag.", "Assign Tag", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _cmbValue.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
        }

        public static (string Key, string Value)? Show(IWin32Window owner, string cardName, CardFilterService filterService, string initialCategory = null)
        {
            using var dlg = new AddTagDialog(cardName, filterService, initialCategory);
            if (dlg.ShowDialog(owner) == DialogResult.OK)
            {
                return (dlg.SelectedKey, dlg.SelectedValue);
            }
            return null;
        }
    }
}