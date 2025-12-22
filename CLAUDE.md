# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

NENet is a lightweight TCP networking library for Unity games/applications. It provides a client-side TCP connection manager with event-driven architecture, thread-safe operations, and custom binary packet protocol.

All source files are located in the project root directory with namespace `NT.Core.Net`.

## Build and Testing

This is a Unity library that is compiled within the Unity Editor. There are no external build commands, test runners, or package managers. The code uses Unity's `Debug` class for logging.

The library is **disabled for WebGL builds** via `#if !UNITY_WEBGL` preprocessor directives.

## Architecture

### Threading Model
The library uses a dedicated thread pattern:
- **Receive thread**: Handles connection establishment and incoming data (`Transport.Receive`)
- **Send thread**: Handles outgoing data (`Transport.Send`)
- **Main thread**: Polls for events via `Client.TryGetNextEvent()`

### Packet Protocol
Binary protocol with big-endian byte order:

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

- Minimum packet size: 12 bytes (command + token only)
- Maximum packet size: 16KB (`Transport.MaxPacketSize`)

### Key Classes

| Class | Responsibility |
|-------|----------------|
| `Client` | Main entry point for connections. Manages send/receive queues, connection lifecycle, connection tags (`tag#id`) |
| `Transport` | Low-level TCP operations. Static methods `Send()` and `Receive()` run on dedicated threads. Uses `[ThreadStatic]` buffers for performance |
| `Packet` | Data container with `Command` (uint), `Token` (ulong), `Body` (byte[]) |
| `Event` | Event wrapper with `tag`, `EventType` (Connected/Data/Disconnected), and optional `Packet` |
| `SafeQueue<T>` | Thread-safe queue with `TryDequeueAll()` for bulk operations (unlike `ConcurrentQueue`) |
| `Utils` | Big-endian byte conversion utilities (10x faster than `BitConverter`) |
| `NetworkStreamExtension` | Provides `ReadExactly()` for blocking reads and `ReadSafely()` for graceful closure handling |

### Data Flow

**Send path**: `Client.Send()` → `_sendQueue` (SafeQueue) → `Transport.Send()` combines packets → TCP stream

**Receive path**: TCP stream → `Transport.Receive()` → `_recvQueue` (ConcurrentQueue) → `Client.TryGetNextEvent()` → application

### Connection Tags
Each connection has a unique `Ctag` in format `"{client_tag}#{connection_id}"`. The `connection_id` increments on each `Connect()` call, allowing identification of connection attempts even before successful connection.

### Performance Considerations
- Packet combining: Multiple packets are combined into single TCP write to reduce overhead
- Thread-local buffers: `[ThreadStatic]` fields in `Transport` avoid per-send allocations
- Queue warning: When receive queue exceeds `Transport.recvQueueWarningLevel` (default: 1000), warnings are logged every 10 seconds
