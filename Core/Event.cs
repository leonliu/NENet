using System;
using System.Buffers;

namespace NT.Core.Net
{
    public enum EventType
    {
        Connected = 0,
        Data,
        Disconnected
    }

    /// <summary>
    /// Protocol-agnostic network event.
    /// Call Dispose() after processing the event to return rented buffers to the pool.
    /// </summary>
    public struct Event : IDisposable
    {
        public readonly string tag;
        public readonly EventType eventType;
        public readonly byte[] data;

        public Event(string tag, EventType eventType, byte[] data)
        {
            this.tag = tag;
            this.eventType = eventType;
            this.data = data;
        }

        /// <summary>
        /// Returns internal buffers to ArrayPool for reuse.
        /// Safe to call multiple times (no-op after first call).
        /// </summary>
        public void Dispose()
        {
            if (data != null && data.Length > 0)
            {
                ArrayPool<byte>.Shared.Return(data, clearArray: false);
            }
        }
    }
}
