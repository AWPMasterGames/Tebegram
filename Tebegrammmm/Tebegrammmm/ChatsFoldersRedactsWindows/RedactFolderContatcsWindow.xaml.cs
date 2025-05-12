using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Tebegrammmm.ChatsFoldersRedactsWindows
{
    /// <summary>
    /// Логика взаимодействия для RedactFolderContatcsWindow.xaml
    /// </summary>
    public partial class RedactFolderContatcsWindow : Window
    {
        private ObservableCollection<Contact> AllConatcs;
        private ObservableCollection<Contact> FolderContacts;

        private ChatFolder Folder;
        public RedactFolderContatcsWindow(ObservableCollection<ChatFolder> chatsFolder, ChatFolder folder)
        {
            InitializeComponent();

            InitializeComponent();
            Folder = folder;
            AllConatcs = chatsFolder[0].Contacts;
            FolderContacts = Folder.Contacts;

            LBMyContacts.ItemsSource = AllConatcs;
            LBFolderContacts.ItemsSource = FolderContacts;
        }

        private void LBFolderContacts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LBFolderContacts.SelectedItem == null)
            {
                LBFolderContacts.SelectedIndex = -1;
                return;
            }
            FolderContacts.Remove(LBFolderContacts.SelectedItem as Contact);
            LBFolderContacts.SelectedIndex = -1;
        }

        private void LBMyContacts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LBMyContacts.SelectedItem == null)
            {
                LBMyContacts.SelectedIndex = -1;
                return;
            }
            for (int i = 0; i < FolderContacts.Count; i++)
            {
                if (LBMyContacts.SelectedItem as Contact == FolderContacts[i])
                {
                    return;
                }
            }
            FolderContacts.Add(LBMyContacts.SelectedItem as Contact);
            LBMyContacts.SelectedIndex = -1;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
