using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
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
using static QDTool.Utility;

namespace QDTool
{
    /// <summary>
    /// Interaction logic for HexBrowser.xaml
    /// </summary>
    public partial class HexBrowser : Window
    {
        public HexBrowser()
        {
            InitializeComponent();
        }

        public void AddTextToCanvas(string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = Brushes.Black,
                Background = Brushes.White
                // Další nastavení vzhledu, pokud je potřeba
            };

            Canvas.SetLeft(textBlock, 50); // Nastavte X pozici
            Canvas.SetTop(textBlock, 50);  // Nastavte Y pozici
            canvas.Children.Add(textBlock);
        }

        private readonly byte[] SharpASCII = {
            (byte)'_', (byte)' ', (byte)'e', (byte)' ', (byte)'~', (byte)' ', (byte)'t', (byte)'g',
            (byte)'h', (byte)' ', (byte)'b', (byte)'x', (byte)'d', (byte)'r', (byte)'p', (byte)'c',
            (byte)'q', (byte)'a', (byte)'z', (byte)'w', (byte)'s', (byte)'u', (byte)'i', (byte)' ',
            (byte)' ', (byte)'k', (byte)'f', (byte)'v', (byte)' ', (byte)' ', (byte)' ', (byte)'j',
            (byte)'n', (byte)' ', (byte)' ', (byte)'m', (byte)' ', (byte)' ', (byte)' ', (byte)'o',
            (byte)'l', (byte)' ', (byte)' ', (byte)' ', (byte)' ', (byte)'y', (byte)'{', (byte)' ',
            (byte)'|' };

        private byte FromSHASCII(byte c)
        {
            if (c <= 0x5d) return c;
            if (c == 0x80) return (byte)'}';
            if (c < 0x90 || c > 0xc0) return (byte)' '; // z neznámých znaků uděláme ' '
            return SharpASCII[c - 0x90];
        }

        private string ConvertToHexDump(byte[] data, int bytesPerLine = 16)
        {
            StringBuilder hexDump = new StringBuilder();

            for (int i = 0; i < data.Length; i += bytesPerLine)
            {
                hexDump.AppendFormat("{0:X8}: ", i); // Hexadecimální adresa

                // Hexadecimální hodnoty bajtů
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length)
                        hexDump.AppendFormat("{0:X2} ", data[i + j]);
                    else
                        hexDump.Append("   "); // Pro poslední řádek s méně než 16 bajty
                    if ((i + j) % 4 == 3)
                        hexDump.Append(' ');
                }

                //hexDump.Append(" ");

