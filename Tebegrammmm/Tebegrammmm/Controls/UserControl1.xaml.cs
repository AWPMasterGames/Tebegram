using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace Tebegrammmm.Controls
{
    /// <summary>
    /// Логика взаимодействия для UserControl1.xaml
    /// </summary>
    public partial class UserControl1 : UserControl, INotifyPropertyChanged
    {
        /*public static readonly DependencyProperty Avatar_prop = DependencyProperty.Register(
            nameof(Avatar), typeof(string), typeof(UserControl1), new PropertyMetadata(default(string)));

        public string Avatar
        {
            get { return (string)GetValue(Avatar_prop); }
            set
            {
                SetValue(Avatar_prop, value);
                *//*BitmapImage bi3 = new BitmapImage();
                bi3.BeginInit();
                bi3.UriSource = new Uri(value, UriKind.Relative);
                bi3.EndInit();
                ImgAvatar.Stretch = Stretch.Fill;
                ImgAvatar.Source = bi3;*//*
            }
        }
        public string Avatar1 = "https://djnkpzmq-5000.euw.devtunnels.ms/avatars/1429941917327687800.png";*/

        private string _Avatar;
        public string Avatar
        {
            get { return _Avatar; }
            set
            {
                _Avatar = value;
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs("Text"));
            }
        }

        public UserControl1()
        {
            InitializeComponent();
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
