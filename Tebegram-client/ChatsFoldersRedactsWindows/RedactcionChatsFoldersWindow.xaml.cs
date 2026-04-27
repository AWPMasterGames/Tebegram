using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Tebegrammmm.ChatsFoldersRedactsWindows
{
    public partial class RedactcionChatsFoldersWindow : Window
    {
        ObservableCollection<ChatFolder> ChatFolders { get; set; }

        public RedactcionChatsFoldersWindow(ObservableCollection<ChatFolder> chatsFolders)
        {
            InitializeComponent();
            ChatFolders = chatsFolders;
            LBChatsFolders.ItemsSource = ChatFolders;
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
                this.DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ChatFolder newFolder = new ChatFolder();
            AddFolderWindow addFolderWindow = new AddFolderWindow(ChatFolders, newFolder);
            if (addFolderWindow.ShowDialog() == true)
                ChatFolders.Add(newFolder);
        }

        private void LBChatsFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LBChatsFolders.SelectedItem == null)
            {
                LBChatsFolders.SelectedIndex = -1;
                return;
            }
            else if ((LBChatsFolders.SelectedItem as ChatFolder).IsCanRedact)
            {
                RedactFolderContatcsWindow rfcw = new RedactFolderContatcsWindow(ChatFolders, (LBChatsFolders.SelectedItem as ChatFolder));
                rfcw.ShowDialog();
                LBChatsFolders.ItemsSource = ChatFolders;
            }
            LBChatsFolders.SelectedIndex = -1;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Delete_Folder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is ChatFolder folderItem)
            {
                if (LBChatsFolders.ItemsSource is ObservableCollection<ChatFolder> collection)
                {
                    if (folderItem.IsCanRedact)
                        collection.Remove(folderItem);
                }
            }
        }
    }
}
