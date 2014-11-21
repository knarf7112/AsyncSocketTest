using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
//
using IM.Daemon;
using Common.Logging;
using SocketServerTest.Handlers;

namespace SocketServerTest
{
    public class AsyncSocketServer :ISocketServer
    {
        #region private field
        private static readonly ILog log = LogManager.GetLogger(typeof(AsyncSocketServer));
        internal IDictionary<int, ClientRequest> dicHandler;
        private Encoding encoding;
        private TcpListener listener;
        private int portNumber;
        private int clientNo;
        private bool keepProcessing;
        #endregion
        public AsyncSocketServer(int port, bool start, Encoding encoding)
        {
            this.portNumber = port;
            this.encoding = encoding;
            this.dicHandler = new Dictionary<int, ClientRequest>();
            if (start)
            {
                this.Start();
            }
        }
        public AsyncSocketServer(int port) : this(port,false,Encoding.GetEncoding("Big5"))
        {
        }

        public void Start()
        {
            this.keepProcessing = true;
            this.listener = new TcpListener(IPAddress.Any, this.portNumber);
            this.listener.Start();
            //var s = this.listener.Server.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger);//.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, this);
            log.Debug(">> Server Started");
            try
            {
                while (this.keepProcessing)
                {
                    TcpClient clientSck = this.listener.AcceptTcpClient();
                    this.clientNo++;
                    log.Debug(">> Server <--> client(" + this.clientNo + ")");
                    ClientRequest clientRequest = new ClientRequest(this.clientNo, clientSck, this, this.encoding);
                    this.dicHandler.Add(this.clientNo, clientRequest);
                    this.dicHandler[this.clientNo].DoCommunicate();
                }
            }
            catch (Exception ex)
            {
                log.Debug("Server Error:" + ex.Message);
            }
        }

        public void Stop()
        {
            if (this.keepProcessing)
            {
                this.keepProcessing = false;
                log.Debug(">> flag => false");
            }
            if (this.listener != null)
            {
                try
                {
                    log.Debug(">> Server => Closing");
                    this.listener.Stop();
                    this.listener = null;
                }
                catch (Exception ex)
                {
                    this.listener.Server.Dispose();
                    log.Error("Server Stop Error:" + ex.Message);
                }
                log.Debug(">> Server => Closed");
            }
        }

        public void RemoveClient(int clientNo)
        {
            if (this.dicHandler[clientNo] != null)
            {
                try
                {
                    this.dicHandler[clientNo].CloseThread();
                    this.dicHandler.Remove(clientNo);
                    log.Debug(">> Remove Client(" + clientNo + ") => Success");
                }
                catch (Exception ex)
                {
                    log.Error("Server Remove Client(" + clientNo + ") failed : " + ex.ToString());
                }
            }
        }
    }
}
