using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Источник для списка чатов LBChats: собирает контакты и группы папки в ОДИН
    /// плоский список в фиксированном порядке
    ///
    ///     1. «Избранное» (чат с самим собой) — всегда первым;
    ///     2. групповые чаты;
    ///     3. остальные контакты — в том порядке, в каком они пришли с сервера.
    ///
    /// Зачем свой класс, а не CompositeCollection, как было раньше: CompositeCollection
    /// склеивает коллекции ЦЕЛИКОМ, поэтому «Избранное» оказывалось внутри блока
    /// контактов, ниже групп, и первым в списке становилась случайная группа.
    /// Вытащить один элемент вверх, не задев остальные, она не умеет.
    ///
    /// Класс подписан на обе исходные коллекции, поэтому новый контакт или пришедшая
    /// по WebSocket группа появляются сами — как и раньше, без ручной перерисовки.
    /// Порядок при этом пересобирается целиком: элементов десятки, а не тысячи,
    /// и так исключены расхождения между «вставили» и «должно быть».
    /// </summary>
    public sealed class ChatListSource : ObservableCollection<object>
    {
        private readonly ObservableCollection<Contact> _contacts;
        private readonly ObservableCollection<Chat> _chats;

        public ChatListSource(ChatFolder folder)
        {
            _contacts = folder.Contacts;
            _chats = folder.Chats;

            _contacts.CollectionChanged += OnSourceChanged;
            _chats.CollectionChanged += OnSourceChanged;

            Rebuild();
        }

        private void OnSourceChanged(object sender, NotifyCollectionChangedEventArgs e) => Rebuild();

        /// <summary>Пересобирает список в порядке «Избранное → группы → контакты».</summary>
        public void Rebuild()
        {
            var desired = BuildOrder();

            // Правим список точечно, а не через Clear + Add: полная очистка сбрасывает
            // выделение в ListBox, и открытый чат переставал подсвечиваться, стоило
            // прийти новой группе или добавиться контакту.
            for (int i = Count - 1; i >= 0; i--)
                if (!desired.Contains(this[i])) RemoveAt(i);

            for (int i = 0; i < desired.Count; i++)
            {
                int current = IndexOf(desired[i]);
                if (current < 0) Insert(i, desired[i]);
                else if (current != i) Move(current, i);
            }
        }

        private List<object> BuildOrder()
        {
            // «Избранное» — чат с самим собой; ищем его отдельно, чтобы не зависеть
            // от того, каким по счёту он пришёл с сервера
            Contact favorites = _contacts.FirstOrDefault(c => c != null && c.IsFavorites);

            var order = new List<object>(_contacts.Count + _chats.Count);
            if (favorites != null) order.Add(favorites);
            foreach (Chat chat in _chats)
                if (chat != null) order.Add(chat);
            foreach (Contact contact in _contacts)
                if (contact != null && !ReferenceEquals(contact, favorites)) order.Add(contact);
            return order;
        }

        /// <summary>Отписка: папку могут открыть и закрыть много раз за сеанс.</summary>
        public void Detach()
        {
            _contacts.CollectionChanged -= OnSourceChanged;
            _chats.CollectionChanged -= OnSourceChanged;
        }
    }
}
