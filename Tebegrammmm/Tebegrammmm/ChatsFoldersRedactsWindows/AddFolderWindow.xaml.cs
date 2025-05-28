using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace Tebegrammmm.ChatsFoldersRedactsWindows
{
    /// <summary>
    /// Логика взаимодействия для AddFolderWindow.xaml
    /// </summary>
    public partial class AddFolderWindow : Window
    {
        private ObservableCollection<Contact> AllConatcs;
        private ObservableCollection<Contact> FolderContacts;

        private ChatFolder NewChatsFolder;
        public AddFolderWindow(ObservableCollection<ChatFolder> chatsFolder, ChatFolder newFolder)
        {
            InitializeComponent();
            FolderContacts = new ObservableCollection<Contact>();
            NewChatsFolder = newFolder;
            AllConatcs = chatsFolder[0].Contacts;
            LBMyContacts.ItemsSource = AllConatcs;

            LBFolderContacts.ItemsSource = FolderContacts;
        }

        private void LBFolderContacts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(LBFolderContacts.SelectedItem == null) return;
            FolderContacts.Remove(LBFolderContacts.SelectedItem as Contact);
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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(TBoxFolderName.Text.Length < 1)
            {
                return;
            }
            NewChatsFolder.ChangeFolderName(TBoxFolderName.Text);
            NewChatsFolder.Contacts = FolderContacts;
            this.DialogResult = true;
            this.Close();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
        private void Remove_Contact(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.CommandParameter is Contact contact)
            {
                if (FolderContacts != null && FolderContacts.Contains(contact))
                {
                    FolderContacts.Remove(contact);

                    if (AllConatcs != null && !AllConatcs.Contains(contact))
                    {
                        AllConatcs.Add(contact);
                    }
                }
            }
        }
    }
}
