using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QDTool
{
    internal class Utility
    {
        public static readonly byte[] ExpectedStartSign = { 0x00, 0x16, 0x16, 0xa5 };
        public static readonly byte[] ExpectedCrc = { (byte)'C', (byte)'R', (byte)'C' };
        public static readonly byte[] ExpectedUnused = { 0x00, 0x00 };
        public static readonly byte[] ZeroStartSign = { 0x00, 0x00, 0x00, 0x00 };
        public static readonly byte[] ZeroCrc = { 0x00, 0x00, 0x00 };

        public static readonly byte[] SharpASCII = {
            (byte)'_', (byte)' ', (byte)'e', (byte)' ', (byte)'~', (byte)' ', (byte)'t', (byte)'g',
            (byte)'h', (byte)' ', (byte)'b', (byte)'x', (byte)'d', (byte)'r', (byte)'p', (byte)'c',
            (byte)'q', (byte)'a', (byte)'z', (byte)'w', (byte)'s', (byte)'u', (byte)'i', (byte)' ',
            (byte)' ', (byte)'k', (byte)'f', (byte)'v', (byte)' ', (byte)' ', (byte)' ', (byte)'j',
            (byte)'n', (byte)' ', (byte)' ', (byte)'m', (byte)' ', (byte)' ', (byte)' ', (byte)'o',
            (byte)'l', (byte)' ', (byte)' ', (byte)' ', (byte)' ', (byte)'y', (byte)'{', (byte)' ',
            (byte)'|' };

        public static byte FromSHASCII(byte c)
        {
            if (c <= 0x5d) return c;
            if (c == 0x80) return (byte)'}';
            if (c < 0x90 || c > 0xc0) return (byte)' '; // z neznámých znaků uděláme ' '
            return SharpASCII[c - 0x90];
        }

        public static byte ToSHASCII(byte c)
        {
            if (c <= 0x5d) return c;
            if (c == (byte)'}') return 0x80;
            for (int i = 0; i < SharpASCII.Length; i++)
            {
                if (c == SharpASCII[i])
                {
                    return (byte)(i + 0x90);
                }
            }
            return (byte)' ';  // z neznámých znaků uděláme ' '
        }

        public static string ConvertMzfNameToASCIIString(byte[] bytes)
        {
            var convertedBytes = bytes.Select(b => FromSHASCII(b)).ToArray();
            int endIndex = Array.IndexOf(convertedBytes, (byte)0x0D);
            if (endIndex == -1)
            {
                endIndex = convertedBytes.Length;
            }
            return Encoding.ASCII.GetString(convertedBytes, 0, endIndex);
        }

        private static ushort crc = 0;

        public static ushort CRC_check(byte data, bool initialize = false)
        {
            if (initialize)
            {
                crc = 0x0000;
            }

            byte data_lsb = data;
            for (int i = 0; i < 8; i++)
            {
                byte exr = (byte)(data_lsb & 1);
                data_lsb >>= 1;

                if ((crc & 0x8000) != 0)
                {
                    exr ^= 1;
                }

                crc <<= 1;
                if (exr != 0)
                {
                    crc ^= 0x8005;
                }
            }

            return crc;
        }

        public static ushort CRC_check(ushort data, bool initialize = false)
        {
            CRC_check((byte)(data & 0xFF), initialize);
            return CRC_check((byte)(data >> 8));
        }

        public static ushort CRC_check(byte[] buffer, int offset, int count, bool initialize = false)
        {
            bool init = initialize;

            for (int i = offset; i < offset + count; i++)
            {
                CRC_check(buffer[i], init);
                init = false;
            }

            return crc;
        }

        public static byte ReverseBits(byte b)
        {
            b = (byte)((b & 0x55) << 1 | (b >> 1) & 0x55);
            b = (byte)((b & 0x33) << 2 | (b >> 2) & 0x33);
            b = (byte)((b & 0x0F) << 4 | (b >> 4) & 0x0F);
            return b;
        }

        public static string ConvertFtypeToDescription(byte ftype)
        {
            switch (ftype)
            {
                case 0x01: return "OBJ"; // Executable
                case 0x02: return "BTX"; // BASIC text
                case 0x03: return "BSD"; // BASIC data
                case 0x04: return "BRD";
                case 0x05: return "RB "; // BASIC program
                case 0x06: return "ASC";
                case 0x07: return "LIB";
                case 0x08: return "PTX";
                case 0x09: return "PSD";
                case 0x0A: return "SYS";
                case 0x0B: return "GR ";
                case 0x0C: return "LOG";
                case 0x0D: return "PIC";
                case 0x41: return "AS1"; // AREM assembler file old version
                case 0x42: return "AS2"; // AREM assembler file new version
                case 0x44: return "DZ2"; // DZ80 assembler file
                case 0x58: return "XB1"; // XBC source file
                case 0x94: return "TXT"; // TEXY
                case 0x95: return "LSP"; // LISP
                case 0xa0: return "PTX"; // PASCAL
                case 0xa1: return "PSD"; // PASCAL data file
                case 0xfe: return "FET"; // FET text file
                default: return "???"; // other/unkown
            }
        }

    }
}
