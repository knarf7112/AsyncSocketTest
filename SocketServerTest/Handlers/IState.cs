using System;

namespace SocketServerTest.Handlers
{
    public interface IState
    {
        string Token { get; set; }
        DateTime TokenCreateTime { get; set; }
        string LastFileVersion { get; set; }
        string TerminalID { get; set; }
        string FilePath { get; set; }
        string FileSize { get; set; }

        void Handle(ClientRequest clientRequest);
    }
}
