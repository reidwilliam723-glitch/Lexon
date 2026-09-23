using System.Diagnostics;
using System.Drawing;

namespace Lexon.Settings;

/// <summary>
/// Form for selecting running applications to block
/// </summary>
public partial class ProcessPickerForm : Form
{
    private ListBox _processListBox = null!;
    private TextBox _searchTextBox = null!;
    private Button _addButton = null!;
    private Button _cancelButton = null!;
    private string? _selectedProcessName;

    private readonly string _prompt;

    public string? SelectedProcessName => _selectedProcessName;

    public ProcessPickerForm(string? prompt = null)
    {
        _prompt = string.IsNullOrWhiteSpace(prompt)
            ? "Select a running application:"
            : prompt;
        InitializeComponent();
        LoadProcesses();
    }

    private void InitializeComponent()
    {
        this.Text = "Add Running Application";
        this.Size = new Size(400, 350);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;

        var titleLabel = new Label
        {
            Text = _prompt,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Location = new Point(20, 20),
            AutoSize = true
        };
        this.Controls.Add(titleLabel);

        _searchTextBox = new TextBox
        {
            Location = new Point(20, 50),
            Size = new Size(340, 25),
            PlaceholderText = "Search processes..."
        };
        _searchTextBox.TextChanged += OnSearchTextChanged;
        this.Controls.Add(_searchTextBox);

        _processListBox = new ListBox
        {
            Location = new Point(20, 85),
            Size = new Size(340, 180),
            Font = new Font("Segoe UI", 9),
            SelectionMode = SelectionMode.One
        };
        this.Controls.Add(_processListBox);

        _addButton = new Button
        {
            Text = "Add",
            Location = new Point(200, 280),
            Size = new Size(75, 30),
            Enabled = false
        };
        _addButton.Click += OnAddClicked;
        this.Controls.Add(_addButton);

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(285, 280),
            Size = new Size(75, 30)
        };
        _cancelButton.Click += (s, e) => this.Close();
        this.Controls.Add(_cancelButton);

        _processListBox.SelectedIndexChanged += (s, e) =>
        {
            _addButton.Enabled = _processListBox.SelectedIndex >= 0;
        };
    }

    private void LoadProcesses()
    {
        _processListBox.Items.Clear();
        
        var processes = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && !string.IsNullOrEmpty(p.ProcessName))
            .OrderBy(p => p.ProcessName)
            .ToList();

        foreach (var process in processes)
        {
            var displayName = $"{process.ProcessName} - {process.MainWindowTitle}";
            _processListBox.Items.Add(new ProcessItem(process.ProcessName, displayName));
            process.Dispose();
        }
    }

    private void OnSearchTextChanged(object? sender, EventArgs e)
    {
        var searchTerm = _searchTextBox.Text.ToLower();
        
        _processListBox.Items.Clear();
        
        var processes = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle) && !string.IsNullOrEmpty(p.ProcessName))
            .Where(p => p.ProcessName.ToLower().Contains(searchTerm) || 
                       p.MainWindowTitle.ToLower().Contains(searchTerm))
            .OrderBy(p => p.ProcessName)
            .ToList();

        foreach (var process in processes)
        {
            var displayName = $"{process.ProcessName} - {process.MainWindowTitle}";
            _processListBox.Items.Add(new ProcessItem(process.ProcessName, displayName));
            process.Dispose();
        }
    }

    private void OnAddClicked(object? sender, EventArgs e)
    {
        if (_processListBox.SelectedItem is ProcessItem item)
        {
            _selectedProcessName = item.ProcessName;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    private class ProcessItem
    {
        public string ProcessName { get; }
        public string DisplayName { get; }

        public ProcessItem(string processName, string displayName)
        {
            ProcessName = processName;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}
