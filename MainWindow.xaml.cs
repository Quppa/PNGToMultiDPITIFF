namespace PNGToMultiDPITIFFPublic
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.IO;
    using System.Collections.ObjectModel;
    using System.Runtime.InteropServices;

    using Microsoft.Win32;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public ObservableCollection<InputFile> InputFiles { get; set; }

        public string OutputDirectory { get; set; }

        private const int MAXIMAGESIZE = 64;

        private int SystemDPIX, SystemDPIY;

        private const int LOGPIXELSX = 88, LOGPIXELSY = 90;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        public MainWindow()
        {
            InitializeComponent();

            SystemDPIX = GetDeviceCaps(GetDC(IntPtr.Zero), LOGPIXELSX);
            SystemDPIY = GetDeviceCaps(GetDC(IntPtr.Zero), LOGPIXELSY);

            InputFiles = new ObservableCollection<InputFile>();

            InputFiles.CollectionChanged += this.InputFiles_CollectionChanged;

            //InputFiles[0] = new InputFile() { Image = new BitmapImage(new Uri(@"D:\Programming\Misc\PNGToMultiDPITIFF\PNGToMultiDPITIFF\Exit-32.png")), DPI = "96", Filename = "Exit-32.png", FilePath = @"D:\Programming\Misc\PNGToMultiDPITIFF\PNGToMultiDPITIFF\Exit-32.png" };

            this.DataContext = this;
        }

        void InputFiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ObservableCollection<InputFile> collection = sender as ObservableCollection<InputFile>;
            if (collection != null)
            {
                if (collection.Count > 0)
                {
                    ClearFilesButton.IsEnabled = true;
                    ProcessFilesButton.IsEnabled = true;
                }
                else
                {
                    ClearFilesButton.IsEnabled = false;
                    ProcessFilesButton.IsEnabled = false;
                }
            }
        }

        private void AddFiles(string[] paths)
        {
            string longestprefix = "";

            foreach (string droppedFilePath in paths)
            {
                if (IsInInputFiles(droppedFilePath))
                    continue;

                BitmapImage originalImage;

                try
                {
                    originalImage = new BitmapImage();
                    originalImage.BeginInit();
                    originalImage.CacheOption = BitmapCacheOption.OnLoad;
                    originalImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    originalImage.UriSource = new Uri(droppedFilePath);
                    originalImage.EndInit();
                }
                catch (Exception)
                {
                    continue;
                }

                int originalWidth = originalImage.PixelWidth;
                int originalHeight = originalImage.PixelHeight;

                double originalDpiX = originalImage.DpiX;
                double originalDpiY = originalImage.DpiY;

                double dpiXdiff = SystemDPIX / originalDpiX;
                double dpiYdiff = SystemDPIY / originalDpiY;

                PixelFormat format = originalImage.Format;
                BitmapPalette palette = originalImage.Palette;

                int stride = originalWidth * (format.BitsPerPixel / 8); // 4 bytes per pixel
                byte[] pixelData = new byte[stride * originalHeight];
                originalImage.CopyPixels(pixelData, stride, 0);

                BitmapSource scaledImage = BitmapSource.Create(originalWidth, originalHeight, SystemDPIX, SystemDPIY, format, palette, pixelData, stride);

                BitmapSource scaledDownImage;

                int maxdimension = Math.Max(originalWidth, originalHeight);
                if (maxdimension > MAXIMAGESIZE)
                {
                    double scalex = MAXIMAGESIZE / (double)originalWidth;
                    double scaley = MAXIMAGESIZE / (double)originalHeight;
                    scaledDownImage = new TransformedBitmap(scaledImage, new ScaleTransform(scalex, scaley));
                }
                else
                {
                    scaledDownImage = scaledImage;
                }

                long filesize = (new FileInfo(droppedFilePath)).Length;

                InputFile inputfile = new InputFile()
                {
                    FilePath = droppedFilePath,
                    Filename = Path.GetFileName(droppedFilePath),
                    DPI = Math.Round(Math.Max(originalDpiX, originalDpiY)).ToString(),
                    ImageScaledDown = scaledDownImage,
                    Image = scaledImage,
                    OriginalImage = originalImage,
                    ImageHeight = originalWidth + "px",
                    ImageWidth = originalHeight + "px",
                    ImageSize = filesize + " bytes",
                    ImageHorizontalDPI = Math.Round(originalDpiX, 2).ToString(),
                    ImageVerticalDPI = Math.Round(originalDpiY, 2).ToString()
                };

                string filedirectory = Path.GetDirectoryName(droppedFilePath);

                OutputDirectory = filedirectory;

                string filenamewithoutext = Path.GetFileNameWithoutExtension(droppedFilePath);

                if (string.IsNullOrEmpty(longestprefix))
                {
                    longestprefix = filenamewithoutext;
                }
                else
                {
                    string prefix = "";
                    for (int i = 0; i < Math.Min(longestprefix.Length, filenamewithoutext.Length)
                        && longestprefix[i] == filenamewithoutext[i]; i++)
                        prefix += filenamewithoutext[i];
                    longestprefix = prefix;
                }

                InputFiles.Add(inputfile);
                //InputList.Items.Add(fileItem);
            }

            if (string.IsNullOrWhiteSpace(OutputFilenameTextBox.Text))
            {
                OutputFilenameTextBox.Text = longestprefix.TrimEnd('-', '.', ',', '=', '(', '[', '#', '_', '^', '@')
                                             + ".tif";
            }
        }

        private bool IsInInputFiles(string path)
        {
            foreach (InputFile file in InputFiles)
                if (file.FilePath == path)
                    return true;
            return false;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedFilePaths = e.Data.GetData(DataFormats.FileDrop, true) as string[];

                AddFiles(droppedFilePaths);
            }
        }

        private void ClearFilesButton_Click(object sender, RoutedEventArgs e)
        {
            OutputFilenameTextBox.Text = string.Empty;
            InputFiles.Clear();
        }

        private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.FileName = ""; // Default file name
            dlg.Filter = "Images|*.png;*.jpg;*.jpeg;*.tif;*.tiff;*.gif;*.bmp;*.ico|All Files|*.*"; // Filter files by extension
            dlg.CheckPathExists = true;
            dlg.Multiselect = true;
            dlg.ValidateNames = true;

            // Show open file dialog box
            bool? result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                AddFiles(dlg.FileNames);
            }
        }

        private void ProcessFilesButton_Click(object sender, RoutedEventArgs e)
        {
            ProcessImage();
        }

        private void ProcessImage()
        {
            using (Stream output = new FileStream(Path.Combine(OutputDirectory, OutputFilenameTextBox.Text), FileMode.Create))
            {
                TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                encoder.Compression = TiffCompressOption.Zip;
                foreach (InputFile image in this.InputFiles)
                {
                    //Console.WriteLine(image.OriginalImage.DpiX + "," + image.OriginalImage.DpiY);
                    byte[] pixels = new byte[image.OriginalImage.PixelWidth * image.OriginalImage.PixelHeight * (image.OriginalImage.Format.BitsPerPixel / 8)];
                    int stride = image.OriginalImage.PixelWidth * (image.OriginalImage.Format.BitsPerPixel / 8);
                    image.OriginalImage.CopyPixels(pixels, stride, 0);
                    //BitmapFrame frame = BitmapFrame.Create(image.OriginalImage);
                    double dpi = Math.Round(Math.Max(image.OriginalImage.DpiX, image.OriginalImage.DpiY));
                    double.TryParse(image.DPI, out dpi);
                    BitmapSource imagenewdpi = BitmapSource.Create(image.OriginalImage.PixelWidth, image.OriginalImage.PixelHeight, dpi, dpi, image.OriginalImage.Format, image.OriginalImage.Palette, pixels, stride);
                    BitmapFrame frame = BitmapFrame.Create(imagenewdpi);
                    encoder.Frames.Add(frame);
                }
                encoder.Save(output);
            }
        }

        private void HyperlinkClick(object sender, RoutedEventArgs e)
        {
            Process.Start("http://www.quppa.net");
        }
    }

    public class InputFile
    {
        public BitmapSource Image { get; set; }
        public BitmapSource OriginalImage { get; set; }
        public BitmapSource ImageScaledDown { get; set; }
        public string ImageHeight { get; set; }
        public string ImageWidth { get; set; }
        public string ImageSize { get; set; }
        public string ImageHorizontalDPI { get; set; }
        public string ImageVerticalDPI { get; set; }
        public string Filename { get; set; }
        public string FilePath { get; set; }
        public string DPI { get; set; }
    }
}
