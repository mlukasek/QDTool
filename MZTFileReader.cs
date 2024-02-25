using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.RightsManagement;
using System.Text;
using System.Threading.Tasks;
using static QDTool.Utility;

namespace QDTool
{
    internal class MZTFileReader
    {
        public long currentPosition = 0;
        public long totalLength = 0;
        public long bytesRemaining = 0;

        public List<(MZQFileHeader, MZQFileBody)> ReadMztFile(string filePath)
        {
            List<(MZQFileHeader, MZQFileBody)> mzfBlocks = new List<(MZQFileHeader, MZQFileBody)>();

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // Read each MZF File
                while ((reader.BaseStream.Position + 128 < reader.BaseStream.Length))
                {
                    var mzfBlock = ReadMzfFile(reader);
                    mzfBlocks.Add(mzfBlock);
                }

                currentPosition = fs.Position;
                totalLength = fs.Length;
                bytesRemaining = totalLength - currentPosition;
            }

            return mzfBlocks;
        }

        public (MZQFileHeader, MZQFileBody) ReadMzfFile(BinaryReader reader)
        {

            MZQFileHeader header = new MZQFileHeader();

            // Načtení jednotlivých členů struktury
            header.StartSign = ExpectedStartSign; // zadny tu neni, ale vyplnime
            header.MzfHeaderSign = 0x00;
            header.DataSize = 0x0040; // (ushort)(header.MzfSize + 128); // ma vyznam asi jen u MZQ - ??? - aaa
            header.MzfFtype = reader.ReadByte();
            header.MzfFname = reader.ReadBytes(16);
            header.MzfFnameEnd = reader.ReadByte();
            header.Unused1 = ExpectedUnused; // zadny tu neni, ale vyplnime
            header.MzfSize = reader.ReadUInt16();
            header.MzfStart = reader.ReadUInt16();
            header.MzfExec = reader.ReadUInt16();

            // Načtení celych 104 bajtů pro MzfHeaderDescription
            header.MzfHeaderDescription = new byte[104]; // Inicializace pole 104 bajty
            byte[] descriptionBytes = reader.ReadBytes(104); // Načtení tady 104 bajtů
            Array.Copy(descriptionBytes, header.MzfHeaderDescription, descriptionBytes.Length);

            // Načtení CRC
            header.Crc = ExpectedCrc; // zadny tu neni, ale vyplnime

            // Načtení zbytku MZF Těla
            MZQFileBody body = new MZQFileBody
            {
                StartSign = ExpectedStartSign,
                MzfBodySign = 0x05,
                DataSize = header.MzfSize, // ??? - aaa
                MzfBody = reader.ReadBytes(header.MzfSize),
                Crc = ExpectedCrc
            };

            return (header, body);
        }

        public void WriteMZFFileHeaderToFile(FileStream fileStream, MZQFileHeader mzfHeader)
        {
            fileStream.WriteByte(mzfHeader.MzfFtype);
            fileStream.Write(mzfHeader.MzfFname, 0, mzfHeader.MzfFname.Length);
            fileStream.WriteByte(mzfHeader.MzfFnameEnd);

            var mzfSizeBytes = BitConverter.GetBytes(mzfHeader.MzfSize);
            fileStream.Write(mzfSizeBytes, 0, mzfSizeBytes.Length);

            var mzfStartBytes = BitConverter.GetBytes(mzfHeader.MzfStart);
            fileStream.Write(mzfStartBytes, 0, mzfStartBytes.Length);

            var mzfExecBytes = BitConverter.GetBytes(mzfHeader.MzfExec);
            fileStream.Write(mzfExecBytes, 0, mzfExecBytes.Length);

            fileStream.Write(mzfHeader.MzfHeaderDescription, 0, 104);
        }

        public void WriteMZFFileBodyToFile(FileStream fileStream, MZQFileBody mzfBody)
        {
            fileStream.Write(mzfBody.MzfBody, 0, mzfBody.DataSize);
        }

    }
}
