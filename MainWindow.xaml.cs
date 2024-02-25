using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static QDTool.Utility;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MZQHeader
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] StartSign; // 4 bytes, 0x00, 0x16, 0x16, 0xa5
    public byte FileBlocksCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] Crc; // 3 bytes, C, R, C
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MZQFileHeader
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] StartSign; // 4 bytes, 0x00, 0x16, 0x16, 0xa5
    public byte MzfHeaderSign; // 0x00
    public ushort DataSize; // 0x40, 0x00 = 64 bytes
    public byte MzfFtype;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] MzfFname; // 16 bytes
    public byte MzfFnameEnd; // 0x0d
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public byte[] Unused1; // 2 bytes, 0x00
    public ushort MzfSize;
    public ushort MzfStart;
    public ushort MzfExec;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 104)]
    public byte[] MzfHeaderDescription; // MZQ has 38 bytes only, first 38 bytes from mzf description
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] Crc; // 3 bytes, C, R, C
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MZQFileBody
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public byte[] StartSign; // 4 bytes, 0x00, 0x16, 0x16, 0xa5
    public byte MzfBodySign; // 0x05
    public ushort DataSize; // body_size
    // Zde může být potřeba dynamické alokace pro MzfBody v závislosti na skutečné velikosti
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 65535)]
    public byte[] MzfBody; // body [body_size], maximum size 65535 bytes
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] Crc; // 3 bytes, C, R, C
}

public class MzfDisplayData
{
    public string MzfFtypeName { get; set; } = string.Empty;
    public string MzfFname { get; set; } = string.Empty;
    public ushort MzfSize { get; set; }
    public string MzfSizeHex
    {
        get { return $"0x{MzfSize:X4}"; }
    }
    public string MzfStartHex { get; set; } = string.Empty;
    public string MzfExecHex { get; set; } = string.Empty;
    public string MzfHeaderDescription { get; set; } = string.Empty;
}

