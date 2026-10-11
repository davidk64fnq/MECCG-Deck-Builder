using System;
using System.Drawing;
using System.Windows.Forms;

namespace MECCG_Deck_Builder
{
    public sealed class ManageCustomTagsForm : Form
    {
        private readonly CardFilterService _filterService;
        private readonly ListBox _listBoxKeys;
        private readonly ListBox _listBoxValues;
        private readonly Button _btnAddKey;
        private readonly Button _btnRenameKey;
        private readonly Button _btnDeleteKey;
        private readonly Button _btnAddValue;
        private readonly Button _btnRenameValue;
        private readonly Button _btnDeleteValue;
        private readonly Button _btnClose;

        public ManageCustomTagsForm(CardFilterService filterService)
        {
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));

            Text = "Manage Custom Tags";
            ClientSize = new Size(540, 400);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Font = new Font("Segoe UI", 9.0f);

            var grpKeys = new GroupBox
            {
                Text = "Categories",
                Location = new Point(12, 12),
                Size = new Size(245, 330)
            };

            _listBoxKeys = new ListBox
            {
                Location = new Point(10, 22),
                Size = new Size(225, 230),
                IntegralHeight = false
            };
            _listBoxKeys.SelectedIndexChanged += ListBoxKeys_SelectedIndexChanged;

            _btnAddKey = new Button
            {
                Text = "Add...",
                Location = new Point(10, 260),
                Size = new Size(70, 26)
            };
            _btnAddKey.Click += BtnAddKey_Click;

            _btnRenameKey = new Button
            {
                Text = "Rename...",
                Location = new Point(86, 260),
                Size = new Size(74, 26),
                Enabled = false
            };
            _btnRenameKey.Click += BtnRenameKey_Click;

            _btnDeleteKey = new Button
            {
                Text = "Delete",
                Location = new Point(166, 260),
                Size = new Size(69, 26),
                Enabled = false
            };
            _btnDeleteKey.Click += BtnDeleteKey_Click;

            grpKeys.Controls.AddRange([_listBoxKeys, _btnAddKey, _btnRenameKey, _btnDeleteKey]);

            var grpValues = new GroupBox
            {
                Text = "Tags",
                Location = new Point(275, 12),
                Size = new Size(245, 330)
            };

            _listBoxValues = new ListBox
            {
                Location = new Point(10, 22),
                Size = new Size(225, 230),
                IntegralHeight = false
            };
            _listBoxValues.SelectedIndexChanged += ListBoxValues_SelectedIndexChanged;

            _btnAddValue = new Button
            {
                Text = "Add...",
                Location = new Point(10, 260),
                Size = new Size(78, 26),
                Enabled = false
            };
            _btnAddValue.Click += BtnAddValue_Click;

            _btnRenameValue = new Button
            {
                Text = "Rename...",
                Location = new Point(94, 260),
                Size = new Size(74, 26),
                Enabled = false
            };
            _btnRenameValue.Click += BtnRenameValue_Click;

            _btnDeleteValue = new Button
            {
                Text = "Delete",
                Location = new Point(174, 260),
                Size = new Size(61, 26),
                Enabled = false
            };
            _btnDeleteValue.Click += BtnDeleteValue_Click;

            grpValues.Controls.AddRange([_listBoxValues, _btnAddValue, _btnRenameValue, _btnDeleteValue]);

            var lblNote = new Label
            {
                Text = "Note: Deleting a category or tag removes it from all cards across the catalog.",
                Location = new Point(12, 355),
                Size = new Size(410, 30),
                ForeColor = Color.DimGray
            };

            _btnClose = new Button
            {
                Text = "Close",
                Location = new Point(440, 352),
                Size = new Size(80, 28),
                DialogResult = DialogResult.OK
            };

            Controls.AddRange([grpKeys, grpValues, lblNote, _btnClose]);
            AcceptButton = _btnClose;
            CancelButton = _btnClose;

