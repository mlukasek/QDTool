using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using static QDTool.Utility;

namespace QDTool
{
    class QDFFileReader
    {
        public long currentPosition = 0;
        public long totalLength = 0;
        public long bytesRemaining = 0;
        public List<long> positions = new List<long>();

        private static bool ValidateStartSign(byte[] startSign)
        {
            return startSign.SequenceEqual(ExpectedStartSign);
        }

        private static bool ValidateCrc(byte[] crc)
        {
            return crc.SequenceEqual(ExpectedCrc);
        }

        private static bool ValidateQDFHeader(MZQHeader header)
        {
            return ValidateStartSign(header.StartSign) && ValidateCrc(header.Crc);
        }

        private static bool ValidateQDiskMzfHeader(MZQFileHeader header)
        {
            return ValidateStartSign(header.StartSign) && ValidateCrc(header.Crc);
        }

        private static bool ValidateQDiskMzfBody(MZQFileBody body)
        {
            return ValidateStartSign(body.StartSign) && ValidateCrc(body.Crc);
        }

        public bool IsQDFHeader(BinaryReader reader)
        {
            byte[] expectedHeaderBytes = Encoding.ASCII.GetBytes("-QD format-")
                .Concat(Enumerable.Repeat((byte)0xFF, 5)).ToArray();
            byte[] headerBytes = reader.ReadBytes(expectedHeaderBytes.Length);

            return headerBytes.SequenceEqual(expectedHeaderBytes);
        }

        public bool FindStartSequence(BinaryReader reader)
        {
            bool found00 = false; // Indikátor nalezení 0x00
            int countOf16 = 0;    // Počet postupných 0x16 bytů

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte currentByte = reader.ReadByte();
                long currentPosition = reader.BaseStream.Position;

                if (currentPosition == 0x12E8)
                    currentPosition++;

                if (currentByte == 0x00)
                {
                    found00 = true; // Nalezen 0x00, čekáme na 0x16
                    countOf16 = 0;
                }
                else // found00 je true
                {
                    if (found00 && currentByte == 0x16)
                    {
                        countOf16++; // Počítání 0x16 bytů
                    }
                    else if (found00 && currentByte == 0xA5 && countOf16 >= 2)
                    {
                        return true; // Úspěšně nalezená sekvence
                    }
                    else
                    {
                        found00 = false; // Reset, pokud byte není 0x16 nebo 0xA5
                    }
                }
            }

            return false;
        }


        public MZQHeader ReadQDFHeader(BinaryReader reader)
        {
            MZQHeader header = new MZQHeader();
            header.StartSign = ZeroStartSign;
            header.FileBlocksCount = 0;
            header.Crc = ZeroCrc;

            if (IsQDFHeader(reader))
            {
                if (FindStartSequence(reader))
                {
                    positions.Add(reader.BaseStream.Position);
                    positions.Add(3);
                    header.StartSign = ExpectedStartSign;
                    CRC_check(0xA5, true);
                    header.FileBlocksCount = reader.ReadByte();
                    ushort crc = CRC_check(header.FileBlocksCount);
                    ushort readCRC = reader.ReadUInt16();
                    byte crcHi = ReverseBits((byte)(crc & 0xFF));  // reverse bits and swap hi-lo bytes too
                    byte crcLo = ReverseBits((byte)(crc >> 8));
                    ushort calculatedCRC = (ushort)(crcLo + (crcHi << 8));
                    //CRC_check((byte)(readCRC & 0xFF));
                    //CRC_check((byte)(readCRC >> 8));
                    crc = CRC_check(readCRC);
                    header.Crc = ExpectedCrc;
                    if (readCRC != calculatedCRC || crc != 0) 
                    {
                        Array.Clear(header.StartSign, 0, header.StartSign.Length);
                        header.FileBlocksCount = 0;
                        Array.Clear(header.Crc, 0, header.Crc.Length);
                    }
                }
            }

            return header;
        }

