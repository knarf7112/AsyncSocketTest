using System;
using Common.Logging;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
namespace SocketServerTest.Handlers
{
    public class StatePass1 : IState
    {
        #region field
        private static readonly ILog log = LogManager.GetLogger(typeof(StatePass1));
        
        private int ChkStringLength;
        protected int ClientNo;
        protected string pinCode;
        public static byte[] FileData;
        #endregion

        #region Property
        public string Token { get; set; }
        public DateTime TokenCreateTime { get; set; }
        public string LastFileVersion { get; set; }
        public string FilePath { get; set; }
        public string FileSize { get; set; }
        public string TerminalID { get; set; }
        #endregion

        #region Constructor
        // 狀態1:檢查資料
        public StatePass1()
        {
            this.ChkStringLength = 12;
            this.FilePath = @"d:\temp\r5.jpg";
            this.FileSize = null;
            this.Token = null;
            this.TerminalID = null;
            this.LastFileVersion = null;
            
        }
        #endregion

        #region Method
        /// <summary>
        /// 狀態一檢查
        /// </summary>
        /// <param name="clientRequest">上一層的client物件</param>
        /// <returns>True/False</returns>
        private bool CheckState(ClientRequest clientRequest)
        {
            this.ClientNo = clientRequest.id;
            string receiveStr = clientRequest.encoding.GetString(clientRequest.receiveData, 0, clientRequest.receiveBytes).Replace("\n","");//字串命令轉換
            //檢查檔案長度必須大於設定長度
            if (receiveStr.Length < this.ChkStringLength)
            {
                return false;
            }
            //檢查PinCode
            if (HasPinCode(receiveStr))
            {
                //產生Token碼並紀錄產生時間並取得檔案版本
                this.Token = Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
                this.TokenCreateTime = DateTime.Now;
                this.FileSize = GetFirmwareSize(FilePath).ToString("D6");
                this.LastFileVersion = GetLastFirmwareVersion(FilePath);
                log.Debug(">> Client(" + clientRequest.id +
                    ") => Token:" + Token +
                    " TokenCreateTime:" + TokenCreateTime.ToString("yyyy-MM-dd hh:mm:ss") +
                    " TerminalID:" + TerminalID +
                    " FirmwareVersion:" + LastFileVersion);

                string sendStr = LastFileVersion + FileSize + Token;
                byte[] sendBytes = clientRequest.encoding.GetBytes(sendStr);
                try
                {
                    clientRequest.ns.Write(sendBytes, 0, sendBytes.Length);//送出命令
                    log.Debug(">> Server send to Client(" + clientRequest.id + ") => " + sendStr);
                }
                catch (Exception ex)
                {
                    log.Error(ex.StackTrace + ":" + ex.Message);
                    return false;
                }
                return true;
            }
            return false;
        }

