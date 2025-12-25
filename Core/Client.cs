#if !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace NT.Core.Net
{
    public class Client
    {
        /// <summary>
        /// The connection is established or not.
        /// </summary>
        /// <value></value>
        public bool Connected
        {
            get
            {
                return _transport != null && _transport.Connected;
            }
        }

        /// <summary>
        /// Client is attempting to establish a connection but not finish yet.
        /// </summary>
        /// <value></value>
        public bool Connecting { get => _connecting; }
        /// <summary>
        /// Connection tag composed of client tag and connection id.
        /// </summary>
        /// <value></value>
        public string Ctag { get => _tag + "#" + _cid; }
        /// <summary>
        /// Number of events piled in receive queue
        /// </summary>
        public int RecvQueueWatermark => _recvQueue.Count;

        /// <summary>
        /// Disables a nagle algorithm when send or receive buffers are not full.
        /// </summary>
        public bool NoDelay = true;

        /// <summary>
        /// The amount of time a connection will wait for a send operation to complete successfully,
        /// in milliseconds.
        /// </summary>
        public int SendTimeout = 5000;

        /// <summary>
        /// Preferred address family for connections. Unspecified = try IPv6 first, fallback to IPv4.
        /// InterNetwork = IPv4 only, InterNetworkV6 = IPv6 only.
        /// </summary>
        public AddressFamily AddressFamily { get; set; } = AddressFamily.Unspecified;

        /// <summary>
        /// TLS/SSL options for secure connections. If null, uses plain TCP.
        /// </summary>
        public TlsOptions TlsOptions { get; set; }

        private Transport _transport;

        // tag of the client
        private string _tag;

        // connection id, only valid during the client lifecycle
        private int _cid;
        private Thread _recvThread;
        private Thread _sendThread;
        private volatile bool _connecting;
        private SafeQueue<byte[]> _sendQueue = new SafeQueue<byte[]>();
        private ConcurrentQueue<Event> _recvQueue = new ConcurrentQueue<Event>();
        ManualResetEvent _sendDataSignal = new ManualResetEvent(false);

        public Client(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentException("Tag cannot be null or empty", nameof(tag));
            _tag = tag;
            _cid = 0;
        }

        public Client()
        {
            _tag = "default";
            _cid = 0;
        }

        void RecvThreadFunc(string ip, int port)
        {
            try
            {
                // this is a blocking call
                _transport.Connect(ip, port);
                _connecting = false;

                // now we connected and the underlied socket is created, set basic options
                _transport.Client.NoDelay = this.NoDelay;
                _transport.Client.SendTimeout = this.SendTimeout;

                // start send thread
                _sendThread = new Thread(() => { Transport.Send(Ctag, _transport, _sendQueue, _sendDataSignal); });
                _sendThread.IsBackground = true;
                _sendThread.Start();

                // start receive loop
                Transport.Receive(Ctag, _transport, _recvQueue);
            }
            catch (SocketException e)
            {
                // connection fail
                Debug.LogWarning($"[Client] connection failed: tag={Ctag}, reason={e}");
                _recvQueue.Enqueue(new Event(Ctag, EventType.Disconnected, null));
            }
            catch (Exception e)
            {
                // the thread maybe interrupted or aborted by disconnect, this is expected as a result
                // of user request. for other type of exceptions, there is really something seriously
                // wrong happened. Anyway we take a log here.
                Debug.LogWarning($"[Client] receive exception: tag={Ctag}, exception={e}");
            }

            // we may be here as connecting failed or connection closed or other exceptions happened
            if (_sendThread != null && _sendThread.IsAlive) _sendThread.Interrupt();

            // reset connecting state since the setting above may not have chance to execute in case of connect fail
            _connecting = false;

            // cleanup in case of connect fail
            if (_transport != null) _transport.Close();
        }

        /// <summary>
        /// Removes and returns the oldest event.
        /// </summary>
        /// <param name="ev"></param>
        /// <returns>true, there is an event returned, otherwise false</returns>
        public bool TryGetNextEvent(out Event ev)
        {
            return _recvQueue.TryDequeue(out ev);
        }

        /// <summary>
        /// Initiates an asynchronous connection to the specified server.
        /// </summary>
        /// <param name="host">The host name or IP address (IPv4 or IPv6) of the server.</param>
        /// <param name="port">The port number of the server.</param>
        public void Connect(string host, int port)
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentException("Host cannot be null or empty", nameof(host));

            if (Connecting || Connected)
            {
                Debug.Log($"[Client] Connect >> already connecting or connected");
                return;
            }

            _connecting = true;

            // Create transport using factory method
            _transport = Transport.Create(TlsOptions, AddressFamily);

            // drain any leftover events from previous connection
            int leftoverCount = 0;
            while (_recvQueue.TryDequeue(out _)) { leftoverCount++; }
            if (leftoverCount > 0)
            {
                Debug.LogWarning($"[Client] Connect >> drained {leftoverCount} unhandled events from previous connection");
            }

            _sendQueue.Clear();
            _cid += 1;

            // do the connecting in a seperate thread since it may take long time
            _recvThread = new Thread(() => { RecvThreadFunc(host, port); });
            _recvThread.IsBackground = true;
            _recvThread.Start();
        }

        /// <summary>
        /// Closes the connection and releases all resources.
        /// </summary>
        public void Disconnect()
        {
            if (Connecting || Connected)
            {
                _transport.Close();

                // wait until thread finished.
                if (_recvThread != null) _recvThread.Interrupt();

                _connecting = false;
                _sendQueue.Clear();

                // drain receive queue to prevent memory leak of unprocessed packets
                while (_recvQueue.TryDequeue(out _)) { }

                _transport = null;

                // dispose and recreate the signal for next connection
                _sendDataSignal?.Dispose();
                _sendDataSignal = new ManualResetEvent(false);
            }
        }

        /// <summary>
        /// Sends raw byte data to the server. The data is sent with a length prefix.
        /// For protocol-specific packets, use PacketClient with a codec instead.
        /// </summary>
        /// <param name="data">The raw byte data to send (max 16KB).</param>
        /// <returns>True if the data was queued successfully, false otherwise.</returns>
        public bool Send(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            bool ret = true;
            if (data.Length == 0)
            {
                ret = false;
                Debug.LogError($"[Client] Send >> data is empty");
            }
            else if (Connected)
            {
                if (data.Length <= Transport.MaxMessageSize)
                {
                    _sendQueue.Enqueue(data);
                    _sendDataSignal.Set();
                }
                else
                {
                    ret = false;
                    Debug.LogError($"[Client] Send >> packet size is too big: {data.Length}");
                }
            }
            else
            {
                ret = false;
                Debug.LogError($"[Client] Send >> not connected");
            }
            return ret;
        }
    }
}
#endif
