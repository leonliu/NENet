#if !UNITY_WEBGL
using System;
using System.Text;

namespace NT.Core.Net.Security
{
    /// <summary>
    /// ChaCha20 stream cipher implementation.
    /// Modern, fast, and secure cipher designed by Daniel J. Bernstein.
    /// Widely adopted in TLS 1.3, WireGuard, and SSH.
    /// </summary>
    public sealed class ChaCha20Cipher : IPacketCipher, IDisposable
    {
        private readonly byte[] _key;
        private readonly byte[] _nonce;
        private const int KeySize = 32;
        private const int NonceSize = 12;

        // ChaCha20 constant ("expand 32-byte k")
        private static readonly uint[] Sigma = new uint[4]
        {
            0x61707865,  // "expa"
            0x3320646e,  // "nd 3"
            0x79622d32,  // "2-by"
            0x6b206574   // "te k"
        };

        /// <summary>
        /// Creates a new ChaCha20 cipher with the given key.
        /// </summary>
        /// <param name="key">The encryption key (32 bytes).</param>
        public ChaCha20Cipher(byte[] key)
        {
            if (key == null || key.Length != KeySize)
                throw new ArgumentException($"Key must be exactly {KeySize} bytes", nameof(key));
            _key = (byte[])key.Clone();
            _nonce = new byte[NonceSize]; // Zero nonce (for single key per connection)
        }

        /// <summary>
        /// Creates a new ChaCha20 cipher with the given key string.
        /// The string is hashed using SHA256 to derive a 32-byte key.
        /// </summary>
        /// <param name="key">The encryption key as a string (any length).</param>
        public ChaCha20Cipher(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            _key = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(key));
            _nonce = new byte[NonceSize];
        }

        /// <summary>
        /// Encrypts the data using ChaCha20.
        /// </summary>
        public byte[] Encrypt(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return Transform(data);
        }

        /// <summary>
        /// Decrypts the data using ChaCha20 (same as encrypt - symmetric).
        /// </summary>
        public byte[] Decrypt(byte[] data) => Encrypt(data);

        /// <summary>
        /// Performs ChaCha20 transform on the input data.
        /// </summary>
        private byte[] Transform(byte[] input)
        {
            byte[] output = new byte[input.Length];

            // Initialize state
            uint[] state = new uint[16];
            InitializeState(state, _key, _nonce, 0);

            // Process in 64-byte blocks
            for (int offset = 0; offset < input.Length; offset += 64)
            {
                // Generate keystream block
                uint[] keystreamState = (uint[])state.Clone();
                ChaCha20Block(keystreamState);

                // XOR with input (up to 64 bytes)
                int blockSize = Math.Min(64, input.Length - offset);
                for (int i = 0; i < blockSize; i++)
                {
                    // Extract byte from keystream (little-endian)
                    uint keystreamWord = keystreamState[i / 4];
                    int shift = (i % 4) * 8;
                    byte keystreamByte = (byte)(keystreamWord >> shift);
                    output[offset + i] = (byte)(input[offset + i] ^ keystreamByte);
                }

                // Increment counter for next block
                state[12]++;
                if (state[12] == 0)
                {
                    // Counter overflow - in production would increment nonce
                }
            }

            return output;
        }

        /// <summary>
        /// Initializes the ChaCha20 state.
        /// </summary>
        private void InitializeState(uint[] state, byte[] key, byte[] nonce, uint counter)
        {
            // Constants (0-3)
            state[0] = Sigma[0];
            state[1] = Sigma[1];
            state[2] = Sigma[2];
            state[3] = Sigma[3];

            // Key (4-11)
            for (int i = 0; i < 8; i++)
            {
                state[4 + i] = BitConverter.ToUInt32(key, i * 4);
            }

            // Counter (12)
            state[12] = counter;

            // Nonce (13-15)
            for (int i = 0; i < 3; i++)
            {
                state[13 + i] = BitConverter.ToUInt32(nonce, i * 4);
            }
        }

        /// <summary>
        /// Performs the ChaCha20 quarter round on four values.
        /// </summary>
        private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a += b; d ^= a; d = RotateLeft(d, 16);
            c += d; b ^= c; b = RotateLeft(b, 12);
            a += b; d ^= a; d = RotateLeft(d, 8);
            c += d; b ^= c; b = RotateLeft(b, 7);
        }

        /// <summary>
        /// ChaCha20 block function (20 rounds).
        /// 10 column rounds + 10 diagonal rounds.
        /// </summary>
        private static void ChaCha20Block(uint[] state)
        {
            uint[] workingState = (uint[])state.Clone();

            // 20 rounds (10 column + 10 diagonal)
            for (int round = 0; round < 10; round++)
            {
                // Column rounds
                QuarterRound(ref workingState[0], ref workingState[4], ref workingState[8],  ref workingState[12]);
                QuarterRound(ref workingState[1], ref workingState[5], ref workingState[9],  ref workingState[13]);
                QuarterRound(ref workingState[2], ref workingState[6], ref workingState[10], ref workingState[14]);
                QuarterRound(ref workingState[3], ref workingState[7], ref workingState[11], ref workingState[15]);

                // Diagonal rounds
                QuarterRound(ref workingState[0], ref workingState[5], ref workingState[10], ref workingState[15]);
                QuarterRound(ref workingState[1], ref workingState[6], ref workingState[11], ref workingState[12]);
                QuarterRound(ref workingState[2], ref workingState[7], ref workingState[8],  ref workingState[13]);
                QuarterRound(ref workingState[3], ref workingState[4], ref workingState[9],  ref workingState[14]);
            }

            // Add initial state to working state
            for (int i = 0; i < 16; i++)
            {
                state[i] = workingState[i] + state[i];
            }
        }

        /// <summary>
        /// Rotates a 32-bit value left by the specified amount.
        /// </summary>
        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        /// <summary>
        /// Cipher name for logging.
        /// </summary>
        public string Name => "ChaCha20";

        /// <summary>
        /// Disposes resources by clearing sensitive data.
        /// </summary>
        public void Dispose()
        {
            if (_key != null)
            {
                Array.Clear(_key, 0, _key.Length);
            }
            if (_nonce != null)
            {
                Array.Clear(_nonce, 0, _nonce.Length);
            }
        }
    }
}
#endif