            PopulateKeys();
        }

        private void ListBoxKeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateKeyButtons();
            PopulateValues();
        }

        private void ListBoxValues_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateValueButtons();
        }

        private void UpdateKeyButtons()
        {
            bool hasSelection = _listBoxKeys.SelectedIndex >= 0;
            _btnRenameKey.Enabled = hasSelection;
            _btnDeleteKey.Enabled = hasSelection;
            _btnAddValue.Enabled = hasSelection;
        }

        private void UpdateValueButtons()
        {
            bool hasSelection = _listBoxValues.SelectedIndex >= 0;
            _btnRenameValue.Enabled = hasSelection;
            _btnDeleteValue.Enabled = hasSelection;
        }

        private void PopulateKeys(string selectKey = null)
        {
            _listBoxKeys.BeginUpdate();
            _listBoxKeys.Items.Clear();

            var keys = _filterService.GetCustomKeyNames();
            foreach (var k in keys)
            {
                if (!string.IsNullOrWhiteSpace(k))
                {
                    _listBoxKeys.Items.Add(k);
                }
            }

            _listBoxKeys.EndUpdate();

            if (!string.IsNullOrEmpty(selectKey))
            {
                int idx = _listBoxKeys.FindStringExact(selectKey);
                if (idx >= 0) _listBoxKeys.SelectedIndex = idx;
            }
            else if (_listBoxKeys.Items.Count > 0)
            {
                _listBoxKeys.SelectedIndex = 0;
            }
            else
            {
                UpdateKeyButtons();
                PopulateValues();
            }
        }

        private void PopulateValues(string selectValue = null)
        {
            _listBoxValues.BeginUpdate();
            _listBoxValues.Items.Clear();

            if (_listBoxKeys.SelectedItem is string selectedKey)
            {
                var values = _filterService.GetCustomKeyValues(selectedKey);
                foreach (var v in values)
                {
                    if (!string.IsNullOrWhiteSpace(v))
                    {
                        _listBoxValues.Items.Add(v);
                    }
                }
            }

            _listBoxValues.EndUpdate();

            if (!string.IsNullOrEmpty(selectValue))
            {
                int idx = _listBoxValues.FindStringExact(selectValue);
                if (idx >= 0) _listBoxValues.SelectedIndex = idx;
            }
            else if (_listBoxValues.Items.Count > 0)
            {
                _listBoxValues.SelectedIndex = 0;
            }

            UpdateValueButtons();
        }

        private void BtnAddKey_Click(object sender, EventArgs e)
        {
            string newKey = PromptDialog.Show(this, "Add New Category", "Enter category name:");
            if (string.IsNullOrWhiteSpace(newKey)) return;

            newKey = newKey.Trim();
            _filterService.AddCustomKeyName(newKey);
            PopulateKeys(newKey);
        }

        private void BtnRenameKey_Click(object sender, EventArgs e)
        {
            if (_listBoxKeys.SelectedItem is not string currentKey) return;

            string renamedKey = PromptDialog.Show(this, "Rename Category", $"Enter new name for category '{currentKey}':", currentKey);
            if (string.IsNullOrWhiteSpace(renamedKey) || string.Equals(renamedKey, currentKey, StringComparison.OrdinalIgnoreCase)) return;

            renamedKey = renamedKey.Trim();
            _filterService.RenameCustomKeyName(currentKey, renamedKey);
            PopulateKeys(renamedKey);
        }

        private void BtnDeleteKey_Click(object sender, EventArgs e)
        {
            if (_listBoxKeys.SelectedItem is not string currentKey) return;

            var res = MessageBox.Show(this,
                $"Are you sure you want to delete the category '{currentKey}' and all its tags?\n\nThis will remove this category from all cards across the catalog.",
                "Delete Category",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                _filterService.DeleteCustomKeyName(currentKey);
                PopulateKeys();
            }
        }

        private void BtnAddValue_Click(object sender, EventArgs e)
        {
            if (_listBoxKeys.SelectedItem is not string currentKey) return;

            string newVal = PromptDialog.Show(this, "Add New Tag", $"Enter new tag for category '{currentKey}':");
            if (string.IsNullOrWhiteSpace(newVal)) return;

            newVal = newVal.Trim();
            _filterService.AddCustomKeyValue(currentKey, newVal);
            PopulateValues(newVal);
        }

        private void BtnRenameValue_Click(object sender, EventArgs e)
        {
            if (_listBoxKeys.SelectedItem is not string currentKey ||
                _listBoxValues.SelectedItem is not string currentVal) return;

            string renamedVal = PromptDialog.Show(this, "Rename Tag", $"Enter new name for tag '{currentVal}':", currentVal);
            if (string.IsNullOrWhiteSpace(renamedVal) || string.Equals(renamedVal, currentVal, StringComparison.OrdinalIgnoreCase)) return;

            renamedVal = renamedVal.Trim();
            _filterService.RenameCustomKeyValue(currentKey, currentVal, renamedVal);
            PopulateValues(renamedVal);
        }

        private void BtnDeleteValue_Click(object sender, EventArgs e)
        {
            if (_listBoxKeys.SelectedItem is not string currentKey ||
                _listBoxValues.SelectedItem is not string currentVal) return;

            var res = MessageBox.Show(this,
                $"Are you sure you want to delete the tag '{currentVal}' from category '{currentKey}'?\n\nThis will remove this tag from all cards tagged with it.",
                "Delete Tag",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                _filterService.DeleteCustomKeyValue(currentKey, currentVal);
                PopulateValues();
            }
        }
    }
}