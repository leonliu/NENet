#if !UNITY_WEBGL
namespace NT.Core.Net
{
    /// <summary>
    /// Centralized configuration for the NENet networking library.
    /// These settings apply to all Client and Transport instances.
    /// </summary>
    public static class NetworkConfig
    {
        /// <summary>
        /// Maximum message size allowed, 16KB should be enough.
        /// </summary>
        public const int MaxMessageSize = 16 * 1024;

        /// <summary>
        /// Maximum send buffer size for message combining, 64KB should be enough.
        /// </summary>
        public const int MaxSendBufferSize = 64 * 1024;

        /// <summary>
        /// Maximum size for ThreadStatic send buffer. If a single batch exceeds this,
        /// we allocate a temporary buffer instead of growing the ThreadStatic one.
        /// This prevents unbounded memory growth in long-lived threads.
        /// </summary>
        public const int MaxThreadStaticBufferSize = 64 * 1024;

        /// <summary>
        /// Alert is set if receive queue size exceeds this value. It is an
        /// indication that the received messages have not been processed in
        /// time.
        /// </summary>
        public const int RecvQueueWarningLevel = 1000;

        /// <summary>
        /// Maximum receive queue size. When exceeded, packets will be dropped
        /// to prevent unbounded memory growth. Default: 10000 events.
        /// </summary>
        public const int MaxRecvQueueSize = 10000;
    }
}
#endif
