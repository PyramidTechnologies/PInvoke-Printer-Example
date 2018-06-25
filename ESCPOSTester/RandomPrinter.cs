using ESCPOSTester.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Printing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ESCPOSTester
{
    public enum RandomPrinterMode
    {
        Sherlock,
        S,
        Empty,
        QR,
        Image,
    }

    public enum EventType
    {
        JobCountUpdate,
        RunCountUpdate
    }

    public class RandomPrinterEvent : EventArgs
    {
        public RandomPrinterEvent() { }

        public EventType EventType { get; set; }

        public int Value { get; set; }
    }

    class RandomPrinter
    {

        #region Fields
        /// <summary>
        /// Sync controls to safely manage the background random task
        /// </summary>
        private readonly object _mLock = new Object();
        bool _mIsRandomRunning = false;
        #endregion

        public RandomPrinter(string printerName)
        {
            PrinterName = printerName;

            Mode = RandomPrinterMode.Sherlock;
            StopAt = 3;
            DelayMS = 4000;
            MinLineCount = 25;
            MaxLineCount = 25;
            RejectAt = 5;
            TickerCount = 0;
            ImageData = Resources.rickqr;
            RandomImageLayout = true;
        }

        public event EventHandler<RandomPrinterEvent> OnDataEvent;
        public event EventHandler OnCutRequested;
        public event EventHandler OnPresentRequested;
        public event EventHandler OnRejectRequested;
        public event EventHandler OnEjectRequested;

        protected void RaiseDataEvent(RandomPrinterEvent args)
        {
            var handler = OnDataEvent;
            if (handler != null)
            {
                handler(this, args);
            }
        }

        protected void RaiseSafeHandler(EventHandler handler)
        {
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        public string PrinterName { get; private set; }

        public string TestName { get; set; }

        public RandomPrinterMode Mode { get; set; }

        public int StopAt { get; set; }

        public int DelayMS { get; set; }

        public int MinLineCount { get; set; }

        public int MaxLineCount { get; set; }

        public int RejectAt { get; set; }

        public int TickerCount { get; set; }

        public Bitmap ImageData { get; set; }

        public bool RandomImageLayout { get; set; }

        public async Task<int> Start()
        {
            _mIsRandomRunning = true;
            return await Task.Factory.StartNew<int>(RunPrintTask);
        }

        public void Stop()
        {
            lock (_mLock)
            {
                _mIsRandomRunning = false;
            }
        }

        private int RunPrintTask()
        {
            // QR mode is a little different
            if (Mode == RandomPrinterMode.QR || Mode == RandomPrinterMode.Image)
            {
                return RunImageTask(ImageData);
            }

            int timeBetween = DelayMS < 0 ? 7000 : DelayMS;
            int minLineCount = MinLineCount;
            int maxLineCount = MaxLineCount;

            int rejectAt = 0;
            int runCount = StopAt;

            while (runCount != 0)
            {
                // Do not decrement negative numbers
                if (runCount > 0)
                {
                    runCount--;
                    RaiseDataEvent(new RandomPrinterEvent()
                    {
                        EventType = EventType.RunCountUpdate,
                        Value = runCount,
                    });
                }

                var str = GetPrintContent();
                RawPrinterHelper.SendBytesToPrinter(PrinterName, Encoding.ASCII.GetBytes(str));

                // Cut, Present, Eject
                RaiseSafeHandler(OnCutRequested);
                RaiseSafeHandler(OnPresentRequested);

                // Reject every nth
                if (RejectAt > 0 && rejectAt++ == RejectAt)
                {
                    RaiseSafeHandler(OnRejectRequested);
                    rejectAt = 0;
                }
                else
                {
                    RaiseSafeHandler(OnEjectRequested);
                }

                AwaitPrintQueueClear();

                Thread.Sleep(timeBetween);

                lock (_mLock)
                {
                    if (!_mIsRandomRunning)
                    {
                        break;
                    }
                }
            }

            return TickerCount;
        }

        /// <summary>
        /// Uses Windows print API to generate image print jobs
        /// </summary>
        /// <returns></returns>
        private int RunImageTask(Bitmap bmp)
        {                                    
            var rnd = new Random((int)DateTime.Now.Ticks);

            var font = new Font("Consolas", 18f);
            var blackBrush = new SolidBrush(Color.Black); 

            int timeBetween = DelayMS < 0 ? 7000 : DelayMS;
            int runCount = StopAt;

            var points = new List<Point>();

            // Y will get overwritten
            var left = new Point(0, 0);
            var center = new Point(95, 0);
            var right = new Point(185, 0);

            while (runCount != 0)
            {
                var doc = new PrintDocument
                {
                    PrintController = new StandardPrintController(),
                    OriginAtMargins = false,
                    PrinterSettings = {PrinterName = PrinterName},
                };
                doc.PrintPage += (s, args) =>
                {
                    // Make sure we operate on correct width                 
                    var bounds = args.Graphics.VisibleClipBounds;
                    bounds.Width *= args.Graphics.DpiX / 96.0f;

                    // Add title
                    using (var renderer = new NativeTextRenderer(args.Graphics))
                    {
                        var str = string.Format("\n<<Ticker #{0} {1}>>\n", ++TickerCount, PrinterName);
                        renderer.DrawString(str,
                            font,
                            Color.Black,
                            bounds,
                            RawPrinterHelper.TextFormatFlags.Center);
                    }
                    using (var renderer = new NativeTextRenderer(args.Graphics))
                    {
                        var str = string.Format("<<{0}>>\n", TestName);
                        renderer.DrawString(str,
                            font,
                            Color.Black,
                            bounds,
                            RawPrinterHelper.TextFormatFlags.Center);
                    }

                    if (RandomImageLayout)
                    {
                        switch (rnd.Next(0, 6))
                        {
                            case 0:
                                points.Add(left);
                                points.Add(center);
                                points.Add(right);
                                break;
                            case 1:
                                points.Add(left);
                                points.Add(right);
                                points.Add(center);
                                break;
                            case 2:
                                points.Add(center);
                                points.Add(left);
                                points.Add(right);
                                break;
                            case 3:
                                points.Add(center);
                                points.Add(right);
                                points.Add(left);
                                break;
                            case 4:
                                points.Add(right);
                                points.Add(left);
                                points.Add(center);
                                break;
                            case 5:
                                points.Add(right);
                                points.Add(center);
                                points.Add(left);
                                break;

                        }
                    }
                    else
                    {
                        // Make sure most of image is within printed area since we're not handling page size
                        points.Add(left);
                    }


                    var y = 30;
                    foreach (var p in points)
                    {
                        args.Graphics.DrawImage(bmp, p.X, y, bmp.Width, bmp.Height);
                        y += bmp.Height;
                    }
                    points.Clear();
                    args.HasMorePages = false;
                };
                doc.Print();

                AwaitPrintQueueClear();

                // Do not decrement negative numbers
                if (runCount > 0)
                {
                    runCount--;
                    RaiseDataEvent(new RandomPrinterEvent()
                    {
                        EventType = EventType.RunCountUpdate,
                        Value = runCount,
                    });
                }


                Thread.Sleep(timeBetween);

                lock (_mLock)
                {
                    if (!_mIsRandomRunning)
                    {
                        break;
                    }
                }
            }

            return TickerCount;
        }

        private void AwaitPrintQueueClear()
        {
            // Wait for print queue to empty
            LocalPrintServer server = new LocalPrintServer();
            PrintQueue q;
            int jobCount = 0;
            try
            {
                while (true)
                {
                    // PrintQueue collection is not updated so requery every loop
                    q = server.GetPrintQueue(PrinterName);
                    if (q != null && jobCount != q.NumberOfJobs)
                    {
                        RaiseDataEvent(new RandomPrinterEvent()
                        {
                            EventType = EventType.JobCountUpdate,
                            Value = q.NumberOfJobs,
                        });
                    }

                    jobCount = q.NumberOfJobs;
                    if (q.NumberOfJobs == 0 || !_mIsRandomRunning)
                    {
                        break;
                    }

                    Thread.Sleep(100);
                }
            }
            catch (Exception)
            {
                // PrintQueue exception may be thrown, just sleep and hope for the best
                Thread.Sleep(1000);
            }
        }

        private string GetPrintContent()
        {
            Random rnd = new Random((int)DateTime.Now.Ticks);
            var sb = new StringBuilder();

            // Add count to end of print string for sniffing      
            sb.AppendFormat(string.Format("\n<<Ticker #{0} {1}>>\n", ++TickerCount, PrinterName));
            sb.AppendFormat(string.Format("<<{0}>>\n", TestName));

            switch (Mode)
            {
                case RandomPrinterMode.Empty:
                    // Empty should not include ANY text at all
                    sb.Clear();
                    for (var i = 0; i < MaxLineCount; i++)
                    {
                        sb.AppendLine();
                        // randomly exit once we reach minimum length
                        if (i > MinLineCount && rnd.Next(100) % 13 == 0)
                        {
                            break;
                        }
                    }
                    break;

                case RandomPrinterMode.S:
                    for (var i = 0; i < MaxLineCount; i++)
                    {
                        sb.AppendLine("SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS");
                        // randomly exit once we reach minimum length
                        if (i > MinLineCount && rnd.Next(100) % 13 == 0)
                        {
                            break;
                        }
                    }
                    break;

                case RandomPrinterMode.Sherlock:
                    // Pick random starting line, random line count (file is 128457 lines)
                    // Numer of line in sherlock txt
                    const int lineCount = 128457;
                    var start = rnd.Next(0, lineCount - MaxLineCount);
                    var len = rnd.Next(MinLineCount, MaxLineCount);

                    // ~32 chars per line
                    var expectedStrLen = 32 * len;
                    using (var reader = new StringReader(Properties.Resources.text))
                    {
                        // Throw away data until we get to starting point
                        while (start-- > 0)
                        {
                            reader.Read();
                        }
                        while (expectedStrLen > 0)
                        {
                            var next = reader.ReadLine();
                            if (!string.IsNullOrEmpty(next))
                            {
                                expectedStrLen -= next.Length;
                                sb.AppendLine(next);
                            }
                        }

                    }
                    break;
            }

            return sb.ToString();
        }
    }
}
