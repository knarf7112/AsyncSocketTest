using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Common.Logging;
using System.Threading;
using System.IO;
using IM.Daemon;
namespace SocketServerTest.Handlers
{
    public class ClientRequest
    {
        #region Field
        private static readonly ILog log = LogManager.GetLogger(typeof(ClientRequest));
        internal readonly int id;
        internal IState iState;
        internal Encoding encoding;
        internal Thread thread;
        internal TcpClient tcpClient;
        internal ISocketServer socketServer;
        internal byte[] receiveData;
        internal int receiveBytes;
        internal NetworkStream ns;
        #endregion

        #region Constructor
        public ClientRequest(int clientNo, TcpClient tcpClient, ISocketServer socketServer, Encoding encoding)
        {
            this.id = clientNo;
            this.tcpClient = tcpClient;
            this.iState = new StatePass1();
            this.encoding = encoding;
            this.socketServer = socketServer;
        }
        #endregion

        #region Method
        /// <summary>
        /// 產生新Thread並執行工作
        /// </summary>
        public virtual void DoCommunicate()
        {
            thread = new Thread(() => DoWork()) { IsBackground = true };
            thread.Start();               
        }

        private void DoWork()
        {
            string receiveStr = null;
            try
            {
                while (tcpClient !=null && tcpClient.Connected)
                {
                    receiveBytes = 0;
                    receiveData = new byte[1026];
                    ns = tcpClient.GetStream();//get NetworkStream object
                    //ns.ReadTimeout = 30000;
                    //ns.WriteTimeout = 30000;
                    if (ns.CanRead)
                    {
                        receiveBytes = ns.Read(receiveData, 0, receiveData.Length);//接收的資料大小
                        log.Debug(">> Client(" + this.id + ") => Receive Cmd: " + this.encoding.GetString(receiveData, 0, receiveBytes));
                    }
                    if (receiveBytes == 0)
                    {
                        this.CloseConnection();
                    }
                    else if (receiveBytes > 0)
                    {
                        receiveStr = this.encoding.GetString(receiveData, 0, receiveBytes);
                        if (receiveStr.ToLower().Contains("exit"))
                        {
                            log.Debug(">> Client(" + this.id + ") => exit");
                            this.CloseConnection();
                            this.socketServer.RemoveClient(this.id);
                            break;
                        }
                        else if (receiveStr.ToLower().Contains("shutdown"))
                        {
                            log.Debug(">> Client(" + this.id + ") => shutdown");
                            this.CloseConnection();
                            this.socketServer.Stop();
                            break;
                        }
                        else
                        {
                            this.iState.Handle(this);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Client(" + this.id + ") DoWork failed:" + ex.Message);
                this.CloseConnection();
                ((AsyncSocketServer)socketServer).dicHandler.Remove(this.id);
            }
        }
        /// <summary>
        /// Stop client's thread
        /// </summary>
        public void CloseThread()
        {
            if (this.thread != null && this.thread.IsAlive)
            {
                try
                {
                    this.thread.Abort();
                    log.Debug("Stop Client(" + this.id + ") Thread => Success");
                }
                catch (Exception ex)
                {
                    log.Error("Stop Client(" + this.id + ")Thread failed =>" + ex.Message);
                }
                finally{
                    this.thread = null;
                }
            }
        }
        public void CloseConnection()
        {
            if (this.tcpClient.Connected)
            {
                this.ns.Close();
                this.ns.Dispose();
                this.tcpClient.Close();
            }
        }
        #endregion
    }
}
