namespace IM.Daemon
{
    /// <summary>
    ///   Interface of socket server
    /// </summary>
    public interface ISocketServer
    {
        /// <summary>
        ///   Start socket server
        /// </summary>
        void Start();

        /// <summary>
        ///   Stop socket server
        /// </summary>
        void Stop();

        /// <summary>
        ///   Remove socket client 
        /// </summary>
        /// <param name="clientNo">tcp client no.</param>
        void RemoveClient( int clientNo );
    }
}
