using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Diagnostics;
using System.Threading;

namespace SocksLib
{
    /// <summary>
    /// A wrapped class for sockets which handles the TCP/IP protocol.
    /// </summary>
    /// <remarks>
    /// A couple of new things were added such as a Try-Catch statement inside of SendAsync and SendRapid.
    /// Also inside of beginRead().
    /// </remarks>
    public sealed class TcpSocket : IDisposable
    {
        //The maximum size of a single receive.
        private const int BUFFER_SIZE = 8192;
        //The size of the size header.
        private const int SIZE_BUFFER_LENGTH = 4;

        #region Properties
        /// <summary>
        /// Used to indicate if this instance is connected or not.
        /// </summary>
        public bool Connected { get; private set; }

        /// <summary>
        /// Used to determine if this current instance is disposed.
        /// </summary>
        public bool IsDisposed { get; private set; }

        #endregion

        #region Events
        /// <summary>
        /// The event called when a connection is accepted or failed.
        /// </summary>
        public event EventHandler<TcpSocketConnectionStateEventArgs> AsyncConnectionResult;
        /// <summary>
        /// The event called when data is incoming.
        /// </summary>
        public event EventHandler<TcpSocketReceivedEventArgs> DataReceived;
        /// <summary>
        /// The event called when there is a disconnection.
        /// </summary>
        public event EventHandler Disconnected;
        #endregion

        #region Variables
        /// <summary>
        /// Holds the buffer used to receive data for the entire lifetime of this instance.
        /// </summary>
        private byte[] buffer;
        /// <summary>
        /// Holds the payload size once decoded by the receiveSizeCallback method.
        /// </summary>
        private int payloadSize;
        /// <summary>
        /// Holds the pieces of the payload across multiple receives to complete the full payload.
        /// </summary>
        private MemoryStream payloadStream;

        /// <summary>
        /// Used to sync threads between sends with SendRapid.
        /// </summary>
        private object sendSync = new object();

        /// <summary>
        /// Holds the Socket handle for the instance we are wrapping.
        /// </summary>
        private Socket socket;
        #endregion

        #region Constructors

        /// <summary>
        /// The constructor for connecting sockets.
        /// </summary>
        public TcpSocket()
        {
            //Creates an instance of an IPv4 TCP socket for connecting
            this.socket = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            //Initialize the receive buffer
            initBuffer();
        }

        /// <summary>
        /// The constructor for the wrapping a Socket into a TcpSocket (Servers would use this for example).
        /// </summary>
        /// <param name="s">The socket we will be wrapping. Be sure its an IPv4 TCP Socket</param>
        public TcpSocket(Socket s)
        {
            //Set the local variable to this socket instead of creating one
            this.socket = s;
            //Set connected to true. We assume the socket is pre-connected.
            this.Connected = true;
            //Initialize the receive buffer
            initBuffer();
            //Start reading data
            beginRead();
        }
        #endregion

        #region Deconstructor
        ~TcpSocket()
        {
            Dispose();
        }
        #endregion 

        #region Methods
        /// <summary>
        /// We used this as a helper method to initialize the receive buffer.
        /// </summary>
        private void initBuffer()
        {
            //Create a new instance of a byte array based on the buffer size
            this.buffer = new byte[BUFFER_SIZE];
        }

        /// <summary>
        /// We use this as a helper method to check if this current instance has been disposed.
        /// </summary>
        private void checkDisposed()
        {
            //If this class is disposed, throw the exception.
            if (this.IsDisposed)
            {
                throw new ObjectDisposedException("this");
            }
        }
        #endregion

