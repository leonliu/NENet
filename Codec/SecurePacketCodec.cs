#if !UNITY_WEBGL
using System;
using NT.Core.Net.Security;

namespace NT.Core.Net.Codec
{
    /// <summary>
    /// Codec wrapper that adds encryption/decryption to any inner codec.
    /// </summary>
    public class SecurePacketCodec : IPacketCodec
    {
        private readonly IPacketCodec _innerCodec;
        private readonly IPacketCipher _cipher;

        /// <summary>
        /// The inner codec being wrapped.
        /// </summary>
        public IPacketCodec InnerCodec => _innerCodec;

        /// <summary>
        /// The cipher used for encryption/decryption.
        /// </summary>
        public IPacketCipher Cipher => _cipher;

        /// <summary>
        /// Creates a new secure packet codec.
        /// </summary>
        /// <param name="innerCodec">The underlying codec to use for packet encoding/decoding.</param>
        /// <param name="cipher">The cipher to use for encryption/decryption.</param>
        public SecurePacketCodec(IPacketCodec innerCodec, IPacketCipher cipher)
        {
            _innerCodec = innerCodec ?? throw new ArgumentNullException(nameof(innerCodec));
            _cipher = cipher ?? throw new ArgumentNullException(nameof(cipher));
        }

        /// <summary>
        /// Encodes and encrypts a packet.
        /// </summary>
        public byte[] Encode(uint command, ulong token, byte[] body)
        {
            byte[] encoded = _innerCodec.Encode(command, token, body);
            return _cipher.Encrypt(encoded);
        }

        /// <summary>
        /// Decrypts and decodes a packet.
        /// </summary>
        public bool Decode(byte[] data, out uint command, out ulong token, out byte[] body)
        {
            try
            {
                byte[] decrypted = _cipher.Decrypt(data);
                return _innerCodec.Decode(decrypted, out command, out token, out body);
            }
            catch (Exception)
            {
                command = 0;
                token = 0;
                body = null;
                return false;
            }
        }
    }
}
#endif
