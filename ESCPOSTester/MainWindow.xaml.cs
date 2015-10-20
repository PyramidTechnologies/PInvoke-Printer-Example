using System;
using System.ComponentModel;
using System.Drawing.Printing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace ESCPOSTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private const String PRINTER_NAME = "Phoenix Printer";
        private const String DEFAULT_TEXT = "My name is {0}";

        #region Fields
        private string _currentPrinter = "";
        private string _currentString = DEFAULT_TEXT;
        #endregion


        #region Properties
        /// <summary>
        /// Gets or sets the current printer name
        /// </summary>
        public string CurrentPrinter
        {
            get { return _currentPrinter; }
            set
            {
                // Inject the printer name if we can
                if (CurrentString.Contains("{0}"))
                {
                    CurrentString = String.Format(_currentString, value);
                }

                _currentPrinter = value;
                NotifyPropertyChanged("CurrentPrinter");
            }
        }

        /// <summary>
        /// Gets or sets the current string that will be printed
        /// </summary>
        public string CurrentString
        {
            get { return _currentString; }
            set
            {
                _currentString = value;
                NotifyPropertyChanged("CurrentString");
            }
        }
        #endregion


        public MainWindow()
        {            
            InitializeComponent();

            DataContext = this;
        }

        #region Listeners
        private void printString_Click(object sender, RoutedEventArgs e)
        {
            RawPrinterHelper.SendStringToPrinter(CurrentPrinter, CurrentString);
        }

        private void printFile_Click(object sender, RoutedEventArgs e)
        {
            ///////
            //https://support.microsoft.com/en-us/kb/322091
            //////
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = "Document"; // Default file name
            dlg.DefaultExt = ".txt"; // Default file extension
            dlg.Filter = "Text documents (.txt)|*.txt"; // Filter files by extension 

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results 
            if (result == true)
            {
                RawPrinterHelper.SendFileToPrinter(CurrentPrinter, dlg.FileName);
            }
        }

        /// <summary>
        /// Occurs when the mouse is realease and the file is "dropped" on the app
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UI_Drop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // Just grab the first file in the list if > 1 was dropped
            var file = files[0];

            if (File.Exists(file))
            {
                // Extract the ASCII formatted hex ESC/POS data... or any data really as long 
                // as the text is space delimited base 16 bytes
                doPrintSend(ByteUtils.ReadFileContainHexStringASCII(file));             
            }
        }

        /// <summary>
        /// Occurs when the mouse is realease and the file is "dropped" on the app
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UI_Drop_Bin(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // Just grab the first file in the list if > 1 was dropped
            var file = files[0];

            if (File.Exists(file))
            {
                // PRN is a raw binary dump - treat it as such
                doPrintSend(File.ReadAllBytes(file));
            }
        }

        private void doPrintSend(byte[] data)
        {
            // Gotta get a pointer on the local heap. Fun fact, the naming suggests that
            // this would be on the stack but it isn't. Windows no longer has a global heap
            // per se so these naming conventions are legacy cruft.
            IntPtr ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);


            RawPrinterHelper.SendBytesToPrinter(CurrentPrinter, ptr, data.Length);


            Marshal.FreeHGlobal(ptr);
        }

        /// <summary>
        /// Shows the proper mouse animation on drag-over
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UI_DragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
        }

        /// <summary>
        /// Run any finalizing setup *after* app UI is loaded but before execution of business logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // The default printer can be pulled from printer settings since the default is always
            // at the top of the list.
            PrinterSettings settings = new PrinterSettings();

            // Note, we do this in the loaded event because of the data binding.
            CurrentPrinter = settings.PrinterName;
            foreach (String printerName in PrinterSettings.InstalledPrinters)
            {
                availablePrinters.Items.Add(printerName);
            }
        }
        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

    }
}
