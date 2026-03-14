using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Tebegrammmm
{
    /// <summary>
    /// Логика взаимодействия для VoiceRoom.xaml
    /// </summary>
    public partial class VoiceRoom : Window
    {
        public VoiceRoom()
        {
            InitializeComponent();
        }
        private bool _isEndAnim;
        private void CallButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
            {
                return;
            }

            if (this._isEndAnim)
            {
                VisualStateManager.GoToState(button, "CallState", true);
            }
            else
            {
                VisualStateManager.GoToState(button, "EndState", true);
            }

            this._isEndAnim = !this._isEndAnim;
        }
    }
}
