using System.ServiceProcess;
//using Spring.Context;
//using Spring.Context.Support;
using Common.Logging;
using SocketServerTest;
using IM.Daemon;
using System.Timers;
using System.Diagnostics;

namespace AsyncSocketServer
{
    public partial class AsyncService : ServiceBase
    {
        //服務寫失敗了 沒有常駐在windows service內
        private static readonly ILog log = LogManager.GetLogger(typeof(AsyncService));
        //IApplicationContext ctx;
        ISocketServer socketServer1;
        ISocketServer socketServer2;
        Timer timer;
        int count;
        public AsyncService()
        {
            InitializeComponent();
            //用Timer監視服務狀態
            timer = new Timer();
            timer.Interval = 60000;// 60 seconds
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();
            //this.ctx = ContextRegistry.GetContext();
            //this.socketServer1 = ctx["multiAsyncSocketServer1"] as ISocketServer;
            //this.socketServer2 = ctx["multiAsyncSocketServer2"] as ISocketServer;
            this.socketServer1 = new SocketServerTest.AsyncSocketServer(6101);
            this.socketServer2 = new SocketServerTest.AsyncSocketServer(6102);
            //產生Windows事件檢視器
            this.AutoLog = false;
            if (!System.Diagnostics.EventLog.SourceExists("Service1"))
            {
                System.Diagnostics.EventLog.CreateEventSource("Service1", "Mylog");
            }
            eventLog1.Source = "Service1";
            eventLog1.Log = "Mylog";
        }

        void OnTimer(object sender, ElapsedEventArgs e)
        {
            eventLog1.WriteEntry("服務中...", EventLogEntryType.Information, ++count);
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("Start Service!");
            log.Debug("Start Service...");
            this.socketServer1.Start();
            //none - blocked 
            this.socketServer2.Start();
            //none - blocked
            log.Debug("None - Block....");

        }

        protected override void OnStop()
        {
            timer.Stop();
            timer.Dispose();
            eventLog1.WriteEntry("Stop Service!");
            System.Diagnostics.EventLog.DeleteEventSource("Service1");
            log.Debug("Stop Service ...");
            this.socketServer1.Stop();
            this.socketServer2.Stop();
        }
    }
}
