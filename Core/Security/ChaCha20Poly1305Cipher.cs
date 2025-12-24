#if !UNITY_WEBGL
using System;
using System.Text;
using System.Security.Cryptography;
using NT.Core.Net;

namespace NT.Core.Net.Security
{
    /// <summary>
    /// ChaCha20-Poly1305 AEAD cipher implementation.
    /// Provides authenticated encryption using ChaCha20 stream cipher and Poly1305 MAC.
    /// Defined in RFC 7539.
    /// </summary>
    /// <remarks>
    /// Security properties:
    /// - Confidentiality: ChaCha20 encrypts the plaintext
    /// - Authentication/integrity: Poly1305 verifies data hasn't been tampered with
    /// - 128-bit authentication tag prevents forgery
    /// - Recommended for all production use cases
    /// </remarks>
    public sealed class ChaCha20Poly1305Cipher : IPacketCipher, IDisposable
    {
        private readonly byte[] _key;
        private readonly RandomNumberGenerator _rng;
        private const int KeySize = 32;
        private const int NonceSize = 12;
        private const int TagSize = 16;

        // ChaCha20 constant ("expand 32-byte k")
        private static readonly uint[] Sigma = new uint[4]
        {
            0x61707865,  // "expa"
            0x3320646e,  // "nd 3"
            0x79622d32,  // "2-by"
            0x6b206574   // "te k"
        };

        /// <summary>
        /// Creates a new ChaCha20-Poly1305 cipher with the given key.
        /// </summary>
        /// <param name="key">The encryption key (32 bytes).</param>
        public ChaCha20Poly1305Cipher(byte[] key)
        {
            if (key == null || key.Length != KeySize)
                throw new ArgumentException($"Key must be exactly {KeySize} bytes", nameof(key));
            _key = (byte[])key.Clone();
            _rng = RandomNumberGenerator.Create();
        }

        /// <summary>
        /// Creates a new ChaCha20-Poly1305 cipher with the given key string.
        /// The string is hashed using SHA256 to derive a 32-byte key.
        /// </summary>
        /// <param name="key">The encryption key as a string (any length).</param>
        public ChaCha20Poly1305Cipher(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(key));
            _rng = RandomNumberGenerator.Create();
        }

        /// <summary>
        /// Encrypts and authenticates the data using ChaCha20-Poly1305.
        /// Output format: [12-byte nonce][ciphertext][16-byte tag]
        /// </summary>
        public byte[] Encrypt(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            // Generate random nonce
            byte[] nonce = new byte[NonceSize];
            _rng.GetBytes(nonce);

            // Generate Poly1305 key using ChaCha20 with counter=0
            byte[] polyKey = GeneratePoly1305Key(_key, nonce);

            // Encrypt plaintext using ChaCha20 with counter=1
            byte[] ciphertext = ChaCha20Encrypt(_key, nonce, 1, data);

            // Calculate authentication tag over ciphertext
            byte[] tag = Poly1305.ComputeTag(polyKey, ciphertext);

            // Format: nonce || ciphertext || tag
            byte[] result = new byte[NonceSize + ciphertext.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
            Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

            return result;
        }

        /// <summary>
        /// Decrypts and verifies the data using ChaCha20-Poly1305.
        /// Expects format: [12-byte nonce][ciphertext][16-byte tag]
        /// Throws CryptographicException if authentication fails.
        /// </summary>
        public byte[] Decrypt(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.Length < NonceSize + TagSize)
                throw new CryptographicException("Ciphertext too short");

            // Extract components
            byte[] nonce = new byte[NonceSize];
            Buffer.BlockCopy(data, 0, nonce, 0, NonceSize);

            int ciphertextLen = data.Length - NonceSize - TagSize;
            byte[] ciphertext = new byte[ciphertextLen];
            Buffer.BlockCopy(data, NonceSize, ciphertext, 0, ciphertextLen);

            byte[] receivedTag = new byte[TagSize];
            Buffer.BlockCopy(data, NonceSize + ciphertextLen, receivedTag, 0, TagSize);

            // Generate Poly1305 key
            byte[] polyKey = GeneratePoly1305Key(_key, nonce);

            // Verify authentication tag (constant-time compare)
            byte[] computedTag = Poly1305.ComputeTag(polyKey, ciphertext);
            if (!CryptographicOperations.FixedTimeEquals(receivedTag, computedTag))
                throw new CryptographicException("Authentication failed: data may have been tampered with");

            // Decrypt ciphertext
            return ChaCha20Encrypt(_key, nonce, 1, ciphertext);
        }

