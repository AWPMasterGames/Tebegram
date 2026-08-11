using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public class Contact : System.ComponentModel.INotifyPropertyChanged
    {
        static HttpClient httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
        });

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void Notify(string prop) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(prop));

        private int _UserId;
        private string _Name = string.Empty;
        private string _Avatar;
        private ObservableCollection<Message> _Messages;
        public int UserId { get { return _UserId; } }
        public string Name { get { return _Name; } }
        public ObservableCollection<Message> Messages { get { return _Messages; } }
        public string Username { get; set; }

        // Уведомление обязательно: аватар подгружается с сервера асинхронно ПОСЛЕ
        // привязки UI - без него список чатов оставался со старым/пустым аватаром
        public string Avatar
        {
            get { return _Avatar; }
            set { _Avatar = value; Notify(nameof(Avatar)); }
        }
        public string Draft { get; set; } = string.Empty; // Черновик сообщения

        /// <summary>Чат с самим собой («Избранное») - рисуется постоянной иконкой-закладкой.</summary>
        public bool IsFavorites =>
            UserData.User != null && Username == UserData.User.Username;

        /// <summary>
        /// Найден глобальным поиском (@логин) и ЕЩЁ НЕ в контактах: показывается
        /// в списке с подсказкой, а по клику сначала добавляется в контакты.
        /// Флаг живёт только в UI, на сервер и в файл не сохраняется.
        /// </summary>
        public bool IsGlobalResult { get; set; }

        /// <summary>Подпись под именем для результата глобального поиска.</summary>
        public string GlobalHint => $"@{Username} · начать чат";

        public Contact()
        {
            _Messages = new ObservableCollection<Message>();
        }
        public Contact(int id, string username, string name)
        {
            _UserId = id;
            _Name = name;
            Username = username;
            _Messages = new ObservableCollection<Message>();
            GetUserAvatar();
        }
        public Contact(int id, string username, string name, ObservableCollection<Message> messages)
        {
            _UserId = id;
            _Name = name;
            Username = username;
            _Messages = messages;
            GetUserAvatar();
        }

        public void ChangeName(string name)
        {
            _Name = name;
            Notify(nameof(Name));
        }

        public void ChangeUsername(string username)
        {
            Username = username;
        }

        private async void GetUserAvatar()
        {
            try
            {
                await ServerData.Ready;
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/avatarsFileName/{UserId}");
                using HttpResponseMessage response = await httpClient.SendAsync(request);
                string content = await response.Content.ReadAsStringAsync();
                // Ставим аватар только при успешном ответе - иначе в URL попадал текст ошибки сервера
                if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(content))
                {
                    // ?t=… обходит кэш картинок WPF (живёт весь процесс): без него после
                    // перезахода под другим аккаунтом показывался старый аватар контакта
                    Avatar = $"{ServerData.ServerAdress}/avatars/{content}?t={DateTime.UtcNow.Ticks}";
                }
            }
            catch (Exception ex)
            {
                Log.Save($"[Contact.GetUserAvatar] {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
