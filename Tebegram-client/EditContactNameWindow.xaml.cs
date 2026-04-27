using System.Windows;
using System.Windows.Input;

namespace Tebegrammmm
{
    public partial class EditContactNameWindow : Window
    {
        private static EditContactNameWindow _instance;

        public Contact Contact { get; private set; }
        public string NewName { get; private set; }

        public EditContactNameWindow(Contact contact)
        {
            InitializeComponent();

            // Если окно уже открыто — вывести его на передний план
            if (_instance != null)
            {
                _instance.Activate();
                if (_instance.WindowState == WindowState.Minimized)
                    _instance.WindowState = WindowState.Normal;
                this.Loaded += (_, __) => { this.DialogResult = false; this.Close(); };
                return;
            }

            _instance = this;
            Contact = contact;
            TBName.Text = contact.Name;
            TBName.Focus();
            TBName.SelectAll();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (_instance == this) _instance = null;
            base.OnClosed(e);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void TBName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Save();
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            Save();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(TBName.Text))
            {
                MessageBox.Show("Пожалуйста, введите имя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            NewName = TBName.Text.Trim();
            this.DialogResult = true;
            this.Close();
        }
    }
}
