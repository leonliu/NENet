namespace NT.Core.Net.Protocol
{
    /// <summary>
    /// NENet protocol packet structure.
    /// Format: [length(4)][command(4)][token(8)][body...]
    /// </summary>
    public class Packet
    {
        public Packet(uint command, ulong token, byte[] body)
        {
            Command = command;
            Token = token;
            Body = body ?? System.Array.Empty<byte>();
        }

        public uint Command { get; set; }
        public ulong Token { get; set; }
        public byte[] Body { get; set; }
    }
}
