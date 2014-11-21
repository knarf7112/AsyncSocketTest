using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
//using IM.Daemon;
//using Common.Logging;
namespace SocketServerTest
{
    public class ClientObject
    {
        public int clientNo;
        public Thread clientThread;
        public Socket clientSocket;
    }
    public class SocketServer //: ISocketServer
    {
        #region field
        //private static readonly ILog log = LogManager.GetLogger(typeof(SocketServer));
        private Socket mainSck;
        private int port;
        private int clientNo;
        private bool keepProcessing;
        private string errorMsg;
        private Encoding encoding;
        private bool isMultiThread;
        public Dictionary<int, ClientObject> dicClient;
        #endregion

        #region Property
        public string ErrorMsg { 
            get 
            {
                return this.errorMsg;
            }
            protected set
            {
                //log.Error(value);
                this.errorMsg = value;
            } 
        }
        #endregion

        #region Constructor
        public SocketServer(int port, bool start, Encoding encoding, bool multiThread = true)
        {
            this.port = port;
            this.mainSck = null;
            this.encoding = encoding;
            this.isMultiThread = multiThread;
            if (start)
            {
                this.Start();
            }
            this.dicClient = new Dictionary<int, ClientObject>();
        }
        public SocketServer(int port)
            : this(port, false,Encoding.UTF8, true)
        {

        }
        #endregion

        #region Method
        public void Start()
        {
            this.keepProcessing = true;
            this.clientNo = 0;
            try
            {
                if (this.mainSck != null) { 
                    this.mainSck.Shutdown(SocketShutdown.Both); 
                    this.mainSck.Close(); 
                }
                this.mainSck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                this.mainSck.Bind(new IPEndPoint(IPAddress.Any, this.port));
                this.mainSck.Listen(5);
                //log.Debug(">> Server Start ...");
                while (this.keepProcessing)
                {
                    SocketAccept();
                }
                if (this.mainSck != null)
                {
                    this.Stop();
                }
                //log.Debug(">> Stop Main Socket ...");
            }
            catch (Exception ex) { this.ErrorMsg = ex.Source + ":" + ex.TargetSite + ":" + ex.Message; }
        }

        //main socket accept and do work 
        private void SocketAccept()
        {
            this.clientNo++;//Client ID
            try
            {
                Socket clientSck = this.mainSck.Accept();// main socket hand住,當收到使用者連線訊息,則產生新socket分支(connected=true)給使用者用
                //log.Debug(">> Client Request No: " + this.clientNo + " => started!");

                //判斷是否要使用Thread
                if (this.isMultiThread)
                {
                    ClientObject clientRequest = new ClientObject();
                    clientRequest.clientSocket = clientSck;
                    clientRequest.clientNo = clientNo;
                    clientRequest.clientThread = new Thread(() => DoWork(this.clientNo, clientSck))
                    { 
                        Name = this.clientNo.ToString(), 
                        IsBackground = true //設了才能終止thread
                    };

                    clientRequest.clientThread.Start();

                    this.dicClient.Add(this.clientNo, clientRequest);
                    //newThread.Join();//加這段就會變成卡住在這個執行緒上(blocking),即執行到此執行緒終止才會換下一個執行緒作用
                }
                else
                {
                    DoWork(this.clientNo, clientSck);
                }
            }
            catch (Exception ex)
            {
                this.ErrorMsg = ex.InnerException.Source + ":" + ex.InnerException.TargetSite + ":" + ex.Message;
            }
        }

        //傳送與接收資料後的工作
        private void DoWork(int clientNo, Socket clientSck)
        {
            byte[] data = new byte[1024];
            
            try
            {
                while (clientSck.Connected)
                {
                    int receiveBytes = clientSck.Receive(data, data.Length, 0);
                    if (receiveBytes == 0) { break; }
                    string requestStr = this.encoding.GetString(data, 0, receiveBytes);
                    //log.Debug(">> From Client(" + clientNo + ") =>" + requestStr);
                    string sendStr = "Server send to Client(" + clientNo + ") =>" + requestStr + "\n";
                    byte[] sendBytes = this.encoding.GetBytes(sendStr);
                    clientSck.Send(sendBytes);
                    //log.Debug(">> " + sendStr);
                    if (requestStr.ToLower().Contains("exit"))
                    {
                        break;
                    }
                    else if (requestStr.ToLower().Contains("shutdown"))
                    {
                        this.Stop();
                        break;
                    }
                }
            }
            catch (Exception ex) {
                this.ErrorMsg = "client" + clientNo + ":" + ex.Message; 
            }
            finally
            {
                clientSck.Shutdown(SocketShutdown.Both);
                clientSck.Close();
                //log.Debug(">> Client(" + clientNo + ") => closed");
            }
        }

