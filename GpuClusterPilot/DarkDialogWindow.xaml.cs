using System.Windows;

namespace RafaoCluster;

public partial class DarkDialogWindow : Window
{
    private DarkDialogWindow(string title, string message, bool confirmation)
    {
        InitializeComponent();
        Title = title;
        DialogTitle.Text = title;
        DialogMessage.Text = message;
        NoButton.Visibility = confirmation ? Visibility.Visible : Visibility.Collapsed;
        YesButton.Content = confirmation ? "Continuar" : "OK";
    }

    public static void ShowMessage(Window owner, string title, string message)
    {
        new DarkDialogWindow(title, message, false) { Owner = owner }.ShowDialog();
    }

    public static bool Confirm(Window owner, string title, string message) =>
        new DarkDialogWindow(title, message, true) { Owner = owner }.ShowDialog() == true;

    private void Yes_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void No_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
