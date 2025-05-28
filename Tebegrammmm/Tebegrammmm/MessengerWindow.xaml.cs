using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Http.Headers;
using Tebegrammmm.ChatsFoldersRedactsWindows;
using Tebegrammmm.Classes;
using System.Threading.Tasks;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для MessengerWindow.xaml
    /// </summary>
    public partial class MessengerWindow : Window
    {
        static HttpClient httpClient = new HttpClient();
        string serverAdress = "https://localhost:7034";
        private static object thisLock = new();
        User User { get; set; }
        Contact Contact { get; set; }

        TcpClient Client { get; set; }

        TcpListener tcpListener = null;
        Thread Thread { get; set; }
        bool IsRunning { get; set; }
        public MessengerWindow(User user)
        {
            InitializeComponent();
            GridMessege.Visibility = Visibility.Hidden;
            GridContactPanel.Visibility = Visibility.Hidden;
            this.User = user;

            LBChatsLoders.ItemsSource = User.ChatsFolders;
            LBChatsLoders.SelectedIndex = 0;

            StartListner();

            Thread = new Thread(new ThreadStart(ReceiveMessage));
            Thread.Start();


        }

        private void StartListner()
        {
            IPEndPoint endP = new IPEndPoint(IPAddress.Any, User.Port);
            tcpListener = new TcpListener(endP);
            tcpListener.Start();
            IsRunning = true;
        }

        private void LBChatsLoders_SelectionChangeFolder(object sender, SelectionChangedEventArgs e)
        {
            if (LBChatsLoders.SelectedItem == null)
            {
                return;
            }
            LBChats.ItemsSource = (LBChatsLoders.SelectedItem as ChatFolder).Contacts;
        }
        private void LBChats_SelectionChangedChat(object sender, SelectionChangedEventArgs e)
        {
            if (LBChats.SelectedItem == null)
            {
                return;
            }
            Contact = LBChats.SelectedItem as Contact;
            GridChat.DataContext = Contact;
            LBMessages.ItemsSource = Contact.Messages;
            GridMessege.Visibility = Visibility.Visible;
            GridContactPanel.Visibility = Visibility.Visible;
        }

        void ReceiveMessage()
        {
            try
            {
                while (IsRunning)
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    StreamReader sr = new StreamReader(client.GetStream(), Encoding.Unicode);
                    string s = sr.ReadToEnd();

                    string[] messageData = s.Split('▫');
                    foreach (Contact contact in User.ChatsFolders[0].Contacts)
                    {
                        if (messageData[0] == contact.IPAddress.ToString() & Convert.ToInt32(messageData[1]) == contact.Port)
                        {
                            if (messageData[2] == "Text")
                            {
                                string text = messageData[5];
                                for (int i = 6; i < messageData.Length; i++)
                                {
                                    text += messageData[i];
                                }
                                Message message = new Message(contact.Name, text, messageData[3]);
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    contact.Messages.Add(message);
                                }));
                                SaveMessageToFile(contact.Name, s, false);
                            }
                            if (messageData[2] == "File")
                            {
                                Message message = new Message(contact.Name, messageData[5], messageData[3], MessageType.File, messageData[4]);
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    contact.Messages.Add(message);
                                }));
                                SaveMessageToFile(contact.Name, s, false);
                            }
                        }
                    }

                    client.Close();
                }
            }
            catch (SocketException ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show($"Sockets error: {ex.Message}");
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }));
            }
        }
        private void SendMessageToUser(Message message)
        {
            try
            {
                IPEndPoint endP = new IPEndPoint(Contact.IPAddress, Contact.Port);
                Client = new TcpClient();
                Client.Connect(endP);
                NetworkStream nw = Client.GetStream();

                string mes = string.Empty;

                mes += $"{User.IpAddress.ToString()}▫";
                mes += $"{User.Port}▫";
                mes += $"{message.MessageType}▫";
                mes += $"{message.Time}▫";
                mes += $"{message.ServerAdress}▫";
                mes += $"{message.Text}";


                byte[] buffer = Encoding.Unicode.GetBytes(mes);
                nw.Write(buffer, 0, buffer.Length);
                Client.Close();
                SaveMessageToFile(User.Name, mes);
            }
            catch (SocketException ex)
            {
                MessageBox.Show($"Sockets error: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void SaveMessageToFile(string ContactName, string messageData, bool IsMe = true)
        {
            try
            {
                string[] MessegeData = messageData.Split('▫');
                string MessegeDataInFile = $"{MessegeData[0]}▫{MessegeData[1]}▫{ContactName}▫{MessegeData[2]}▫{MessegeData[3]}▫{MessegeData[4]}▫{MessegeData[5]}";

                string userId = User.Id.ToString();
                string dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

                if (!Directory.Exists(dataFolder))
                {
                    Directory.CreateDirectory(dataFolder);
                }

                string userFolder = Path.Combine(dataFolder, userId);
                if (!Directory.Exists(userFolder))
                {
                    Directory.CreateDirectory(userFolder);
                }
                string ContactFolder;
                if (IsMe)
                {
                    ContactFolder = Path.Combine(userFolder, Contact.Name);
                }
                else
                {
                    ContactFolder = Path.Combine(userFolder, ContactName);
                }

                if (!Directory.Exists(ContactFolder))
                {
                    Directory.CreateDirectory(ContactFolder);
                }
                DateTime dateTime = DateTime.Now;

                string fileName = $"{dateTime.ToString("dd.MM.yyyy")}.txt";
                string filePath = Path.Combine(ContactFolder, fileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine(MessegeDataInFile);
                lock (thisLock)
                {
                    File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении сообщения: {ex.Message}");
            }
        }
        private void SendMessage(string message, MessageType messageType = MessageType.Text, string ServerFilePath = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                TBMessage.Text = string.Empty;
                return;
            }

            Message Message = new Message(User.Name, message, DateTime.Now.ToString("hh:mm"), messageType, ServerFilePath);
            Contact.Messages.Add(Message);
            SendMessageToUser(Message);
            TBMessage.Text = string.Empty;
        }

        private void Button_Click_SendMessage(object sender, RoutedEventArgs e)
        {
            if (LBChats.SelectedItem == null)
            {
                return;
            }
            SendMessage(TBMessage.Text);
            TBMessage.Focus();
        }
        private void TBMessage_KeyDown_SendMessage(object sender, KeyEventArgs e)
        {
            if (LBChats.SelectedItem == null)
            {
                return;
            }
            if (e.Key == Key.Enter)
            {
                SendMessage(TBMessage.Text);
            }
        }

        private void Button_Click_AddContact(object sender, RoutedEventArgs e)
        {
            Contact newContact = new Contact();
            AddContact addContact = new AddContact(newContact, "Добавить контакт");

            if (addContact.ShowDialog() == true)
            {
                User.ChatsFolders[0].Contacts.Add(newContact);
            }
        }
        private void Button_Click_RemoveContact(object sender, RoutedEventArgs e)
        {
            GridContactPanel.Visibility = Visibility.Hidden;
            GridMessege.Visibility = Visibility.Hidden;
            Contact contact = (LBChats.SelectedItem as Contact);
            for (int i = 0; i < User.ChatsFolders.Count; i++)
            {
                for (int j = 0; j < User.ChatsFolders[i].Contacts.Count; j++)
                {
                    if (User.ChatsFolders[i].Contacts[j].Name == contact.Name)
                    {
                        User.ChatsFolders[i].Contacts.RemoveAt(j);
                    }
                }
            }
        }

        private void Button_Click_ContactRedact(object sender, RoutedEventArgs e)
        {
            if (LBChats.SelectedItem == null)
            {
                return;
            }
            AddContact addContact = new AddContact(Contact, "Редактировать контакт");

            if (addContact.ShowDialog() == true)
            {
                User.ChatsFolders[0].RemoveContact(Contact);
                User.ChatsFolders[0].AddContact(Contact);
                LBChats.SelectedIndex = LBChats.Items.Count - 1;
                MessageBox.Show("Контакт изменён");
                GridChat.DataContext = Contact;
                TBChat_Name.Text = Contact.Name;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }

        private void Button_Click_FoldersMenu(object sender, RoutedEventArgs e)
        {
            RedactcionChatsFoldersWindow RCFW = new RedactcionChatsFoldersWindow(User.ChatsFolders);
            RCFW.ShowDialog();
        }
        private async Task SendFileToServer(string filePath)
        {
            string mimeType = MIME.GetMimeType(Path.GetFileName(Path.GetExtension(filePath)));
            if (mimeType == "application/octet-stream")
            {
                MessageBox.Show("Неизвестный тип файла");
                return;
            }

            using var multipar = new MultipartFormDataContent();
            var fileStream = new StreamContent(File.OpenRead(filePath));
            fileStream.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            multipar.Add(fileStream, name: "file", fileName: Path.GetFileName(filePath));

            using var response = await httpClient.PostAsync($"{serverAdress}/upload", multipar);
            var ResponseText = await response.Content.ReadAsStringAsync();
            this.Dispatcher.Invoke(new Action(() => { SendMessage(Path.GetFileName(filePath), MessageType.File, $"{serverAdress}/upload/{Path.GetFileName(filePath)}"); }));
            MessageBox.Show(ResponseText);
        }
        private async void Button_Click_SelectFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog fileDialog = new OpenFileDialog();
            //fileDialog.ShowDialog();


            if (fileDialog.ShowDialog() != true)
            {
                return;
            }
            else if (string.IsNullOrEmpty(fileDialog.FileName))
            {
                return;
            }

            await SendFileToServer(fileDialog.FileName);
        }

        private async void LBMessages_SelectionChangeMessage(object sender, SelectionChangedEventArgs e)
        {
            if (LBMessages.SelectedItem == null)
            {
                LBMessages.SelectedIndex = -1;
                return;
            }
            else if ((LBMessages.SelectedItem as Message).MessageType == MessageType.File)
            {
                OpenFolderDialog openFolderDialog = new OpenFolderDialog();
                //openFolderDialog.ShowDialog();

                if (openFolderDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(openFolderDialog.FolderName))
                {
                    MessageBox.Show("Ошибка сервера");
                    return;
                }

                string fileName = (LBMessages.SelectedItem as Message).Text;
                var fileUrl = $"{serverAdress}/upload/{fileName}";

                /*using var response = await httpClient.GetStreamAsync(fileUrl);
                using var fs = new FileStream($"{openFolderDialog.FolderName}/{fileName}",FileMode.OpenOrCreate);
                await response.CopyToAsync(fs);

                MessageBox.Show($"Файл {fileName} скачен");*/

                try
                {
                    using var response = await httpClient.GetStreamAsync(fileUrl);
                    using var fs = new FileStream($"{openFolderDialog.FolderName}/{fileName}", FileMode.OpenOrCreate);
                    await response.CopyToAsync(fs);

                    MessageBox.Show($"Файл {fileName} скачен");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при скачивании файла: {ex.Message}");
                }
            }
        }

        private void Button_Click_Settings(object sender, RoutedEventArgs e)
        {
            SettingsPanelWindow SPW = new SettingsPanelWindow(User);
            SPW.ShowDialog();
        }
    }
}
