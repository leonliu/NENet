# NENet

A lightweight TCP networking library for Unity games/applications with protocol-agnostic transport layer, pluggable codec system, built-in security layer, and TLS/SSL support.

## Features

- **Event-driven architecture** - Non-blocking operations with polled events
- **Thread-safe** - Dedicated send/receive threads with concurrent queues
- **Protocol-agnostic transport** - Core networking layer works with raw byte arrays
- **Pluggable codec system** - Easy to implement custom packet protocols via `IPacketCodec`
- **Built-in NENet protocol** - Efficient big-endian packet format with `NENetPacketCodec`
- **Security layer** - Production-grade encryption with pluggable ciphers (ChaCha20-Poly1305, ChaCha20, RC4, XOR)
- **TLS/SSL support** - Transport-layer security with pluggable TLS options (TLS 1.2)
- **IPv6 support** - Full support for IPv6 with happy eyeballs (IPv6-first, fallback to IPv4)
- **Connection tagging** - Unique identifiers for multiple connection attempts
- **Cross-platform** - Works on little-endian and big-endian systems
- **WebGL-safe** - Automatically disabled for WebGL builds

## Requirements

- Unity 2019.4 or later
- .NET Standard 2.0+

## Installation

Copy all `.cs` files from this repository into your Unity project under `Assets/Scripts/NENet/`.

## Quick Start

### TLS/SSL Secure Connection

```csharp
using NT.Core.Net;

// Create a client with TLS support
Client client = new Client("myclient");
client.TlsOptions = TlsOptions.Default;  // TLS 1.2 with default certificate validation

// Connect with TLS
client.Connect("example.com", 443);

// Poll for events
void Update()
{
    if (client.TryGetNextEvent(out Event ev))
    {
        switch (ev.eventType)
        {
            case EventType.Connected:
                Debug.Log($"TLS Connected: {client.Ctag}");
                break;
            case EventType.Data:
                Debug.Log($"Received: {ev.data.Length} bytes");
                break;
            case EventType.Disconnected:
                Debug.Log($"Disconnected: {client.Ctag}");
                break;
        }
    }
}

// Send data (encrypted via TLS)
client.Send(System.Text.Encoding.UTF8.GetBytes("Hello"));
```

### TLS with Custom Certificate Validation

```csharp
// Accept self-signed certificates for development
client.TlsOptions = new TlsOptions {
    Protocols = SslProtocols.Tls12,
    CertificateValidator = (sender, cert, chain, errors) => true
};
```

### TLS with Mutual Authentication

```csharp
// Provide client certificate for mutual TLS
var cert = new X509Certificate2("client.pfx", "password");
client.TlsOptions = new TlsOptions {
    ClientCertificate = cert,
    Protocols = SslProtocols.Tls12
};
```

### Plain TCP (Default, Lightweight)

```csharp
// No TLS options = plain TCP
Client client = new Client("myclient");
client.Connect("127.0.0.1", 8080);
```

### Using PacketClient with Encryption (Recommended)

```csharp
using NT.Core.Net;
using NT.Core.Net.Security;

// Create a packet client with encryption
var codec = new DefaultPacketCodec();
var cipher = new ChaCha20Poly1305Cipher("my-secret-key");
var secureCodec = new SecurePacketCodec(codec, cipher);

PacketClient client = new PacketClient("myclient", secureCodec);

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

// Send a packet with command, token, and body (automatically encrypted)
client.SendPacket(1, 0, System.Text.Encoding.UTF8.GetBytes("Hello"));

// Disconnect
client.Disconnect();
```

### Available Ciphers

| Cipher | Type | Security Level | Use Case |
|--------|------|----------------|----------|
| `ChaCha20Poly1305Cipher` | AEAD | Strong (recommended) | Production use, provides confidentiality + authentication |
| `ChaCha20Cipher` | Stream cipher | Strong | Alternative to ChaCha20-Poly1305, confidentiality only |
| `Rc4Cipher` | Stream cipher | Weak (legacy) | Compatibility with existing servers only |
| `XorCipher` | Obfuscation | None | Light obfuscation only, not real encryption |
| `NullCipher` | None | None | Plaintext, for testing/debugging |

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
│                   Security Layer (Optional)                  │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              SecurePacketCodec (Decorator)            │    │
│  │  ┌────────────┐ ┌────────────┐ ┌─────────────────┐   │    │
│  │  │ChaCha20-Poly│ │ ChaCha20   │ │ RC4 │ XOR │ None │   │    │
│  │  │1305 (AEAD) │ │ (Stream)   │ └─────────────────┘   │    │
│  │  └────────────┘ └────────────┘                         │    │
│  └─────────────────────────────────────────────────────┘    │
├─────────────────────────────────────────────────────────────┤
│                     Transport Layer                          │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │    Transport     │  │ SecureTransport │                │
│  │   (Plain TCP)    │  │   (TLS/SSL)      │                │
│  └──────────────────┘  └──────────────────┘                │
│  - Length-prefix framing: [4-byte length][payload]           │
│  - Send/Receive threads                                       │
│  - IPv6 support with happy eyeballs                          │
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

## Security Layer

### Adding Encryption to Any Codec

The `SecurePacketCodec` decorator wraps any `IPacketCodec` with encryption:

```csharp
// Wrap any codec with any cipher
var codec = new DefaultPacketCodec();
var cipher = new ChaCha20Poly1305Cipher("secret-key");
var secureCodec = new SecurePacketCodec(codec, cipher);
```

### Cipher Details

#### ChaCha20-Poly1305 (Recommended)

- **Type**: Authenticated Encryption with Associated Data (AEAD)
- **Key size**: 256 bits (derived from any string via SHA256)
- **Nonce**: Auto-generated per message (12 bytes)
- **Tag**: 128-bit authentication tag
- **Output format**: `[12B nonce][ciphertext][16B tag]`
- **Protection**: Confidentiality + integrity + authentication
- **Standard**: RFC 7539

```csharp
var cipher = new ChaCha20Poly1305Cipher("my-secret-key");
// or with raw key:
var cipher = new ChaCha20Poly1305Cipher(key32Bytes);
```

#### ChaCha20

- **Type**: Stream cipher
- **Key size**: 256 bits
- **Nonce**: Auto-generated per message (12 bytes)
- **Output format**: `[12B nonce][ciphertext]`
- **Protection**: Confidentiality only
- **Standard**: RFC 7539

```csharp
var cipher = new ChaCha20Cipher("my-secret-key");
// or with fixed nonce (not recommended for multiple messages):
var cipher = new ChaCha20Cipher(key32Bytes, nonce12Bytes);
```

#### RC4 (Legacy)

- **Type**: Stream cipher (legacy, cryptographically broken)
- **Key size**: 1-256 bytes
- **Use case**: Compatibility with existing servers only
- **Warning**: Do not use for new systems requiring strong security

#### XOR

- **Type**: Simple obfuscation
- **Key size**: Any length
- **Use case**: Light obfuscation only, not real security

## TLS/SSL Transport Layer

### TlsOptions Configuration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Protocols` | `SslProtocols` | `Tls12` | TLS protocol version |
| `CertificateValidator` | `RemoteCertificateValidationCallback` | `null` | Custom certificate validation callback |
| `ClientCertificate` | `X509Certificate2` | `null` | Client certificate for mutual TLS |
| `CheckCertificateRevocation` | `bool` | `true` | Whether to check certificate revocation |

### Client Certificate Validation

When providing a `ClientCertificate`, the library validates:
- Private key is present (required for mutual TLS)
- Certificate is not expired
- Certificate is valid (not before date has passed)

### IPv6 Support

The library supports IPv6 with "happy eyeballs" algorithm:

```csharp
var client = new Client("myclient");

// Auto-detect (try IPv6 first, fallback to IPv4)
client.AddressFamily = AddressFamily.Unspecified;  // default
client.Connect("example.com", 8080);

// Force IPv4 only
client.AddressFamily = AddressFamily.InterNetwork;
client.Connect("example.com", 8080);

// Force IPv6 only
client.AddressFamily = AddressFamily.InterNetworkV6;
client.Connect("example.com", 8080);
```

## API Reference

### Client

Low-level protocol-agnostic client for raw byte communication.

| Member | Type | Description |
|--------|------|-------------|
| `Connected` | `bool` | Whether the connection is established |
| `Connecting` | `bool` | Whether a connection attempt is in progress |
| `Ctag` | `string` | Connection tag (`{client_tag}#{connection_id}`) |
| `RecvQueueWatermark` | `int` | Number of events pending in the receive queue |
| `AddressFamily` | `AddressFamily` | IPv4/IPv6 preference (default: Unspecified) |
| `TlsOptions` | `TlsOptions` | TLS/SSL configuration (null = plain TCP) |
| `NoDelay` | `bool` | Disable Nagle's algorithm (default: `true`) |
| `SendTimeout` | `int` | Send timeout in milliseconds (default: `5000`) |

#### Methods

- `void Connect(string host, int port)` - Start a connection attempt (parameter renamed from `ip` to `host`)
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

### IPacketCipher

Interface for implementing custom encryption.

```csharp
public interface IPacketCipher
{
    byte[] Encrypt(byte[] data);
    byte[] Decrypt(byte[] data);
    string Name { get; }
}
```

### TlsOptions

Configuration for TLS/SSL connections.

```csharp
public class TlsOptions
{
    public SslProtocols Protocols { get; set; } = SslProtocols.Tls12;
    public RemoteCertificateValidationCallback CertificateValidator { get; set; }
    public X509Certificate2 ClientCertificate { get; set; }
    public bool CheckCertificateRevocation { get; set; } = true;
    public static TlsOptions Default => new TlsOptions();
}
```

### Transport (Internal)

Low-level TCP transport with length-prefix framing.

| Static Member | Type | Default | Description |
|---------------|------|---------|-------------|
| `Create(TlsOptions, AddressFamily)` | `static Transport` | - | Factory method for creating transport instances |
| `RecvQueueWarningLevel` | `int` | 1000 | Log warning if queue exceeds this |
| `MaxMessageSize` | `int` | 16KB | Maximum message size allowed |

## Threading Model

- **Receive thread** - Handles connection establishment and incoming data (`Transport.Receive`)
- **Send thread** - Handles outgoing data (`Transport.Send`)
- **Main thread** - Polls for events via `TryGetNextEvent()` or `TryGetNextPacket()`

## License

See [LICENSE](LICENSE) file.