        #region Connection
        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="host">The host as a string.</param>
        /// <param name="port">The port of the host.</param>
        public void Connect(string host, int port)
        {
            checkDisposed();
            this.socket.Connect(host, port);
            OnSyncConnect();
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="endPoint">The endpoint server to connect too.</param>
        public void Connect(IPEndPoint endPoint)
        {
            checkDisposed();
            this.socket.Connect(endPoint);
            OnSyncConnect();
        }

        /// <summary>
        /// Connect to a server.
        /// </summary>
        /// <param name="ipAddress">The IPAddress instance of the host.</param>
        /// <param name="port">The port of the host.</param>
        public void Connect(IPAddress ipAddress, int port)
        {
            checkDisposed();
            this.socket.Connect(ipAddress, port);
            OnSyncConnect();
        }

        /// <summary>
        /// Connects to a server asynchronously.
        /// </summary>
        /// <param name="host">The address of the host in a string</param>
        /// <param name="port">The port of the host</param>
        public void ConnectAsync(string host, int port)
        {
            checkDisposed();
            this.socket.BeginConnect(host, port, connectCallback, null);
        }

        /// <summary>
        /// Connects to a server asynchronously.
        /// </summary>
        /// <param name="endPoint">The IPEndPoint instance of the host.</param>
        public void ConnectAsync(IPEndPoint endPoint)
        {
            checkDisposed();
            this.socket.BeginConnect(endPoint, connectCallback, null);
        }

        /// <summary>
        /// Connects to a server asynchronously.
        /// </summary>
        /// <param name="ipAddress">The IPAddress instance of the host</param>
        /// <param name="port">The port of the host</param>
        public void ConnectAsync(IPAddress ipAddress, int port)
        {
            checkDisposed();
            this.socket.BeginConnect(ipAddress, port, connectCallback, null);
        }

        /// <summary>
        /// The callback of the BeginConnect method
        /// </summary>
        /// <param name="ar">The result of the BeginConnect call.</param>
        private void connectCallback(IAsyncResult ar)
        {
            checkDisposed();
            Exception connectEx = null;
            try
            {
                //Attempt to end the connection
                this.socket.EndConnect(ar);
                //If no exception is thrown, all is well.
                this.Connected = true;
            }
            catch(Exception ex)
            {
                //Set connectEx to the exception to be passed to OnConnect
                connectEx = ex;
            }
            finally
            {
                //Call OnConnect to handle if the connection was successful or not, and take the appropriate action
                OnConnect(Connected, connectEx);
            }
        }

        /// <summary>
        /// Used to handle if the connection was successful or not if the ConnectAsync method.
        /// </summary>
        /// <param name="connected">If the connection was successful or not.</param>
        /// <param name="ex">If the connection was not successful, this will not be null</param>
        private void OnConnect(bool connected, Exception ex)
        {
            checkDisposed();
            //If the connection is successful, start reading data.
            if (connected)
            {
                beginRead();
            }
            //Call the event.
            AsyncConnectionResult?.Invoke(this, new TcpSocketConnectionStateEventArgs(connected,
                ex));
        }

        /// <summary>
        /// The sync version of OnConnect
        /// </summary>
        private void OnSyncConnect()
        {
            //If it made it this far, the connection was successful, so set Connected and begin reading.
            this.Connected = true;
            beginRead();
        }
        #endregion

        #region Receive
        /// <summary>
        /// Starts the receiving loop.
        /// </summary>
        private void beginRead()
        {
            try
            {
                //Attempt to receive the buffer size
                this.socket.BeginReceive(this.buffer, 0, SIZE_BUFFER_LENGTH, 0,
                    readSizeCallback, null);
            }
            catch(Exception ex)
            {
                Debug.Print(ex.StackTrace);
                //This was missed in the tutorial. OnDisocnnected should be added here as well.
                OnDisconnected();
            }
        }

        /// <summary>
        /// This callback is for handling the receive of the size header.
        /// </summary>
        /// <param name="ar"></param>
        private void readSizeCallback(IAsyncResult ar)
        {
            try
            {
                //Attempt to end the read
                var read = this.socket.EndReceive(ar);

                /*An exception should be thrown on EndReceive if the connection was lost.
                However, that is not always the case, so we check if read is zero or less.
                Which means disconnection.
                If there is a disconnection, we throw an exception*/
                if (read <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionAborted);
                }

                //If we didn't receive the full buffer size, something is lagging behind.
                if (read < SIZE_BUFFER_LENGTH)
                {
                    //Calculate how much is missing.
                    var left = SIZE_BUFFER_LENGTH - read;

                    //Wait until there is at least that much avilable.
                    while (socket.Available < left)
                    {
                        Thread.Sleep(100);
                    }

                    //Use the synchronous receive since the data is close behind and shouldn't take much time.
                    this.socket.Receive(this.buffer, read, left, 0);
                }

                //Get the converted int value for the payload size from the received data
                this.payloadSize = BitConverter.ToInt32(this.buffer, 0);

                /*Get the initialize size we will read
                 * If its not more than the buffer size, we'll just use the full length*/
                var initialSize = this.payloadSize > BUFFER_SIZE ? BUFFER_SIZE :
                    this.payloadSize;

                //Initialize a new MemStream to receive chunks of the payload
                this.payloadStream = new MemoryStream();

                //Start the receive loop of the payload
                this.socket.BeginReceive(this.buffer, 0, initialSize, 0,
                    receivePayloadCallback, null);
            }
            catch(Exception ex)
            {
                OnDisconnected();
                Debug.Print(ex.StackTrace);
            }
        }

