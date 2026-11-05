using System.Windows;
using System.Windows.Input;

namespace TimTicker.App.Views;

public partial class TaskFolderEditorWindow : Window
{
    public TaskFolderEditorWindow(string? folderName = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(folderName))
        {
            Title = "重命名任务文件夹";
            HeadingText.Text = "重命名任务文件夹";
            DescriptionText.Text = "输入新的文件夹名称。";
            SaveButton.Content = "保存名称";
            NameTextBox.Text = folderName;
        }

        Loaded += (_, _) =>
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        };
    }

    public string? FolderName { get; private set; }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        string name = NameTextBox.Text.Trim();
        if (name.Length == 0)
        {
            ValidationMessage.Text = "请输入文件夹名称。";
            NameTextBox.Focus();
            return;
        }

        FolderName = name;
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
            SaveButton_Click(sender, e);
        }
    }
}
