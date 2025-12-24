#if !UNITY_WEBGL
using System;
using System.Text;

namespace NT.Core.Net.Security
{
    /// <summary>
    /// XOR cipher with repeating key.
    /// Lightweight, suitable for obfuscation only (not real security).
    /// </summary>
    public sealed class XorCipher : IPacketCipher
    {
        private readonly byte[] _key;

        /// <summary>
        /// Creates a new XOR cipher with the given key.
        /// </summary>
        /// <param name="key">The encryption key (1 or more bytes).</param>
        public XorCipher(byte[] key)
        {
            if (key == null || key.Length == 0)
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            _key = (byte[])key.Clone();
        }

        /// <summary>
        /// Creates a new XOR cipher with the given key string.
        /// </summary>
        /// <param name="key">The encryption key as a UTF-8 string.</param>
        public XorCipher(string key) : this(Encoding.UTF8.GetBytes(key))
        {
        }

        /// <summary>
        /// Encrypts the data using XOR cipher.
        /// </summary>
        public byte[] Encrypt(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            byte[] result = new byte[data.Length];
            int keyLen = _key.Length;

            for (int i = 0; i < data.Length; i++)
            {
                result[i] = (byte)(data[i] ^ _key[i % keyLen]);
            }
            return result;
        }

        /// <summary>
        /// Decrypts the data (XOR is symmetric, same as encrypt).
        /// </summary>
        public byte[] Decrypt(byte[] data) => Encrypt(data);

        /// <summary>
        /// Cipher name for logging.
        /// </summary>
        public string Name => "XOR";
    }
}
#endif
