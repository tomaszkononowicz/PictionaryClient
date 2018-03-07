using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PictionaryClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
  

    public partial class MainWindow : Window
    {
        //dla indeksowania tablicy datagramu UDP
        private static int COMMAND = 0;
        private static int ID = 1;

        Point currentPoint = new Point();
        Color currentColor = new Color();
        UdpClient client = new UdpClient();
        UdpClient reciver;
        CancellationTokenSource ts;
        bool connected = false;
        byte id = 0;

        IPAddress serverIp;
        int serverPortToConnecting;
        int serverSendingPort;
        int serverRecivingPort;

        
        public MainWindow()
        {
            InitializeComponent();
            currentColor = Colors.Black;
            textBoxServerIp.Text = "127.0.0.1";
            textBoxServerPort.Text = "11000";
            textBoxStatus.Text = "Disconected";         
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            currentPoint = e.GetPosition(paintArea);
            if (connected)
            {
                IPEndPoint ep = new IPEndPoint(serverIp, serverRecivingPort);
                byte[] datagram = new byte[18];
                datagram[0] = (byte)Commands.FirstCoords;
                datagram[1] = id;
                byte[] x = BitConverter.GetBytes(currentPoint.X);
                byte[] y = BitConverter.GetBytes(currentPoint.Y);
                Buffer.BlockCopy(x, 0, datagram, 2, x.Length);
                Buffer.BlockCopy(y, 0, datagram, 2 + x.Length, y.Length);
                client.Send(datagram, datagram.Length, ep);
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Line line = new Line();              
                line.Stroke = new SolidColorBrush(currentColor);
                line.StrokeThickness = 5;
                line.X1 = currentPoint.X;
                line.Y1 = currentPoint.Y;
                line.X2 = e.GetPosition(paintArea).X;
                line.Y2 = e.GetPosition(paintArea).Y;

                currentPoint = e.GetPosition(paintArea);
                if (connected)
                {
                    IPEndPoint ep = new IPEndPoint(serverIp, serverRecivingPort);
                    byte[] datagram = new byte[18];
                    datagram[0] = (byte)Commands.NextCoords;
                    datagram[1] = id;
                    byte[] x = BitConverter.GetBytes(currentPoint.X);
                    byte[] y = BitConverter.GetBytes(currentPoint.Y);
                    Buffer.BlockCopy(x, 0, datagram, 2, x.Length);
                    Buffer.BlockCopy(y, 0, datagram, 2 + x.Length, y.Length);
                    client.Send(datagram, datagram.Length, ep);
                }
                paintArea.Children.Add(line);
            }
        }

        private void buttonColor_Click(object sender, RoutedEventArgs e)
        {
            if (gridColors.Visibility != Visibility.Visible)
            {
                gridColors.Visibility = Visibility.Visible;
            }
            else
            {
                gridColors.Visibility = Visibility.Hidden;
            }
        }



        private void pickerColor_MouseDown(object sender, MouseButtonEventArgs e)
        {
            currentColor = (Color)ColorConverter.ConvertFromString((sender as Rectangle).Fill.ToString());
            rectangleColor.Fill = new SolidColorBrush(currentColor);
            if (connected)
            {
                IPEndPoint ep = new IPEndPoint(serverIp, serverRecivingPort);
                client.Send(new byte[] { (byte)Commands.Color, id, currentColor.R, currentColor.G, currentColor.B }, 5, ep);
            }
        }

        private void buttonConnect_Click(object sender, RoutedEventArgs e)
        {
            if (!connected)
            {
                serverIp = IPAddress.Parse(textBoxServerIp.Text.ToString());
                serverPortToConnecting = int.Parse(textBoxServerPort.Text.ToString());
                IPEndPoint ep = new IPEndPoint(serverIp, serverPortToConnecting);
                client.Send(new byte[] { (byte)Commands.Connect, id, currentColor.R, currentColor.G, currentColor.B }, 5, ep);
                byte[] receivedData = client.Receive(ref ep);
                connected = true;
                id = receivedData[ID];
                textBoxStatus.Text = "Connected " + id;
                serverRecivingPort = BitConverter.ToInt32(receivedData, 2);
                serverSendingPort = BitConverter.ToInt32(receivedData, 6);
                reciver = new UdpClient(serverSendingPort);
                ts = new CancellationTokenSource();
                Task.Factory.StartNew(() => { reciving(); }, ts.Token);
            }

        }

        private void reciving()
        {
            IPEndPoint epr = new IPEndPoint(serverIp, serverSendingPort);
            while (!ts.IsCancellationRequested)
            {
                try
                {
                    byte[] receivedData = reciver.Receive(ref epr);
                    if (receivedData[COMMAND] == (byte)Commands.Line)
                    {
                        Color color = new Color();
                        color.A = 255;
                        color.R = receivedData[2];
                        color.G = receivedData[3];
                        color.B = receivedData[4];
                        Application.Current.Dispatcher.BeginInvoke(
                          DispatcherPriority.Background,
                          new Action(() => {
                              Line line = new Line();
                              line.Stroke = new SolidColorBrush(color);
                              line.StrokeThickness = 5;
                              line.X1 = BitConverter.ToDouble(receivedData, 5);
                              line.Y1 = BitConverter.ToDouble(receivedData, 13);
                              line.X2 = BitConverter.ToDouble(receivedData, 21);
                              line.Y2 = BitConverter.ToDouble(receivedData, 29);
                              //Console.WriteLine("Robie linie z {0} {1} do {2} {3} kolor {4} ",line.X1, line.Y1, line.X2, line.Y2, color.ToString());
                              paintArea.Children.Add(line);
                          }));                       
                    }
                }
                catch (Exception e) {
                    Console.WriteLine("Odbieranie przerwane");
                };
            }

        }



        private void buttonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            if (connected)
            {
                IPEndPoint ep = new IPEndPoint(serverIp, serverPortToConnecting);
                connected = false;
                ts.Cancel();
                reciver.Close();
                textBoxStatus.Text = "Disconected";
                client.Send(new byte[] { (byte)Commands.Disconnect, id }, 2, ep);
            }
            
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (connected)
            {
                IPEndPoint ep = new IPEndPoint(serverIp, serverPortToConnecting);
                client.Send(new byte[] { (byte)Commands.Disconnect, id }, 2, ep);
            }
        }
    }
}
