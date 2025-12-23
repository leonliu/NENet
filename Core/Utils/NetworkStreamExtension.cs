#if !UNITY_WEBGL
using System.IO;
using System.Net.Sockets;

namespace NT.Core.Net
{
    public static class NetworkStreamExtension
    {
        /// <summary>
        /// A handy version of NetworkStream.Read method. .Read method returns 0 if remote closed
        /// the connection but throws an IOException if client closed its connection voluntarily.
        /// ReadSafely returns 0 for both cases so caller does not have to worry about the 
        /// IOException since the disconnect is by intention.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static int ReadSafely(this NetworkStream stream, byte[] buffer, int offset, int size)
        {
            try
            {
                return stream.Read(buffer, offset, size);
            }
            catch (IOException)
            {
                return 0;
            }
        }

        /// <summary>
        /// Read exactly 'amount' bytes.
        /// NetworkStream.Read reads up to 'amount' bytes. This method is blocking until
        /// 'amount' bytes were received.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="buffer"></param>
        /// <param name="amount"></param>
        /// <returns>true if 'amount' bytes are read, false if connection is closed</returns>
        public static bool ReadExactly(this NetworkStream stream, byte[] buffer, int amount)
        {
            int bytesRead = 0;
            while (bytesRead < amount)
            {
                int bytesLeft = amount - bytesRead;
                int result = stream.ReadSafely(buffer, bytesRead, bytesLeft);

                // the connection is closed locally or remotely
                if (result == 0)
                {
                    return false;
                }

                bytesRead += result;
            }
            return true;
        }
    }
}
#endif