#if !UNITY_WEBGL
namespace NT.Core.Net.Security
{
    /// <summary>
    /// No encryption (plaintext pass-through).
    /// </summary>
    public sealed class NullCipher : IPacketCipher
    {
        /// <summary>
        /// Returns the data unchanged (no encryption).
        /// </summary>
        public byte[] Encrypt(byte[] data)
        {
            return data;
        }

        /// <summary>
        /// Returns the data unchanged (no decryption).
        /// </summary>
        public byte[] Decrypt(byte[] data)
        {
            return data;
        }

        /// <summary>
        /// Cipher name for logging.
        /// </summary>
        public string Name => "None";
    }
}
#endif
