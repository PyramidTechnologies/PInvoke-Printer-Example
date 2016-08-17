﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Printing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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
        private string _currentHex = "";
        private byte[] _esc_pos_clear = new byte[]{ 0x1D, 0x65, 0xFF, 0x01 };
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

        /// <summary>
        /// Parse the 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SendCustomHex_Click(object sender, RoutedEventArgs e)
        {
            var raw = Utilities.StringToByteArray(CurrentHex);
            doPrintSend(raw);
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            doPrintSend(_esc_pos_clear);
            var raw = new byte[] { 0x1B, 0x69 };
            doPrintSend(raw);
        }

        private void Present_Click(object sender, RoutedEventArgs e)
        {
            doPrintSend(_esc_pos_clear);
            var raw = new byte[] { 0x1D, 0x65, 0x03, 0x0C };
            doPrintSend(raw);
        }

        private void Eject_Click(object sender, RoutedEventArgs e)
        {
            doPrintSend(_esc_pos_clear);
            var raw = new byte[] { 0x1D, 0x65, 0x05 };
            doPrintSend(raw);
        }

        private readonly object m_lock = new Object();
        bool isRandomRunning = false;
        private void Random_Click(object sender, RoutedEventArgs e)
        {
            lock(m_lock) {

                if (!isRandomRunning)
                {
                    Task.Factory.StartNew(() => randomTask());
                    isRandomRunning = true;
                    RandomBtn.Content = "Stop Random";
                }
                else
                {
                    // Stop the randomTask process
                    isRandomRunning = false;
                    RandomBtn.Content = "Start Random";
                }
            }
        }

        private void randomTask()
        {
            Random rnd = new Random((int)DateTime.Now.Ticks);

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "ESCPOSTester.Resources.text.txt";
            var bytes = new List<byte>();

            const int lineCount = 128457;
            const int timeBetween = 10000;
            const int minLineCount = 20;
            const int maxLineCount = 140;
            while (true)
            {

                // Pick random starting line, random line count (file is 128457 lines
                var start = rnd.Next(0, lineCount - maxLineCount);
                var len = rnd.Next(minLineCount, maxLineCount);

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    // Throw away data until we get to starting point
                    while (start-- > 0)
                    {
                        reader.ReadLine();
                    }
                    while (len-- > 0)
                    {
                        bytes.AddRange(System.Text.ASCIIEncoding.ASCII.GetBytes(reader.ReadLine()));
                    }
                }

                doPrintSend(bytes.ToArray());
                bytes.Clear();

                // Cut, Present, Eject
                Cut_Click(this, null);

                Present_Click(this, null);

                Eject_Click(this, null);

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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.LAST_PRINTER = CurrentPrinter;
            Properties.Settings.Default.Save();
        }

    }
}