        public void Stop()
        {
            if (this.keepProcessing)
            {
                this.keepProcessing = false;
                //log.Debug(">> Set flag => false");
            }
            if (this.mainSck != null)
            {
                try
                {
                    this.mainSck.Close();
                    this.mainSck = null;
                    //log.Debug(">> Main Socket => closed");
                }
                catch 
                { 
                    this.mainSck.Dispose();
                    //log.Debug(">> Main Socket => dispose");
                }
            }
        }

        public void RemoveClient(int clientNo)
        {
            try
            {
                if (dicClient[clientNo] != null && dicClient[clientNo].clientThread.IsAlive)
                {
                    this.dicClient[clientNo].clientThread.Abort();
                    this.dicClient[clientNo].clientSocket.Shutdown(SocketShutdown.Both);
                    this.dicClient[clientNo].clientSocket.Close();
                    this.dicClient[clientNo].clientSocket.Dispose();
                }
            }
            catch (ThreadAbortException ex)
            {
                this.errorMsg = "client" + clientNo + ":" + ex.Message;
            }
            catch (Exception ex)
            {
                this.errorMsg = "client" + clientNo + ":" + ex.Message;
            }
        }
        #endregion
    }
    public class ClsSocketServer : IDisposable
    {

        //  在宣告區先行宣告 Socket 物件 
        Socket[] SckSs = null; //  
        Socket MainSck = null; //
        int socketCIndex;      // 定義一個指標用來判斷現下有哪一個空的 Socket 可以分配給 Client 端連線;
        int SPort = 6101;
        bool KeepProcessing = true;
        
        ~ClsSocketServer()
        {
            if (SckSs != null)
            {
                foreach (Socket i in this.SckSs)
                {
                    if (i != null)
                    {
                        i.Close();
                        i.Dispose();
                    }
                }
                this.SckSs = null;
            }
            if (MainSck != null)
            {
                MainSck.Close();
                MainSck.Dispose();
                MainSck = null;
            }
        }

        public string ErrorMsg { get; set; }

        // 聆聽
        public void Listen()
        {
            Array.Resize(ref SckSs, 1); // 用 Resize 的方式動態增加 Socket 的數目
            MainSck = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            MainSck.Bind(new IPEndPoint(IPAddress.Any, SPort));
            Console.WriteLine("Server Binding...");
            // 其中 LocalIP 和 SPort 分別為 string 和 int 型態, 前者為 Server 端的IP, 後者為Server 端的Port
            MainSck.Listen(3);         // 進行聆聽; Listen( )為允許 Client 同時連線的最大數
            Console.WriteLine("Server Listened(5)...");
            SckSsWaitAccept();          // 另外寫一個函數用來分配 Client 端的 Socket
        }

        //等待Client連線
        private void SckSsWaitAccept()
        {
            bool FlagFinded = false;    //判斷是否有空的socket可提供client端連線
            for (int i = 0; i < SckSs.Length; i++)
            {
                // SckSs[i] 若不為 null 表示已被實作過, 判斷是否有 Client 端連線
                if (SckSs[i] != null)
                {
                    // 如果目前第 i 個 Socket 若沒有人連線, 便可提供給下一個 Client 進行連線
                    if (SckSs[i].Connected == false)
                    {
                        FlagFinded = true;
                        socketCIndex = i;
                        break;
                    }
                }
            }

            // 如果 FlagFinded 為 false 表示目前並沒有多餘的 Socket 可供 Client 連線
            if (FlagFinded)
            {
                // 增加 Socket 的數目以供下一個 Client 端進行連線
                socketCIndex = SckSs.Length;
                Array.Resize(ref SckSs, socketCIndex + 1);
            }
            
            // 以下兩行為多執行緒的寫法, 因為接下來 Server 端的部份要使用 Accept() 讓 Cleint 進行連線;

            // 該執行緒有需要時再產生即可, 因此定義為區域性的 Thread. 命名為 SckSAcceptTd;

            // 在 new Thread( ) 裡為要多執行緒去執行的函數. 這裡命名為 SckSAcceptProc;

            //Thread SckSAcceptTd = new Thread(SckSAcceptProc);
            //SckSAcceptTd.Start(); // 開始執行 SckSAcceptTd 這個執行緒

            // 這裡要點出 SckSacceptTd 這個執行緒會在 Start( ) 之後開始執行 SckSAcceptProc 裡的程式碼, 同時主程式的執行緒也會繼續往下執行各做各的. 

            // 主程式不用等到 SckSAcceptProc 的程式碼執行完便會繼續往下執行. 
            while (this.KeepProcessing)
            {
                //Thread SckSAcceptTd = new Thread(SckSAcceptProc);
                //SckSAcceptTd.Start();
                SckSAcceptProc();
            }
        }
        