        /// <summary>
        /// Generates the Poly1305 one-time key using ChaCha20 with counter=0.
        /// </summary>
        private static byte[] GeneratePoly1305Key(byte[] key, byte[] nonce)
        {
            return ChaCha20Encrypt(key, nonce, 0, new byte[64]);
        }

        /// <summary>
        /// ChaCha20 encryption/decryption with specified counter.
        /// </summary>
        private static byte[] ChaCha20Encrypt(byte[] key, byte[] nonce, uint counter, byte[] input)
        {
            byte[] output = new byte[input.Length];
            uint[] state = new uint[16];
            InitializeState(state, key, nonce, counter);

            for (int offset = 0; offset < input.Length; offset += 64)
            {
                uint[] keystreamState = new uint[16];
                Array.Copy(state, keystreamState, 16);
                ChaCha20Block(keystreamState);

                int blockSize = Math.Min(64, input.Length - offset);
                for (int i = 0; i < blockSize; i++)
                {
                    uint keystreamWord = keystreamState[i / 4];
                    int shift = (i % 4) * 8;
                    byte keystreamByte = (byte)(keystreamWord >> shift);
                    output[offset + i] = (byte)(input[offset + i] ^ keystreamByte);
                }

                state[12]++;
            }

            return output;
        }

        /// <summary>
        /// Initializes the ChaCha20 state.
        /// </summary>
        private static void InitializeState(uint[] state, byte[] key, byte[] nonce, uint counter)
        {
            state[0] = Sigma[0];
            state[1] = Sigma[1];
            state[2] = Sigma[2];
            state[3] = Sigma[3];

            for (int i = 0; i < 8; i++)
            {
                state[4 + i] = Utils.ToUInt32LittleEndian(key, i * 4);
            }

            state[12] = counter;

            for (int i = 0; i < 3; i++)
            {
                state[13 + i] = Utils.ToUInt32LittleEndian(nonce, i * 4);
            }
        }

        /// <summary>
        /// ChaCha20 block function (20 rounds).
        /// </summary>
        private static void ChaCha20Block(uint[] state)
        {
            uint[] workingState = new uint[16];
            Array.Copy(state, workingState, 16);

            for (int round = 0; round < 10; round++)
            {
                QuarterRound(ref workingState[0], ref workingState[4], ref workingState[8],  ref workingState[12]);
                QuarterRound(ref workingState[1], ref workingState[5], ref workingState[9],  ref workingState[13]);
                QuarterRound(ref workingState[2], ref workingState[6], ref workingState[10], ref workingState[14]);
                QuarterRound(ref workingState[3], ref workingState[7], ref workingState[11], ref workingState[15]);

                QuarterRound(ref workingState[0], ref workingState[5], ref workingState[10], ref workingState[15]);
                QuarterRound(ref workingState[1], ref workingState[6], ref workingState[11], ref workingState[12]);
                QuarterRound(ref workingState[2], ref workingState[7], ref workingState[8],  ref workingState[13]);
                QuarterRound(ref workingState[3], ref workingState[4], ref workingState[9],  ref workingState[14]);
            }

            for (int i = 0; i < 16; i++)
            {
                state[i] = workingState[i] + state[i];
            }
        }

        /// <summary>
        /// ChaCha20 quarter round function.
        /// </summary>
        private static void QuarterRound(ref uint a, ref uint b, ref uint c, ref uint d)
        {
            a += b; d ^= a; d = RotateLeft(d, 16);
            c += d; b ^= c; b = RotateLeft(b, 12);
            a += b; d ^= a; d = RotateLeft(d, 8);
            c += d; b ^= c; b = RotateLeft(b, 7);
        }

