using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Tebegrammmm.ChatsFoldersRedactsWindows
{
    /// <summary>
    /// Логика взаимодействия для RedactcionChatsFoldersWindow.xaml
    /// </summary>
    public partial class RedactcionChatsFoldersWindow : Window
    {
        ObservableCollection<ChatFolder> ChatFolders { get; set; }
        public RedactcionChatsFoldersWindow(ObservableCollection<ChatFolder> chatsFolders)
        {
            InitializeComponent();

            ChatFolders = chatsFolders;
            LBChatsFolders.ItemsSource = ChatFolders;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ChatFolder newFolder = new ChatFolder();

            AddFolderWindow addFolderWindow = new AddFolderWindow(ChatFolders, newFolder);
            if(addFolderWindow.ShowDialog() == true)
            {
                ChatFolders.Add(newFolder);
            }
        }

        private void LBChatsFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LBChatsFolders.SelectedItem == null)
            {
                LBChatsFolders.SelectedIndex = -1;
                return;
            }
            else if((LBChatsFolders.SelectedItem as ChatFolder).IsCanRedact)
            {
                RedactFolderContatcsWindow RFCW = new RedactFolderContatcsWindow(ChatFolders, (LBChatsFolders.SelectedItem as ChatFolder));
                RFCW.ShowDialog();
                LBChatsFolders.ItemsSource = ChatFolders;
            }
            LBChatsFolders.SelectedIndex = -1;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private T FindButton<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T result)
                    return result;
                var childResult = FindButton<T>(child);
                if (childResult != null)
                    return childResult;
            }
            return null;
        }
    }
}
