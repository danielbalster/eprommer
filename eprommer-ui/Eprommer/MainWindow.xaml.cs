using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.IO.Ports;
using Microsoft.Win32;
using System.IO;

namespace Eprommer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            lv.Items.Clear();
            Setup();
        }

        void updateHex(byte[] data)
        {
            var list = new List<Hexline>();
            for (int i = 0; i < data.Length; i += 16)
            {
                var d = new byte[16];
                Buffer.BlockCopy(data, i, d, 0, 16);
                list.Add(new Hexline(i, d));
            }
            lv.ItemsSource = list;
        }

        static public SerialPort Port;

        byte[] data;
        void update()
        {
            if (data == null) return;
            var list = new List<Hexline>();
            for (int i = 0; i < data.Length; i += 16)
            {
                var d = new byte[16];
                Buffer.BlockCopy(data, i, d, 0, 16);
                list.Add(new Hexline(i, d));
            }
            lv.ItemsSource = list;
            CRC.Text = string.Format("CRC: {0:X8}", Crc32Algorithm.Compute(data));
        }

        const int buffer_size = 65536;
        int buffer_fill = 0;
        int buffer_offset = 0;
        byte[] buffer = new byte[buffer_size];

        void updateRom()
        {
            P1.Text = "Vpp"; P28.Text = "Vcc";
            P2.Text = "A12"; P27.Text = "/PGM";
            P3.Text = "A7"; P26.Text = "nc";
            P4.Text = "A6"; P25.Text = "A8";
            P5.Text = "A5"; P24.Text = "A9";
            P6.Text = "A4"; P23.Text = "A11";
            P7.Text = "A3"; P22.Text = "/OE";
            P8.Text = "A2"; P21.Text = "A10";
            P9.Text = "A1"; P20.Text = "/CE";
            P10.Text = "A0"; P19.Text = "D7";
            P11.Text = "D0"; P18.Text = "D6";
            P12.Text = "D1"; P17.Text = "D5";
            P13.Text = "D2"; P16.Text = "D4";
            P14.Text = "Vss"; P15.Text = "D3";

            switch (RomType.SelectedIndex)
            {
                case 0:
                    P1.Text = "nc";
                    P28.Text = "Vcc=5V";
                    Rom.Text = "27c64";
                    break;
                case 1:
                    P1.Text = "Vpp=12V";
                    P28.Text = "Vcc=5V";
                    Rom.Text = "winbond";
                    //Rom.Text = "Vpp=12V Vcc=5V tW=100µs"; //winbond
                    break;
                case 2:
                    P1.Text = "Vpp=5V";
                    P28.Text = "Vcc=5V";
                    Rom.Text = "ee";
                    //Rom.Text = "Vpp=5V Vcc=5V tW=20µs";
                    break;
                case 3:
                    P1.Text = "Vpp=13V";
                    P28.Text = "Vcc=6.5V";
                    Rom.Text = "ee";
                    //Rom.Text = "Vpp=13V Vcc=6.5V tW=95µs";
                    break;
            }

        }

        private void Setup()
        {
            string[] ports = SerialPort.GetPortNames();
            SerialPorts.ItemsSource = ports;
            SerialPorts.SelectedIndex = ports.Length > 1 ? 1 : 0;

            tc.Visibility = Visibility.Collapsed;
            Toolbar.Visibility = Visibility.Collapsed;
        }

        private void Prepare()
        {
            tc.Visibility = Visibility.Visible;
            Toolbar.Visibility = Visibility.Visible;
        }

        enum Command
        {
            CMD_PRINT = 1,
            CMD_SET_LED = 2,
            CMD_READ_BLOCK = 3,
            CMD_READ_NEXT = 4,
            CMD_READ_AGAIN = 5,
            CMD_WRITE_BLOCK = 6,
            CMD_WRITE_NEXT = 7,
            CMD_WRITE_AGAIN = 8,
            CMD_ERASE = 9,
        }

        int write_block_count;
        ushort write_block_number;
        void writeBlock()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                WriteBlock(write_block_number, romtype);
            }, null);
        }

        int read_block_count;
        ushort read_block_number;
        void readBlock()
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                ReadBlock(read_block_number);
            }, null);
        }

        void Process(Command command, byte[] message)
        {
            switch (command)
            {
                case Command.CMD_WRITE_NEXT:
                    {
                        write_block_number++;
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            pb.Value = write_block_number;
                            status.Content = "Ok";
                        }));
                        if (write_block_number < write_block_count)
                        {
                            writeBlock();
                        }
                        else
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                pb.Visibility = Visibility.Collapsed;
                                status.Content = "Written.";
                            }));
                        }
                    }
                    break;
                case Command.CMD_WRITE_AGAIN:
                    {
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            status.Content = "Retrying...";
                        }));
                        writeBlock();
                    }
                    break;
                case Command.CMD_PRINT:
                    {
                        Application.Current.Dispatcher.Invoke(new Action(() =>
                        {
                            status.Content = Encoding.ASCII.GetString(message);
                            if (status.Content.ToString()=="EPROMMER Version 1.4" || status.Content.ToString() == "EPROMMER Version 1.3")
                            {
                                Prepare();
                            }
                        }));
                    }
                    break;
                case Command.CMD_READ_BLOCK:
                    {
                        int block = message[0] + message[1] * 256;
                        int chksum = message[2] + message[3] * 256;
                        int sumchk = 0;
                        for (int i = 0; i < 64; ++i)
                        {
                            sumchk += message[4 + i];
                        }
                        if (sumchk != chksum)
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                status.Content = "Retrying...";
                            }));
                            readBlock();
                        }
                        else
                        {
                            if (block <= (data.Length / 64))
                            {
                                Buffer.BlockCopy(message, 4, data, block * 64, 64);
                            }
                            read_block_number++;
                            if (read_block_number < read_block_count)
                            {
                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    pb.Value = read_block_number;
                                    status.Content = "Ok";
                                }));

                                readBlock();
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(new Action(() =>
                                {
                                    pb.Visibility = Visibility.Collapsed;
                                    status.Content = "Read.";
                                    update();
                                }));
                            }

                        }
                    }
                    break;
            }
        }

        void ProcessBuffer()
        {
            while (buffer_fill > 0)
            {
                int i = buffer_offset;

                if (buffer[i + 1] <= (buffer_fill - 2))
                {
                    int len = buffer[i + 1];
                    byte[] msg = new byte[len];
                    Buffer.BlockCopy(buffer, i + 2, msg, 0, len);
                    Process((Command)buffer[0], msg);

                    var temp = new byte[buffer_size];
                    buffer_offset = 0;
                    buffer_fill -= 2 + len;
                    Buffer.BlockCopy(buffer, i + 2 + len, temp, 0, buffer_fill);
                    buffer = temp;
                }
                else break;
            }
        }

        void ReadBlock(ushort block)
        {
            if (!IsConnected) return;
            if (block >= (data.Length / 64))
            {
                return;
            }
            var b = new byte[6];
            b[0] = (byte)Command.CMD_READ_BLOCK;
            b[1] = (byte)(block & 255);
            b[2] = (byte)(block >> 8);
            b[3] = (byte)(b[0] ^ 255);
            b[4] = (byte)(b[1] ^ 255);
            b[5] = (byte)(b[2] ^ 255);
            Port.Write(b, 0, b.Length);
        }

        void WriteBlock(ushort block, byte mode)
        {
            if (!IsConnected) return;
            if (block >= (data.Length / 64))
            {
                return;
            }
            var b = new byte[6 + 64];
            b[0] = (byte)Command.CMD_WRITE_BLOCK;
            b[1] = (byte)(block & 255);
            b[2] = (byte)(block >> 8);
            b[3] = mode;
            int sumchk = 0;
            for (int i = 0; i < 64; ++i)
            {
                byte v = data[(block * 64) + i];
                b[6 + i] = v;
                sumchk += v;
            }
            b[4] = (byte)(sumchk & 255);
            b[5] = (byte)((sumchk >> 8) & 255);
            Port.Write(b, 0, b.Length);
        }

        void handler(object _sender, SerialDataReceivedEventArgs _e)
        {
            int size = Port.BytesToRead;
            buffer_fill += Port.Read(buffer, buffer_fill, size);
            ProcessBuffer();
        }

        private void SetLED(bool on)
        {
            if (!IsConnected) return;

            var b = new byte[2];
            b[0] = (byte)Command.CMD_SET_LED;
            b[1] = (byte)(on ? 1 : 0);
            Port.Write(b, 0, 2);
        }

        public bool IsConnected
        {
            get
            {
                return Port != null && Port.IsOpen;
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (Port != null)
            {
                if (Port.IsOpen)
                {
                    Port.DiscardOutBuffer();
                    Port.DiscardInBuffer();
                    Port.Close();
                }
                buffer_fill = 0;
                buffer_offset = 0;
                Port.Dispose();
                Port = null;
                SerialIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Eprommer;component/famfamfam_silk_icons_v013/icons/disconnect.png"));
                SerialControl.Visibility = Visibility.Visible;

                Setup();
            }
            else
            {
                Port = new SerialPort()
                {
                    PortName = SerialPorts.SelectedItem.ToString(),
                    BaudRate = int.Parse(Baudrate.Text),
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    DtrEnable = false,
                    RtsEnable = false
                };

                Port.DataReceived += handler;

                Port.Open();

                SerialIcon.Source = new BitmapImage(new Uri("pack://application:,,,/Eprommer;component/famfamfam_silk_icons_v013/icons/connect.png"));
                SerialControl.Visibility = Visibility.Collapsed;
                RomSize.SelectedIndex = 0;
                status.Content = "Press RESET on Eprommer to connect!";
                tc.SelectedIndex = 0;

            }
        }
        private void ReadClicked(object sender, RoutedEventArgs e)
        {
            read_block_count = data.Length / 64;
            read_block_number = 0;

            pb.Minimum = 0;
            pb.Maximum = read_block_count;
            pb.Visibility = Visibility.Visible;
            status.Content = "Reading...";
            readBlock();
        }

        private void WriteClicked(object sender, RoutedEventArgs e)
        {
            write_block_count = data.Length / 64;
            write_block_number = 0;

            pb.Minimum = 0;
            pb.Maximum = write_block_count;
            pb.Visibility = Visibility.Visible;
            status.Content = "Writing...";
            writeBlock();
        }

        private void VerifyClicked(object sender, RoutedEventArgs e)
        {
        }

        private void TestClicked(object sender, RoutedEventArgs e)
        {

        }

        private void EraseClicked(object sender, RoutedEventArgs e)
        {
            if (!IsConnected) return;

            var b = new byte[1];
            b[0] = (byte)Command.CMD_ERASE;
            Port.Write(b, 0, b.Length);
        }

        private void RomSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RomSize.SelectedIndex == -1) return;
            switch (RomSize.SelectedIndex)
            {
                case 0:
                    data = new byte[8 * 1024];
                    break;
                case 1:
                    data = new byte[16 * 1024];
                    break;
                case 2:
                    data = new byte[32 * 1024];
                    break;
                case 3:
                    data = new byte[64 * 1024];
                    break;
            }
            update();
        }

        private void LoadRomClicked(object sender, RoutedEventArgs e)
        {
            int addr = 0;
            if (int.TryParse(LoadAddress.Text, System.Globalization.NumberStyles.HexNumber, null, out addr))
            {
                var ofd = new OpenFileDialog();
                if (ofd.ShowDialog() != null && File.Exists(ofd.FileName))
                {
                    Application.Current.MainWindow.Cursor = Cursors.Wait;

                    var bytes = System.IO.File.ReadAllBytes(ofd.FileName);

                    int len = bytes.Length;
                    int rem = data.Length - addr;
                    if (len > rem) len = rem;

                    Buffer.BlockCopy(bytes, 0, data, addr, len);
                    update();

                    Application.Current.MainWindow.Cursor = null;
                }
            }
        }

        private void FillClicked(object sender, RoutedEventArgs e)
        {
            byte fillvalue = 0;
            if (byte.TryParse(FillValue.Text, System.Globalization.NumberStyles.HexNumber, null, out fillvalue))
            {
                for (int i = 0; i < data.Length; ++i)
                {
                    data[i] = fillvalue;
                }
                update();
            }
        }

        private void lv_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lv.SelectedIndex == -1) return;
            LoadAddress.Text = string.Format("{0:X4}", lv.SelectedIndex * 16);
        }

        private void CRC_MouseUp(object sender, MouseButtonEventArgs e)
        {
            CRC.Text = string.Format("CRC: {0:X8}", Crc32Algorithm.Compute(data));
            status.Content = string.Format("Updated CRC");
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {
        }

        private void Lowercase_Click(object sender, RoutedEventArgs e)
        {
        }

        private void ToLowercase(object sender, RoutedEventArgs e)
        {
            Hexline.Lowercase = true;
            update();
        }

        private void ToUppercase(object sender, RoutedEventArgs e)
        {
            Hexline.Lowercase = false;
            update();
        }

        byte romtype;
        private void RomType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            romtype = (byte) RomType.SelectedIndex;
            if (Rom == null) return;
            updateRom();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Note that you can have more than one file.
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (var file in files)
                {
                    if (file.ToUpper().EndsWith(".CRT"))
                    {
                        var bytes = System.IO.File.ReadAllBytes(file); // beware, big endian here
                        string cartmagic = System.Text.Encoding.ASCII.GetString(bytes, 0x00, 16);
                        string cartname = System.Text.Encoding.ASCII.GetString(bytes, 0x20, 32);
                        int i = 0x40;

                        byte exrom = bytes[0x18];
                        byte game = bytes[0x19];

                        int no = 0;
                        int rest = bytes.Length - 0x40;
                        while (rest > 0)
                        {
                            int size = ((bytes[i + 4] << 24) + (bytes[i + 5] << 16) + (bytes[i + 6] << 8) + (bytes[i + 7]));
                            int type = (bytes[i + 8] << 8) + bytes[i + 9];
                            int loadaddr = (bytes[i + 0xc] << 8) + bytes[i + 0xd];
                            int romsize = (bytes[i + 0xe] << 8) + bytes[i + 0xf];

                            data = new byte[romsize];

                            Buffer.BlockCopy(bytes, i + 0x10, data, 0, romsize);
                            status.Content = string.Format("No:{2} Type:{0:X4} EXROM:{4} GAME:{5} Address:{1:X4} Size:{3:X4}", type, loadaddr, no, romsize,exrom,game);

                            //var dlg = new StoreRom();
                            //dlg.Data = data;
                            //dlg.Label = string.Format("No:{2} Type:{0:X4} EXROM:{4} GAME:{5} Address:{1:X4} Size:{3:X4}", type, loadaddr, no, romsize, exrom, game);
                            //dlg.ShowDialog();

                            update();

                            i += size;
                            rest -= size;
                            no++;
                        }
                    }
                    else
                    {
                        data = System.IO.File.ReadAllBytes(file);
                        status.Content = string.Format("Dropped new rom");
                        update();
                    }
                }
            }
        }

        private void Store(object sender, RoutedEventArgs e)
        {
            //var dlg = new StoreRom();
            //dlg.Data = data;
            //dlg.ShowDialog();
            
        }

        private void Browse(object sender, RoutedEventArgs e)
        {

        }

        private void CropClicked(object sender, RoutedEventArgs e)
        {
            ushort offset = 0;
            ushort size = 0;
            if (ushort.TryParse(CropOffset.Text, System.Globalization.NumberStyles.HexNumber, null, out offset) &&
                ushort.TryParse(CropSize.Text, System.Globalization.NumberStyles.HexNumber, null, out size)
                )
            {
                var tgt = new byte[size];
                Buffer.BlockCopy(data, offset, tgt, 0, size);
                data = tgt;
                update();
            }
        }
    }
}