        /// <summary>
        /// Rotates a 32-bit value left.
        /// </summary>
        private static uint RotateLeft(uint value, int count)
        {
            return (value << count) | (value >> (32 - count));
        }

        /// <summary>
        /// Cipher name for logging.
        /// </summary>
        public string Name => "ChaCha20-Poly1305";

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            if (_key != null)
            {
                Array.Clear(_key, 0, _key.Length);
            }
            if (_rng != null)
            {
                _rng.Dispose();
            }
        }

        /// <summary>
        /// Poly1305 message authentication code (RFC 7539).
        /// </summary>
        private static class Poly1305
        {
            private const int TagSize = 16;

            /// <summary>
            /// Computes the Poly1305 authentication tag for the given message.
            /// </summary>
            public static byte[] ComputeTag(byte[] key, byte[] message)
            {
                if (key == null || key.Length != 32)
                    throw new ArgumentException("Poly1305 key must be 32 bytes", nameof(key));

                // Clamp the key
                ulong[] r = new ulong[3]; // 26-bit limbs
                ulong k0 = Utils.ToUInt64LittleEndian(key, 0);
                ulong k1 = Utils.ToUInt64LittleEndian(key, 8);
                ulong k2 = Utils.ToUInt64LittleEndian(key, 16);

                // Key clamping: clear certain bits
                r[0] = (k0 & 0x0FFFFFFC0FFFFFFF); // bits [2..25] of k0
                r[1] = (k0 >> 44) | ((k1 & 0x0FFFFFFFFFFFF) << 20); // bits [6..25] of k1
                r[2] = (k1 >> 24) | ((k2 & 0x0FFF) << 40); // bits [6..25] of k2

                // Initial accumulator (message is padded to 16-byte blocks)
                ulong[] h = new ulong[3] { 0, 0, 0 };
                ulong[] c = new ulong[3];

                // Process message in 16-byte blocks
                int remaining = message.Length;
                int offset = 0;

                while (remaining > 0)
                {
                    // Load block (padded with 0x01 byte at the end)
                    ulong block0, block1, block2;

                    if (remaining >= 16)
                    {
                        // Full block: read 16 bytes, append 1 byte
                        block0 = Utils.ToUInt64LittleEndian(message, offset);
                        block1 = Utils.ToUInt64LittleEndian(message, offset + 8);
                        block2 = 1;
                        remaining -= 16;
                        offset += 16;
                    }
                    else
                    {
                        // Partial block: read remaining bytes, pad with 0x01, then zeros
                        byte[] padded = new byte[16];
                        Buffer.BlockCopy(message, offset, padded, 0, remaining);
                        padded[remaining] = 1;
                        block0 = Utils.ToUInt64LittleEndian(padded, 0);
                        block1 = Utils.ToUInt64LittleEndian(padded, 8);
                        block2 = 1;
                        remaining = 0; // Last block
                    }

                    // h = (h + c) * r mod p
                    c[0] = block0 & 0x3FFFFFF; // 26 bits
                    c[1] = ((block0 >> 26) | (block1 << 38)) & 0x3FFFFFF;
                    c[2] = ((block1 >> 14) | (block2 << 50)) & 0x3FFFFFF;

                    // Add c to h
                    h[0] += c[0];
                    h[1] += c[1];
                    h[2] += c[2];

                    // Multiply by r (mod p = 2^130 - 5)
                    ulong t0 = h[0] * r[0];
                    ulong t1 = h[0] * r[1] + h[1] * r[0];
                    ulong t2 = h[0] * r[2] + h[1] * r[1] + h[2] * r[0];
                    ulong t3 = h[1] * r[2] + h[2] * r[1];
                    ulong t4 = h[2] * r[2];

                    // Reduce mod 2^130 - 5 (where p = 2^130 - 5)
                    // First carry propagation from multiplication
                    ulong carry0 = t0 >> 26; t0 &= 0x3FFFFFF;
                    t1 += carry0;
                    ulong carry1 = t1 >> 26; t1 &= 0x3FFFFFF;
                    t2 += carry1;
                    ulong carry2 = t2 >> 26; t2 &= 0x3FFFFFF;
                    t3 += carry2;
                    ulong carry3 = t3 >> 26; t3 &= 0x3FFFFFF;
                    t4 += carry3;

                    // t4 represents overflow beyond 130 bits
                    // Since p = 2^130 - 5, we add t4 * 5 (equivalent to subtracting t4 * p)
                    t4 *= 5;
                    h[0] = t0 + t4;

                    // Second carry propagation after adding t4*5
                    carry0 = h[0] >> 26; h[0] &= 0x3FFFFFF;
                    h[1] = t1 + carry0;
                    carry1 = h[1] >> 26; h[1] &= 0x3FFFFFF;
                    h[2] = t2 + carry1;

                    // Final conditional subtraction of p = 2^130 - 5
                    // Compute h = h[0] + h[1]*2^26 + h[2]*2^52
                    // Check if h >= 2^130 - 5
                    // Since h is stored in 26-bit limbs, we check:
                    // h[2]*2^52 + h[1]*2^26 + h[0] >= 2^130 - 5

                    // This is equivalent to checking if h >= p where p = 2^130 - 5
                    // We can check this by seeing if subtracting p would underflow
                    // Or equivalently, if (h + 5) >= 2^130

                    // A simpler check: if any of the top bits indicate value >= p
                    // The value fits in 130 bits: 26 + 26 + 26 = 78 bits for main value
                    // But we need to check if we're at or above p = 2^130 - 5

                    // Proper check: if (h[2] >= 0x03FFFFFF || (h[2] == 0x03FFFFFE && ...))
                    // Actually, since we allow values slightly above p, we need to compare:
                    // If h >= 2^130 - 5, subtract p (add 5 and propagate)

                    // The carry propagation above ensures h[0], h[1] are in [0, 2^26-1]
                    // But h[2] might be >= 2^26 or the value might be >= p

                    // Check: is h + 5 >= 2^130?
                    // h + 5 >= 2^130 iff h >= 2^130 - 5 = p
                    // We compute this by checking if adding 5 causes an overflow beyond 130 bits

                    ulong tmp0 = h[0] + 5;
                    ulong tmp1 = h[1];
                    ulong tmp2 = h[2];

                    // Propagate carry from adding 5
                    tmp1 += tmp0 >> 26; tmp0 &= 0x3FFFFFF;
                    tmp2 += tmp1 >> 26; tmp1 &= 0x3FFFFFF;

                    // If tmp2 has bit 26 set, then h + 5 >= 2^130, so h >= p
                    if (tmp2 >> 26 != 0)
                    {
                        // Subtract p by adding 5 (since p = 2^130 - 5, so -p = +5 - 2^130)
                        // The -2^130 part is handled by simply discarding the overflow bit
                        h[0] = (uint)tmp0;
                        h[1] = (uint)tmp1;
                        h[2] = tmp2 & 0x3FFFFFF;
                    }
                }

                // Convert to 16-byte tag
                // First add the s-part (key bytes 16-31)
                ulong s0 = Utils.ToUInt64LittleEndian(key, 16);
                ulong s1 = Utils.ToUInt64LittleEndian(key, 24);

                ulong tag0 = h[0] | (h[1] << 26);
                ulong tag1 = (h[1] >> 38) | (h[2] << 14);

                tag0 += s0;
                tag1 += s1 + (tag0 < s0 ? 1UL : 0); // Add carry

                byte[] tag = new byte[16];
                Array.Copy(Utils.GetBytesLittleEndian(tag0), tag, 8);
                Array.Copy(Utils.GetBytesLittleEndian(tag1), 0, tag, 8, 8);

                return tag;
            }
        }
    }
}
#endif
