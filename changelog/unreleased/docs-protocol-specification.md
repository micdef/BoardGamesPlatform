### Added

- Developer documentation, `doc/dev/protocol.md`: the specification of version 1 of the socket protocol (frames, handshake, control messages, envelopes, error codes and what closes a connection, cancellation, heartbeat, limits, reconnection, reserved log event ids).

### Changed

- Roadmap: the done criterion of 0.4.0 is rewritten to what a test can prove (in-flight requests fail with a code, calls fail at once while the Very Back is down, the first call once it is back succeeds), the branches of 0.4.0 are listed as delivered, and the decisions on the protocol version, the error responses and the robustness of the protocol are recorded.
