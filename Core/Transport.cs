#if !UNITY_WEBGL
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace NT.Core.Net
{
    /// <summary>
    /// Low-level TCP transport layer. Protocol-agnostic - works with raw byte arrays.
    /// Uses a simple length-prefix protocol: [4-byte length][payload bytes]
    /// </summary>
    public class Transport
    {
        private TcpClient _socket;
        public TcpClient Socket { get => _socket; }
        public bool Connected
        {
            get
            {
                return _socket != null && _socket.Connected && _socket.Client.Connected;
            }
        }

        /// <summary>
        /// Alert is set if receive queue size exceeds this value. It is an
        /// indication that the received messages have not been processed in
        /// time.
        /// </summary>
        public static int RecvQueueWarningLevel = 1000;

        /// <summary>
        /// Maximum receive queue size. When exceeded, packets will be dropped
        /// to prevent unbounded memory growth. Default: 10000 events.
        /// </summary>
        public static int MaxRecvQueueSize = 10000;

        /// <summary>
        /// Gets the stream for this transport. Can be overridden by subclasses
        /// to provide a wrapped stream (e.g., SslStream for TLS).
        /// </summary>
        protected virtual Stream GetStream()
        {
            return Socket.GetStream();
        }

        /// <summary>
        /// Maximum message size allowed, 16KB should be enough.
        /// </summary>
        public static int MaxMessageSize = 16 * 1024;

        /// <summary>
        /// Maximum send buffer size for message combining, 64KB should be enough.
        /// </summary>
        private const int MaxSendBufferSize = 64 * 1024;

        /// <summary>
        /// Maximum size for ThreadStatic send buffer. If a single batch exceeds this,
        /// we allocate a temporary buffer instead of growing the ThreadStatic one.
        /// This prevents unbounded memory growth in long-lived threads.
        /// </summary>
        private const int MaxThreadStaticBufferSize = 64 * 1024;

        /// <summary>
        /// Preferred address family for connections. Unspecified = try IPv6 first, fallback to IPv4.
        /// InterNetwork = IPv4 only, InterNetworkV6 = IPv6 only.
        /// </summary>
        public AddressFamily AddressFamily { get; set; } = AddressFamily.Unspecified;

        // pre-allocated thread local buffers to avoid memory allocations
        [ThreadStatic]
        static byte[] _header;

        // send buffer, every thread has a send buffer pre-allocated
        [ThreadStatic]
        static byte[] _buffer;

        public Transport()
        {
            _socket = new TcpClient();
        }

        /// <summary>
        /// Factory method to create the appropriate transport based on TLS options.
        /// </summary>
        /// <param name="tlsOptions">TLS options for secure connections, or null for plain TCP.</param>
        /// <param name="addressFamily">Preferred address family for connections.</param>
        /// <returns>A Transport instance (either Transport or SecureTransport).</returns>
        public static Transport Create(TlsOptions tlsOptions, AddressFamily addressFamily)
        {
            Transport transport;
            if (tlsOptions != null)
            {
                transport = new SecureTransport(tlsOptions);
            }
            else
            {
                transport = new Transport();
            }
            transport.AddressFamily = addressFamily;
            return transport;
        }

        /// <summary>
        /// Connects to the specified host and port.
        /// </summary>
        /// <param name="host">The host name or IP address (IPv4 or IPv6 literal).</param>
        /// <param name="port">The port number.</param>
        public void Connect(string host, int port)
        {
            if (IPAddress.TryParse(host, out IPAddress ip))
            {
                // Direct IP connection (supports both IPv4 and IPv6 literals)
                _socket.Connect(new IPEndPoint(ip, port));
            }
            else
            {
                // Hostname - DNS resolution with address family preference
                ConnectDns(host, port);
            }
        }

        /// <summary>
        /// Connects to a host via DNS resolution, respecting the AddressFamily preference.
        /// </summary>
        private void ConnectDns(string host, int port)
        {
            IPAddress[] results = Dns.GetHostAddresses(host);
            IPAddress chosen = null;

            if (AddressFamily == AddressFamily.InterNetwork)
            {
                // IPv4 only
                chosen = results.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }
            else if (AddressFamily == AddressFamily.InterNetworkV6)
            {
                // IPv6 only
                chosen = results.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
            }
            else
            {
                // Unspecified: Try IPv6 first (happy eyeballs - typically faster), fallback to IPv4
                chosen = results.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6)
                     ?? results.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            }

            if (chosen == null)
                throw new SocketException((int)SocketError.AddressNotAvailable);

            _socket.Connect(new IPEndPoint(chosen, port));
        }

        public void Close()
        {
            _socket.Close();
        }

        internal static bool SendMessage(Stream stream, List<byte[]> messages)
        {
            try
            {
                // combine multiple messages to avoid TCP overhead and get higher performance
                // if total size exceeds MaxSendBufferSize, split into multiple batches
                int startIndex = 0;
                while (startIndex < messages.Count)
                {
                    SendMessageBatch(stream, messages, ref startIndex);
                }
                return true;
            }
            catch (Exception e)
            {
                // stream.Write throws exceptions if server shuts down
                Debug.LogWarning($"[Transport] Send exception: {e}");
                return false;
            }
        }

        internal static void SendMessageBatch(Stream stream, List<byte[]> messages, ref int startIndex)
        {
            // calculate how many messages we can fit in this batch
            int totalSize = 0;
            int endIndex = startIndex;

            while (endIndex < messages.Count)
            {
                int messageSize = sizeof(int) + messages[endIndex].Length;
                if (totalSize + messageSize > MaxSendBufferSize)
                    break;
                totalSize += messageSize;
                endIndex++;
            }

            // ensure we include at least one message (even if it exceeds buffer size)
            if (endIndex == startIndex)
                endIndex = startIndex + 1;

            // Use ThreadStatic buffer if within size limit, otherwise allocate temporary
            byte[] buffer = GetOrCreateBuffer(totalSize);
            bool isTempBuffer = buffer.Length > MaxThreadStaticBufferSize;

            int pos = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (_header == null)
                {
                    _header = new byte[4];
                }

                // save the message length to header
                Utils.GetBytes(messages[i].Length, _header);

                // pack header and message data to buffer
                Array.Copy(_header, 0, buffer, pos, _header.Length);
                pos += _header.Length;
                Array.Copy(messages[i], 0, buffer, pos, messages[i].Length);
                pos += messages[i].Length;
            }

            // send to remote, the Write method blocks until the requested number
            // of bytes is sent or a SocketException is thrown.
            stream.Write(buffer, 0, totalSize);

            startIndex = endIndex;
        }

        /// <summary>
        /// Gets or creates the ThreadStatic buffer, capped at MaxThreadStaticBufferSize.
        /// </summary>
        private static byte[] GetOrCreateBuffer(int minimumSize)
        {
            if (minimumSize > MaxThreadStaticBufferSize)
                minimumSize = MaxThreadStaticBufferSize;

            if (_buffer == null || _buffer.Length < minimumSize)
            {
                _buffer = new byte[minimumSize];
            }
            return _buffer;
        }

        internal static bool ReceiveMessage(Stream stream, out byte[] data)
        {
            data = null;

            if (_header == null)
                _header = new byte[4];

            // Read message length (4 bytes)
            if (!stream.ReadExactly(_header, 4))
                return false;

            int size = Utils.ToInt32(_header);

            // Validate message size
            if (size <= 0 || size > MaxMessageSize)
            {
                Debug.LogError($"[Transport] Receive invalid message size: {size}");
                return false;
            }

            // Read message payload - rent from ArrayPool to reduce allocations
            data = ArrayPool<byte>.Shared.Rent(size);
            if (!stream.ReadExactly(data, size))
            {
                ArrayPool<byte>.Shared.Return(data, clearArray: false);
                data = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Receive thread procedure. Receives raw byte messages from the server and queues events.
        /// Uses length-prefix protocol: [4-byte length][payload]
        /// </summary>
        /// <param name="tag">Connection tag for logging and event tagging.</param>
        /// <param name="transport">The transport instance.</param>
        /// <param name="recvQueue">Queue to enqueue received events.</param>
        /// <param name="cancellationToken">Token to signal cancellation.</param>
        public static void Receive(string tag, Transport transport, ConcurrentQueue<Event> recvQueue, CancellationToken cancellationToken)
        {
            Stream stream = transport.GetStream();
            DateTime lastWarnTime = DateTime.Now;

            try
            {
                recvQueue.Enqueue(new Event(tag, EventType.Connected, null));
                while (!cancellationToken.IsCancellationRequested)
                {
                    byte[] data;
                    if (!ReceiveMessage(stream, out data))
                        break;

                    // Drop packet if queue is at maximum size to prevent unbounded memory growth
                    if (recvQueue.Count >= MaxRecvQueueSize)
                    {
                        Debug.LogError($"[Transport] Receive queue full ({recvQueue.Count}), dropping packet to prevent memory exhaustion");
                        continue;
                    }

                    recvQueue.Enqueue(new Event(tag, EventType.Data, data));
                    if (recvQueue.Count > RecvQueueWarningLevel)
                    {
                        TimeSpan elapsed = DateTime.Now - lastWarnTime;
                        if (elapsed.TotalSeconds > 10)
                        {
                            Debug.LogWarning($"[Transport] Receive Queue is piled too much events: {recvQueue.Count}");
                            lastWarnTime = DateTime.Now;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during disconnect - stop gracefully
                Debug.LogWarning($"[Transport] Receive proc cancelled! tag={tag}");
            }
            catch (Exception e)
            {
                // remote closed the connection or we closed it
                Debug.LogWarning($"[Transport] Receive proc finished! tag={tag}, reason={e}");
            }
            finally
            {
                stream.Close();
                transport.Socket.Close();

                recvQueue.Enqueue(new Event(tag, EventType.Disconnected, null));
            }
        }

        /// <summary>
        /// Send thread procedure. Sends messages from the send queue to the server.
        /// </summary>
        /// <param name="tag">Connection tag for logging.</param>
        /// <param name="transport">The transport instance.</param>
        /// <param name="sendQueue">Queue containing messages to send.</param>
        /// <param name="mre">Signal to wake the send thread when data is available.</param>
        /// <param name="cancellationToken">Token to signal cancellation.</param>
        public static void Send(string tag, Transport transport, SafeQueue<byte[]> sendQueue, ManualResetEvent mre, CancellationToken cancellationToken)
        {
            Stream stream = transport.GetStream();
            List<byte[]> messageBuffer = new List<byte[]>();  // Reusable buffer to avoid allocations

            try
            {
                while (transport.Socket.Connected && !cancellationToken.IsCancellationRequested)
                {
                    // reset the signal
                    mre.Reset();

                    if (sendQueue.TryDequeueAll(messageBuffer))
                    {
                        if (!SendMessage(stream, messageBuffer))
                            break;
                    }

                    // wait for more data blockingly, or until cancellation is requested
                    WaitHandle.WaitAny(new WaitHandle[] { mre, cancellationToken.WaitHandle });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during disconnect - stop gracefully
                Debug.LogWarning($"[Transport] Send proc cancelled! tag={tag}");
            }
            catch (Exception e)
            {
                // Exceptions happen when thread is stopped or connection is closed by either
                // local or remote. Stop and cleanup gracefully
                Debug.LogWarning($"[Transport] Send proc finished! tag={tag}, reason={e}");
            }
            finally
            {
                // When we close the socket in send loop, the receive loop will finally encounter failure
                // and fire the Disconnected event. Thus we do not need fire the event here.
                stream.Close();
                transport.Socket.Close();
            }
        }
    }
}
#endif
