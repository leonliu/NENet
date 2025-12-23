#if !UNITY_WEBGL
namespace NT.Core.Net
{
    /// <summary>
    /// Interface for encoding and decoding packet data.
    /// Implement this to support custom packet protocols.
    /// </summary>
    public interface IPacketCodec
    {
        /// <summary>
        /// Encodes packet data into a byte array for transmission.
        /// </summary>
        /// <param name="command">The command identifier.</param>
        /// <param name="token">The token identifier.</param>
        /// <param name="body">The packet body data.</param>
        /// <returns>Encoded byte array ready to send.</returns>
        byte[] Encode(uint command, ulong token, byte[] body);

        /// <summary>
        /// Decodes a byte array into packet components.
        /// </summary>
        /// <param name="data">The received byte array.</param>
        /// <param name="command">Output: The command identifier.</param>
        /// <param name="token">Output: The token identifier.</param>
        /// <param name="body">Output: The packet body data.</param>
        /// <returns>True if decoding succeeded, false otherwise.</returns>
        bool Decode(byte[] data, out uint command, out ulong token, out byte[] body);
    }
}
#endif
