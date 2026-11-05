using System.Windows;
using System.Windows.Input;

namespace TimTicker.App.Views;

public partial class DeleteTaskFolderConfirmationWindow : Window
{
    public DeleteTaskFolderConfirmationWindow(string folderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        InitializeComponent();
        FolderNameText.Text = $"“{folderName}”";
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }
}
