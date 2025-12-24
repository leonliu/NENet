#if !UNITY_WEBGL
using System;
using System.Text;

namespace NT.Core.Net.Security
{
    /// <summary>
    /// RC4 stream cipher.
    /// Fast symmetric stream cipher, suitable for high-throughput scenarios.
    /// Note: RC4 is considered cryptographically broken and should not be used
    /// for new systems requiring strong security. Consider ChaCha20 for new designs.
    /// Included primarily for compatibility with existing servers.
    /// </summary>
    public sealed class Rc4Cipher : IPacketCipher
    {
        private readonly byte[] _key;

        /// <summary>
        /// Creates a new RC4 cipher with the given key.
        /// </summary>
        /// <param name="key">The encryption key (1-256 bytes).</param>
        public Rc4Cipher(byte[] key)
        {
            if (key == null || key.Length < 1 || key.Length > 256)
                throw new ArgumentException("Key must be 1-256 bytes", nameof(key));
            _key = (byte[])key.Clone();
        }

        /// <summary>
        /// Creates a new RC4 cipher with the given key string.
        /// </summary>
        /// <param name="key">The encryption key as a UTF-8 string.</param>
        public Rc4Cipher(string key) : this(Encoding.UTF8.GetBytes(key))
        {
        }

        /// <summary>
        /// Encrypts the data using RC4 stream cipher.
        /// </summary>
        public byte[] Encrypt(byte[] data) => Transform(data);

        /// <summary>
        /// Decrypts the data (RC4 is symmetric, same as encrypt).
        /// </summary>
        public byte[] Decrypt(byte[] data) => Transform(data);

        /// <summary>
        /// RC4 transform (encrypt/decrypt are identical).
        /// </summary>
        private byte[] Transform(byte[] input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            // Initialize S-box
            byte[] s = new byte[256];
            for (int i = 0; i < 256; i++)
                s[i] = (byte)i;

            // Key scheduling algorithm (KSA)
            byte j = 0;
            byte[] key = _key;
            int keyLen = key.Length;

            for (int i = 0; i < 256; i++)
            {
                j = (byte)((j + s[i] + key[i % keyLen]) & 0xFF);
                // Swap s[i] and s[j]
                byte temp = s[i];
                s[i] = s[j];
                s[j] = temp;
            }

            // Pseudo-random generation algorithm (PRGA) + XOR
            byte[] result = new byte[input.Length];
            byte i2 = 0, j2 = 0;

            for (int k = 0; k < input.Length; k++)
            {
                i2 = (byte)((i2 + 1) & 0xFF);
                j2 = (byte)((j2 + s[i2]) & 0xFF);
                // Swap s[i2] and s[j2]
                byte temp = s[i2];
                s[i2] = s[j2];
                s[j2] = temp;
                result[k] = (byte)(input[k] ^ s[(byte)((s[i2] + s[j2]) & 0xFF)]);
            }

            return result;
        }

        /// <summary>
        /// Cipher name for logging.
        /// </summary>
        public string Name => "RC4";
    }
}
#endif
