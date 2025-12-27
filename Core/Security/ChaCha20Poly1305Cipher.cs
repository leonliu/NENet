#if !UNITY_WEBGL
using System;
using System.Security.Cryptography;
using System.Text;

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
            byte[] cipherText = Transform(_key, nonce, 1, data);

            // Calculate authentication tag over ciphertext
            byte[] tag = Poly1305.ComputeTag(polyKey, cipherText);

            // Format: nonce || ciphertext || tag
            byte[] result = new byte[NonceSize + cipherText.Length + TagSize];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
            Buffer.BlockCopy(cipherText, 0, result, NonceSize, cipherText.Length);
            Buffer.BlockCopy(tag, 0, result, NonceSize + cipherText.Length, TagSize);

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

            int cipherTextLen = data.Length - NonceSize - TagSize;
            byte[] cipherText = new byte[cipherTextLen];
            Buffer.BlockCopy(data, NonceSize, cipherText, 0, cipherTextLen);

            byte[] receivedTag = new byte[TagSize];
            Buffer.BlockCopy(data, NonceSize + cipherTextLen, receivedTag, 0, TagSize);

            // Generate Poly1305 key
            byte[] polyKey = GeneratePoly1305Key(_key, nonce);

            // Verify authentication tag (constant-time compare)
            byte[] computedTag = Poly1305.ComputeTag(polyKey, cipherText);
            if (!CryptographicOperations.FixedTimeEquals(receivedTag, computedTag))
                throw new CryptographicException("Authentication failed: data may have been tampered with");

            // Decrypt ciphertext
            return Transform(_key, nonce, 1, cipherText);
        }

        /// <summary>
        /// Generates the Poly1305 one-time key using ChaCha20 with counter=0.
        /// </summary>
        private static byte[] GeneratePoly1305Key(byte[] key, byte[] nonce)
        {
            return Transform(key, nonce, 0, new byte[64]);
        }

        /// <summary>
        /// ChaCha20 encryption/decryption with specified counter.
        /// </summary>
        private static byte[] Transform(byte[] key, byte[] nonce, uint counter, byte[] input)
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
                QuarterRound(ref workingState[0], ref workingState[4], ref workingState[8], ref workingState[12]);
                QuarterRound(ref workingState[1], ref workingState[5], ref workingState[9], ref workingState[13]);
                QuarterRound(ref workingState[2], ref workingState[6], ref workingState[10], ref workingState[14]);
                QuarterRound(ref workingState[3], ref workingState[7], ref workingState[11], ref workingState[15]);

                QuarterRound(ref workingState[0], ref workingState[5], ref workingState[10], ref workingState[15]);
                QuarterRound(ref workingState[1], ref workingState[6], ref workingState[11], ref workingState[12]);
                QuarterRound(ref workingState[2], ref workingState[7], ref workingState[8], ref workingState[13]);
                QuarterRound(ref workingState[3], ref workingState[4], ref workingState[9], ref workingState[14]);
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
        /// Implementation based on OpenSSL's 32-bit reference implementation.
        /// Uses 4 x 32-bit limbs for r and 5 x 32-bit limbs for accumulator h.
        /// </summary>
        private static class Poly1305
        {
            /// <summary>
            /// Computes the Poly1305 authentication tag for the given message.
            /// </summary>
            public static byte[] ComputeTag(byte[] key, byte[] message)
            {
                if (key == null || key.Length != 32)
                    throw new ArgumentException("Poly1305 key must be 32 bytes", nameof(key));

                // Key clamping (RFC 7539 Section 2.5.1)
                // r &= 0xffffffc0ffffffc0ffffffc0fffffff
                uint r0 = Utils.ToUInt32LittleEndian(key, 0) & 0x0fffffff;
                uint r1 = Utils.ToUInt32LittleEndian(key, 4) & 0x0ffffffc;
                uint r2 = Utils.ToUInt32LittleEndian(key, 8) & 0x0ffffffc;
                uint r3 = Utils.ToUInt32LittleEndian(key, 12) & 0x0ffffffc;

                // Pre-compute r[i] * 5/4 for the reduction trick
                // Since bottom 2 bits of r1, r2, r3 are cleared, r[i] >> 2 loses no info
                uint s1 = r1 + (r1 >> 2);  // r1 * (1 + 1/4) = r1 * 5/4
                uint s2 = r2 + (r2 >> 2);
                uint s3 = r3 + (r3 >> 2);

                // Initial accumulator h (5 x 32-bit words to handle overflow)
                uint h0 = 0, h1 = 0, h2 = 0, h3 = 0, h4 = 0;

                // Process message in 16-byte blocks
                for (int offset = 0; offset < message.Length; offset += 16)
                {
                    // h += m[i] (load 16-byte block)
                    ulong d0, d1, d2, d3;
                    uint padbit = 1;

                    if (offset + 16 <= message.Length)
                    {
                        // Full block
                        d0 = (ulong)h0 + Utils.ToUInt32LittleEndian(message, offset + 0);
                        d1 = (ulong)h1 + (d0 >> 32) + Utils.ToUInt32LittleEndian(message, offset + 4);
                        d2 = (ulong)h2 + (d1 >> 32) + Utils.ToUInt32LittleEndian(message, offset + 8);
                        d3 = (ulong)h3 + (d2 >> 32) + Utils.ToUInt32LittleEndian(message, offset + 12);
                        h4 = (uint)(d3 >> 32) + padbit;
                    }
                    else
                    {
                        // Partial block: pad with 0x01 byte, then zeros
                        byte[] padded = new byte[16];
                        int remaining = message.Length - offset;
                        Buffer.BlockCopy(message, offset, padded, 0, remaining);
                        padded[remaining] = 1;

                        d0 = (ulong)h0 + Utils.ToUInt32LittleEndian(padded, 0);
                        d1 = (ulong)h1 + (d0 >> 32) + Utils.ToUInt32LittleEndian(padded, 4);
                        d2 = (ulong)h2 + (d1 >> 32) + Utils.ToUInt32LittleEndian(padded, 8);
                        d3 = (ulong)h3 + (d2 >> 32) + Utils.ToUInt32LittleEndian(padded, 12);
                        h4 = (uint)(d3 >> 32) + 1;  // padbit = 1 for partial block too
                    }

                    h0 = (uint)d0;
                    h1 = (uint)d1;
                    h2 = (uint)d2;
                    h3 = (uint)d3;

                    // h *= r "%" p (partial remainder, using OpenSSL's formula)
                    d0 = ((ulong)h0 * r0) + ((ulong)h1 * s3) + ((ulong)h2 * s2) + ((ulong)h3 * s1);
                    d1 = ((ulong)h0 * r1) + ((ulong)h1 * r0) + ((ulong)h2 * s3) + ((ulong)h3 * s2) + ((ulong)h4 * s1);
                    d2 = ((ulong)h0 * r2) + ((ulong)h1 * r1) + ((ulong)h2 * r0) + ((ulong)h3 * s3) + ((ulong)h4 * s2);
                    d3 = ((ulong)h0 * r3) + ((ulong)h1 * r2) + ((ulong)h2 * r1) + ((ulong)h3 * r0) + ((ulong)h4 * s3);
                    h4 = h4 * r0;

                    // Carry propagation
                    h0 = (uint)d0;
                    h1 = (uint)(d1 += d0 >> 32);
                    h2 = (uint)(d2 += d1 >> 32);
                    h3 = (uint)(d3 += d2 >> 32);
                    h4 += (uint)(d3 >> 32);

                    // Partial reduction: (h4:h0 += (h4:h0>>130) * 5) %= 2^130
                    // Shifting by 4 limbs = 128 bits, then 2 more bits for total 130
                    // c = (h4 >> 2) * 5 + (h4 & 3) recovers the lost 2 bits and multiplies by 5
                    uint c = (h4 >> 2) + (h4 & ~3U);
                    h4 &= 3;

                    h0 += c;
                    h1 += (c = ConstantTimeCarry(h0, c));
                    h2 += (c = ConstantTimeCarry(h1, c));
                    h3 += ConstantTimeCarry(h2, c);
                }

                // Final reduction: compare to modulus p = 2^130 - 5
                // Compute g = h + (-p mod 2^130) = h + 5
                uint g0 = (uint)((ulong)h0 + 5);
                uint g1 = (uint)((ulong)h1 + ((ulong)h0 + 5 >> 32));
                uint g2 = (uint)((ulong)h2 + ((ulong)h1 + ((ulong)h0 + 5 >> 32) >> 32));
                uint g3 = (uint)((ulong)h3 + ((ulong)h2 + ((ulong)h1 + ((ulong)h0 + 5 >> 32) >> 32) >> 32));
                uint g4 = h4 + (uint)((ulong)h3 + ((ulong)h2 + ((ulong)h1 + ((ulong)h0 + 5 >> 32) >> 32) >> 32) >> 32);

                // Constant-time conditional: if g4 >> 2 (carry into 131st bit), use g, else use h
                uint mask = 0 - (g4 >> 2);
                g0 &= mask;
                g1 &= mask;
                g2 &= mask;
                g3 &= mask;
                mask = ~mask;
                h0 = (h0 & mask) | g0;
                h1 = (h1 & mask) | g1;
                h2 = (h2 & mask) | g2;
                h3 = (h3 & mask) | g3;

                // mac = (h + nonce) % 2^128 (nonce is key bytes 16-31)
                ulong t;
                uint n0 = Utils.ToUInt32LittleEndian(key, 16);
                uint n1 = Utils.ToUInt32LittleEndian(key, 20);
                uint n2 = Utils.ToUInt32LittleEndian(key, 24);
                uint n3 = Utils.ToUInt32LittleEndian(key, 28);

                h0 = (uint)(t = (ulong)h0 + n0);
                h1 = (uint)(t = (ulong)h1 + (t >> 32) + n1);
                h2 = (uint)(t = (ulong)h2 + (t >> 32) + n2);
                h3 = (uint)(t = (ulong)h3 + (t >> 32) + n3);

                byte[] tag = new byte[16];
                Buffer.BlockCopy(Utils.GetBytesLittleEndian(h0), 0, tag, 0, 4);
                Buffer.BlockCopy(Utils.GetBytesLittleEndian(h1), 0, tag, 4, 4);
                Buffer.BlockCopy(Utils.GetBytesLittleEndian(h2), 0, tag, 8, 4);
                Buffer.BlockCopy(Utils.GetBytesLittleEndian(h3), 0, tag, 12, 4);

                return tag;
            }

            /// <summary>
            /// Constant-time carry detection: returns 1 if a less than b (carry occurred), else 0.
            /// Uses bitwise operations to avoid branching.
            /// </summary>
            private static uint ConstantTimeCarry(uint a, uint b)
            {
                return (a ^ ((a ^ b) | ((a - b) ^ b))) >> 31;
            }
        }
    }
}
#endif
