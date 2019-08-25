using System.Text;

namespace Eprommer
{
    public class Dummy
    {
        public string Addr { get; set; }
        public string Bytes { get; set; }
        public string Text { get; set; }
    }
    public class Hexline
    {
        byte[] d;
        int a;
        public Hexline(int _a,  byte[] _d)
        {
            a = _a;
            d = _d;
        }
        public string Addr
        {
            get
            {
                return string.Format("{0:X4}", a);
            }
            set
            {
                a = int.Parse(value);
            }
        }
        public string Bytes
        {
            get
            {
                return string.Format("{0:X2} {1:X2} {2:X2} {3:X2} {4:X2} {5:X2} {6:X2} {7:X2} {8:X2} {9:X2} {10:X2} {11:X2} {12:X2} {13:X2} {14:X2} {15:X2} ", 
                    d[0], d[1], d[2], d[3],
                    d[4], d[5], d[6], d[7],
                    d[8], d[9], d[10], d[11],
                    d[12], d[13], d[14], d[15]);
            }
        }
        private static int vendorbase = 0xe000;
        public static bool Lowercase
        {
            set
            {
                if (value)
                    vendorbase = 0xe100;
                else
                    vendorbase = 0xe000;
            }
            get
            {
                return vendorbase == 0xe100;
            }
        }
        public string Text
        {
            get
            {
                var sb = new StringBuilder(32);
                for (int i = 0; i < 16; ++i)
                {
                    var c = (char) (vendorbase | d[i]);
                    if (d[i] < 0x20) c = ' ';
                    if (d[i]>=0x80 && d[i] < 0xa0) c = ' ';
                    sb.Append( c );
                }
                return sb.ToString();
            }
        }
    }
}
