namespace NT.Core.Net
{
    public enum EventType
    {
        Connected = 0,
        Data,
        Disconnected
    }

    public struct Event
    {
        public readonly string tag;
        public readonly EventType eventType;
        public readonly Packet packet;

        public Event(string tag, EventType eventType, Packet packet)
        {
            this.tag = tag;
            this.eventType = eventType;
            this.packet = packet;
        }
    }
}
