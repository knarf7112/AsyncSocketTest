using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Common.Logging;
namespace SocketServerTest.Handlers
{
    public class StatePass2 : IState
    {
        #region field
        private static readonly ILog log = LogManager.GetLogger(typeof(StatePass2));
        private int timeOutSec;
        private int maxOffset;
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
        //狀態2:傳輸檔案
        public StatePass2(IState state1)
        {
            this.FilePath = state1.FilePath;
            this.FileSize = state1.FileSize;
            this.Token = state1.Token;
            this.TokenCreateTime = state1.TokenCreateTime;
            this.TerminalID = state1.TerminalID;
            this.LastFileVersion = state1.LastFileVersion;
            this.timeOutSec = 1800;
            FileData = StatePass1.FileData;
            if (FileData.Length == 0)
            {
                this.FileSize = GetFirmwareSize(this.FilePath).ToString("D6");
            }
        }
        #endregion
        public void Handle(ClientRequest clientRequest)
        {
            try
            {
                if (Math.Floor((DateTime.Now - this.TokenCreateTime).TotalSeconds) <= timeOutSec)
                {
                    string receiveStr = clientRequest.encoding.GetString(clientRequest.receiveData, 0, clientRequest.receiveBytes).Replace("\n", "");
                    int offSet = -1;
                    int segmentSize = 0;
                    //遠端傳來要求的offset位置和要求傳輸的檔案size大小

                    if (int.TryParse(receiveStr.Substring(8, 4), out offSet) &&
                        int.TryParse(receiveStr.Substring(12, 4), out segmentSize))
                    {
                        //檔案起始位置從0開始
                        this.maxOffset = Convert.ToInt32(Math.Ceiling((decimal)FileData.Length / segmentSize));// 檔案總片段數 = 檔案總長度 / 要求的長度(讓他多跑一次避免太早斷連線造成Client端Exception)
                    }
                    //順序傳送Package(1,2,3,4,5,....last)--若是使用斷點傳送則要產生陣列來做檢查,Client端做?Server端需要記錄嗎?
                    if (offSet < maxOffset && offSet >= 0)
                    {
                        byte[] sendData = GetData(FileData, offSet, segmentSize);
                        List<byte> list = sendData.ToList();
                        if (offSet < 256)
                        {
                            list.Add(0);
                            list.Add(Convert.ToByte(offSet));
                        }
                        else if (offSet > 255 && offSet < 65536)
                        {
                            list.Add(Convert.ToByte(Math.Floor((decimal)offSet / 256)));
                            list.Add(Convert.ToByte(offSet % 256));
                        }
                        else
                        {
                            log.Error("檔案超過65MB,無法上傳");
                            throw new IndexOutOfRangeException("檔案超過65MB,無法上傳");
                        }
                        clientRequest.ns.Write(list.ToArray(), 0, list.Count);//送出檔案片段
                        int startPosition = offSet * segmentSize;//送出檔案的起始位置
                        int endPosition = startPosition + sendData.Length;//送出檔案的結束位置
                        log.Debug(">> Server Send Data => " + startPosition + "~" + endPosition + " / " + FileData.Length);
                    }
                    else //if (offSet == maxOffset || offSet == -1)
                    {
                        clientRequest.CloseConnection();
                        ((AsyncSocketServer)clientRequest.socketServer).RemoveClient(clientRequest.id);
                        log.Debug(">> Client(" + clientRequest.id + ") => Closed!");
                        clientRequest.CloseThread();
                    }
                }
                else
                {
                    //Token過期 => 回到狀態1
                    clientRequest.iState = new StatePass1();
                    clientRequest.iState.Handle(clientRequest);
                    log.Error(">> Client(" + clientRequest.id + ") => Token Experid and Back to State1");
                }
            }
            catch (Exception ex)
            {
                log.Error("State2 Handle failed : " + ex.Message);
                clientRequest.CloseConnection();
            }
        }

        //切割檔案資料
        protected byte[] GetData(byte[] fileData,int offset,int size)
        {
            try
            {
                if (fileData.Length >= size && (offset * size) <= fileData.Length)
                {
                    ArraySegment<byte> segment;
                    if (fileData.Length - (offset * size) < size)
                    {
                        int lastSegment = fileData.Length - (offset * size);
                        segment = new ArraySegment<byte>(fileData, offset * size, lastSegment);//取得檔案片段
                    }
                    else
                    {
                        segment = new ArraySegment<byte>(fileData, offset * size, size);
                    }
                    log.Debug(">> Segment Offset :" + offset + " Size:" + segment.Count);
                    return segment.ToArray();
                }
                return null;
            }
            catch (Exception ex)
            {
                log.Error("File separate failed : " + ex.Message);
                return null;
            }

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
    }
}
