using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Printing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ESCPOSTester
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// 
    /// This utility is to demonstrate how to use InteropServices for handling print jobs
    /// from C# .NET. Key highlights are:
    /// 1) ESC_XXXXX byte arrays = simple ESC/POS commands
    /// 2) RawPrintHelper show how to DLLImport and has links to helpful docs.
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// This string must match the installed printer name.
        /// Protip: Printers can be easily renamed from the MSC control printmanagement.msc
        /// </summary>
        private const String PRINTER_NAME = "Reliance Printer";
        private const String DEFAULT_TEXT = "My name is {0}";

        #region Fields
        private string _currentPrinter = "";
        private string _currentString = DEFAULT_TEXT;
        private string _currentHex = "";

        /// <summary>
        /// ESC @ command - Initialize Printer
        /// resets all settings to default. Effectively a soft-reset
        /// </summary>
        private static byte[] ESC_InitPrinter = new byte[]{ 0x1B, 0x40 };

        /// <summary>
        /// GS e - Ejector
        /// Present ticket with 12 (0x0c) steps of paper
        /// </summary>
        private static byte[] ESC_Eject12Steps = new byte[] { 0x1D, 0x65, 0x03, 0x0C };

        /// <summary>
        /// GS e - Ejector
        /// Retract ticket if retraction is enabled. Command is ignored if retraction is disabled.
        /// </summary>
        private static byte[] ESC_Retract = new byte[] { 0x1D, 0x65, 0x02 };

        /// <summary>
        /// GS e - Ejector
        /// Eject Ticket if possible
        /// </summary>
        private static byte[] ESC_Eject = new byte[] { 0x1D, 0x65, 0x05 };

        /// <summary>
        /// ESC i - Total Cut
        /// Perfirms a fullk cut on the current ticket
        /// </summary>
        private static byte[] ESC_CutPaper = new byte[] { 0x1B, 0x69 };

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

        /// <summary>
        /// Gets or sets the hex commands to send to printer over USB
        /// </summary>
        public string CurrentHex
        {
            get { return _currentHex; }
            set
            {
                _currentHex = value;
                NotifyPropertyChanged("CurrentHex");
            }
        }
        #endregion

        #region ctor
        public MainWindow()
        {            
            InitializeComponent();

            DataContext = this;

            CurrentHex = "0x1B 0x40";
        }
        #endregion

        #region Listeners
        private void printString_Click(object sender, RoutedEventArgs e)
        {
            RawPrinterHelper.SendStringToPrinter(CurrentPrinter, CurrentString);
        }

        /// <summary>
        /// Parse the 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendCustomHex_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var raw = Utilities.StringToByteArray(CurrentHex);
                doPrintSend(raw);
            }
            catch (Exception)
            { }
        }

        /// <summary>
        /// Initializes printer and sends cut command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            doPrintSend(ESC_CutPaper);
        }

        /// <summary>
        /// Initializes printer and sends present command
       /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Present_Click(object sender, RoutedEventArgs e)
        {
            doPrintSend(ESC_Eject12Steps);
        }
        /// <summary>
        /// Initialized printer and send eject command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Eject_Click(object sender, RoutedEventArgs e)
        {
            doPrintSend(ESC_Eject);
        }

        /// <summary>
        /// Initialized printer and send reject command
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reject_Click(object sender, RoutedEventArgs e)
        {
            doPrintSend(ESC_Retract);
        }

        /// <summary>
        /// Reboots the printer which also initializes the printer
        /// NOTE: Not supported on Reliance/Phoenix
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Reboot_Click(object sender, RoutedEventArgs e)
        {
            RawPrinterHelper.RebootPrinter(CurrentPrinter);
        }

        /// <summary>
        /// Sync controls to safely manage the background random task
        /// </summary>
        private readonly object m_lock = new Object();
        bool isRandomRunning = false;


        /// <summary>
        /// Used for testing a printer by sending random data to it forever or for x number of tickets
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Random_Click(object sender, RoutedEventArgs e)
        {
            // Negative number will loop forever
            var stopAt = -1;
            Int32.TryParse(txtTicketCount.Text, out stopAt);


            lock(m_lock) {

                if (!isRandomRunning)
                {

                    isRandomRunning = true;
                    RandomBtn.Content = "Stop Random";

                    // Run task and if we return, stop the test
                    Task.Factory.StartNew(() =>{
                        randomTask(stopAt);
                        isRandomRunning = false;
                        RandomBtn.Content = "Start Random";
                    });

                }
                else
                {
                    // Stop the randomTask process
                    isRandomRunning = false;
                    RandomBtn.Content = "Start Random";
                }
            }
        }

        /// <summary>
        /// Performs random prints of random lengths with random time between tickets.
        /// </summary>
        /// <param name="runCount">Number of tickets to printer. Negative value will printer tickets forever.</param>
        private void randomTask(int runCount)
        {
            Random rnd = new Random((int)DateTime.Now.Ticks);

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ESCPOSTester.Resources.text.txt";
            var bytes = new List<byte>();

            const int lineCount = 128457;
            const int timeBetween = 7000;
            const int minLineCount = 40;
            const int maxLineCount = 100;

            int rejectAt = 0;
            int counter = 0;
            char[] buffer = new char[64];

            StringBuilder sb = new StringBuilder();
            while (runCount != 0)
            {
                // Do not decrement negative numbers
                if (runCount > 0)
                {
                    runCount--;
                    DoOnUIThread(()=>txtTicketCount.Text = string.Format("{0}", runCount));
                }

                // Pick random starting line, random line count (file is 128457 lines
                var start = rnd.Next(0, lineCount - maxLineCount);
                var len = rnd.Next(minLineCount, maxLineCount);

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    // Throw away data until we get to starting point
                    while (start-- > 0)
                    {
                        reader.Read();
                    }
                    while (len-- > 0)
                    {
                        var next = reader.ReadLine();
                        if (!string.IsNullOrEmpty(next))
                        {
                            sb.Append(buffer);
                            sb.Append('\n');
                        }
                    }
          
                }

                // Add count to end of print string for sniffing
                counter++;
                sb.AppendFormat(string.Format("\n\t\t\t<<{0}>>", counter));
                if (sb.Length % 2 != 0)
                {
                    sb.Append(' ');
                }
                foreach (char c in sb.ToString().ToCharArray())
                {
                    bytes.Add((byte)c);
                }

                doPrintSend(bytes.ToArray());
                bytes.Clear();

                // Cut, Present, Eject
                Cut_Click(this, null);

                Present_Click(this, null);

                // Give time to fully present
                Thread.Sleep(3000);

                if (rejectAt++ == 5)
                {
                    Reject_Click(this, null);
                    rejectAt = 0;
                }
                else
                {
                    Eject_Click(this, null);
                }

                Thread.Sleep(timeBetween);

                lock (m_lock)
                {
                    if (!isRandomRunning)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Opens a file dialog and allows for the selection of a single file to send to the printer
        /// as a print job.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
        private void UI_PrintTxt(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            // Just grab the first file in the list if > 1 was dropped
            var file = files[0];

            if (File.Exists(file))
            {
                // Extract the ASCII formatted hex ESC/POS data... or any data really as long 
                // as the text is space delimited base 16 bytes
                var bytes = File.ReadAllBytes(file);
                doPrintSend(bytes);             
            }
        }

        /// <summary>
        /// Occurs when the mouse is realease and the file is "dropped" on the app
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UI_PrintBin(object sender, DragEventArgs e)
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

        /// <summary>
        /// Common print hanlder for printing raw data bytes
        /// </summary>
        /// <param name="data"></param>
        private void doPrintSend(byte[] data)
        {
            // Gotta get a pointer on the local heap. Fun fact, the naming suggests that
            // this would be on the stack but it isn't. Windows no longer has a global heap
            // per se so these naming conventions are legacy cruft.
            IntPtr ptr = Marshal.AllocHGlobal(data.Length);
            Marshal.Copy(data, 0, ptr, data.Length);


            RawPrinterHelper.SendBytesToPrinter(CurrentPrinter, ptr, data.Length);


            Marshal.FreeHGlobal(ptr);

            Thread.Sleep(100);
        }

        /// <summary>
        /// Runs the specified action on the UI thread. Please be sure 
        /// to mark your delegates as async before passing them to this function.
        /// </summary>
        /// <param name="action"></param>
        private void DoOnUIThread(Action action)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(action);
            }
            else
            {
                action.Invoke();
            }
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

            if(string.IsNullOrEmpty(Properties.Settings.Default.LAST_PRINTER)) {
                Properties.Settings.Default.LAST_PRINTER = CurrentPrinter;
            }
            
            if(availablePrinters.Items.Contains(Properties.Settings.Default.LAST_PRINTER))
            {
                CurrentPrinter = Properties.Settings.Default.LAST_PRINTER;
                availablePrinters.SelectedItem = CurrentPrinter;
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

        /// <summary>
        /// Returns true if the string is a valid number
        /// </summary>
        /// <param name="text">input string</param>
        /// <returns>bool</returns>
        private static bool IsNumericText(string text)
        {
            Regex regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text
            return !regex.IsMatch(text);
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.LAST_PRINTER = CurrentPrinter;
            Properties.Settings.Default.Save();
        }

        #region Numeric Inputbox Tests
        private void txtTicketCount_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Test for numeric content
            e.Handled = !IsNumericText(e.Text);
        }

        private void txtTicketCount_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (!IsNumericText(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
        #endregion
    }
}
