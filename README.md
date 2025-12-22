# NENet

A lightweight TCP networking library for Unity games/applications.

## Features

- **Event-driven architecture** - Non-blocking operations with polled events
- **Thread-safe** - Dedicated send/receive threads with concurrent queues
- **Custom binary protocol** - Efficient big-endian packet format
- **Connection tagging** - Unique identifiers for multiple connection attempts
- **WebGL-safe** - Automatically disabled for WebGL builds

## Requirements

- Unity 2019.4 or later
- .NET Standard 2.0+

## Installation

Copy all `.cs` files from this repository into your Unity project under `Assets/Scripts/NENet/`.

## Quick Start

```csharp
using NT.Core.Net;

// Create a client with an optional tag
Client client = new Client("myclient");

// Connect to a server
client.Connect("127.0.0.1", 8080);

// In your Update loop, poll for events
void Update()
{
    if (client.TryGetNextEvent(out Event ev))
    {
        switch (ev.eventType)
        {
            case EventType.Connected:
                Debug.Log($"Connected: {ev.tag}");
                break;
            case EventType.Data:
                HandlePacket(ev.packet);
                break;
            case EventType.Disconnected:
                Debug.Log($"Disconnected: {ev.tag}");
                break;
        }
    }
}

// Send raw packet data (must follow NENet protocol)
client.Send(packetBytes);

// Disconnect
client.Disconnect();
```

## Packet Protocol

NENet uses a binary protocol with big-endian byte order:

```
+--------+--------+--------+--------+
|     packet length (4 bytes)      |
+-----------------------------------+
|     command (4 bytes)            |
+-----------------------------------+
|     token (8 bytes)              |
+-----------------------------------+
|     body (variable)              |
+-----------------------------------+
```

- Minimum packet size: **12 bytes** (command + token only)
- Maximum packet size: **16KB**

## API Reference

### Client

| Member | Type | Description |
|--------|------|-------------|
| `Connected` | `bool` | Whether the connection is established |
| `Connecting` | `bool` | Whether a connection attempt is in progress |
| `Ctag` | `string` | Connection tag (`{client_tag}#{connection_id}`) |
| `RecvQueueWatermark` | `int` | Number of events pending in the receive queue |
| `NoDelay` | `bool` | Disable Nagle's algorithm (default: `true`) |
| `SendTimeout` | `int` | Send timeout in milliseconds (default: `5000`) |

#### Methods

- `void Connect(string ip, int port)` - Start a connection attempt
- `void Disconnect()` - Close the connection
- `bool Send(byte[] data)` - Send packet data (max 16KB)
- `bool TryGetNextEvent(out Event ev)` - Poll for the next event

### Event

```csharp
public struct Event
{
    public readonly string tag;        // Connection tag
    public readonly EventType eventType; // Connected/Data/Disconnected
    public readonly Packet packet;      // Null for Connected/Disconnected
}
```

### Packet

```csharp
public class Packet
{
    public uint Command { get; set; }
    public ulong Token { get; set; }
    public byte[] Body { get; set; }
}
```

## Architecture

- **Receive thread** - Handles connection and incoming data (`Transport.Receive`)
- **Send thread** - Handles outgoing data (`Transport.Send`)
- **Main thread** - Polls events via `Client.TryGetNextEvent()`

## License

See [LICENSE](LICENSE) file.