        public (MZQFileHeader, MZQFileBody) ReadQDFMzfBlock(BinaryReader reader)
        {

            MZQFileHeader header = new MZQFileHeader();
            MZQFileBody body = new MZQFileBody();

            if (FindStartSequence(reader))
            {
                positions.Add(reader.BaseStream.Position);
                positions.Add(64+5);
                // Načtení jednotlivých členů struktury
                header.StartSign = ExpectedStartSign;
                CRC_check(0xA5, true);
                header.MzfHeaderSign = reader.ReadByte();
                CRC_check(header.MzfHeaderSign);
                header.DataSize = reader.ReadUInt16();
                CRC_check(header.DataSize);
                header.MzfFtype = reader.ReadByte();
                CRC_check(header.MzfFtype);
                header.MzfFname = reader.ReadBytes(16); // Předpokládáme, že MzfFname má vždy 16 bajtů
                for (int i = 0; i < 16; i++)
                {
                    CRC_check(header.MzfFname[i]);
                }
                header.MzfFnameEnd = reader.ReadByte();
                CRC_check(header.MzfFnameEnd);
                header.Unused1 = reader.ReadBytes(2); // Předpokládáme, že Unused1 má vždy 2 bajty
                CRC_check(header.Unused1[0]);
                CRC_check(header.Unused1[1]);
                header.MzfSize = reader.ReadUInt16();
                CRC_check(header.MzfSize);
                header.MzfStart = reader.ReadUInt16();
                CRC_check(header.MzfStart);
                header.MzfExec = reader.ReadUInt16();
                CRC_check(header.MzfExec);

                // Načtení prvních 38 bajtů pro MzfHeaderDescription
                header.MzfHeaderDescription = new byte[104]; // Inicializace pole 104 bajty
                byte[] descriptionBytes = reader.ReadBytes(38); // Načtení pouze 38 bajtů
                Array.Copy(descriptionBytes, header.MzfHeaderDescription, descriptionBytes.Length);
                for (int i = 0; i < 38; i++)
                {
                    CRC_check(header.MzfHeaderDescription[i]);
                }

                // Načtení CRC
                header.Crc = ExpectedCrc;
                ushort readCRC = reader.ReadUInt16();
                ushort crc = CRC_check(readCRC);

                if (crc != 0)
                {
                    header.Crc = ZeroCrc;
                    throw new InvalidOperationException("Invalid CRC in QDF MZF Header");
                }

                if (!ValidateQDiskMzfHeader(header))
                {
                    throw new InvalidOperationException("Neplatný MZF Header");
                }

                if (FindStartSequence(reader))
                {
                    // Načtení začátku MZF Těla pro získání DataSize
                    // byte[] mzfBodyStartBytes = reader.ReadBytes(7); // Předpokládáme, že prvních 7 bajtů obsahuje StartSign (4 bajty), MzfBodySign (1 bajt) a DataSize (2 bajty)
                    // ushort bodyDataSize = // BitConverter.ToUInt16(mzfBodyStartBytes, 5);

                    positions.Add(reader.BaseStream.Position);
                    body.StartSign = ExpectedStartSign;
                    CRC_check(0xA5, true);
                    body.MzfBodySign = reader.ReadByte();
                    CRC_check(body.MzfBodySign);
                    body.DataSize = reader.ReadUInt16();
                    positions.Add(body.DataSize + 5);
                    CRC_check(body.DataSize);
                    body.MzfBody = reader.ReadBytes(body.DataSize);
                    for (int i = 0; i < body.DataSize; i++)
                    {
                        CRC_check(body.MzfBody[i]);
                    }
                    body.Crc = ExpectedCrc;
                    readCRC = reader.ReadUInt16();
                    crc = CRC_check(readCRC);

                    if (crc != 0)
                    {
                        body.Crc = ZeroCrc;
                        throw new InvalidOperationException("Invalid CRC in QDF MZF Body");
                    }

                    if (!ValidateQDiskMzfBody(body))
                    {
                        throw new InvalidOperationException("Invlid QD/MZF Header");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Missing data block after header block.");
                }
            }
            return (header, body);
        }

        public List<(MZQFileHeader, MZQFileBody)> ReadFile(string filePath)
        {
            List<(MZQFileHeader, MZQFileBody)> mzfBlocks = new List<(MZQFileHeader, MZQFileBody)>();

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // Read the file header
                MZQHeader header = ReadQDFHeader(reader);

                if (!ValidateQDFHeader(header))
                {
                    throw new InvalidOperationException("Neplatný QDFHeader");
                }

                //// Read each MZF block
                while ((reader.BaseStream.Position < reader.BaseStream.Length) && (mzfBlocks.Count() < header.FileBlocksCount / 2))
                {
                    var mzfBlock = ReadQDFMzfBlock(reader);
                    if(mzfBlock.Item1.MzfFname != null) 
                    {
                        mzfBlocks.Add(mzfBlock);
                    }
                }

                currentPosition = fs.Position;
                totalLength = fs.Length;
                bytesRemaining = totalLength - currentPosition;
            }

            return mzfBlocks;
        }

        public void WriteBytesToStream(FileStream fileStream, byte val, long repeat)
        {
            for (long i = 0; i < repeat; i++)
            {
                fileStream.WriteByte(val);
            }
        }

