### Added

- Socket protocol, error codes: a response holds either its body or an error, `{"error":{"code":"…"}}`, never both. The codes are short dotted names, never display texts and never internal details (`ErrorCodes`: `request.unknownOperation`, `request.invalid`, `internal`, `link.unavailable`). The caller receives a `RemoteErrorException` holding the code, and the connection stays open.
- Socket protocol, `LinkFailureException`: thrown by the client of a link when the peer cannot be reached or the connection is lost before the response, with the code `link.unavailable`. The cause is kept as the inner exception, for the logs of the tier.

### Changed

- Socket protocol, operation dispatcher: an unknown operation, a body that does not match its operation, or a failing handler is answered with an error code instead of closing the connection that every caller shares. A tier behind that cannot be reached is answered `link.unavailable`, any other failure `internal`. The details go to the log of the tier only (event ids 9021-9024). A malformed envelope still closes the connection. The dispatcher now takes the logger of the tier.
- Back: when the Very Back cannot be reached, a `Ping` of the Gateway is answered `link.unavailable`, and the connection of the Gateway stays open.