        public void Handle(ClientRequest clientRequest)
        {
            string receiveStr = clientRequest.encoding.GetString(clientRequest.receiveData, 0, clientRequest.receiveBytes);
            // 判別端末編號
            if (HasTerminalID(receiveStr))
            {
                // check state 1
                if (CheckState(clientRequest))
                {
                    clientRequest.iState = new StatePass2(this);//此client物件的State欄位轉換至狀態二並傳入參數
                    log.Debug(">> Client(" + clientRequest.id + ") State =>" + clientRequest.iState.GetType().Name);
                    this.CloseConnection(clientRequest);
                }
                // check state 2
                else if (CheckToken(clientRequest, 1800))
                {
                    clientRequest.iState.Handle(clientRequest);//執行狀態二處理程序
                }
            }
            else
            {
                // casting
                if (clientRequest.socketServer is AsyncSocketServer)
                {
                    //驗證失敗並移除client物件
                    ((AsyncSocketServer)clientRequest.socketServer).dicHandler.Remove(clientRequest.id);
                    this.CloseConnection(clientRequest);
                }
                log.Debug(">> Client(" + clientRequest.id + ") => Validation failed");
            }
        }
        // check dictionary's token and if timeout ,remove it
        protected bool CheckToken(ClientRequest clientRequest,int timeoutSecond)
        {
            string receiveStr = clientRequest.encoding.GetString(clientRequest.receiveData, 0, clientRequest.receiveBytes);
            if (!HasTerminalID(receiveStr))
            {
                return false;
            }
            string tokenStr = receiveStr.Substring(4,4);//取得Token字串
            DateTime now = DateTime.Now;
            //搜尋dictionary並比對驗證Token正確性
            foreach (var i in ((AsyncSocketServer)clientRequest.socketServer).dicHandler)
            {
                //比對Token字串
                if (i.Value.iState.Token == tokenStr)
                {
                    //比對Token時效性
                    int clientTimeoutSecond = Convert.ToInt32(Math.Floor((now - i.Value.iState.TokenCreateTime).TotalSeconds));
                    if (clientTimeoutSecond < timeoutSecond)
                    {
                        if (clientRequest.socketServer is AsyncSocketServer)
                        {
                            //從dictionary內,把舊client物件的資料轉移到新client物件內
                            this.TerminalID = i.Value.iState.TerminalID;
                            this.LastFileVersion = i.Value.iState.LastFileVersion;
                            this.FileSize = i.Value.iState.FileSize;
                            this.Token = i.Value.iState.Token;
                            this.TokenCreateTime = DateTime.Now;
                            clientRequest.iState =i.Value.iState;
                            // 移除dictionary內的舊client物件
                             log.Debug(">> Client(" + clientRequest.id + ") => Change Data form client(" + i.Key + ")");
                            ((AsyncSocketServer)clientRequest.socketServer).dicHandler.Remove(i.Key);
                            return true;
                        }
                        else
                        {
                            log.Error(">> Casting to AsyncSocketServer failed!");
                            return false;
                        }
                    }
                    else
                    {
                        log.Debug(">> Client(" + i.Key + ") => Token expired and remove it!");
                        ((AsyncSocketServer)clientRequest.socketServer).RemoveClient(i.Key);
                        return false;
                    }
                }
            }
            
            return false;
        }

        public void CloseConnection(ClientRequest clientRequest)
        {
            clientRequest.ns.Close();
            clientRequest.ns.Dispose();
            clientRequest.tcpClient.Close();
            log.Debug(">> Sever <--X--> Client(" + clientRequest.id + ")");
        }
        protected int GetFirmwareSize(string fileFullPath)
        {
            if (File.Exists(fileFullPath))
            {
                FileData = File.ReadAllBytes(fileFullPath);
                log.Debug(">> Firmware File size:" + FileData.Length);

                return FileData.Length;
            }
            else
            {
                log.Error(">> Firmware File not exists => " + fileFullPath);
                return 0;
            }
        }
        protected bool HasTerminalID(string receiveString)
        {
            bool hasTerminal = false;
            if (receiveString.Length >= 4)
            {
                this.TerminalID = Regex.IsMatch(receiveString.Substring(0, 4), @"^0[0-2]0[0-2]$") ? receiveString.Substring(0, 4) : null;
                if (TerminalID == null)
                {
                    log.Debug(">> Client(" + this.ClientNo + ") => TerminalID check failed");
                    hasTerminal = false;
                }
                else
                {
                    hasTerminal = true;
                }
            }
            return hasTerminal;
        }

        protected bool HasPinCode(string receiveString)
        {
            this.pinCode = Regex.IsMatch(receiveString.Substring(4, 4), @"^A[A-Z]A[A-Z]$") ? receiveString.Substring(4, 4) : null;
            if (pinCode == null)
            {
                log.Debug(">> Client(" + this.ClientNo + ") => PinCode check failed");
                return false;
            }
            else
            {
                return true;
            }
        }

        protected string GetLastFirmwareVersion(string fileFullPath)
        {
            if (File.Exists(fileFullPath))
            {
                FileVersionInfo fileVrs = FileVersionInfo.GetVersionInfo(fileFullPath);
                return fileVrs.FileVersion;
            }
            return null;
        }
        #endregion
    }
}
