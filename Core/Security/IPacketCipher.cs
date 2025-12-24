#if !UNITY_WEBGL
using System;

namespace NT.Core.Net.Security
{
    /// <summary>
    /// Interface for packet encryption/decryption.
    /// </summary>
    public interface IPacketCipher
    {
        /// <summary>
        /// Encrypts the given data.
        /// </summary>
        /// <param name="data">The plaintext data.</param>
        /// <returns>The encrypted data.</returns>
        byte[] Encrypt(byte[] data);

        /// <summary>
        /// Decrypts the given data.
        /// </summary>
        /// <param name="data">The encrypted data.</param>
        /// <returns>The decrypted data.</returns>
        byte[] Decrypt(byte[] data);

        /// <summary>
        /// Name of this cipher for logging.
        /// </summary>
        string Name { get; }
    }
}
#endif
