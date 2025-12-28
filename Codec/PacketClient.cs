#if !UNITY_WEBGL
using System;
using UnityEngine;

namespace NT.Core.Net
{
    /// <summary>
    /// High-level client with codec support for protocol-specific packet handling.
    /// Extends the base Client with Encode/Decode functionality using IPacketCodec.
    /// </summary>
    public class PacketClient : Client
    {
        private IPacketCodec _codec;

        /// <summary>
        /// The codec used for encoding and decoding packets.
        /// </summary>
        public IPacketCodec Codec => _codec;

        /// <summary>
        /// Creates a new PacketClient with the specified tag and codec.
        /// </summary>
        /// <param name="tag">Client tag for connection identification.</param>
        /// <param name="codec">The packet codec to use. If null, uses NENetPacketCodec.</param>
        public PacketClient(string tag, IPacketCodec codec = null) : base(tag)
        {
            // codec can be null - will use default NENetPacketCodec
            // base(tag) already validates tag
            _codec = codec ?? new NENetPacketCodec();
        }

        /// <summary>
        /// Creates a new PacketClient with default tag and codec.
        /// </summary>
        public PacketClient() : this("default", null)
        {
        }

        /// <summary>
        /// Sends a packet with command, token, and body using the codec.
        /// </summary>
        /// <param name="command">The command identifier.</param>
        /// <param name="token">The token identifier.</param>
        /// <param name="body">The packet body data.</param>
        /// <returns>True if the packet was encoded and queued successfully, false otherwise.</returns>
        public bool Send(uint command, ulong token, byte[] body)
        {
            try
            {
                byte[] encoded = _codec.Encode(command, token, body);
                return Send(encoded);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PacketClient] SendPacket failed: {e}");
                return false;
            }
        }

        /// <summary>
        /// Tries to get and decode the next event as a packet.
        /// </summary>
        /// <param name="command">Output: The command identifier.</param>
        /// <param name="token">Output: The token identifier.</param>
        /// <param name="body">Output: The packet body data.</param>
        /// <param name="eventType">Output: The event type.</param>
        /// <returns>True if a Data event was successfully decoded, false otherwise.</returns>
        public bool TryGetNextPacket(out uint command, out ulong token, out byte[] body, out EventType eventType)
        {
            command = 0;
            token = 0;
            body = null;
            eventType = EventType.Disconnected;

            if (TryGetNextEvent(out Event ev))
            {
                eventType = ev.eventType;

                if (ev.eventType == EventType.Data && ev.data != null)
                {
                    return _codec.Decode(ev.data, out command, out token, out body);
                }

                // For Connected/Disconnected events, return true with null values
                return ev.eventType == EventType.Connected || ev.eventType == EventType.Disconnected;
            }

            return false;
        }
    }
}
#endif
