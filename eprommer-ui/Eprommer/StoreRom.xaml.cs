using System;
using System.Windows;
using System.IO;

namespace Eprommer
{
    /// <summary>
    /// Interaction logic for StoreRom.xaml
    /// </summary>
    public partial class StoreRom : Window
    {
        public StoreRom()
        {
            InitializeComponent();
        }

        byte[] data;
        public byte[] Data
        {
            get
            {
                return data;
            }
            set
            {
                data = value;
                CRC.Text = string.Format("{0:X8}", Crc32Algorithm.Compute(Data));
                Size.Text = string.Format("{0} Bytes", Data.Length);
            }
        }

        public string Label
        {
            set
            {
                Name.Text = value;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Eprommer", "ROMS");
            Directory.CreateDirectory(dir);
            var filename = Path.ChangeExtension(Path.Combine(dir, CRC.Text), ".rom");
            File.WriteAllBytes(filename,Data);
            filename = Path.ChangeExtension(Path.Combine(dir, CRC.Text), ".txt");
            File.WriteAllText(filename, Name.Text);
            DialogResult = true;
            Close();
        }
    }
}