        // 接收來自Client的連線與Client傳來的資料
        private void SckSAcceptProc()
        {
            // 這裡加入 try 是因為 SckSs[0] 若被 Close 的話, SckSs[0].Accept() 會產生錯誤
            try
            {
                //IAsyncResult check = MainSck.BeginAccept(SckSs[socketCIndex],1024,callback,);
                
                SckSs[socketCIndex] = MainSck.Accept();// 等待Client 端連線
                Console.WriteLine("Server Accept...");
                // 為什麼 Accept 部份要用多執行緒, 因為 SckSs[0] 會停在這一行程式碼直到有 Client 端連上線, 並分配給 SckSs[SckCIndex] 給 Client 連線之後程式才會繼續往下, 若是將 Accept 寫在主執行緒裡, 在沒有Client連上來之前, 主程式將會被hand在這一行無法再做任何事了!!

                // 能來這表示有 Client 連上線. 記錄該 Client 對應的 SckCIndex 
                int Scki = socketCIndex;

                // 再產生另一個執行緒等待下一個 Client 連線
                //SckSsWaitAccept();
                Thread newthread = new Thread(DoWork);
                newthread.Start();
                //DoWork();
                
            }
            // 這裡若出錯主要是來自 SckSs[Scki] 出問題, 可能是自己 Close, 也可能是 Client 斷線, 自己加判斷吧~
            catch (Exception ex) { this.ErrorMsg = ex.StackTrace; }
        }

        protected void DoWork()
        {
            int IntAcceptData = 0;
            
            byte[]  clientData = null;
            
            while (SckSs[socketCIndex].Connected)
            {
                
                clientData = new byte[1024];
                // 程式會被 hand 在此, 等待接收來自 Client 端傳來的資料
                try
                {
                    IntAcceptData = SckSs[socketCIndex].Receive(clientData, clientData.Length, 0);//socketCIndex].Receive(clientData);
                    // 往下就自己寫接收到來自Client端的資料後要做什麼事唄~^^”
                }
                catch (Exception ex) { Console.WriteLine(ex.Source + ":" + ex.Message); }
                if (IntAcceptData > 0)
                {
                    string receiveData = Encoding.UTF8.GetString(clientData, 0, IntAcceptData);
                    Console.WriteLine("Client:" + receiveData);
                    Console.WriteLine("目前Client: " + socketCIndex);
                    SckSend();

                    if (receiveData.ToLower().Contains("exit"))
                    {
                        break;
                    }
                    else if (receiveData.ToLower().Contains("shutdown"))
                    {
                        this.KeepProcessing = false;
                        break;
                    }
                }
                else if (IntAcceptData == 0)
                {
                    break;
                }

            }
            
            SckSs[socketCIndex].Shutdown(SocketShutdown.Both);
            //SckSs[socketCIndex].Disconnect(true);
            SckSs[socketCIndex].Close();
            Console.WriteLine("Close Socket " + socketCIndex);
        }

        // Server 傳送資料給所有Client
        private void SckSend()
        {
            if (SckSs[socketCIndex] != null && SckSs[socketCIndex].Connected)
            {
                byte[] sendData = Encoding.UTF8.GetBytes("Server say Hello!! \nClientID:" + socketCIndex);
                try { 
                    int data = SckSs[socketCIndex].Send(sendData, sendData.Length, 0);
                    Console.WriteLine("Send Data Size: " + data + "bytes");
                }
                catch (Exception ex) { 
                    this.ErrorMsg = ex.Message + " : " + ex.Source;
                }
            }
        }

        public void Dispose()
        {
            foreach (Socket i in this.SckSs)
            {
                i.Close();
                i.Dispose();
            }
            this.SckSs = null;
            this.MainSck.Close();
            this.MainSck.Dispose();
            this.MainSck = null;
        }
    }
}
