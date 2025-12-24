#if !UNITY_WEBGL
using System;
using System.Security.Cryptography;
using System.Text;
using NT.Core.Net;

namespace NT.Core.Net.Security
{
    /// <summary>
    /// ChaCha20 stream cipher implementation.
    /// Modern, fast, and secure cipher designed by Daniel J. Bernstein.
    /// Widely adopted in TLS 1.3, WireGuard, and SSH.
    /// </summary>
    /// <remarks>
    /// Security considerations:
    /// - Each key/nonce pair should encrypt at most 2^48 blocks (~1 Zettabyte)
    /// - Counter overflow after 2^32 blocks will throw an exception
    /// - For best security, use a unique nonce per message or use ChaCha20Poly1305Cipher
    /// </remarks>
    public sealed class ChaCha20Cipher : IPacketCipher, IDisposable
    {
        private readonly byte[] _key;
        private readonly byte[] _baseNonce;
        private readonly bool _autoGenerateNonce;
        private readonly RandomNumberGenerator _rng;
        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const ulong MaxBlocks = (1UL << 32); // Counter is 32-bit

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
        /// Nonce will be auto-generated per message and prepended to ciphertext.
        /// </summary>
        /// <param name="key">The encryption key (32 bytes).</param>
        public ChaCha20Cipher(byte[] key) : this(key, null)
        {
            _autoGenerateNonce = true;
            _rng = RandomNumberGenerator.Create();
            _baseNonce = new byte[NonceSize];
        }

        /// <summary>
        /// Creates a new ChaCha20 cipher with the given key and fixed nonce.
        /// </summary>
        /// <param name="key">The encryption key (32 bytes).</param>
        /// <param name="nonce">The nonce (12 bytes). If null, nonce will be auto-generated per message.</param>
        /// <remarks>
        /// WARNING: Using a fixed nonce for multiple messages is a security vulnerability.
        /// Use auto-generated nonce (null parameter) or ChaCha20Poly1305Cipher for production.
        /// </remarks>
        public ChaCha20Cipher(byte[] key, byte[] nonce)
        {
            if (key == null || key.Length != KeySize)
                throw new ArgumentException($"Key must be exactly {KeySize} bytes", nameof(key));

            _key = (byte[])key.Clone();
            _autoGenerateNonce = false;
            _rng = null;

            if (nonce != null)
            {
                if (nonce.Length != NonceSize)
                    throw new ArgumentException($"Nonce must be exactly {NonceSize} bytes", nameof(nonce));
                _baseNonce = (byte[])nonce.Clone();
            }
            else
            {
                _baseNonce = new byte[NonceSize];
                _autoGenerateNonce = true;
                _rng = RandomNumberGenerator.Create();
            }
        }

        /// <summary>
        /// Creates a new ChaCha20 cipher with the given key string.
        /// The string is hashed using SHA256 to derive a 32-byte key.
        /// Nonce will be auto-generated per message.
        /// </summary>
        /// <param name="key">The encryption key as a string (any length).</param>
        public ChaCha20Cipher(string key) : this(SHA256.HashData(Encoding.UTF8.GetBytes(key)))
        {
        }

        /// <summary>
        /// Encrypts the data using ChaCha20.
        /// If auto-generating nonce, prepends 12-byte nonce to ciphertext.
        /// </summary>
        public byte[] Encrypt(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            byte[] nonce;
            if (_autoGenerateNonce)
            {
                nonce = new byte[NonceSize];
                _rng.GetBytes(nonce);

                byte[] ciphertext = Transform(data, nonce, 0);
                byte[] result = new byte[NonceSize + ciphertext.Length];
                Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
                Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
                return result;
            }
            else
            {
                return Transform(data, _baseNonce, 0);
            }
        }

        /// <summary>
        /// Decrypts the data using ChaCha20.
        /// Expects nonce to be prepended if auto-generating nonce.
        /// </summary>
        public byte[] Decrypt(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            byte[] nonce;
            byte[] ciphertext;

            if (_autoGenerateNonce)
            {
                if (data.Length < NonceSize)
                    throw new CryptographicException("Ciphertext too short: missing nonce");

                nonce = new byte[NonceSize];
                Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);

                ciphertext = new byte[data.Length - NonceSize];
                Buffer.BlockCopy(data, NonceSize, ciphertext, 0, ciphertext.Length);
            }
            else
            {
                nonce = _baseNonce;
                ciphertext = data;
            }

            return Transform(ciphertext, nonce, 0);
        }

        /// <summary>
        /// Performs ChaCha20 transform on the input data with specified nonce and counter.
        /// </summary>
        private byte[] Transform(byte[] input, byte[] nonce, uint startCounter)
        {
            byte[] output = new byte[input.Length];

            // Initialize state
            uint[] state = new uint[16];
            InitializeState(state, _key, nonce, startCounter);

            // Calculate max blocks we can process with 32-bit counter
            ulong inputBlocks = ((ulong)input.Length + 63) / 64;
            if (inputBlocks > MaxBlocks)
                throw new CryptographicException(
                    $"Data too large ({input.Length} bytes). Counter would overflow. " +
                    $"Maximum is {MaxBlocks * 64} bytes per key/nonce combination.");

            // Process in 64-byte blocks
            for (int offset = 0; offset < input.Length; offset += 64)
            {
                // Generate keystream block
                uint[] keystreamState = new uint[16];
                Array.Copy(state, keystreamState, 16);
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
                state[4 + i] = Utils.ToUInt32LittleEndian(key, i * 4);
            }

            // Counter (12)
            state[12] = counter;

            // Nonce (13-15)
            for (int i = 0; i < 3; i++)
            {
                state[13 + i] = Utils.ToUInt32LittleEndian(nonce, i * 4);
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
            uint[] workingState = new uint[16];
            Array.Copy(state, workingState, 16);

            // 20 rounds (10 column + 10 diagonal)
            for (int round = 0; round < 10; round++)
            {
                // Column rounds
                QuarterRound(ref workingState[0], ref workingState[4], ref workingState[8], ref workingState[12]);
                QuarterRound(ref workingState[1], ref workingState[5], ref workingState[9], ref workingState[13]);
                QuarterRound(ref workingState[2], ref workingState[6], ref workingState[10], ref workingState[14]);
                QuarterRound(ref workingState[3], ref workingState[7], ref workingState[11], ref workingState[15]);

                // Diagonal rounds
                QuarterRound(ref workingState[0], ref workingState[5], ref workingState[10], ref workingState[15]);
                QuarterRound(ref workingState[1], ref workingState[6], ref workingState[11], ref workingState[12]);
                QuarterRound(ref workingState[2], ref workingState[7], ref workingState[8], ref workingState[13]);
                QuarterRound(ref workingState[3], ref workingState[4], ref workingState[9], ref workingState[14]);
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
            if (_baseNonce != null)
            {
                Array.Clear(_baseNonce, 0, _baseNonce.Length);
            }
            if (_rng != null)
            {
                _rng.Dispose();
            }
        }
    }
}
#endif
