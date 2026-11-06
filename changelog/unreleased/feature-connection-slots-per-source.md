### Added

- Socket protocol, link server: `LinkServerLimits` (served connections, connections in their handshake, connections in their handshake per source, pace of the refusal logs), with a constructor of `LinkServer` that takes it and the clock of the logs. Event ids 9041-9043.

### Changed

- Socket protocol, link server: a connection in its handshake (TLS, then protocol) takes one of 4 places of its own, 2 of them at most from one IP address, and a served place only once its handshake is done; a silent or unknown peer can no longer take the places of the connections the tier serves. When the places of the handshakes are taken, the oldest connection in its handshake (of the same source when it holds its part, else of all) is closed to make room for the new one, so that a peer must open connections without pause to keep the client tier out. A connection whose handshake ends when every served place is taken is closed.
- Socket protocol, link server: a refused connection, or one closed to make room, is logged at most once every 10 seconds (`LogPace`, shared with the TLS listener); the next log gives the number of refusals in between, so that a flood of connections does not flood the logs.
- Tests: the socket protocol tests get a second block of test ports (17600-17699), locked only once the first one is used up (`TestPortBlockSet`).