namespace QDTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<(MZQFileHeader, MZQFileBody)> mzfBlocks = new List<(MZQFileHeader, MZQFileBody)>();
        string actFileName = string.Empty;

        public ObservableCollection<MzfDisplayData> MzfDisplayDataCollection { get; set; }

        public MainWindow()
        {
            InitializeComponent();
            MzfDisplayDataCollection = new ObservableCollection<MzfDisplayData>();
            MzfDataGrid.ItemsSource = MzfDisplayDataCollection;
            this.Title = "QDTool";
            viewButton.IsEnabled = false; // Zakážem některá tlačítka při spuštění
            moveUpButton.IsEnabled = false;
            moveDownButton.IsEnabled = false;
            exportButton.IsEnabled = false;
            exportAllButton.IsEnabled = false;
            deleteButton.IsEnabled = false;
            clearAllButton.IsEnabled = false;
            saveButton.IsEnabled = false;
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy; // Změňte ukazatel, aby uživatel věděl, že soubor může být zde upuštěn
            else
                e.Effects = DragDropEffects.None; // Jinak neumožněte drop
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files != null && files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        AddFile(file);
                    }

                    if (this.Title == "QDTool")
                    {
                        string fileName = System.IO.Path.GetFileName(files[0]); // s prvniho souboru vezmeme title, pokud neni nic nacteno
                        this.Title = $"QDTool - {fileName}";
                        actFileName = fileName;
                    }
                    saveButton.IsEnabled = true;
                    exportAllButton.IsEnabled = true;
                    clearAllButton.IsEnabled = true;
                    UpdateStatus();
                }
            }
        }
        private void LoadDataToGrid(List<(MZQFileHeader, MZQFileBody)> mzfBlocks)
        {
            foreach (var block in mzfBlocks)
            {
                var header = block.Item1;
                var displayData = new MzfDisplayData
                {
                    MzfFtypeName = ConvertFtypeToDescription(header.MzfFtype),
                    MzfFname = ConvertMzfNameToASCIIString(header.MzfFname),
                    MzfSize = header.MzfSize,
                    MzfStartHex = $"0x{header.MzfStart:X4}",
                    MzfExecHex = $"0x{header.MzfExec:X4}",
                    MzfHeaderDescription = ConvertMzfNameToASCIIString(header.MzfHeaderDescription)
                };
                MzfDisplayDataCollection.Add(displayData);
            }
        }
 
        private void UpdateStatus()
        {
            int totalSize = 0;
            int sizeOnQDF = 4852 + 3;
            int sizeOnMZQ = 7;
            bool first = true;

            foreach (var block in mzfBlocks)
            {
                MZQFileHeader header = block.Item1;
                MZQFileBody body = block.Item2;
                totalSize += header.MzfSize;
                if (first)
                {
                    first = false;
                    sizeOnQDF += 2810 + 69 + 273 + header.MzfSize + 5;
                    sizeOnMZQ += 4 + 70 + 4 + header.MzfSize + 6;
                }
                else
                {
                    sizeOnQDF += 273 + 69 + 273 + header.MzfSize + 5;
                    sizeOnMZQ += 4 + 70 + 4 + header.MzfSize + 6;
                }
            }
            infoText.Content = $"Total {mzfBlocks.Count} files contain {totalSize} bytes, est. {(float)sizeOnQDF/819.36:F0}% of QDF or {(float)sizeOnMZQ / 614.71:F0}% of MZQ.";
        }

        private void button_Click_Open(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All tape/QD files |*.mzt;*.mzf;*.mzq;*.qdf|Quickdisk file (*.mzq)|*.mzq|Multiple files tape (*.mzt)|*.mzt|Single tape file (*.mzf)|*.mzf|Quickdisk file (*.qdf)|*.qdf|All files(*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                mzfBlocks.Clear();

                // string currentDirectory = Directory.GetCurrentDirectory();
                string filePath = openFileDialog.FileName;
                AddFile(filePath);

                string fileName = System.IO.Path.GetFileName(filePath);
                this.Title = $"QDTool - {fileName}";
                actFileName = fileName;

                exportAllButton.IsEnabled = true;
                clearAllButton.IsEnabled = true;
                saveButton.IsEnabled = true;

                UpdateStatus();
            }
        }

        private void button_Click_Save(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Quickdisk file (*.qdf)|*.qdf|Quickdisk file (*.mzq)|*.mzq|Multiple files tape (*.mzt)|*.mzt|Single tape file (*.mzf)|*.mzf|All files(*.*)|*.*";
            string filenameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(actFileName);
            saveFileDialog.FileName = filenameWithoutExtension;
            string extension = System.IO.Path.GetExtension(actFileName).ToLower();
            int filterIndex = 1; // Default to the first filter
            if (extension == ".mzq")
            {
                filterIndex = 2;
            }
            else if (extension == ".mzt")
            {
                filterIndex = 3;
            }
            else if (extension == ".mzf")
            {
                filterIndex = 4;
            }
            saveFileDialog.FilterIndex = filterIndex;

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                string fileExtension = System.IO.Path.GetExtension(filePath).ToLower();

                if (fileExtension == ".mzq")
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        MZQFileReader mzqf = new MZQFileReader();
                        mzqf.WriteMZQHeaderToFile(fileStream, (byte)(mzfBlocks.Count * 2)); // mělo by se zkontrolovat, jestli tam toho náhodou není moc ???
                        foreach (var (header, body) in mzfBlocks)
                        {
                            mzqf.WriteMZQFileHeaderToFile(fileStream, header);
                            mzqf.WriteMZQFileBodyToFile(fileStream, body);
                        }
                    }
                }
                else if (fileExtension == ".qdf")
                {
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        QDFFileReader qdfr = new QDFFileReader();
                        qdfr.WriteQDFHeaderToFile(fileStream, (byte)(mzfBlocks.Count * 2)); // mělo by se zkontrolovat, jestli tam toho náhodou není moc ???
                        foreach (var (header, body) in mzfBlocks)
                        {
                            qdfr.WriteQDFFileHeaderToFile(fileStream, header);
                            qdfr.WriteQDFFileBodyToFile(fileStream, body);
                        }
                        long currentSize = fileStream.Length; // Aktuální velikost streamu
                        long bytesToWrite = 81936 - currentSize; // Počet bytů, které je třeba doplnit
                        qdfr.WriteBytesToStream(fileStream, 0x00, bytesToWrite);
                    }
                }
                else if (fileExtension == ".mzt" || fileExtension == ".mzf")
                {
                    if(fileExtension == ".mzf" && mzfBlocks.Count > 1)
                    {
                        MessageBox.Show("MZF file should contain only one tape file. Please use Export button or Save as MZT file.", "MZF file limitation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    else
                    {
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        {
                            MZTFileReader mztfr = new MZTFileReader();
                            foreach (var (header, body) in mzfBlocks)
                            {
                                mztfr.WriteMZFFileHeaderToFile(fileStream, header);
                                mztfr.WriteMZFFileBodyToFile(fileStream, body);
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show($"I do not know how to save file with extension {fileExtension} (yet).", "Unknown file extension", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void button_Click_View(object sender, RoutedEventArgs e)
        {
            HexBrowser hexBrowserWindow = new HexBrowser();

            int selectedIndex = MzfDataGrid.SelectedIndex; // Získání indexu vybraného řádku

            if (selectedIndex >= 0 && selectedIndex < mzfBlocks.Count)
            {
                var selectedPair = mzfBlocks[selectedIndex];

                hexBrowserWindow.ShowHexDump(selectedPair);
            }

            hexBrowserWindow.ShowDialog(); // Zobrazí HexBrowser jako modální dialogové okno
        }

        private void MzfDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Tlačítko "View" bude aktivní pouze pokud je vybrán nějaký řádek
            viewButton.IsEnabled = MzfDataGrid.SelectedItem != null;
            exportButton.IsEnabled = MzfDataGrid.SelectedItem != null;
            deleteButton.IsEnabled = MzfDataGrid.SelectedItem != null;

            int selectedIndex = MzfDataGrid.SelectedIndex;
            moveUpButton.IsEnabled = selectedIndex > 0 && mzfBlocks.Count > 1;
            moveDownButton.IsEnabled = selectedIndex < mzfBlocks.Count - 1 && selectedIndex >= 0;
        }

        private void button_Click_Up(object sender, RoutedEventArgs e)
        {
            int selectedIndex = MzfDataGrid.SelectedIndex;
            if (selectedIndex > 0)
            {
                var itemToMoveUp = mzfBlocks[selectedIndex];
                mzfBlocks.RemoveAt(selectedIndex);
                mzfBlocks.Insert(selectedIndex - 1, itemToMoveUp);

                //MzfDataGrid.ItemsSource = null;
                //MzfDataGrid.ItemsSource = mzfBlocks;

                MzfDisplayDataCollection.Clear();
                LoadDataToGrid(mzfBlocks);

                MzfDataGrid.SelectedIndex = selectedIndex - 1;
                MzfDataGrid.Focus();
            }
        }

        private void button_Click_Down(object sender, RoutedEventArgs e)
        {
            int selectedIndex = MzfDataGrid.SelectedIndex;
            if (selectedIndex < mzfBlocks.Count - 1 && selectedIndex >= 0)
            {
                var itemToMoveDown = mzfBlocks[selectedIndex];
                mzfBlocks.RemoveAt(selectedIndex);
                mzfBlocks.Insert(selectedIndex + 1, itemToMoveDown);

                //MzfDataGrid.ItemsSource = null;
                //MzfDataGrid.ItemsSource = mzfBlocks;

                MzfDisplayDataCollection.Clear();
                LoadDataToGrid(mzfBlocks);

                //var currentSource = MzfDataGrid.ItemsSource;
                //MzfDataGrid.ItemsSource = null;
                //MzfDataGrid.ItemsSource = currentSource;

                MzfDataGrid.SelectedIndex = selectedIndex + 1;
                MzfDataGrid.Focus();
            }
        }

        private void AddFile(string filePath)
        {
            List<(MZQFileHeader, MZQFileBody)> mzfBlocksToAdd = new List<(MZQFileHeader, MZQFileBody)>();

            string fileExtension = System.IO.Path.GetExtension(filePath).ToLower();

            if (fileExtension == ".mzq")
            {
                //mzfBlocks.Clear();
                MzfDisplayDataCollection.Clear();

                try
                {
                    MZQFileReader qdfr = new MZQFileReader();
                    mzfBlocksToAdd = qdfr.ReadFile(filePath);
                    mzfBlocks.AddRange(mzfBlocksToAdd);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error reading file", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                LoadDataToGrid(mzfBlocks);

            }
            else if (fileExtension == ".qdf")
            {
                //mzfBlocks.Clear();
                MzfDisplayDataCollection.Clear();

                try
                {
                    QDFFileReader qdfr = new QDFFileReader();
                    mzfBlocksToAdd = qdfr.ReadFile(filePath);
                    mzfBlocks.AddRange(mzfBlocksToAdd);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error reading file", MessageBoxButton.OK, MessageBoxImage.Error);
                }


                LoadDataToGrid(mzfBlocks);

            }
            else if (fileExtension == ".mzt" || fileExtension == ".mzf")
            {
                //mzfBlocks.Clear();
                MzfDisplayDataCollection.Clear();

                try
                {
                    MZTFileReader mztfr = new MZTFileReader();
                    mzfBlocksToAdd = mztfr.ReadMztFile(filePath);
                    mzfBlocks.AddRange(mzfBlocksToAdd);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error reading file", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            LoadDataToGrid(mzfBlocks);
            }
            else
            {
                MessageBox.Show($"I do not know how to process file with extension {fileExtension} (yet).", "Unknown file extension", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }

        private void button_Click_Add(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Quickdisk file (*.mzq)|*.mzq|Multiple files tape (*.mzt)|*.mzt|Single tape file (*.mzf)|*.mzf|Quickdisk file (*.qdf)|*.qdf|All files(*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                // string currentDirectory = Directory.GetCurrentDirectory();
                string filePath = openFileDialog.FileName;
                AddFile(filePath);

                if(this.Title == "QDTool")
                {
                    string fileName = System.IO.Path.GetFileName(filePath);
                    this.Title = $"QDTool - {fileName}";
                    actFileName = fileName;
                }

                saveButton.IsEnabled = true;
                exportAllButton.IsEnabled = true;
                clearAllButton.IsEnabled = true;
                UpdateStatus();
            }
        }

        private void button_Click_Export(object sender, RoutedEventArgs e)
        {
            int selectedIndex = MzfDataGrid.SelectedIndex; // Získání indexu vybraného řádku

            if (selectedIndex >= 0 && selectedIndex < mzfBlocks.Count)
            {
                var item = mzfBlocks[selectedIndex];

                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Single tape file (*.mzf)|*.mzf";
                MZQFileHeader header = item.Item1;
                MZQFileBody body = item.Item2;
                saveFileDialog.FileName = ConvertMzfNameToASCIIString(header.MzfFname);

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;

                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        MZTFileReader mztfr = new MZTFileReader();
                        mztfr.WriteMZFFileHeaderToFile(fileStream, header);
                        mztfr.WriteMZFFileBodyToFile(fileStream, body);
                    }
                }
            }
        }

        private void check_QDF_Files()
        {
            QDFFileReader qdfr = new QDFFileReader();

            string directoryPath = @"H:\Sharp\QDC\";
            string searchPattern = "*.qdf"; // Hledá všechny soubory s příponou .qdf
            string outFilePath = "output.txt";
            if (File.Exists(outFilePath))
            {
                File.Delete(outFilePath);
            }

            try
            {
                // Search for all .qdf files in subfolders
                string[] files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);

                foreach (string filePath in files)
                {
                    mzfBlocks.Clear();
                    qdfr.positions.Clear();
                    int cnt = 1;
                    long sumDta = 0;
                    long lastPos = 0;
                    bool firstPass = true;
                    int blocks = -1;

                    mzfBlocks = qdfr.ReadFile(filePath);

                    using (StreamWriter writer = new StreamWriter(outFilePath, append: true))
                    {
                        writer.WriteLine(filePath);

                        foreach (long position in qdfr.positions)
                        {
                            if (cnt++ % 2 == 1)
                            {
                                writer.Write($"{position:X5} ");
                            }
                            else
                            {
                                writer.Write($"({position}) ");
                            }

                            if (firstPass)
                            {
                                firstPass = false;
                                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                                {
                                    fileStream.Seek(position, SeekOrigin.Begin);
                                    blocks = fileStream.ReadByte();
                                }
                                writer.Write($"[{blocks}] ");
                                if (blocks != mzfBlocks.Count * 2)
                                {
                                    writer.Write($"BAD BLOCK COUNT ");
                                }
                            }

                            if (cnt % 2 == 0)
                            {
                                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                                {
                                    fileStream.Seek(position - 1, SeekOrigin.Begin);
                                    byte blockStart = (byte)fileStream.ReadByte();
                                    if (blockStart != 0xA5) // this should never happen
                                        writer.Write($"BLOCKSTART=0x{blockStart:X2} ");
                                }
                            }
                        }
                        writer.WriteLine();

                        foreach (long position in qdfr.positions)
                        {
                            if (cnt++ % 2 == 1)
                            {
                                writer.Write(position - sumDta - lastPos);
                                lastPos = position - sumDta;
                                writer.Write(" ");
                            }
                            else
                            {
                                sumDta += position;
                            }
                        }
                        writer.WriteLine();
                    }
                }
                MessageBox.Show("Done");
            }
            catch (Exception ex)
            {
                // Zachytávání a zpracování výjimek
                Console.WriteLine("Došlo k chybě: " + ex.Message);
            }
        }

        private void button_Click_Delete(object sender, RoutedEventArgs e)
        {
            int selectedIndex = MzfDataGrid.SelectedIndex; // Získání indexu vybraného řádku

            if (selectedIndex >= 0 && selectedIndex < mzfBlocks.Count)
            {
                mzfBlocks.RemoveAt(selectedIndex);

                MzfDisplayDataCollection.Clear();
                LoadDataToGrid(mzfBlocks);

                UpdateStatus();

                //MzfDataGrid.SelectedIndex = selectedIndex - 1;
                //MzfDataGrid.Focus();
            }
        }

        private void button_Click_ExportAll(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                ValidateNames = false,
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select folder",
                Filter = "Folders|*.this.directory",
                DereferenceLinks = true // odkazy na složky budou následovány
            };

            if (dialog.ShowDialog() == true)
            {
                string? exportPath = System.IO.Path.GetDirectoryName(dialog.FileName);

                if(exportPath == null)
                {
                    MessageBox.Show("Export path cannot be empty", "Invalid path", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    foreach (var (header, body) in mzfBlocks)
                    {
                        string fileName = ConvertMzfNameToASCIIString(header.MzfFname) + ".mzf";
                        string filePath = System.IO.Path.Combine(exportPath, fileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                        {
                            MZTFileReader mztfr = new MZTFileReader();
                            mztfr.WriteMZFFileHeaderToFile(fileStream, header);
                            mztfr.WriteMZFFileBodyToFile(fileStream, body);
                        }
                    }
                }
            }
        }

        private void button_Click_ClearAll(object sender, RoutedEventArgs e)
        {
            mzfBlocks.Clear();
            MzfDisplayDataCollection.Clear();
            exportAllButton.IsEnabled = false;
            LoadDataToGrid(mzfBlocks);
            UpdateStatus();
        }

        private void button_Click_About(object sender, RoutedEventArgs e)
        {
            AboutDialog aboutDialog = new AboutDialog();
            aboutDialog.ShowDialog();
        }
    }
}
