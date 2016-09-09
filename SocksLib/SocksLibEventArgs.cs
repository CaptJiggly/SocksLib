using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

namespace SocksLib
{
    /// <summary>
    /// Used with TcpSocketListener for accepted clients.
    /// </summary>
    public class AcceptedTcpSocketEventArgs : EventArgs
    {
        /// <summary>
        /// The client accepted by the TcpSocketListener.
        /// </summary>
        public TcpSocket AcceptedSocket { get; private set; }

        /// <summary>
        /// The EndPoint of the connected client.
        /// </summary>
        public IPEndPoint RemoteEndPoint { get; private set; }

        public AcceptedTcpSocketEventArgs(Socket s)
        {
            this.AcceptedSocket = new TcpSocket(s);

            this.RemoteEndPoint = (IPEndPoint)s.RemoteEndPoint;
        }
    }

    /// <summary>
    /// Used with TcpSocket for connecting clients.
    /// </summary>
    public class TcpSocketConnectionStateEventArgs : EventArgs
    {
        public Exception Exception { get; private set; }
        public bool Connected { get; private set; }

        public TcpSocketConnectionStateEventArgs(bool connected, Exception ex)
        {
            this.Exception = ex;
            this.Connected = connected;
        }
    }

    /// <summary>
    /// Used by TcpSocket for incoming data
    /// </summary>
    public class TcpSocketReceivedEventArgs : EventArgs
    {
        public byte[] Payload { get; private set; }

        public TcpSocketReceivedEventArgs(byte[] payload)
        {
            this.Payload = payload;
        }
    }
}
