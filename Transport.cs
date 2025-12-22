#if !UNITY_WEBGL
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Net.Sockets;
using UnityEngine;

namespace NT.Core.Net
{
    public class Transport
    {
        private TcpClient _client;
        public TcpClient Client { get => _client; }
        public bool Connected
        {
            get
            {
                return (_client != null && _client.Connected) && (_client.Client != null && _client.Client.Connected);
            }
        }                

        /// <summary>
        /// Alert is set if receive queue size exceeds this value. It is an
        /// indication that the received messages have not been processed in
        /// time.
        /// </summary>
        public static int recvQueueWarningLevel = 1000;

        /// <summary>
        /// Maximum packet size allowed, 16KB should be enough.
        /// </summary>
        public static int MaxPacketSize = 16 * 1024;        

        // pre-allocated thread local buffers to avoid memory allocations
        [ThreadStatic]
        static byte[] _header;
        [ThreadStatic]
        static byte[] _command;
        [ThreadStatic]
        static byte[] _token;

        // send buffer, every thread has a send buffer pre-allocated
        [ThreadStatic]
        static byte[] _buffer;

        public Transport()
        {
            // TcpClient() creates IPv4 socket, clear the internal socket
            // so that later Connect() call will resolve the hostname and 
            // create IPv4 or IPv6 socket as needed.
            _client = new TcpClient();
            _client.Client = null;
        }

        public void Connect(string ip, int port)
        {
            _client.Connect(ip, port);
        }

        public void Close()
        {
            _client.Close();
        }

        static bool SendPacket(NetworkStream stream, byte[][] packets)
        {
            try
            {
                // combine multiple packets to avoid TCP overhead and get higher performance
                int totalSize = 0;
                for (int i = 0; i < packets.Length; i++)
                {
                    // 4 bytes header + payload
                    totalSize += sizeof(int) + packets[i].Length;
                }

                if (_buffer == null || _buffer.Length < totalSize)
                {
                    _buffer = new byte[totalSize];
                }

                int pos = 0;
                for (int i = 0; i < packets.Length; i++)
                {
                    if (_header == null)
                    {
                        _header = new byte[4];
                    }

                    // save the packet length to header
                    Utils.GetBytes(packets[i].Length, _header);

                    // pack header and packet data to buffer
                    Array.Copy(_header, 0, _buffer, pos, _header.Length);
                    pos += _header.Length;
                    Array.Copy(packets[i], 0, _buffer, pos, packets[i].Length);
                    pos += packets[i].Length;
                }

                // send to remote, the Write method blocks until the requested number 
                // of bytes is sent or a SocketException is thrown.
                stream.Write(_buffer, 0, totalSize);
                return true;
            }
            catch (Exception e)
            {
                // stream.Write throws exceptions if server shuts down
                Debug.Log($"[Transport] Send exception: {e}");
                return false;
            }
        }

        static bool ReceivePacket(NetworkStream stream, out Packet packet)
        {
            packet = null;

            if (_header == null)
                _header = new byte[4];

            if (!stream.ReadExactly(_header, 4))
                return false;

            int size = Utils.ToInt32(_header);

            // the packet payload always contains a 4 bytes command field and
            // 8 bytes token.
            if (size > MaxPacketSize || size <= 12)
            {
                Debug.LogError($"[Transport] Receive invalid packet size: {size}");
                return false;
            }

            if (_command == null)
                _command = new byte[4];

            if (!stream.ReadExactly(_command, 4))
                return false;

            uint command = Utils.ToUInt32(_command);

            if (_token == null)
                _token = new byte[8];

            if (!stream.ReadExactly(_token, 8))
                return false;

            ulong token = Utils.ToUInt64(_token);

            byte[] data = new byte[size - 12];
            if (!stream.ReadExactly(data, size))
                return false;

            packet = new Packet(command, token, data);
            return true;
        }

        public static void Receive(string tag, TcpClient client, ConcurrentQueue<Event> recvQueue)
        {
            NetworkStream stream = client.GetStream();
            DateTime lastWarnTime = DateTime.Now;

            try
            {
                recvQueue.Enqueue(new Event(tag, EventType.Connected, null));
                while (true)
                {
                    Packet packet;
                    if (!ReceivePacket(stream, out packet))
                        break;

                    recvQueue.Enqueue(new Event(tag, EventType.Data, packet));
                    if (recvQueue.Count > recvQueueWarningLevel)
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
            catch (Exception e)
            {
                // something wrong happens, the thread was interrupted or remote closed
                // the connection or we closed it. Stop gracefully.
                Debug.Log($"[Transport] Receive proc finished! tag={tag}, reason={e}");
            }
            finally
            {
                stream.Close();
                client.Close();

                recvQueue.Enqueue(new Event(tag, EventType.Disconnected, null));
            }
        }

        public static void Send(string tag, TcpClient client, SafeQueue<byte[]> sendQueue, ManualResetEvent mre)
        {
            NetworkStream stream = client.GetStream();

            try
            {
                while (client.Connected)
                {
                    // reset the signal
                    mre.Reset();

                    byte[][] packets;
                    if (sendQueue.TryDequeueAll(out packets))
                    {
                        if (!SendPacket(stream, packets))
                            break;
                    }

                    // wait for more data blockingly
                    mre.WaitOne();
                }
            }
            catch (Exception e)
            {
                // Exceptions happen when thread is stopped, interrupted or connection is closed by either
                // local or remote. Stop and cleanup gracefully
                Debug.Log($"[Transport] Send proc finished! tag={tag}, reason={e}");
            }
            finally
            {
                // When we close the socket in send loop, the receive loop will finally encounter failure
                // and fire the Disconnected event. Thus we do not need fire the event here.
                stream.Close();
                client.Close();
            }
        }
    }
}
#endif