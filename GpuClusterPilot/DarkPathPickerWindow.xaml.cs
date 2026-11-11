using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RafaoCluster;

public partial class DarkPathPickerWindow : Window
{
    private readonly bool _selectFolder;
    private readonly string? _extension;
    private string _currentDirectory;
    public string? SelectedPath { get; private set; }

    public DarkPathPickerWindow(string title, string initialPath, bool selectFolder, string? extension = null)
    {
        InitializeComponent();
        _selectFolder = selectFolder;
        _extension = extension;
        Title = title;
        PickerTitle.Text = title;
        SelectButton.Content = selectFolder ? "Selecionar esta pasta" : "Selecionar arquivo";

        var candidate = initialPath;
        if (File.Exists(candidate)) candidate = Path.GetDirectoryName(candidate) ?? @"C:\";
        if (!Directory.Exists(candidate)) candidate = Directory.GetLogicalDrives().FirstOrDefault() ?? @"C:\";
        _currentDirectory = candidate;
        LoadDirectory();
    }

    private void LoadDirectory()
    {
        try
        {
            CurrentPathInput.Text = _currentDirectory;
            var entries = new List<PathEntry>();
            if (string.IsNullOrWhiteSpace(_currentDirectory))
            {
                entries.AddRange(Directory.GetLogicalDrives().Select(path => new PathEntry(path, path, true)));
            }
            else
            {
                entries.AddRange(Directory.EnumerateDirectories(_currentDirectory)
                    .Select(path => new PathEntry($"📁  {Path.GetFileName(path)}", path, true)));
                if (!_selectFolder)
                    entries.AddRange(Directory.EnumerateFiles(_currentDirectory)
                        .Where(path => _extension is null || path.EndsWith(_extension, StringComparison.OrdinalIgnoreCase))
                        .Select(path => new PathEntry($"◻  {Path.GetFileName(path)}", path, false)));
            }
            EntriesList.ItemsSource = entries.OrderByDescending(x => x.IsDirectory).ThenBy(x => x.Label).ToList();
            SelectButton.IsEnabled = _selectFolder;
        }
        catch (Exception ex)
        {
            DarkDialogWindow.ShowMessage(this, "Não foi possível abrir", ex.Message);
        }
    }

    private void Navigate(string path)
    {
        if (!Directory.Exists(path)) return;
        _currentDirectory = path;
        LoadDirectory();
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentDirectory)) return;
        var parent = Directory.GetParent(_currentDirectory);
        if (parent is null)
        {
            _currentDirectory = "";
            LoadDirectory();
        }
        else Navigate(parent.FullName);
    }

    private void Go_Click(object sender, RoutedEventArgs e) => Navigate(CurrentPathInput.Text.Trim());
    private void CurrentPath_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Navigate(CurrentPathInput.Text.Trim()); }

    private void EntriesList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntriesList.SelectedItem is not PathEntry entry) return;
        if (entry.IsDirectory) Navigate(entry.Path);
        else Choose(entry.Path);
    }

    private void EntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectFolder) return;
        SelectButton.IsEnabled = EntriesList.SelectedItem is PathEntry { IsDirectory: false };
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (_selectFolder) Choose(_currentDirectory);
        else if (EntriesList.SelectedItem is PathEntry { IsDirectory: false } file) Choose(file.Path);
    }

    private void Choose(string path)
    {
        SelectedPath = path;
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed record PathEntry(string Label, string Path, bool IsDirectory)
    {
        public override string ToString() => Label;
    }
}
