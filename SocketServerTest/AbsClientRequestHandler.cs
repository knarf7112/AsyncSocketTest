using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
//
using System.Net.Sockets;
using System.IO;
using System.ComponentModel;
//
using Common.Logging;
//
using IM.Daemon.Handlers.States;

namespace IM.Daemon.Handlers
{
    public abstract class AbsClientRequestHandler : IClientRequestHandler
    {
        protected static ILog log;

        /// <summary>
        ///   socket client 識別號碼   
        /// </summary>
        public int ClientNo { get; set; }   
        
        /// <summary>
        ///   socket client reuqest
        /// </summary>
        public virtual TcpClient TcpClient { get; set; }

        /// <summary>
        ///   socket server
        /// </summary>
        public virtual ISocketServer SocketServer { get; set; }

        public virtual IState ServiceState { get; set; }

        public virtual bool KeepService { get; set; }

        public virtual string Message { get; set; }

        public virtual byte[] MsgBytes { get; set; }

        /// <summary>
        ///   background worker 
        /// </summary>
        protected BackgroundWorker backgroundWorker { get; set; }  
      
        protected int readTimeout = 30000; // Set to 30000 msecs
        protected int writeTimeout = 30000; // Set to 30000 msecs

        /// server & client 間相互進行通訊
        public virtual void DoCommunicate()
        {
            //產生 BackgroundWorker 負責處理每一個 socket client 的 reuqest
            this.backgroundWorker = new BackgroundWorker
            {
                WorkerSupportsCancellation = true
            };
            this.backgroundWorker.DoWork += HandleWork;
            this.backgroundWorker.RunWorkerAsync();
        }

        public virtual void CancelAsync()
        {
            if (this.backgroundWorker.IsBusy)
            {
                this.backgroundWorker.CancelAsync();
                if (this.TcpClient != null)
                {
                    try
                    {
                        this.TcpClient.Client.Shutdown(SocketShutdown.Both);
                        this.TcpClient.Close();
                        this.TcpClient = null;
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message);                       
                    }
                }
            }
        }

        #region BackgroundWorker Handler for future rewriting
        /// Sample doWork  處理 socket client request
        public virtual void HandleWork(object sender, DoWorkEventArgs e)
        {
            //server & client 已經連線完成
            //this.TcpClient.Connected is a readonly flag, replace with this.KeepService 
            while ( this.KeepService )
            {
                this.ServiceState.Handle(this);                
            }                            
        }
        #endregion
             
        /// <summary>
        /// 由stream中讀入訊息
        /// </summary>
        /// <param name="s">stream</param>
        /// <returns>訊息字串</returns>
        public virtual string ReadLine(Stream s)
        {
            List<byte> lineBuffer = new List<byte>();
            while (true)
            {
                int b = s.ReadByte();
                if (b == 10 || b < 0) break; // 0x0A ,LF or End of Data
                if (b != 13) lineBuffer.Add((byte)b); // 0x0D, CR
            }
            return Encoding.UTF8.GetString(lineBuffer.ToArray());
        }

        /// <summary>
        ///   由stream中讀入訊息
        /// </summary>
        /// <param name="s">stream</param>
        /// <returns>byte[],訊息位元陣列</returns>
        public virtual byte[] sReadBytes( Stream s )
        {
            // Read Stream s and write in MemoryStream ms
            byte[] block = new byte[0x1000]; // blocks of 4K.
            MemoryStream ms = new MemoryStream();
            while (true)
            {
                int bytesRead = s.Read( block, 0, block.Length );
                //log.Debug("bytesRead: " + bytesRead); 
                if (bytesRead > 0)
                {
                    ms.Write(block, 0, bytesRead);
                }
                if (bytesRead == 0 || bytesRead < block.Length )
                {
                    return ms.ToArray();
                }
            }        
        }
      
        /// <summary>
        ///   由NetworkStream中讀入訊息
        /// </summary>
        /// <param name="s">network stream</param>
        /// <returns>byte[],訊息位元陣列</returns>
        public virtual byte[] NetReadBytes( NetworkStream ns )
        {
            while (!ns.DataAvailable)
            {
                System.Threading.Thread.Sleep(5);                
            }
            // Read Stream ns and write in MemoryStream ms
            byte[] block = new byte[0x1000]; // blocks of 4K.
            MemoryStream ms = new MemoryStream();
            while (true)
            {
                int bytesRead = ns.Read( block, 0, block.Length );
                if (bytesRead > 0)
                {
                    ms.Write(block, 0, bytesRead);
                }
                if (bytesRead == 0 || bytesRead < block.Length )
                {
                    return ms.ToArray();
                }
            }        
        }       

        /// <summary>
        /// 將訊息寫入stream
        /// </summary>
        /// <param name="s">stream</param>
        /// <param name="line">message to write</param>
        public virtual void WriteLine(Stream s, string line)
        {
            byte[] response = Encoding.UTF8.GetBytes(line);
            s.Write(response, 0, response.Length);            
            s.WriteByte(13); // 0x0D,CR
            s.WriteByte(10); // 0x0A,LF
            s.Flush();
        }

        /// <summary>
        /// 將訊息寫入stream
        /// </summary>
        /// <param name="s">stream</param>
        /// <param name="line">message to write</param>
        public virtual void WriteBytes( Stream s, byte[] msg )
        {
            s.Write( msg, 0, msg.Length );
            s.Flush();
        }
    }
    
}