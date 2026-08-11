using System.Windows;
using System.Windows.Controls;

namespace Tebegrammmm.Controls
{
    /// <summary>
    /// Кружок аватара: фон + иконка-человечек по умолчанию + фото (заполняет весь круг) + рамка.
    /// Avatar - DependencyProperty, поэтому можно привязывать: Avatar="{Binding Avatar}".
    /// </summary>
    public partial class UserControl1 : UserControl
    {
        public static readonly DependencyProperty AvatarProperty = DependencyProperty.Register(
            nameof(Avatar), typeof(string), typeof(UserControl1), new PropertyMetadata(null));

        public string Avatar
        {
            get => (string)GetValue(AvatarProperty);
            set => SetValue(AvatarProperty, value);
        }

        public UserControl1()
        {
            InitializeComponent();
        }
    }
}
