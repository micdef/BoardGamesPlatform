### Added

- Tests of time and space complexity: `AllocationMeter` (`test/BoardGamesPlatform.TestKit`) measures the bytes an operation allocates on
  the thread of the test, the operation completing synchronously on a stream in memory, so that the result never depends on the load of
  the machine; a hot path is measured with a small and a large input, the difference giving its cost per byte. The frame codec (a frame
  written allocates the same whatever its payload; a frame read allocates its payload and a fixed cost under 1 KiB; a frame arriving byte
  by byte is read in one pass) and the envelopes of the requests (written once, read without a second copy of the body, same bytes as
  before).
- `doc/dev/complexity.md`: how the complexity is tested, the cost of each hot path with its test, and the collections bounded by the
  limits of the links; a rule of the coding conventions: a hot path gets a complexity test.

### Changed

- Socket protocol, frames: a frame written builds its header and its payload in a buffer of the shared pool, given back cleared, instead
  of a new buffer of its size: no allocation per byte of payload (64 KiB less per frame of 64 KiB).
- Socket protocol, messages: a request whose body is an object with properties is written in one pass, instead of its body first, then
  the envelope: 1 byte allocated per byte of request instead of 2. The body of a request read is checked as UTF-8 on its raw
  bytes, instead of a copy as text of 2 bytes per byte: 1 byte allocated per byte of request instead of 3. The bytes on the wire do not
  change.

### Fixed

- Tests: the renewal test of the link client waits for the log of the renewal before it disposes the client, which otherwise logged the
  old connection as closed by the disposal, at random.
- Tests: the heartbeat test of a large request written slowly moves the clock only while the client writes; once the request is written,
  the bytes left in the buffers of Linux (several MiB) are read without moving it, since a client that waits for a silent server rightly
  closes after its timeout. It failed at random on Linux.
