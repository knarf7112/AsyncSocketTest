using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SocketServerTest
{
    class Program
    {
        static void Main(string[] args)
        {
            AsyncSocketServer asyncServer = new AsyncSocketServer(6103);
            asyncServer.Start();
            
        }
    }
}
