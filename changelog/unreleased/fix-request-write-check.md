### Fixed

- Socket protocol, messages: a request or a response written is now read back with the rules of the reader before it is sent: its
  depth, a property appearing only once at every level, and valid UTF-8. Before, a property written twice one level below the body (by
  extension data), a body deeper than the reader allows, or invalid UTF-8 written by a converter could leave: the peer refused it, and
  its connection closed. Such a body is now an `ArgumentException` for the caller, never a `JsonException`; on a server, the dispatcher
  answers an internal error instead of the response. The check reads the message without copying it.
- Docs: in `doc/dev/complexity.md`, a frame written erases only its own bytes before its buffer goes back to the pool (the table said the
  buffer was cleared).

### Added

- Tests of the message writer: a property written twice in a nested body, and invalid UTF-8 written by a converter, are refused before
  sending, for a request and for a response; a body too deep for the reader is refused; at every depth from 1 to 20, the writer accepts
  exactly what the reader accepts.
