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
    /// </summary>
    public struct Event
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
    }
}
