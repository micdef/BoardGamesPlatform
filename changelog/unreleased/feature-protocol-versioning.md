### Added

- Socket protocol, control messages: frames on the reserved correlation identifier 0 carry control messages, JSON objects whose first property `control` names their kind. They are read strictly, and an unknown kind closes the connection.
- Socket protocol, handshake (`ProtocolHandshake`): every connection opens with a `hello` from the client (the range of protocol versions it speaks and its largest payload). The server answers `helloAck`, with the newest common version (1 for now) and its limit of requests handled at the same time, or `helloRefused`, with the code `protocol.versionUnsupported` or `protocol.limitsMismatch`, and closes. A request before the hello is a protocol violation. A server allows 5 seconds for the handshake. A client bounds it with its connect timeout and checks the answer again.
- Test kit: `TestRequestClients.ConnectAsync` opens a request client on a listening tier with the handshake, as a real client does.

### Changed

- Links: the link server runs the handshake on each accepted connection before serving it. The link client runs it after connecting, within the connect timeout. A refusal reaches the caller as a `LinkFailureException` that keeps the code of the refusal; any other failure of the handshake gives `link.unavailable`.
