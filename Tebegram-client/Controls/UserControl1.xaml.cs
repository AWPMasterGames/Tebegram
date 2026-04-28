using System.Windows;
using System.Windows.Controls;

namespace Tebegrammmm.Controls
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class UserControl1 : UserControl
    {
        public static readonly DependencyProperty AvatarProperty = DependencyProperty.Register(
            nameof(Avatar),
            typeof(string),
            typeof(UserControl1),
            new PropertyMetadata(null));

        public string Avatar
        {
            get => (string)GetValue(AvatarProperty);
            set => SetValue(AvatarProperty, value);
        }

        public UserControl1()
        {
            InitializeComponent();
            DataContext = this;
        }
    }
}
