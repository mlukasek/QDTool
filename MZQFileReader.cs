using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static QDTool.Utility;

namespace QDTool
{
    internal class MZQFileReader
    {
        public long currentPosition = 0;
        public long totalLength = 0;
        public long bytesRemaining = 0;

        private static bool ValidateStartSign(byte[] startSign)
        {
            return startSign.SequenceEqual(ExpectedStartSign);
        }

        private static bool ValidateCrc(byte[] crc)
        {
            return crc.SequenceEqual(ExpectedCrc);
        }

        private static bool ValidateQDiskHeader(MZQHeader header)
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

        public MZQHeader ReadMZQHeader(BinaryReader reader)
        {
            MZQHeader header = new MZQHeader();

            header.StartSign = reader.ReadBytes(4);
            header.FileBlocksCount = reader.ReadByte();
            header.Crc = reader.ReadBytes(3);

            return header;
        }

        public (MZQFileHeader, MZQFileBody) ReadMzfBlock(BinaryReader reader)
        {

            MZQFileHeader header = new MZQFileHeader();

            header.StartSign = reader.ReadBytes(4); // Předpokládáme, že StartSign má vždy 4 bajty
            header.MzfHeaderSign = reader.ReadByte();
            header.DataSize = reader.ReadUInt16();
            header.MzfFtype = reader.ReadByte();
            header.MzfFname = reader.ReadBytes(16); // Předpokládáme, že MzfFname má vždy 16 bajtů
            header.MzfFnameEnd = reader.ReadByte();
            header.Unused1 = reader.ReadBytes(2); // Předpokládáme, že Unused1 má vždy 2 bajty
            header.MzfSize = reader.ReadUInt16();
            header.MzfStart = reader.ReadUInt16();
            header.MzfExec = reader.ReadUInt16();

            // Načtení prvních 38 bajtů pro MzfHeaderDescription
            header.MzfHeaderDescription = new byte[104]; // Inicializace pole 104 bajty
            byte[] descriptionBytes = reader.ReadBytes(38); // Načtení pouze 38 bajtů
            Array.Copy(descriptionBytes, header.MzfHeaderDescription, descriptionBytes.Length);

            header.Crc = reader.ReadBytes(3); // Předpokládáme, že Crc má vždy 3 bajty

            if (!ValidateQDiskMzfHeader(header))
            {
                throw new InvalidOperationException("Neplatný MZF Header");
            }

            // Načtení začátku MZF Těla pro získání DataSize
            byte[] mzfBodyStartBytes = reader.ReadBytes(7); // Předpokládáme, že prvních 7 bajtů obsahuje StartSign (4 bajty), MzfBodySign (1 bajt) a DataSize (2 bajty)
            ushort bodyDataSize = BitConverter.ToUInt16(mzfBodyStartBytes, 5);

            // Načtení zbytku MZF Těla
            MZQFileBody body = new MZQFileBody
            {
                StartSign = mzfBodyStartBytes.Take(4).ToArray(),
                MzfBodySign = mzfBodyStartBytes[4],
                DataSize = bodyDataSize,
                MzfBody = reader.ReadBytes(bodyDataSize),
                Crc = reader.ReadBytes(3)
            };

            if (!ValidateQDiskMzfBody(body))
            {
                throw new InvalidOperationException("Neplatný MZF Header");
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
                MZQHeader header = ReadMZQHeader(reader);

                if (!ValidateQDiskHeader(header))
                {
                    throw new InvalidOperationException("Neplatný MZQHeader");
                }

                // Read each MZF block
                while ((reader.BaseStream.Position < reader.BaseStream.Length) && (mzfBlocks.Count() < header.FileBlocksCount / 2))
                {
                    var mzfBlock = ReadMzfBlock(reader);
                    mzfBlocks.Add(mzfBlock);
                }

                currentPosition = fs.Position;
                totalLength = fs.Length;
                bytesRemaining = totalLength - currentPosition;
            }

            return mzfBlocks;
        }
        public void WriteMZQHeaderToFile(FileStream fileStream, byte fbCount)
        {
            MZQHeader header;

            header.StartSign = new byte[] { 0x00, 0x16, 0x16, 0xa5 };
            fileStream.Write(header.StartSign, 0, header.StartSign.Length);

            header.FileBlocksCount = fbCount;
            fileStream.WriteByte(header.FileBlocksCount);

            header.Crc = Encoding.ASCII.GetBytes("CRC");
            fileStream.Write(header.Crc, 0, header.Crc.Length);
        }

        public void WriteMZQFileHeaderToFile(FileStream fileStream, MZQFileHeader mzfHeader)
        {
            fileStream.Write(mzfHeader.StartSign, 0, mzfHeader.StartSign.Length);

            fileStream.WriteByte(mzfHeader.MzfHeaderSign);

            var dataSizeBytes = BitConverter.GetBytes(mzfHeader.DataSize);
            fileStream.Write(dataSizeBytes, 0, dataSizeBytes.Length);

            fileStream.WriteByte(mzfHeader.MzfFtype);
            fileStream.Write(mzfHeader.MzfFname, 0, mzfHeader.MzfFname.Length);
            fileStream.WriteByte(mzfHeader.MzfFnameEnd);
            fileStream.Write(mzfHeader.Unused1, 0, mzfHeader.Unused1.Length);

            var mzfSizeBytes = BitConverter.GetBytes(mzfHeader.MzfSize);
            fileStream.Write(mzfSizeBytes, 0, mzfSizeBytes.Length);

            var mzfStartBytes = BitConverter.GetBytes(mzfHeader.MzfStart);
            fileStream.Write(mzfStartBytes, 0, mzfStartBytes.Length);

            var mzfExecBytes = BitConverter.GetBytes(mzfHeader.MzfExec);
            fileStream.Write(mzfExecBytes, 0, mzfExecBytes.Length);

            fileStream.Write(mzfHeader.MzfHeaderDescription, 0, 38); // mzfHeader.MzfHeaderDescription.Length

            fileStream.Write(mzfHeader.Crc, 0, mzfHeader.Crc.Length);
        }

        public void WriteMZQFileBodyToFile(FileStream fileStream, MZQFileBody mzfBody)
        {
            fileStream.Write(mzfBody.StartSign, 0, mzfBody.StartSign.Length);
            fileStream.WriteByte(mzfBody.MzfBodySign);

            var dataSizeBytes = BitConverter.GetBytes(mzfBody.DataSize);
            fileStream.Write(dataSizeBytes, 0, dataSizeBytes.Length);

            fileStream.Write(mzfBody.MzfBody, 0, mzfBody.DataSize);

            fileStream.Write(mzfBody.Crc, 0, mzfBody.Crc.Length);
        }
    }
}