        private void receivePayloadCallback(IAsyncResult ar)
        {
            try
            {
                //Attempt to finish the async read.
                var read = this.socket.EndReceive(ar);

                //Same as above
                if (read <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionAborted);
                }

                //Subtract what we read from the payload size.
                this.payloadSize -= read;

                //Write the data to the payload stream.
                this.payloadStream.Write(this.buffer, 0, read);

                //If there is more data to receive, keep the loop going.
                if (this.payloadSize > 0)
                {
                    //See how much data we need to receive like the initial receive.
                    int receiveSize = this.payloadSize > BUFFER_SIZE ? BUFFER_SIZE :
                        this.payloadSize;
                    this.socket.BeginReceive(this.buffer, 0, receiveSize, 0, 
                        receivePayloadCallback, null);
                }
                else //If we received everything
                {
                    //Close the payload stream
                    this.payloadStream.Close();
                    //Get the full payload
                    byte[] payload = this.payloadStream.ToArray();
                    //Set the stream to null so the GC knows its ready to collect
                    this.payloadStream = null;
                    //Start reading
                    beginRead();
                    //Call the event method
                    OnDataReceived(payload);
                }
            }
            catch(Exception ex)
            {
                OnDisconnected();
                Debug.Print(ex.StackTrace);
            }
        }

        /// <summary>
        /// Calls the DataReceived event
        /// </summary>
        /// <param name="payload"></param>
        private void OnDataReceived(byte[] payload)
        {
            DataReceived?.Invoke(this, new TcpSocketReceivedEventArgs(payload));
        }
        #endregion

        #region Send
        /// <summary>
        /// Sends data asynchronously.
        /// </summary>
        /// <param name="payload">The data to send.</param>
        public void SendAsync(byte[] payload)
        {
            checkDisposed();
            //Get the byte[] version of the payload size.
            byte[] sizeBuffer = BitConverter.GetBytes(payload.Length);
            //Instantiate a new byte array based on the size header length + the total payload size.
            byte[] fullBuffer = new byte[sizeBuffer.Length + payload.Length];

            //Copy the size buffer into the full buffer
            Buffer.BlockCopy(sizeBuffer, 0, fullBuffer, 0, sizeBuffer.Length);
            //Copy the payload into the full buffer right after the size header
            Buffer.BlockCopy(payload, 0, fullBuffer, sizeBuffer.Length, payload.Length);

            //Send the data off.
            //The Try-Catch statement is new. Added after the tutorial just in case
            try
            {
                this.socket.BeginSend(fullBuffer, 0, fullBuffer.Length, 0, sendCallback, null);
            }
            catch(Exception ex)
            {
                OnDisconnected();
                Debug.Print(ex.StackTrace);
            }
        }

        /// <summary>
        /// Used to send data quickly without causing a memory leak.
        /// </summary>
        /// <param name="payload">The data to send.</param>
        public void SendRapid(byte[] payload)
        {
            checkDisposed();
            //Lock sendSync to ensure this is the only thread sending data with this method.
            lock (sendSync)
            {
                //Send the payload length
                this.socket.Send(BitConverter.GetBytes(payload.Length));
                //Send the payload
                this.socket.Send(payload);
            }
        }

        /// <summary>
        /// Used as the BeginSend callback
        /// </summary>
        /// <param name="ar"></param>
        private void sendCallback(IAsyncResult ar)
        {
            try
            {
                //Why can't everything be this easy?
                this.socket.EndSend(ar);
            }
            catch(Exception ex)
            {
                OnDisconnected();
                Debug.Print(ex.StackTrace);
            }
        }
        #endregion

        #region Disconnection
        /// <summary>
        /// Disconnects the client from the server if connected.
        /// </summary>
        public void Disconnect()
        {
            checkDisposed();
            //If there is a connection:
            if (this.Connected)
            {
                //Disconnect the socket, but allow reuse (true).
                this.socket.Disconnect(true);
                //Set Connected to false.
                this.Connected = false;
            }
        }

        /// <summary>
        /// Calls the Disconnected event
        /// </summary>
        private void OnDisconnected()
        {
            this.Connected = false;
            this.Disconnected?.Invoke(this, EventArgs.Empty);
        }
        #endregion

        #region Cleanup
        /// <summary>
        /// Cleans up the resources that must be released.
        /// </summary>
        public void Dispose()
        {
            checkDisposed();
            if (!this.IsDisposed)
            {
                this.socket.Close();
                this.socket = null;
                this.buffer = null;
                this.payloadSize = 0;
                this.Connected = false;
            }
        }
        #endregion
    }
}