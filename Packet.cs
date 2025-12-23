namespace NT.Core.Net
{
    /// <summary>
    ///  0        1        2        3
    ///  +--------+--------+--------+--------+
    ///  |           packet length           |
    ///  +-----------------------------------+
    ///  |              command              |
    ///  +-----------------------------------+
    ///  |              token                |
    ///  |                                   |
    ///  +-----------------------------------+
    ///  |              body ...             |
    ///  |                                   |
    ///  +-----------------------------------+
    /// </summary>
    public class Packet
    {
        public Packet(uint command, ulong token, byte[] body)
        {
            Command = command;
            Token = token;
            Body = body ?? Array.Empty<byte>();
        }

        public uint Command { get; set; }
        public ulong Token { get; set; }
        public byte[] Body { get; set; }
    }
}
