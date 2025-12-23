#if !UNITY_WEBGL
using System;

namespace NT.Core.Net
{
    /// <summary>
    /// Default NENet packet codec implementation.
    /// Protocol: [length(4)][command(4)][token(8)][body...]
    /// </summary>
    public class NENetPacketCodec : IPacketCodec
    {
        /// <summary>
        /// Minimum packet size: 4 bytes command + 8 bytes token.
        /// </summary>
        public const int MinPacketSize = 12;

        /// <summary>
        /// Maximum packet size allowed.
        /// </summary>
        public const int MaxPacketSize = 16 * 1024;

        /// <summary>
        /// Encodes packet data into the NENet protocol format.
        /// </summary>
        public byte[] Encode(uint command, ulong token, byte[] body)
        {
            body = body ?? Array.Empty<byte>();

            int totalSize = 4 + 4 + 8 + body.Length;  // length + command + token + body
            byte[] packet = new byte[totalSize];

            // Write packet length (excluding the length field itself)
            int payloadSize = 4 + 8 + body.Length;
            Utils.GetBytes(payloadSize, packet);

            // Write command
            Utils.GetBytes((int)command, packet, 4);

            // Write token
            Utils.GetBytes((long)token, packet, 8);

            // Write body
            if (body.Length > 0)
            {
                Array.Copy(body, 0, packet, 16, body.Length);
            }

            return packet;
        }

        /// <summary>
        /// Decodes NENet protocol packet data.
        /// </summary>
        public bool Decode(byte[] data, out uint command, out ulong token, out byte[] body)
        {
            command = 0;
            token = 0;
            body = Array.Empty<byte>();

            if (data == null || data.Length < MinPacketSize)
            {
                return false;
            }

            // Extract command (bytes 4-7)
            byte[] commandBytes = new byte[4];
            Array.Copy(data, 4, commandBytes, 0, 4);
            command = Utils.ToUInt32(commandBytes);

            // Extract token (bytes 8-15)
            byte[] tokenBytes = new byte[8];
            Array.Copy(data, 8, tokenBytes, 0, 8);
            token = Utils.ToUInt64(tokenBytes);

            // Extract body (bytes 16+)
            int bodySize = data.Length - 16;
            if (bodySize > 0)
            {
                body = new byte[bodySize];
                Array.Copy(data, 16, body, 0, bodySize);
            }
            else
            {
                body = Array.Empty<byte>();
            }

            return true;
        }

        /// <summary>
        /// Validates a packet size against protocol limits.
        /// </summary>
        public bool IsValidPacketSize(int size)
        {
            return size >= MinPacketSize && size <= MaxPacketSize;
        }
    }
}
#endif
