using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace Tebegrammmm.Classes
{
    /// <summary>
    /// Источник данных для списка чатов LBChats. Сводит контакты и группы папки
    /// в один плоский список фиксированного порядка:
    ///
    ///     1. «Избранное», то есть чат с самим собой;
    ///     2. групповые чаты;
    ///     3. прочие контакты в порядке получения с сервера.
    ///
    /// Ранее применялась CompositeCollection, но она склеивает коллекции целиком.
    /// «Избранное» попадало внутрь блока контактов, ниже групп, и первой строкой
    /// списка оказывалась произвольная группа. Поднять один элемент, не разбивая
    /// исходную коллекцию, CompositeCollection не позволяет.
    ///
    /// Класс подписан на обе исходные коллекции, поэтому новый контакт и пришедшая
    /// по WebSocket группа отображаются без явной перерисовки. Порядок при каждом
    /// изменении пересобирается целиком: элементов десятки, а не тысячи, зато
    /// расхождение между вычисленным и фактическим порядком исключено.
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
            // «Избранное» - чат с самим собой; ищем его отдельно, чтобы не зависеть
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
