using System.Windows;
using System.Windows.Input;
using TimTicker.App.ViewModels;

namespace TimTicker.App.Views;

public partial class TaskFolderMoveWindow : Window
{
    public TaskFolderMoveWindow(
        string folderName,
        IReadOnlyList<TaskFolderOption> folderOptions,
        Guid? currentParentFolderId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderName);
        ArgumentNullException.ThrowIfNull(folderOptions);
        InitializeComponent();
        DescriptionText.Text = $"选择“{folderName}”的新位置。";
        TargetFolderComboBox.ItemsSource = folderOptions;
        TargetFolderComboBox.SelectedItem = folderOptions.FirstOrDefault(option => option.FolderId == currentParentFolderId)
            ?? (folderOptions.Count > 0 ? folderOptions[0] : null);
    }

    public Guid? ParentFolderId { get; private set; }

    private void MoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (TargetFolderComboBox.SelectedItem is not TaskFolderOption selectedFolder)
        {
            ValidationMessage.Text = "请选择目标位置。";
            return;
        }

        ParentFolderId = selectedFolder.FolderId;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
        else if (e.Key == Key.Enter)
        {
            MoveButton_Click(sender, e);
        }
    }
}
