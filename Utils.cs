namespace NT.Core.Net
{
    public static class Utils
    {
        /// <summary>
        /// Get bytes of int value, in network order(big endian).
        /// This method is 10x faster than BitConverter.GetBytes().
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] GetBytes(int value)
        {
            return new byte[] {
               (byte)(value >> 24),
               (byte)(value >> 16),
               (byte)(value >> 8),
               (byte)value
           };
        }

        public static void GetBytes(int value, byte[] bytes)
        {
            bytes[0] = (byte)(value >> 24);
            bytes[1] = (byte)(value >> 16);
            bytes[2] = (byte)(value >> 8);
            bytes[3] = (byte)value;
        }

        public static void GetBytes(int value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = (byte)(value >> 24);
            bytes[offset + 1] = (byte)(value >> 16);
            bytes[offset + 2] = (byte)(value >> 8);
            bytes[offset + 3] = (byte)value;
        }

        /// <summary>
        /// Get bytes of long value, in network order (big endian).
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] GetBytes(long value)
        {
            return new byte[] {
               (byte)(value >> 56),
               (byte)(value >> 48),
               (byte)(value >> 40),
               (byte)(value >> 32),
               (byte)(value >> 24),
               (byte)(value >> 16),
               (byte)(value >> 8),
               (byte)value
           };
        }

        public static void GetBytes(long value, byte[] bytes, int offset)
        {
            bytes[offset + 0] = (byte)(value >> 56);
            bytes[offset + 1] = (byte)(value >> 48);
            bytes[offset + 2] = (byte)(value >> 40);
            bytes[offset + 3] = (byte)(value >> 32);
            bytes[offset + 4] = (byte)(value >> 24);
            bytes[offset + 5] = (byte)(value >> 16);
            bytes[offset + 6] = (byte)(value >> 8);
            bytes[offset + 7] = (byte)value;
        }

        /// <summary>
        /// Get int value from bytes in big endian order.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static int ToInt32(byte[] bytes)
        {
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        /// <summary>
        /// Get uint value from bytes in big endian order.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static uint ToUInt32(byte[] bytes)
        {
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        /// <summary>
        /// Get ulong value from bytes in big endian order.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static ulong ToUInt64(byte[] bytes)
        {
            return ((ulong)bytes[0] << 56)
                 | ((ulong)bytes[1] << 48)
                 | ((ulong)bytes[2] << 40)
                 | ((ulong)bytes[3] << 32)
                 | ((ulong)bytes[4] << 24)
                 | ((ulong)bytes[5] << 16)
                 | ((ulong)bytes[6] << 8)
                 | bytes[7];
        }
    }
}
