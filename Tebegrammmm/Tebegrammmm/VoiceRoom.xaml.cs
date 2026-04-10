using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Tebegrammmm.Classes;
using Tebegrammmm.Data;

namespace Tebegrammmm
{
    public enum Mode
    {
        AcceptCall,
        ActiveCall
    }
    public partial class VoiceRoom : Window
    {
        static HttpClient httpClient = new HttpClient();
        private string Token { get; set; }
        private Contact Contact { get; set; }
        private bool IsMicrophoneOn { get; set; }

        public VoiceRoom(Mode mode, Contact contact, string token)
        {
            InitializeComponent();
            Contact = contact;
            Token = token;
            switch (mode)
            {
                case Mode.AcceptCall:
                    DefoultVoiceRoom.Visibility = Visibility.Visible;
                    ActiveVoiceRoom.Visibility = Visibility.Hidden;
                    break;
                case Mode.ActiveCall:

                    Init();
                    DefoultVoiceRoom.Visibility = Visibility.Hidden;
                    ActiveVoiceRoom.Visibility = Visibility.Visible;
                    break;
            }

            MainGrid.DataContext = contact;
        }

        Thread SendVoiceThread;
        Thread ReceiveVoiceThread;

        private ClientWebSocket ws;
        private WaveInEvent waveIn;
        private WaveOutEvent waveOut = new WaveOutEvent();

        byte[] buffer;
        BufferedWaveProvider waveProvider;

        private async void Init()
        {

            ws = new ClientWebSocket();
            waveIn = new WaveInEvent();
            waveIn.DeviceNumber = UserData.User.SelectedDeviceNum;
            waveIn.WaveFormat = new WaveFormat(20480, 16, 1);

            waveProvider = new BufferedWaveProvider(new WaveFormat(20480, 16, 1));
            buffer = new byte[4096];

            waveOut.Init(waveProvider);

            waveIn.DataAvailable += async (s, e) =>
            {
                if (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(new ArraySegment<byte>(e.Buffer, 0, e.BytesRecorded), WebSocketMessageType.Binary, true, CancellationToken.None);
                        //Console.WriteLine($"Send {} Bytes");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }

                    //Thread.Sleep(200);
                    //Console.WriteLine(e.Buffer[0]);
                }
            };

            await ws.ConnectAsync(new Uri($"{ServerData.ServerAdress.Replace("https:", "ws:")}/Voice/ws?userId={UserData.User.Id}&roomToken={Token}"),
    CancellationToken.None);

            IsMicrophoneOn = true;

            StartSVT();
            StartRVT();
        }


        private void StartSVT()
        {
            SendVoiceThread = new Thread(() =>
            {
                waveIn.StartRecording();
            });

            SendVoiceThread.Start();
        }

        private void StartRVT()
        {
            ReceiveVoiceThread = new Thread(() =>
            {
                waveOut.Play();
                ReceiveVoice();
            });
            ReceiveVoiceThread.Start();
        }

        private async void ReceiveVoice()
        {
            while (ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    //Console.WriteLine(buffer[0]);
                    waveProvider.AddSamples(buffer, 0, result.Count);
                }
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    switch (message)
                    {
                        case "CloseConnection":
                            Dispatcher.Invoke(new Action(() =>
                            {
                                this.Close();
                            }));
                            break;
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Button_Click_Accept(object sender, RoutedEventArgs e)
        {
            DefoultVoiceRoom.Visibility = Visibility.Hidden;
            ActiveVoiceRoom.Visibility = Visibility.Visible;
            Init();
        }

        private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if(ws != null)
                if(ws.State == WebSocketState.Open)
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            UserData.User.InCall = false;
        }

        private async void Button_Click_Decline(object sender, RoutedEventArgs e)
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{ServerData.ServerAdress}/Voice/DeclineCall/{UserData.User.Id}-{Token}");
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            string Content = await response.Content.ReadAsStringAsync();
            
            this.Close();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

        }
    }
}
