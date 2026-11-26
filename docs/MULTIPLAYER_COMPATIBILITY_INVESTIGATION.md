# Multiplayer compatibility investigation

**Status:** Investigation completed; no multiplayer implementation is claimed.

## Upstream architecture observed

The upstream Open Rails multiplayer subsystem uses a TCP client/server design:

- `ClientComm` opens a TCP connection, starts a dedicated receive thread, and sends/receives messages over `NetworkStream`.
- `ServerComm` uses `TcpListener`/TCP accept and creates a receive thread per connected player.
- Messages are Unicode (the .NET `Encoding.Unicode` implementation, UTF-16 little-endian) and framed by a length prefix followed by a colon/space delimiter; `Decoder` buffers partial receives and extracts complete messages.
- `Message.Decode` dispatches typed commands for movement, switch/signal state, player/train registration, control, coupling/uncoupling, weather, and other state.
- `MPManager` centralizes client/server roles, update intervals, state synchronization and authority decisions.
- Upstream documentation describes a dispatcher/host role and clients. Players must use the same route; clients generally use Explore or a simple timetable activity without AI traffic. It also warns that the multiplayer server code is not hardened for prolonged/public exposure.

These findings come from source inspection of `ClientComm.cs`, `ServerComm.cs`, `Decoder.cs`, `Message.cs`, `Server.cs`, `MPManager.cs`, and the upstream multiplayer manual.

## Browser-port compatibility blockers

1. Browsers cannot open arbitrary raw TCP sockets, so the upstream `TcpClient`/`TcpListener` transport cannot be reused as-is.
2. Thread-based receive loops and abort-based shutdown need asynchronous browser-compatible lifecycle management.
3. The current browser prototype does not yet have the full upstream simulation, signalling, switch, activity, timetable, or train-state message model to participate faithfully.
4. A protocol bridge would require incremental framing, strict message-size limits, validation, and safe error handling; a browser transport alone is not sufficient.
5. Authentication/authorization and server-side input validation are essential before exposing any relay/host service. The upstream warning about its security posture should not be interpreted as permission to expose a public server.

## Options considered

| Approach | Assessment |
|---|---|
| Raw TCP directly from browser | Not available in normal browser APIs |
| WebSocket relay translating to/from upstream TCP | Potential interoperability path, but requires a separate trusted relay/server and protocol validation |
| WebSocket-native multiplayer server | Better browser fit but would be a new protocol and would not be wire-compatible with desktop clients without a translator |
| WebRTC peer-to-peer | Could reduce relay traffic but adds NAT/session negotiation and does not replace authoritative state/simulation requirements |

## Recommendation and decision

Do not start multiplayer implementation before the single-player simulation and content/activity pipeline are substantially complete. If interoperability becomes a target, prototype a separately hosted WebSocket-to-TCP protocol bridge first, with bounded frame/message sizes, authentication, rate limits, and a small verified command subset. Keep browser simulation authority and state validation server-side for any hosted session. Do not advertise desktop multiplayer compatibility until protocol-level and state synchronization tests demonstrate it.

This investigation completes the roadmap's **Investigate multiplayer compatibility** task only; it does not mean multiplayer is implemented or usable.
