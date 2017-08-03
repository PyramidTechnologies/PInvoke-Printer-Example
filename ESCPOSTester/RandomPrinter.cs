using System;
using System.Collections.Generic;
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
        Empty
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

        public RandomPrinterMode Mode { get; set; }

        public int StopAt { get; set; }

        public int DelayMS { get; set; }

        public int MinLineCount { get; set; }

        public int MaxLineCount { get; set; }

        public int RejectAt { get; set; }

        public int TickerCount { get; set; }

        public async Task<int> Start()
        {
            _mIsRandomRunning = true;
            return await Task.Factory.StartNew<int>(() => RunPrintTask());
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
            LocalPrintServer server = new LocalPrintServer();

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

                // Wait for print queue to empty
                PrintQueue q;
                int jobCount = 0;
                while (true)
                {
                    // PrintQueue collection is not updated so requery every loop
                    q = server.GetPrintQueue(PrinterName);
                    if (jobCount != q.NumberOfJobs)
                    {
                        RaiseDataEvent(new RandomPrinterEvent()
                        {
                            EventType = EventType.JobCountUpdate,
                            Value = q.NumberOfJobs,
                        });
                    }

                    jobCount = q.NumberOfJobs;
                    if (q != null && q.NumberOfJobs == 0) break;
                    Thread.Sleep(100);
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


        private string GetPrintContent()
        {
            Random rnd = new Random((int)DateTime.Now.Ticks);
            var sb = new StringBuilder();

            // Add count to end of print string for sniffing      
            sb.AppendFormat(string.Format("\n\t\t\t<<Ticker #{0} {1}>>\n", ++TickerCount, PrinterName));

            switch (Mode)
            {
                case RandomPrinterMode.Empty:
                    for (var i = 0; i < MaxLineCount; i++)
                    {
                        sb.AppendLine();
                        // randomly exit once we reach minimum length
                        if (i > MinLineCount && rnd.Next(100) % 13 == 0) break;
                    }
                    break;

                case RandomPrinterMode.S:
                    for (var i = 0; i < MaxLineCount; i++)
                    {
                        sb.AppendLine("SSSSSSSSSSSSSSSSSSSSSSSSSSSSSSSS");
                        // randomly exit once we reach minimum length
                        if (i > MinLineCount && rnd.Next(100) % 13 == 0) break;
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
