### Changed

- Socket protocol, link client: the failures in a row are only forgotten once a connection answers a request (an error code of the peer included), no longer as soon as it opens. A connection lost before any response counts as a failure to open, so that a peer in a crash loop (it accepts the handshake, then dies at the first request) is reconnected with the back-off, doubled each time, instead of on every call. Event id 9007 (`LinkClient` now uses 9001-9007).

### Added

- Protocol tests: `KillablePeer.CrashOnPing`; a peer in a crash loop is reached once per back-off delay, the delay doubles, and the peer is served again once it answers.