                // ASCII reprezentace bajtů
                //int cnt = 0;
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length)
                    {
                        hexDump.Append(char.IsControl((char)data[i + j]) ? '.' : (char)data[i + j]);
                        //cnt++;
                    }
                    else
                        hexDump.Append(' '); // Pro poslední řádek s méně než 16 bajty
                }
                //for (int j = 0; j < bytesPerLine - cnt; j++)
                //{
                //    hexDump.Append(' ');
                //}

                hexDump.Append("  ");

                // ASCII reprezentace bajtů
                for (int j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < data.Length)
                        hexDump.Append(char.IsControl((char)data[i + j]) ? '.' : (char)FromSHASCII(data[i + j]));
                }

                hexDump.AppendLine();
            }
            return hexDump.ToString();
        }

        public void ShowHexDump((MZQFileHeader , MZQFileBody) MzfBlock)
        {
            MZQFileHeader header = MzfBlock.Item1;
            MZQFileBody body = MzfBlock.Item2;

            StringBuilder hexDump = new StringBuilder();

            byte[] mzfHeaderData = new byte[128];
            mzfHeaderData[0] = header.MzfFtype;
            Array.Copy(header.MzfFname, 0, mzfHeaderData, 1, header.MzfFname.Length);
            mzfHeaderData[17] = header.MzfFnameEnd;
            var mzfSizeBytes = BitConverter.GetBytes(header.MzfSize);
            Array.Copy(mzfSizeBytes, 0, mzfHeaderData, 18, mzfSizeBytes.Length);
            var mzfStartBytes = BitConverter.GetBytes(header.MzfStart);
            Array.Copy(mzfStartBytes, 0, mzfHeaderData, 20, mzfStartBytes.Length);
            var mzfExecBytes = BitConverter.GetBytes(header.MzfExec);
            Array.Copy(mzfExecBytes, 0, mzfHeaderData, 22, mzfExecBytes.Length);
            Array.Copy(header.MzfHeaderDescription, 0, mzfHeaderData, 24, 38+2+64);

            byte[] qdfHeaderData = new byte[70];
            qdfHeaderData[0] = 0xA5;
            CRC_check(0xA5, true);
            qdfHeaderData[1] = header.MzfHeaderSign;
            CRC_check(header.MzfHeaderSign);
            var dataSizeBytes = BitConverter.GetBytes(header.DataSize);
            Array.Copy(dataSizeBytes, 0, qdfHeaderData, 2, dataSizeBytes.Length);
            CRC_check(dataSizeBytes, 0, dataSizeBytes.Length);
            qdfHeaderData[4] = header.MzfFtype;
            CRC_check(header.MzfFtype);
            Array.Copy(header.MzfFname, 0, qdfHeaderData, 5, header.MzfFname.Length);
            CRC_check(header.MzfFname, 0, header.MzfFname.Length);
            qdfHeaderData[21] = header.MzfFnameEnd;
            CRC_check(header.MzfFnameEnd);
            Array.Copy(header.Unused1, 0, qdfHeaderData, 22, header.Unused1.Length); 
            CRC_check(header.Unused1, 0, header.Unused1.Length);
            Array.Copy(mzfSizeBytes, 0, qdfHeaderData, 24, mzfSizeBytes.Length);
            CRC_check(mzfSizeBytes, 0, mzfSizeBytes.Length);
            Array.Copy(mzfStartBytes, 0, qdfHeaderData, 26, mzfStartBytes.Length);
            CRC_check(mzfStartBytes, 0, mzfStartBytes.Length);
            Array.Copy(mzfExecBytes, 0, qdfHeaderData, 28, mzfExecBytes.Length);
            CRC_check(mzfExecBytes, 0, mzfExecBytes.Length);
            Array.Copy(header.MzfHeaderDescription, 0, qdfHeaderData, 30, 38);
            ushort crc = CRC_check(header.MzfHeaderDescription, 0, 38);
            qdfHeaderData[68] = ReverseBits((byte)(crc >> 8));
            qdfHeaderData[69] = ReverseBits((byte)(crc & 0xFF));

            hexDump.Append("ADDRESS   MZF HEADER DATA                                     ASCII             SHASCII (EU)");
            hexDump.AppendLine();
            hexDump.Append(ConvertToHexDump(mzfHeaderData));
            hexDump.AppendLine();

            hexDump.Append("ADDRESS   QDF HEADER DATA                                     ASCII             SHASCII (EU)");
            hexDump.AppendLine();
            hexDump.Append(ConvertToHexDump(qdfHeaderData));
            hexDump.AppendLine();

            hexDump.Append("ADDRESS   FILE DATA                                           ASCII             SHASCII (EU)");
            hexDump.AppendLine();
            hexDump.Append(ConvertToHexDump(body.MzfBody));

            TextBlock textBlock = new TextBlock
            {
                Text = hexDump.ToString(),
                FontFamily = new FontFamily("Consolas"), // Monospace font pro lepší čitelnost
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(10)
            };

            double letterWidth = 0;
            double lineHeight = 0;

            textBlock.Loaded += (sender, e) =>
            {
                canvas.MinHeight = textBlock.ActualHeight;

                Typeface typeface = new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch);
                double fontSize = textBlock.FontSize;
                double pixelsPerDip = VisualTreeHelper.GetDpi(textBlock).PixelsPerDip;

                // Předpokládáme, že používáte monospace font
                string sampleText = "M"; // Máme zvolené "M", protože je to často jeden z nejširších znaků v monospace fontech

                FormattedText formattedText = new FormattedText(
                    sampleText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    new NumberSubstitution(),
                    TextFormattingMode.Display,
                    pixelsPerDip);

                letterWidth = formattedText.Width; // WidthIncludingTrailingWhitespace;
                lineHeight = formattedText.Height;

                Rectangle rect1 = new Rectangle
                {
                    Width = letterWidth * 11 + 1,
                    Height = lineHeight,
                    Stroke = Brushes.Red,
                    StrokeThickness = 1,
                    Fill = null
                };

                Rectangle rect2 = new Rectangle
                {
                    Width = letterWidth * 5 + 2,
                    Height = lineHeight,
                    Stroke = Brushes.Blue,
                    StrokeThickness = 1,
                    Fill = null
                };

                var transform = textBlock.TransformToAncestor(canvas);
                var position = transform.Transform(new Point(0, 0));

                Canvas.SetLeft(rect1, position.X + letterWidth * 10 - 7);
                Canvas.SetTop(rect1, position.Y + lineHeight * 11 + 1);

                Canvas.SetLeft(rect2, position.X + letterWidth * 22 - 4);
                Canvas.SetTop(rect2, position.Y + lineHeight * 15 + 1);

                canvas.Children.Add(rect1);
                canvas.Children.Add(rect2);

            };

            canvas.Children.Clear();
            canvas.Children.Add(textBlock);

        }

    }
}
