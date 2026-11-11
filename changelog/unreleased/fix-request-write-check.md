### Fixed

- Socket protocol, messages: a request written in one pass is now read back with the rules of the reader before it is sent: its depth,
  and a property written only once at every level. Before, a property written twice one level below the body (by extension data), or a
  body deeper than the reader allows, could leave: the peer refused it, and the connection closed. Such a body is now an
  `ArgumentException` for the caller, as documented, never a `JsonException`. The check reads the request without copying it.

### Added

- Tests of the request writer: a property written twice in a nested body is refused before sending; a body too deep for the reader is
  refused; at every depth from 1 to 20, the writer accepts exactly what the reader accepts.
