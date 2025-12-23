# NENet

A lightweight TCP networking library for Unity games/applications with protocol-agnostic transport layer and pluggable codec system.

## Features

- **Event-driven architecture** - Non-blocking operations with polled events
- **Thread-safe** - Dedicated send/receive threads with concurrent queues
- **Protocol-agnostic transport** - Core networking layer works with raw byte arrays
- **Pluggable codec system** - Easy to implement custom packet protocols via `IPacketCodec`
- **Built-in NENet protocol** - Efficient big-endian packet format with `NENetPacketCodec`
- **Connection tagging** - Unique identifiers for multiple connection attempts
- **WebGL-safe** - Automatically disabled for WebGL builds

## Requirements

- Unity 2019.4 or later
- .NET Standard 2.0+

## Installation

Copy all `.cs` files from this repository into your Unity project under `Assets/Scripts/NENet/`.

## Quick Start

### Using PacketClient (Recommended - with built-in protocol)

```csharp
using NT.Core.Net;

// Create a packet client with the default NENet codec
PacketClient client = new PacketClient("myclient");

// Connect to a server
client.Connect("127.0.0.1", 8080);

// In your Update loop, poll for events
void Update()
{
    uint command;
    ulong token;
    byte[] body;
    EventType eventType;

    if (client.TryGetNextPacket(out command, out token, out body, out eventType))
    {
        switch (eventType)
        {
            case EventType.Connected:
                Debug.Log($"Connected: {client.Ctag}");
                break;
            case EventType.Data:
                Debug.Log($"Received: cmd={command}, token={token}, body={body.Length} bytes");
                break;
            case EventType.Disconnected:
                Debug.Log($"Disconnected: {client.Ctag}");
                break;
        }
    }
}

// Send a packet with command, token, and body
client.SendPacket(1, 0, System.Text.Encoding.UTF8.GetBytes("Hello"));

// Disconnect
client.Disconnect();
```

### Using Raw Client (Protocol-agnostic)

```csharp
using NT.Core.Net;

// Create a raw client - works with any protocol
Client client = new Client("myclient");

client.Connect("127.0.0.1", 8080);

void Update()
{
    if (client.TryGetNextEvent(out Event ev))
    {
        switch (ev.eventType)
        {
            case EventType.Data:
                // Handle raw bytes - decode with your custom codec
                HandleRawData(ev.data);
                break;
        }
    }
}

// Send raw byte data (length-prefix is added automatically)
client.Send(yourEncodedData);
```

### Custom Codec Example

```csharp
using NT.Core.Net;

// Implement IPacketCodec for your custom protocol
public class MyCodec : IPacketCodec
{
    public byte[] Encode(uint command, ulong token, byte[] body)
    {
        // Your encoding logic
    }

    public bool Decode(byte[] data, out uint command, out ulong token, out byte[] body)
    {
        // Your decoding logic
    }
}

// Use with PacketClient
PacketClient client = new PacketClient("myclient", new MyCodec());
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                       Application Layer                       │
├─────────────────────────────────────────────────────────────┤
│  PacketClient (codec-based API)  │  Client (raw byte API)   │
├─────────────────────────────────────────────────────────────┤
│                       IPacketCodec                           │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │ NENetPacketCodec │  │  Custom Codecs   │                │
│  └──────────────────┘  └──────────────────┘                │
├─────────────────────────────────────────────────────────────┤
│                     Transport (protocol-agnostic)            │
│  - Length-prefix framing: [4-byte length][payload]           │
│  - Send/Receive threads                                       │
├─────────────────────────────────────────────────────────────┤
│                       TCP Socket                              │
└─────────────────────────────────────────────────────────────┘
```

## NENet Packet Protocol

The default `NENetPacketCodec` uses a binary protocol with big-endian byte order:

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

### PacketClient

High-level client with built-in codec support.

| Member | Type | Description |
|--------|------|-------------|
| `Codec` | `IPacketCodec` | The codec for encoding/decoding packets |

#### Methods

- `bool SendPacket(uint command, ulong token, byte[] body)` - Encode and send a packet
- `bool TryGetNextPacket(out uint command, out ulong token, out byte[] body, out EventType eventType)` - Poll and decode next event

### Client

Low-level protocol-agnostic client for raw byte communication.

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
- `bool Send(byte[] data)` - Send raw byte data (max 16KB, length-prefix added)
- `bool TryGetNextEvent(out Event ev)` - Poll for the next event

### Event

Protocol-agnostic event struct.

```csharp
public struct Event
{
    public readonly string tag;        // Connection tag
    public readonly EventType eventType; // Connected/Data/Disconnected
    public readonly byte[] data;       // Raw bytes (null for Connected/Disconnected)
}
```

### IPacketCodec

Interface for implementing custom packet protocols.

```csharp
public interface IPacketCodec
{
    byte[] Encode(uint command, ulong token, byte[] body);
    bool Decode(byte[] data, out uint command, out ulong token, out byte[] body);
}
```

### Transport (Internal)

Low-level TCP transport with length-prefix framing.

| Static Member | Type | Default | Description |
|---------------|------|---------|-------------|
| `RecvQueueWarningLevel` | `int` | 1000 | Log warning if queue exceeds this |
| `MaxMessageSize` | `int` | 16KB | Maximum message size allowed |

## Threading Model

- **Receive thread** - Handles connection establishment and incoming data (`Transport.Receive`)
- **Send thread** - Handles outgoing data (`Transport.Send`)
- **Main thread** - Polls for events via `TryGetNextEvent()` or `TryGetNextPacket()`

## License

See [LICENSE](LICENSE) file.