        public void WriteQDFHeaderToFile(FileStream fileStream, byte fbCount)
        {
            // BinaryWriter writer = new BinaryWriter(fileStream);

            byte[] QDFFileSignature = Encoding.ASCII.GetBytes("-QD format-")
                .Concat(Enumerable.Repeat((byte)0xFF, 5)).ToArray();
            fileStream.Write(QDFFileSignature, 0, QDFFileSignature.Length);

            WriteBytesToStream(fileStream, 0x00, 0x12EA - 16);
            WriteBytesToStream(fileStream, 0x16, 9);
            fileStream.WriteByte(0xA5);
            CRC_check(0xA5, true);

            fileStream.WriteByte(fbCount);
            ushort crc = CRC_check(fbCount);

            byte crcHi = ReverseBits((byte)(crc & 0xFF));  // reverse bits and swap hi-lo bytes too
            byte crcLo = ReverseBits((byte)(crc >> 8));
            ushort calculatedCRC = (ushort)(crcLo + (crcHi << 8));
            fileStream.WriteByte(crcLo);
            fileStream.WriteByte(crcHi);

            WriteBytesToStream(fileStream, 0x16, 6);
            WriteBytesToStream(fileStream, 0x00, 2794);

            //header.Crc = Encoding.ASCII.GetBytes("CRC");
            //fileStream.Write(header.Crc, 0, header.Crc.Length);
        }

        public void WriteQDFFileHeaderToFile(FileStream fileStream, MZQFileHeader mzfHeader)
        {
            WriteBytesToStream(fileStream, 0x16, 10);
            fileStream.WriteByte(0xA5);
            CRC_check(0xA5, true);

            fileStream.WriteByte(mzfHeader.MzfHeaderSign);
            CRC_check(mzfHeader.MzfHeaderSign);

            var dataSizeBytes = BitConverter.GetBytes(mzfHeader.DataSize);
            fileStream.Write(dataSizeBytes, 0, dataSizeBytes.Length);
            CRC_check(dataSizeBytes, 0, dataSizeBytes.Length);

            fileStream.WriteByte(mzfHeader.MzfFtype);
            CRC_check(mzfHeader.MzfFtype);
            fileStream.Write(mzfHeader.MzfFname, 0, mzfHeader.MzfFname.Length);
            CRC_check(mzfHeader.MzfFname, 0, mzfHeader.MzfFname.Length);
            fileStream.WriteByte(mzfHeader.MzfFnameEnd);
            CRC_check(mzfHeader.MzfFnameEnd);
            fileStream.Write(mzfHeader.Unused1, 0, mzfHeader.Unused1.Length);
            CRC_check(mzfHeader.Unused1, 0, mzfHeader.Unused1.Length);

            var mzfSizeBytes = BitConverter.GetBytes(mzfHeader.MzfSize);
            fileStream.Write(mzfSizeBytes, 0, mzfSizeBytes.Length);
            CRC_check(mzfSizeBytes, 0, mzfSizeBytes.Length);

            var mzfStartBytes = BitConverter.GetBytes(mzfHeader.MzfStart);
            fileStream.Write(mzfStartBytes, 0, mzfStartBytes.Length);
            CRC_check(mzfStartBytes, 0, mzfStartBytes.Length);

            var mzfExecBytes = BitConverter.GetBytes(mzfHeader.MzfExec);
            fileStream.Write(mzfExecBytes, 0, mzfExecBytes.Length);
            CRC_check(mzfExecBytes, 0, mzfExecBytes.Length);

            fileStream.Write(mzfHeader.MzfHeaderDescription, 0, 38); // mzfHeader.MzfHeaderDescription.Length
            ushort crc = CRC_check(mzfHeader.MzfHeaderDescription, 0, 38);

            byte crcHi = ReverseBits((byte)(crc & 0xFF));  // reverse bits and swap hi-lo bytes too
            byte crcLo = ReverseBits((byte)(crc >> 8));
            ushort calculatedCRC = (ushort)(crcLo + (crcHi << 8));
            fileStream.WriteByte(crcLo);
            fileStream.WriteByte(crcHi);

            WriteBytesToStream(fileStream, 0x16, 7);
            WriteBytesToStream(fileStream, 0x00, 254);
        }

        public void WriteQDFFileBodyToFile(FileStream fileStream, MZQFileBody mzfBody)
        {
            WriteBytesToStream(fileStream, 0x16, 10);
            fileStream.WriteByte(0xA5);
            CRC_check(0xA5, true);

            fileStream.WriteByte(mzfBody.MzfBodySign);
            CRC_check(mzfBody.MzfBodySign);

            var dataSizeBytes = BitConverter.GetBytes(mzfBody.DataSize);
            fileStream.Write(dataSizeBytes, 0, dataSizeBytes.Length);
            CRC_check(dataSizeBytes, 0, dataSizeBytes.Length);

            fileStream.Write(mzfBody.MzfBody, 0, mzfBody.DataSize);
            ushort crc = CRC_check(mzfBody.MzfBody, 0, mzfBody.DataSize);

            byte crcHi = ReverseBits((byte)(crc & 0xFF));  // reverse bits and swap hi-lo bytes too
            byte crcLo = ReverseBits((byte)(crc >> 8));
            ushort calculatedCRC = (ushort)(crcLo + (crcHi << 8));
            fileStream.WriteByte(crcLo);
            fileStream.WriteByte(crcHi);

            WriteBytesToStream(fileStream, 0x16, 7);
            WriteBytesToStream(fileStream, 0x00, 256);
        }
    }
}
